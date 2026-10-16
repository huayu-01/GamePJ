using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// **武技 2「刹那之眼」**（角色 A 武器 3「弓」，设计文档第 87 行）：
///
/// <para>第二轮澄清：按住维持的纯效果，可以与其他技能并用。用户确认保留前 0.3 秒无动画软启动，
/// 以及启动期间的免伤与免费后撤；软启动结束后放大视角、每 0.5 秒给准星附近目标叠加标记。</para>
///
/// <para>四件事各自的落点（全部表驱动）：</para>
/// <list type="number">
/// <item><b>无动画软启动 0.3s</b>：由基类 <c>HoldToMaintain</c> / <c>HeldTime</c> 计时；
///   不进入蓄力状态、不占角色动作。用户第二轮确认保留启动期间的防御效果。</item>
/// <item><b>放大视角</b>：改当前 <c>Camera3D</c> 的 <c>Fov</c>（表值 <see cref="ZoomFov"/>，
///   松手/收招时按同样的平滑度还原）—— 这是"准星视角放大"这条机制。</item>
/// <item><b>每 0.5s 给准星附近的一个目标上标记</b>：<see cref="MarkIntervalSeconds"/> 到点就在
///   "准星世界点 <see cref="MarkRadius"/> 米内"挑**最近**的敌对单位，挂上
///   <see cref="MarkStatusId"/> 那条状态。标记是**可叠加、不限单目标**的（`Unit.ApplyStatus`
///   的 `MaxStacks` 路径，见 `docs/refactor-survey.md` 硬编码审计第 6 条）。</item>
/// <item><b>软启动期间被击中 → 无效化 + 免费的"后撤"</b>：订阅 <c>Unit.HitReceived</c>
///   （它在 <c>ReceiveHit</c> 里**先于无敌判定**触发），把 <c>IsInvulnerable</c> 置位 → 这一次命中被判为
///   无敌而不掉血；随后按表里的 <see cref="DodgeAbilityId"/> 找到「后撤」技能并强制触发一次
///   （<c>RefreshCooldown → TryActivate → RefreshCooldown</c> = **不消耗层数/冷却**）。</item>
/// </list>
///
/// <para>⚠️ <b>箭的追踪不在这里</b>：追踪是**箭自己的子弹特性**（<c>Projectile.HomingStatusId</c>，
/// 由弓的两个武技行填 <c>Mark</c>）—— 这个技能只负责"谁被标记了"。</para>
/// </summary>
public partial class HawkEyeAbility : Ability
{
	[ExportGroup("放大视角")]
	/// <summary>放大后的视野角（度；越小越放大）。</summary>
	[Export(PropertyHint.Range, "10,120,1")] public float ZoomFov { get; set; } = 32f;

	/// <summary>视角放大/还原的平滑速度（1/秒）。</summary>
	[Export] public float ZoomSharpness { get; set; } = 8f;

	[ExportGroup("标记（对准星附近的目标）")]
	/// <summary>标记状态的 Id（效果表 `Config/Tables/Effects/eff_*.tres` 的行 Id）。</summary>
	[Export] public string MarkStatusId { get; set; } = "Mark";

	/// <summary>每层标记的持续时间（秒）。</summary>
	[Export] public float MarkDuration { get; set; } = 6f;

	/// <summary>标记间隔（秒；文档：每 0.5s 一个）。</summary>
	[Export] public float MarkIntervalSeconds { get; set; } = 0.5f;

	/// <summary>"准星附近"的半径（米）—— 在这个半径内挑离准星点最近的那个目标。</summary>
	[Export] public float MarkRadius { get; set; } = 3f;

	/// <summary>求"准星世界点"时射线的最大长度。</summary>
	[Export] public float MaxAimDistance { get; set; } = 200f;

	[ExportGroup("软启动期间被击中 → 无效化 + 免费的「后撤」")]
	/// <summary>「后撤」技能的**行 Id**（强制触发时按它找技能；留空 = 不触发后撤）。</summary>
	[Export] public string DodgeAbilityId { get; set; } = "skl_a1_skill_1";

	[ExportGroup("运行期可读状态（自测 / UI）")]
	/// <summary>本次施法一共标记了几个目标。</summary>
	public int MarkedCount { get; private set; }

	/// <summary>本次施法一共被无效化了几次命中。</summary>
	public int NegatedHitCount { get; private set; }

	/// <summary>累计强制触发了几次"不消耗层数的后撤"。</summary>
	public int ForcedDodgeCount { get; private set; }

	/// <summary>软启动完成后维持瞄准，与蓄力状态和角色动作互不占用。</summary>
	public bool IsAiming => IsMaintaining && MaintainStartupComplete;
	public bool StartupProtectionActive => IsMaintaining && !MaintainStartupComplete;

	/// <summary>当前视角是否已经被放大（自测取证用）。</summary>
	public bool ZoomApplied { get; private set; }

	/// <summary>
	/// **放大视角的进度**（0 = 完全没放大，1 = 已经到 <see cref="ZoomFov"/>）—— **只读**，
	/// 供表现层（`Scripts/UI/ScopeOverlay.cs` 开镜特效）消费。
	///
	/// <para>为什么加它而不是让表现层自己读相机：表现层拿不到"施法前那个 FOV"（<see cref="ZoomFov"/>
	/// 只是目标值），所以进度只能由**知道基准的人**算。这里只是把那一次除法暴露出来，
	/// **没有改任何行为**：FOV 的写入、平滑、还原、收招时机全部与 P3 逐字相同。</para>
	/// </summary>
	public float ZoomProgress { get; private set; }

	private Camera3D? _camera;
	private float _baseFov;
	private float _zoomSpan;
	private bool _baseFovCaptured;
	private bool _restoring;
	private bool _negateOwned;
	private float _markTimer;
	private BackdashAbility? _dodge;
	private bool _hooked;

	protected override void OnReady()
	{
		if (OwnerUnit != null)
		{
			OwnerUnit.HitReceived += OnOwnerHit;
			_hooked = true;
		}
	}

	public override void _ExitTree()
	{
		if (_hooked && OwnerUnit != null && IsInstanceValid(OwnerUnit))
		{
			OwnerUnit.HitReceived -= OnOwnerHit;
		}

		_hooked = false;
		ReleaseInvulnerability();
		RestoreZoomNow();
		base._ExitTree();
	}

	protected override void OnActivate(Unit? target)
	{
		MarkedCount = 0;
		NegatedHitCount = 0;
		ForcedDodgeCount = 0;
		_markTimer = 0f;
		_restoring = false;
		ZoomApplied = false;
		ZoomProgress = 0f;
		_dodge = Ability.FindByAbilityId(OwnerUnit!, DodgeAbilityId) as BackdashAbility;
		CaptureBaseFov();
		GameLog.Write(
			"Combat",
			$"刹那之眼：{AbilityId} 后台维持开始（无动画软启动 {MaintainStartupSeconds:0.##}s，之后视角 FOV {ZoomFov:0.#}、每 {MarkIntervalSeconds:0.##}s 标记；可与其他技能并用，后撤技能行={DodgeAbilityId}）");
	}

	/// <summary>松手立即停止标记，视角独立淡出；后台收尾不能打断别的技能动画。</summary>
	protected override void OnMaintainReleased()
	{
		GameLog.Write("Combat", $"刹那之眼：{AbilityId} 松手结束（共标记 {MarkedCount} 次、无效化 {NegatedHitCount} 次命中、强制后撤 {ForcedDodgeCount} 次）");
		BeginRestoreZoom();
	}

	protected override void Tick(float delta)
	{
		// 这次命中的"无效化"只借用 IsInvulnerable 一帧；后撤自己的无敌帧由它自己管，别抢。
		if (_negateOwned && !(_dodge?.InvulnerableOwned ?? false))
		{
			ReleaseInvulnerability();
		}

		if (_restoring)
		{
			StepZoom(delta, restoring: true);
			return;
		}

		if (!IsAiming)
		{
			return;
		}

		StepZoom(delta, restoring: false);

		_markTimer += delta;
		if (MarkIntervalSeconds > 0f && _markTimer >= MarkIntervalSeconds)
		{
			_markTimer -= MarkIntervalSeconds;
			TryMark();
		}
	}

	// ────────────────────────────── 放大视角 ──────────────────────────────

	private void CaptureBaseFov()
	{
		_camera = GetViewport()?.GetCamera3D();
		if (_camera == null)
		{
			GD.PushWarning($"[HawkEyeAbility] {AbilityId} 找不到 Camera3D，视角放大这一项不生效。");
			GameLog.Write("Combat", $"⚠ 刹那之眼 {AbilityId}：没有相机，视角放大不生效。");
			return;
		}

		if (!_baseFovCaptured)
		{
			_baseFov = _camera.Fov;
			_baseFovCaptured = true;
		}

		// 表现层读数用的"放大跨度"（>0 = 这一行真的是放大；≤0 表示 ZoomFov 没收紧，进度恒 0）。
		_zoomSpan = _baseFov - ZoomFov;
	}

	/// <summary>平滑走近目标 FOV（放大 = 变小；还原 = 回到施法前那个值）。</summary>
	private void StepZoom(float delta, bool restoring)
	{
		if (_camera == null || !IsInstanceValid(_camera))
		{
			_camera = GetViewport()?.GetCamera3D();
			if (_camera == null)
			{
				ConclusionReady();
				return;
			}
		}

		float target = restoring ? _baseFov : ZoomFov;
		float weight = ZoomSharpness <= 0f ? 1f : 1f - Mathf.Exp(-ZoomSharpness * delta);
		_camera.Fov = Mathf.Lerp(_camera.Fov, target, weight);
		ZoomApplied = Mathf.Abs(_camera.Fov - _baseFov) > 0.5f;
		ZoomProgress = _zoomSpan <= 0.0001f
			? 0f
			: Mathf.Clamp((_baseFov - _camera.Fov) / _zoomSpan, 0f, 1f);

		if (restoring && Mathf.Abs(_camera.Fov - _baseFov) < 0.05f)
		{
			_camera.Fov = _baseFov;
			ZoomApplied = false;
			ZoomProgress = 0f;
			ConclusionReady();
		}
	}

	/// <summary>
	/// 视角收尾：**还原到施法前那个值之后才算收招**（<c>Finish()</c>）。
	/// 这样"放大视角"这件事不会在技能结束后留在相机上。
	/// </summary>
	private void ConclusionReady()
	{
		_restoring = false;
		if (IsActive)
		{
			Finish();
		}
	}

	private void BeginRestoreZoom()
	{
		if (_camera == null || !_baseFovCaptured)
		{
			ConclusionReady();
			return;
		}

		_restoring = true;
	}

	/// <summary>立刻还原（退树 / 被中断时用，不给相机留状态）。</summary>
	private void RestoreZoomNow()
	{
		if (_camera != null && IsInstanceValid(_camera) && _baseFovCaptured)
		{
			_camera.Fov = _baseFov;
		}

		ZoomApplied = false;
		ZoomProgress = 0f;
		_restoring = false;
		_baseFovCaptured = false;
	}

	// ────────────────────────────── 标记 ──────────────────────────────

	/// <summary>
	/// 对准星**附近最近的一个敌对单位**挂一层标记。标记状态来自效果表（可叠加、不限单目标），
	/// 所以这里不需要任何"谁能不能被标记"的白名单。
	/// </summary>
	private bool TryMark()
	{
		if (OwnerUnit == null || string.IsNullOrEmpty(MarkStatusId))
		{
			return false;
		}

		Vector3 point = AimUtil.GetCrosshairPoint(
			GetViewport(), OwnerUnit, MaxAimDistance, CombatLayers.World | CombatLayers.UnitMask);

		Unit? best = null;
		float bestDistance = MarkRadius * MarkRadius;
		foreach (Node node in GetTree().GetNodesInGroup("enemy_units"))
		{
			if (node is not Unit candidate || !candidate.IsValidTarget || !OwnerUnit.IsHostileTo(candidate))
			{
				continue;
			}

			float distance = point.DistanceSquaredTo(candidate.GlobalPosition);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = candidate;
			}
		}

		if (best == null || StatusPresets.Create(MarkStatusId, MarkDuration) is not { } status)
		{
			return false;
		}

		best.ApplyStatus(status);
		MarkedCount++;
		GameLog.Write(
			"Combat",
			$"刹那之眼·标记：{AbilityId} → {best.Name}（准星点 {MarkRadius:0.##}m 内最近；{MarkStatusId} 现在 {best.GetStatusStacks(MarkStatusId)} 层 / 上限 {status.MaxStacks}，"
			+ $"持续 {status.Duration:0.##}s）");
		return true;
	}

	// ────────────────────────── 被击中 → 无效化 + 免费后撤 ──────────────────────────

	/// <summary>
	/// <c>Unit.HitReceived</c> 在 <c>ReceiveHit</c> 里**先于无敌判定**触发（见 <c>Unit.ReceiveHit</c> 的开头），
	/// 所以在这里把 <c>IsInvulnerable</c> 置位就等价于"无效化这一次伤害"，而且**不改 Unit 一行代码**。
	/// 条件 = 本次维持尚未完成软启动；进入瞄准后不再提供这个防御窗口。
	/// </summary>
	private void OnOwnerHit(HitInstance hit)
	{
		if (!StartupProtectionActive || OwnerUnit == null)
		{
			return;
		}

		_negateOwned = !OwnerUnit.IsInvulnerable;
		OwnerUnit.IsInvulnerable = true;
		NegatedHitCount++;

		GameLog.Write(
			"Combat",
			$"刹那之眼·无效化：{AbilityId} 在无动画软启动期间（{HeldTime:0.###}s / {MaintainStartupSeconds:0.##}s）被 {hit.Attacker?.Name ?? "（未知）"} 击中 → 无效化伤害并强制免费后撤");

		ForceFreeDodge();
		OnInputReleased();
	}

	/// <summary>
	/// **强制触发一次不消耗层数的「后撤」**：<c>RefreshCooldown</c>（把层数/冷却补满）→ <c>TryActivate</c>
	/// → 再 <c>RefreshCooldown</c>（把这一次消耗补回去）。全程只认**技能行 Id**（表里的
	/// <see cref="DodgeAbilityId"/>），不认节点名 / 类名。
	/// </summary>
	private void ForceFreeDodge()
	{
		_dodge ??= Ability.FindByAbilityId(OwnerUnit!, DodgeAbilityId) as BackdashAbility;
		if (_dodge == null)
		{
			GD.PushWarning($"[HawkEyeAbility] {AbilityId} 要强制触发「{DodgeAbilityId}」，但玩家身上找不到这个技能。");
			GameLog.Write("Combat", $"⚠ 刹那之眼 {AbilityId}：找不到后撤技能行 {DodgeAbilityId}，强制后撤没生效。");
			return;
		}

		_dodge.RefreshCooldown();
		bool fired = _dodge.TryActivate();
		_dodge.RefreshCooldown();
		if (fired)
		{
			ForcedDodgeCount++;
		}

		GameLog.Write(
			"Combat",
			$"刹那之眼·强制后撤：{DodgeAbilityId} TryActivate={fired} → 层数/冷却已补回（**不消耗层数**，层数={_dodge.ChargesRemaining}/{_dodge.MaxCharges}）");
	}

	private void ReleaseInvulnerability()
	{
		if (!_negateOwned)
		{
			return;
		}
		_negateOwned = false;
		if (OwnerUnit != null && IsInstanceValid(OwnerUnit))
		{
			OwnerUnit.IsInvulnerable = false;
		}
	}

	protected override void OnFinishOrCancel()
	{
		if (!(_dodge?.InvulnerableOwned ?? false))
		{
			ReleaseInvulnerability();
		}
		else
		{
			// 无敌帧已交给后撤，不能由后台效果的收尾提前清掉。
			_negateOwned = false;
		}
		RestoreZoomNow();
	}
}

using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// **武技 2「刹那之眼」**（角色 A 武器 3「弓」，设计文档第 87 行）：
///
/// <blockquote>「基础蓄力{0.3}秒后**放大视角**，每{0.5}秒的对准星附近的某个目标施加"标记"，
/// 你射出的箭会追踪被打上"标记"的最近目标，基础蓄力期间如果被击中会**无效化这次伤害**并
/// **强制触发一次不消耗层数的"后撤"**」</blockquote>
///
/// <para>四件事各自的落点（全部表驱动）：</para>
/// <list type="number">
/// <item><b>基础蓄力 0.3s</b>：<see cref="Ability"/> 基类那套（<c>HoldToCharge</c> +
///   <c>ChargeBaseSeconds</c>），本类只在"蓄满之后仍然按住"的区间里做下面两件事。</item>
/// <item><b>放大视角</b>：改当前 <c>Camera3D</c> 的 <c>Fov</c>（表值 <see cref="ZoomFov"/>，
///   松手/收招时按同样的平滑度还原）—— 这是"准星视角放大"这条机制。</item>
/// <item><b>每 0.5s 给准星附近的一个目标上标记</b>：<see cref="MarkIntervalSeconds"/> 到点就在
///   "准星世界点 <see cref="MarkRadius"/> 米内"挑**最近**的敌对单位，挂上
///   <see cref="MarkStatusId"/> 那条状态。标记是**可叠加、不限单目标**的（`Unit.ApplyStatus`
///   的 `MaxStacks` 路径，见 `docs/refactor-survey.md` 硬编码审计第 6 条）。</item>
/// <item><b>基础蓄力期间被击中 → 无效化 + 免费的"后撤"</b>：订阅 <c>Unit.HitReceived</c>
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

	[ExportGroup("基础蓄力期间被击中 → 无效化 + 免费的「后撤」")]
	/// <summary>「后撤」技能的**行 Id**（强制触发时按它找技能；留空 = 不触发后撤）。</summary>
	[Export] public string DodgeAbilityId { get; set; } = "skl_a1_skill_1";

	[ExportGroup("运行期可读状态（自测 / UI）")]
	/// <summary>本次施法一共标记了几个目标。</summary>
	public int MarkedCount { get; private set; }

	/// <summary>本次施法一共被无效化了几次命中。</summary>
	public int NegatedHitCount { get; private set; }

	/// <summary>累计强制触发了几次"不消耗层数的后撤"。</summary>
	public int ForcedDodgeCount { get; private set; }

	/// <summary>现在是不是"基础蓄力已完成、正在瞄准"（放大视角中）。</summary>
	public bool IsAiming => IsBusy && IsCharging && ChargeBaseComplete;

	/// <summary>当前视角是否已经被放大（自测取证用）。</summary>
	public bool ZoomApplied { get; private set; }

	private Camera3D? _camera;
	private float _baseFov;
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
		_dodge = Ability.FindByAbilityId(OwnerUnit!, DodgeAbilityId) as BackdashAbility;
		CaptureBaseFov();
		PlaySkillAnimation();
		GameLog.Write(
			"Combat",
			$"刹那之眼：{AbilityId} 开始蓄力（基础 {ChargeBaseSeconds:0.##}s，蓄满后放宽视角到 FOV {ZoomFov:0.#} 并每 {MarkIntervalSeconds:0.##}s 标记准星 {MarkRadius:0.##}m 内最近的目标；后撤技能行={DodgeAbilityId}）");
	}

	/// <summary>动画里 <c>AimOn</c> 那一帧 = 立刻做第一次标记尝试（表现与逻辑对齐）。</summary>
	protected override void OnAnimEvent(string name) => TryMark();

	/// <summary>蓄满之后松手 = 结束瞄准（视角还原、收招）。</summary>
	protected override void OnChargeReleased(bool perfect, int steps, float powerScale)
	{
		GameLog.Write("Combat", $"刹那之眼：{AbilityId} 松手结束（共标记 {MarkedCount} 次、无效化 {NegatedHitCount} 次命中、强制后撤 {ForcedDodgeCount} 次）");
		BeginRestoreZoom();
	}

	/// <summary>基础蓄力没完成就松手 = 不释放（设计文档第 14 行）。</summary>
	protected override void OnChargeAborted()
	{
		GameLog.Write("Combat", $"刹那之眼：{AbilityId} 基础蓄力未完成（{ChargeTime:0.##}s / {ChargeBaseSeconds:0.##}s）就松手，本次不释放。");
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

		if (restoring && Mathf.Abs(_camera.Fov - _baseFov) < 0.05f)
		{
			_camera.Fov = _baseFov;
			ZoomApplied = false;
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
		if (IsBusy)
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
		_restoring = false;
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
	/// 条件 = 设计文档那句"**基础蓄力期间**如果被击中"（已蓄满、正在瞄准时不算）。
	/// </summary>
	private void OnOwnerHit(HitInstance hit)
	{
		if (!IsBusy || !IsCharging || ChargeBaseComplete || OwnerUnit == null)
		{
			return;
		}

		OwnerUnit.IsInvulnerable = true;
		_negateOwned = true;
		NegatedHitCount++;

		GameLog.Write(
			"Combat",
			$"刹那之眼·无效化：{AbilityId} 在基础蓄力期间（{ChargeTime:0.###}s / {ChargeBaseSeconds:0.##}s）被 {hit.Attacker?.Name ?? "（未知）"} 击中 → 这次伤害被无效化（IsInvulnerable 置位，随后强制一次不消耗层数的「后撤」）");

		ForceFreeDodge();
		BeginRestoreZoom();
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
		_negateOwned = false;
		if (OwnerUnit != null && IsInstanceValid(OwnerUnit))
		{
			OwnerUnit.IsInvulnerable = false;
		}
	}
}

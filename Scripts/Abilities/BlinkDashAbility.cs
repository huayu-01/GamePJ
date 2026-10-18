using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// **技能 1「闪」**（角色 B，设计文档第 101 行）：
///
/// <code>
/// 技能1[闪]{cd:5}:角色向准星方向冲刺(全程穿透但无法穿透地形),对路径上的敌人造成伤害{n}
///                (可以冲至空中,需要设立一条可视化的路径).
/// </code>
///
/// <list type="bullet">
/// <item><b>向准星方向冲刺</b>：<see cref="Ability.Aim"/> = Crosshair（表列），方向走基类的
///   <see cref="Ability.ResolveAimDirection"/>。</item>
/// <item><b>全程穿透</b>：冲刺期间角色的 <c>CollisionMask</c> 只留**世界层**（地形），
///   单位层被摘掉 —— 于是"穿过敌人但撞得到地形"这件事由**物理配置**表达，
///   而 <c>MoveAndSlide</c> 仍然会沿地形滑（"无法穿透地形"）。</item>
/// <item><b>对路径上的敌人造成伤害</b>：冲刺期间命中盒一直开着（<see cref="HitPolicy.IntervalPerTarget"/>）。</item>
/// <item><b>可以冲至空中</b>：真实Action后由CharacterMovement持有完整三维速度，
///   结束归还普通重力。最后不足一物理步的距离按剩余时长缩短，避免超出配置距离。</item>
/// <item><b>可视化的路径</b>：<see cref="DashPathIndicator"/> 沿冲刺方向采样一条线，
///   颜色/线宽/抬升全来自表列。</item>
/// </list>
///
/// <para>与角色 A 的「后撤」（<c>BackdashAbility</c>，走外力 + 帧无敌）的差别只在**数据**：那一条是
/// <c>Aim=Facing</c> + 无敌帧 + 不摘碰撞层；这一条是 <c>Aim=Crosshair</c> + 穿透 + 路径线。
/// 所以它是**新类**而不是改既有类（既有类的语义一个字没动）。</para>
/// <para>⚠️ P6a：原来这里拿来对照的 <c>DashAbility</c>（旧角色 1 的战术滑铲）已随旧角色删除。</para>
/// </summary>
public partial class BlinkDashAbility : Ability
{
	[ExportGroup("冲刺")]
	/// <summary>冲刺总距离（米）。文档写的是 `{n}` —— 表里给"能跑起来的最小预设值"。</summary>
	[Export] public float Distance { get; set; } = 7f;

	/// <summary>冲刺时长（秒）；速度 = 距离 ÷ 时长。</summary>
	[Export] public float Duration { get; set; } = 0.18f;

	/// <summary>冲刺期间是否**穿透单位**（只留世界层）。文档：「全程穿透」。</summary>
	[Export] public bool PierceUnits { get; set; } = true;

	/// <summary>冲刺期间是否无敌（文档没写无敌，默认关 —— 不改既有语义）。</summary>
	[Export] public bool GrantInvulnerability { get; set; }

	[ExportGroup("可视化路径（文档：需要设立一条可视化的路径）")]
	/// <summary>要不要画那条线（关掉 = 与没有这个功能逐位一致）。</summary>
	[Export] public bool ShowPath { get; set; } = true;

	/// <summary>线的颜色（RGBA）。</summary>
	[Export] public Color PathColor { get; set; } = new(0.55f, 0.9f, 1f, 0.85f);

	/// <summary>线宽（米）。</summary>
	[Export] public float PathWidth { get; set; } = 0.07f;

	/// <summary>离地抬升（米；避免与地面 z-fighting）。</summary>
	[Export] public float PathGroundOffset { get; set; } = 0.06f;

	/// <summary>采样段数（≥1）。</summary>
	[Export] public int PathSteps { get; set; } = 16;

	/// <summary>线在冲刺结束后还留多久（秒；0 = 冲刺一结束就收）。</summary>
	[Export] public float PathLingerSeconds { get; set; } = 0.15f;

	[ExportGroup("运行期可读状态（自测 / UI）")]
	/// <summary>本次冲刺实际用的方向。</summary>
	public Vector3 LastDirection { get; private set; }

	/// <summary>本次画了几个点（0 = 没画）。</summary>
	public int LastPathPoints { get; private set; }

	/// <summary>冲刺期间摘掉过单位的碰撞层吗（自测取证："穿透"真的生效）。</summary>
	public bool PierceApplied { get; private set; }

	private AttackHitbox? _hitbox;
	private DashPathIndicator? _path;
	private uint _savedMask;
	private bool _maskSaved;
	private float _linger;
	private bool _invulnerableOwned;
	private bool _started;
	private Vector3 _previousPosition;

	protected override void OnReady()
	{
		_hitbox = GetNodeOrNull<AttackHitbox>("Attack");
		_path = GetNodeOrNull<DashPathIndicator>("Path");
		if (_path != null)
		{
			_path.Configure(PathColor, PathWidth, PathGroundOffset);
		}
	}

	protected override bool CanActivate(Unit? target) => OwnerUnit != null && Movement != null;

	protected override void OnActivate(Unit? target)
	{
		_linger = 0;
		_started = false;
		LastPathPoints = 0;
		if (OwnerUnit == null || Movement == null)
		{
			Cancel();
			return;
		}

		if (string.IsNullOrEmpty(AnimName) || string.IsNullOrEmpty(AnimEvent) || PlaySkillAnimation().Length == 0) Cancel();
	}
	protected override void OnAnimEvent(string name)
	{
		if (name != AnimEvent || _started || OwnerUnit == null || Movement == null) return;
		Vector3 direction = ResolveAimDirection();
		if (direction.LengthSquared() < 0.04f)
		{
			direction = -OwnerUnit.GlobalTransform.Basis.Z;
		}

		// ⚠️ 方向**保留 Y 分量**（"可以冲至空中"）：而不是压平它。
		direction = direction.Normalized();
		LastDirection = direction;

		float speed = Distance / Mathf.Max(0.05f, Duration);
		if (!Movement.BeginVelocityOverride(this, direction * speed, Mathf.Max(0.05f, Duration), isDash: true)) { Cancel(); return; }
		_previousPosition = OwnerUnit.GlobalPosition;
		_started = true;
		PierceApplied = false;

		if (PierceUnits)
		{
			_savedMask = OwnerUnit.CollisionMask;
			_maskSaved = true;
			// 只留世界层（地形）：单位层被摘掉 → 全程穿透；MoveAndSlide 仍会沿地形滑（无法穿透地形）。
			OwnerUnit.CollisionMask = CombatLayers.World;
			PierceApplied = true;
		}

		if (GrantInvulnerability && !OwnerUnit.IsInvulnerable)
		{
			OwnerUnit.IsInvulnerable = true;
			_invulnerableOwned = true;
		}

		AttackOffense offense = AttackOffense.Capture(OwnerUnit, _hitbox?.CritChance ?? 0f, _hitbox?.CritMultiplier ?? 2f);
		_hitbox?.Activate(OwnerUnit, new AttackInstance
		{
			Attacker = OwnerUnit,
			Ability = AbilityId,
			BaseDamage = (_hitbox?.Damage ?? 0f) * offense.DamageMultiplier,
			CanCrit = true,
			CritChance = offense.CritChance,
			CritMultiplier = offense.CritMultiplier,
			Knockback = _hitbox?.Knockback ?? 0f,
			Policy = HitPolicy.IntervalPerTarget,
			HitInterval = _hitbox?.HitInterval ?? 0.2f,
			Tags = _hitbox?.AttackTags ?? System.Array.Empty<string>(),
			AbilityKind = AbilityId,
		});

		DrawPath();

		GameLog.Write(
			"Combat",
			$"闪：{AbilityId} 朝 {direction} 冲刺 {Distance:0.##}m / {Duration:0.##}s（速度 {speed:0.##}）"
			+ $"｜穿透单位={PierceUnits}（碰撞层摘到只剩世界层：{PierceApplied}）无敌={GrantInvulnerability}"
			+ $"｜可视化路径 {LastPathPoints} 点（颜色 {PathColor} 宽 {PathWidth:0.##}）—— 路径抬升 {PathGroundOffset:0.##}m");
	}

	/// <summary>沿冲刺方向采样一条线（点从**角色前方**出发，并整体抬高避免贴地闪烁）。</summary>
	private void DrawPath()
	{
		if (!ShowPath || _path == null || OwnerUnit == null)
		{
			LastPathPoints = 0;
			return;
		}

		Vector3 origin = OwnerUnit.GlobalPosition + (Vector3.Up * PathGroundOffset);
		_path.Configure(PathColor, PathWidth, PathGroundOffset);
		var points = DashPathIndicator.SampleLine(origin, LastDirection, Distance, PathSteps);
		_path.Show(points);
		LastPathPoints = points.Count;
	}

	protected override void Tick(float delta)
	{
		if (!IsBusy)
		{
			// 冲刺结束后路径还能留一会儿（表现层的事，与逻辑无关）。
			if (_linger > 0f)
			{
				_linger -= delta;
				if (_linger <= 0f)
				{
					_path?.HidePath();
				}
			}

			return;
		}

		if (!_started)
		{
			if (StateTime > AnimationEventTimeout / Mathf.Max(0.01f, AnimSpeedScale)) Cancel();
			return;
		}
		if (OwnerUnit != null)
		{
			_hitbox?.SweepSphere(_previousPosition, OwnerUnit.GlobalPosition);
			_previousPosition = OwnerUnit.GlobalPosition;
		}
		if (Movement != null && Movement.VelocityOverrideFinished(this))
		{
			EndDash();
			if (PathLingerSeconds > 0f)
			{
				_linger = PathLingerSeconds;
				Finish();
			}
			else
			{
				_path?.HidePath();
				Finish();
			}
		}
	}

	private void EndDash()
	{
		Movement?.EndVelocityOverride(this);
		_started = false;
		RestorePierce();

		if (_invulnerableOwned && OwnerUnit != null && IsInstanceValid(OwnerUnit))
		{
			OwnerUnit.IsInvulnerable = false;
		}

		_invulnerableOwned = false;
		_hitbox?.Deactivate();
	}

	/// <summary>把碰撞层还原（**必须还原**：不还原的话"穿透"会永久留在角色身上）。</summary>
	private void RestorePierce()
	{
		if (!_maskSaved || OwnerUnit == null || !IsInstanceValid(OwnerUnit))
		{
			PierceApplied = false;
			return;
		}

		OwnerUnit.CollisionMask = _savedMask;
		_maskSaved = false;
		PierceApplied = false;
	}

	protected override void OnFinishOrCancel()
	{
		EndDash();
		if (_linger <= 0) _path?.HidePath();
	}

	protected override void OnLoadoutReset()
	{
		EndDash();
		_linger = 0;
		_path?.HidePath();
	}

	public override void _ExitTree()
	{
		EndDash();
		_path?.HidePath();
		base._ExitTree();
	}
}

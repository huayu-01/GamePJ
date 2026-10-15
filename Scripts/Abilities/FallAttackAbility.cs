using System;
using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// **下落攻击**（角色 B 被动「落」/ 三把武器共用的那条"空中攻击"，设计文档第 99 行）：
///
/// <code>
/// 被动技能[落]{cd:1}:当角色在空中时按下奔跑键或ctrl键可以使用下落攻击,
///   会大幅提高下落速度并在下落过程中碰到的敌人造成伤害,
///   高度越高时伤害越高,每{n}增加{20}%,最高增加{100}%
/// </code>
///
/// <list type="bullet">
/// <item><b>触发</b>：空中 + 按键（表列 <c>InputAction</c> = `fall_attack`）。
///   不满足（在地面 / 冷却没好）→ <see cref="CanActivate"/> 返回 false，技能根本不执行。</item>
/// <item><b>下落速度</b>：<see cref="DiveSpeed"/> 直接写垂直速度（"大幅提高下落速度"）。</item>
/// <item><b>下落途中伤害</b>：命中盒在**下落期间一直开着**（<see cref="HitPolicy.IntervalPerTarget"/> 配
///   <c>HitInterval</c>，同一目标按间隔反复吃伤）。</item>
/// <item><b>高度越高伤害越高</b>：高度取 <see cref="CharacterMovement.FallHeight"/>（"这次离地后
///   **净下落**了多少米"，物理量、不是"离地高度"），每满 <see cref="HeightStep"/> 米加
///   <see cref="DamageBonusPerStep"/>（文档 20%），封顶 <see cref="MaxDamageBonus"/>（文档 100%）。</item>
/// <item><b>落地那一帧</b>：命中盒的最后一次判定 + 触发 <see cref="Landed"/> 事件 +
///   <see cref="OnLanded"/> 钩子 —— 「落剑式」（落地额外一圈范围攻击）与「落日余晖」
///   （朝准星方位冲下）都挂在这一个事件上，见 <see cref="LandingBurstAbility"/> 与
///   <see cref="BlinkDashAbility"/> 的说明。</item>
/// </list>
///
/// <para><b>「落日余晖」的"朝准星指向的最近方位冲下"</b>：<see cref="HomingMode"/> 是**表驱动的枚举**
/// （<c>None</c> = 直直下落、<c>Crosshair</c> = 朝准星方位冲下）。超出指向范围时（夹角大于
/// <see cref="MaxDiveAngleDegrees"/>）就夹到最大范围的**同方向**上；准星没指向地面
/// （射线没打到地面 / 打到的点在单位上方）时回落成直直下落 —— 与文档第 155 行逐条对应。</para>
///
/// <para>所有数值（一个都没有硬编码）来自技能行 `skl_b*_passive.tres` 与
/// `skl_b*_w*_fall.tres` 的列与 Params。</para>
/// </summary>
public partial class FallAttackAbility : Ability
{
	/// <summary>下落时的朝向模式（表列 <c>HomingMode</c>）。</summary>
	public enum DiveMode
	{
		/// <summary>直直下落（被动「落」/「落剑式」）。</summary>
		Straight,

		/// <summary>朝准星指向的最近方位冲下（「落日余晖」）。</summary>
		Crosshair,
	}

	[ExportGroup("下落攻击（被动「落」）")]
	/// <summary>下落速度（m/s，写垂直速度）。文档："大幅提高下落速度"。</summary>
	[Export] public float DiveSpeed { get; set; } = 34f;

	/// <summary>下落期间命中盒的开启时长上限（秒；落地会提前结束）。</summary>
	[Export] public float MaxDiveSeconds { get; set; } = 4f;

	/// <summary>落地后收招的延迟（秒）—— 给表现层留一点时间。</summary>
	[Export] public float LandRecoverSeconds { get; set; } = 0.08f;

	[ExportGroup("高度越高伤害越高（文档：每 n 米 +20%，最高 +100%）")]
	/// <summary>**每多少米加一档**（文档里的 `n`；表里填"能跑起来的最小预设值" 2m）。</summary>
	[Export] public float HeightStep { get; set; } = 2f;

	/// <summary>每档增加的伤害比例（文档 20% → 0.2）。</summary>
	[Export] public float DamageBonusPerStep { get; set; } = 0.2f;

	/// <summary>伤害加成上限（文档 100% → 1.0）。</summary>
	[Export] public float MaxDamageBonus { get; set; } = 1f;

	[ExportGroup("「落日余晖」：朝准星指向的最近方位冲下")]
	/// <summary>冲下模式（表列 <c>HomingMode</c>；0 = Straight / 1 = Crosshair）。</summary>
	[Export] public DiveMode HomingMode { get; set; } = DiveMode.Straight;

	/// <summary>**指向范围**（度）：准星方位与"正下方"的最大夹角。超过它就把水平分量夹到最大范围同方向上。</summary>
	[Export] public float MaxDiveAngleDegrees { get; set; } = 35f;

	/// <summary>求"准星世界点"时射线的最大长度（判断"准星有没有指向地面"用）。</summary>
	[Export] public float MaxAimDistance { get; set; } = 60f;

	[ExportGroup("运行期可读状态（自测 / UI / 落剑式）")]
	/// <summary>本次下落攻击**起手时**的高度（米）。</summary>
	public float LastFallHeight { get; private set; }

	/// <summary>本次下落攻击的**伤害加成比例**（0.2 = +20%）。</summary>
	public float LastDamageBonus { get; private set; }

	/// <summary>本次下落攻击实际用的伤害（表值 ×(1+加成)）——「落剑式」的额外范围攻击读它做基准。</summary>
	public float LastDamage { get; private set; }

	/// <summary>落地了（自测取证）。</summary>
	public bool HasLanded { get; private set; }

	/// <summary>**落地事件**：「落剑式」的额外范围攻击挂它。</summary>
	public event Action? Landed;

	/// <summary>下落期间每次判定额外加成的比例（自测取一档看得到）。</summary>
	public float DamageMultiplierForHeight(float height)
	{
		if (HeightStep <= 0f)
		{
			return 1f;
		}

		int steps = Mathf.FloorToInt(Mathf.Max(0f, height) / HeightStep);
		float bonus = Mathf.Min(MaxDamageBonus, steps * DamageBonusPerStep);
		return 1f + Mathf.Max(0f, bonus);
	}

	private AttackHitbox? _hitbox;
	private bool _diving;
	private bool _landed;
	private float _diveTime;
	private float _landTimer;

	protected override void OnReady()
	{
		_hitbox = GetNodeOrNull<AttackHitbox>("Attack");
	}

	/// <summary>**只有在空中才能用**（文档：「当角色在空中时…可以使用下落攻击」）。</summary>
	protected override bool CanActivate(Unit? target) => Movement is { IsGrounded: false };

	/// <summary>自测用：把 <see cref="CanActivate"/> 的结果暴露出来（它就是那条"空中才能用"的闸门）。</summary>
	public bool CanActivateForTest() => CanActivate(null);

	protected override void OnActivate(Unit? target)
	{
		if (OwnerUnit == null || Movement == null)
		{
			Cancel();
			return;
		}

		_diving = true;
		_landed = false;
		_diveTime = 0f;
		_landTimer = 0f;
		HasLanded = false;

		LastFallHeight = Movement.FallHeight;
		LastDamageBonus = DamageMultiplierForHeight(LastFallHeight) - 1f;
		LastDamage = (_hitbox?.Damage ?? 0f) * (1f + LastDamageBonus);

		PlaySkillAnimation();

		// 大幅提高下落速度（"落下攻击"的本体）。
		Movement.SetVerticalVelocity(-Mathf.Abs(DiveSpeed));
		ApplyDiveDirection();

		// 下落期间命中盒一直开着（同一目标按 HitInterval 反复吃伤）。
		ActivateDiveHitbox();

		GameLog.Write(
			"Combat",
			$"下落攻击：{AbilityId} 起手高度 {LastFallHeight:0.##}m（每 {HeightStep:0.##}m +{DamageBonusPerStep * 100f:0.#}%，上限 +{MaxDamageBonus * 100f:0.#}%）"
			+ $" → 加成 +{LastDamageBonus * 100f:0.#}% 伤害 {LastDamage:0.##}｜下落速度 {-Mathf.Abs(DiveSpeed):0.##} m/s 模式 {HomingMode}");
	}

	/// <summary>「落日余晖」：把下落方向拨到准星方位（夹在指向范围内）。</summary>
	private void ApplyDiveDirection()
	{
		if (HomingMode != DiveMode.Crosshair || OwnerUnit == null || Movement == null)
		{
			return;
		}

		Vector3 direction = ResolveDiveDirection();
		Vector3 planar = new(direction.X, 0f, direction.Z);
		float horizontal = planar.Length();
		if (horizontal < 0.001f)
		{
			GameLog.Write("Combat", $"落日余晖：{AbilityId} 准星没有指向地面（或指向不出去）→ 直直下落。");
			return;
		}

		// 水平分量以 DiveSpeed 的分量给出：夹角越大水平越快（夹在 MaxDiveAngleDegrees 内）。
		float angle = Mathf.Clamp(Mathf.RadToDeg(Mathf.Atan2(horizontal, Mathf.Abs(direction.Y))), 0f, MaxDiveAngleDegrees);
		Vector3 flat = planar.Normalized();
		float vertical = -Mathf.Abs(DiveSpeed) * Mathf.Cos(Mathf.DegToRad(angle));
		float lateral = Mathf.Abs(DiveSpeed) * Mathf.Sin(Mathf.DegToRad(angle));

		Movement.SetVerticalVelocity(vertical);
		Movement.AddImpulse(flat * lateral);

		GameLog.Write(
			"Combat",
			$"落日余晖：{AbilityId} 朝准星方位冲下（方位 {flat}，夹角 {angle:0.#}° ≤ 指向范围 {MaxDiveAngleDegrees:0.#}°，垂直 {vertical:0.##} 水平 {lateral:0.##}）"
			+ $"｜准星世界点 {AimPoint()}");
	}

	/// <summary>准星打到的东西（判断"有没有指向地面"）→ 朝那个点的方向；没打到地面就返回向下。</summary>
	private Vector3 ResolveDiveDirection()
	{
		if (OwnerUnit == null)
		{
			return Vector3.Down;
		}

		Vector3 point = AimPoint();
		Vector3 delta = point - OwnerUnit.GlobalPosition;
		return delta.LengthSquared() > 0.0001f ? delta.Normalized() : Vector3.Down;
	}

	private Vector3 AimPoint()
	{
		if (OwnerUnit == null)
		{
			return Vector3.Zero;
		}

		return AimUtil.GetCrosshairPoint(
			GetViewport(),
			OwnerUnit,
			MaxAimDistance,
			CombatLayers.World | CombatLayers.UnitMask);
	}

	private void ActivateDiveHitbox()
	{
		if (_hitbox == null || OwnerUnit == null)
		{
			return;
		}

		var attack = new AttackInstance
		{
			Attacker = OwnerUnit,
			Ability = AbilityId,
			BaseDamage = LastDamage,
			CritChance = _hitbox.CritChance,
			CritMultiplier = _hitbox.CritMultiplier,
			Knockback = _hitbox.Knockback,
			Policy = HitPolicy.IntervalPerTarget,
			HitInterval = _hitbox.HitInterval,
			Tags = _hitbox.AttackTags,
			AbilityKind = AbilityId,
		};

		_hitbox.Activate(OwnerUnit, attack);
	}

	protected override void Tick(float delta)
	{
		if (!IsBusy)
		{
			return;
		}

		if (_diving)
		{
			_diveTime += delta;
			// 落地判定：物理帧里 IsOnFloor 为真（下落攻击把垂直速度压到 -DiveSpeed，一碰地就为真）。
			if (Movement is { IsGrounded: true })
			{
				Land();
				return;
			}

			if (_diveTime > MaxDiveSeconds)
			{
				GameLog.Write("Combat", $"⚠ 下落攻击 {AbilityId} 超过 {MaxDiveSeconds:0.##}s 还没落地，收招（命中盒关掉）。");
				Land();
			}

			return;
		}

		if (_landed)
		{
			_landTimer -= delta;
			if (_landTimer <= 0f)
			{
				Finish();
			}
		}
	}

	/// <summary>落地：关命中盒 + 抛 <see cref="Landed"/>（「落剑式」「落日余晖」挂它）+ 进入收招。</summary>
	private void Land()
	{
		_diving = false;
		_landed = true;
		HasLanded = true;
		_landTimer = Mathf.Max(0.02f, LandRecoverSeconds);
		_hitbox?.Deactivate();

		GameLog.Write("Combat", $"下落攻击：{AbilityId} 落地（起手高度 {LastFallHeight:0.##}m，伤害 {LastDamage:0.##}）—— 触发落地事件给「落剑式/落日余晖」这类被动。");
		Landed?.Invoke();
	}

	/// <summary>
	/// 「落剑式」等被动读它：**这次的伤害**（"不改变原有攻击，就是多一圈攻击判定"——
	/// 额外那圈用同一个基准伤害，所以被动不需要自己再算一遍高度）。
	/// </summary>
	public float CurrentBasisDamage => LastDamage;

	public float CurrentBasisHeight => LastFallHeight;

	public override void _ExitTree()
	{
		_hitbox?.Deactivate();
		base._ExitTree();
	}

	protected override void OnFinishOrCancel()
	{
		_diving = false;
		_hitbox?.Deactivate();
	}
}

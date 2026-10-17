using System;
using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// 武器大师共通「落」：按下记录脚下真实World高度，Action事件才下冲并打开命中体。
/// 2026-10-17用户更新：倍率=起手高度/普通单跳高度×垂直速度/标准速度，有表配置上限。
/// 标准单跳为1倍；额外空中跳高50%时同速为1.5倍，不额外再加1。
/// 长枪由武器资源覆盖准星定向；只有真实落地派发Landed，超时与打断均取消。
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
	/// <summary>下冲总速度（m/s）；伤害只取该方向的垂直向下分量。</summary>
	[Export] public float DiveSpeed { get; set; } = 34f;

	/// <summary>下落期间命中盒的开启时长上限（秒；落地会提前结束）。</summary>
	[Export] public float MaxDiveSeconds { get; set; } = 4f;

	/// <summary>落地后收招的延迟（秒）—— 给表现层留一点时间。</summary>
	[Export] public float LandRecoverSeconds { get; set; } = 0.08f;

	[ExportGroup("高度与垂直速度倍率（2026-10-17用户更新）")]
	[Export] public float StandardDiveSpeed { get; set; } = 34f;
	[Export] public float MaxDamageMultiplier { get; set; } = 2f;
	[Export] public float EventWaitSeconds { get; set; } = 2f;
	[Export] public float GroundProbeDistance { get; set; } = 512f;
	[Export] public float GroundMinNormalY { get; set; } = 0.6f;

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
	public float LastDamageMultiplier => 1 + LastDamageBonus;
	public float LastReferenceFallTime { get; private set; }
	public float LastDownwardSpeed { get; private set; }
	public Vector3 LastDiveDirection { get; private set; } = Vector3.Down;
	public Vector3 LastAimPoint { get; private set; }
	public bool HasGroundAim { get; private set; }

	/// <summary>本次下落攻击实际用的伤害（表值 ×(1+加成)）——「落剑式」的额外范围攻击读它做基准。</summary>
	public float LastDamage { get; private set; }

	/// <summary>落地了（自测取证）。</summary>
	public bool HasLanded { get; private set; }

	/// <summary>只读的下落阶段，供风效读取；不含落地后的收招。</summary>
	public bool IsDiving => _diving;

	/// <summary>**落地事件**：「落剑式」的额外范围攻击挂它。</summary>
	public event Action? Landed;

	/// <summary>下落期间每次判定额外加成的比例（自测取一档看得到）。</summary>
	public float DamageMultiplierForHeight(float height)
	{
		return DamageMultiplierForDive(height, StandardDiveSpeed);
	}
	/// <summary>标准速度求参考耗时，避免实际速度乘实际耗时退化成纯高度。</summary>
	public float DamageMultiplierForDive(float height, float downwardSpeed)
	{
		float referenceTime = Mathf.Max(0, height) / Mathf.Max(0.001f, StandardDiveSpeed);
		float heightReference = Movement?.StandardJumpHeight ?? 1;
		return Mathf.Clamp(Mathf.Max(0, downwardSpeed) * referenceTime / Mathf.Max(0.001f, heightReference), 0, Mathf.Max(0, MaxDamageMultiplier));
	}

	private AttackHitbox? _hitbox;
	private bool _diving;
	private bool _landed;
	private float _diveTime;
	private float _landTimer;
	private Vector3 _previousPosition;

	protected override void OnReady()
	{
		_hitbox = GetNodeOrNull<AttackHitbox>("Attack");
	}

	/// <summary>**只有在空中才能用**（文档：「当角色在空中时…可以使用下落攻击」）。</summary>
	protected override bool CanActivate(Unit? target) => Movement is { IsGrounded: false } && TryGroundHeight(out _);
	private bool TryGroundHeight(out float height)
	{
		height = 0;
		if (OwnerUnit == null) return false;
		Vector3 feet = OwnerUnit.GlobalPosition;
		var query = PhysicsRayQueryParameters3D.Create(feet + Vector3.Up * 0.02f, feet + Vector3.Down * GroundProbeDistance, CombatLayers.World);
		query.Exclude = new Godot.Collections.Array<Rid> { OwnerUnit.GetRid() };
		var hit = OwnerUnit.GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0 || hit["normal"].AsVector3().Y < GroundMinNormalY) return false;
		height = Mathf.Max(0, feet.Y - hit["position"].AsVector3().Y);
		return height > 0.001f;
	}

	/// <summary>自测用：把 <see cref="CanActivate"/> 的结果暴露出来（它就是那条"空中才能用"的闸门）。</summary>
	public bool CanActivateForTest() => CanActivate(null);

	protected override void OnActivate(Unit? target)
	{
		if (OwnerUnit == null || Movement == null)
		{
			Cancel();
			return;
		}

		_diving = false;
		_landed = false;
		_diveTime = 0f;
		_landTimer = 0f;
		HasLanded = false;

		TryGroundHeight(out float height);
		LastFallHeight = height;
		LastReferenceFallTime = height / Mathf.Max(0.001f, StandardDiveSpeed);
		LastDamageBonus = -1;
		LastDamage = 0;
		HasGroundAim = false;
		if (string.IsNullOrEmpty(AnimName) || string.IsNullOrEmpty(AnimEvent) || PlaySkillAnimation().Length == 0) Cancel();
	}

	/// <summary>「落日余晖」：把下落方向拨到准星方位（夹在指向范围内）。</summary>
	protected override void OnAnimEvent(string name)
	{
		if (name != AnimEvent || _diving || _landed || OwnerUnit == null || Movement == null) return;
		if (Movement.IsGrounded) { Cancel(); return; }
		LastDiveDirection = ResolveDiveDirection();
		Vector3 velocity = LastDiveDirection * Mathf.Abs(DiveSpeed);
		if (!Movement.BeginVelocityOverride(this, velocity)) { Cancel(); return; }
		LastDownwardSpeed = Mathf.Max(0, -velocity.Y);
		LastDamageBonus = DamageMultiplierForDive(LastFallHeight, LastDownwardSpeed) - 1;
		LastDamage = (_hitbox?.Damage ?? 0) * LastDamageMultiplier * OwnerUnit.Stats.Get("DamageMultiplier");
		_previousPosition = OwnerUnit.GlobalPosition;
		_diving = true;
		ActivateDiveHitbox();
		GameLog.Write("Combat", $"下落攻击：{AbilityId} Action起动，起手离地{LastFallHeight:0.####}m，参考耗时{LastReferenceFallTime:0.####}s，垂直速{LastDownwardSpeed:0.####}m/s，倍率{LastDamageMultiplier:0.####}，伤害{LastDamage:0.####}，方向{LastDiveDirection}");
	}

	/// <summary>准星打到的东西（判断"有没有指向地面"）→ 朝那个点的方向；没打到地面就返回向下。</summary>
	private Vector3 ResolveDiveDirection()
	{
		if (OwnerUnit == null)
		{
			return Vector3.Down;
		}

		if (HomingMode != DiveMode.Crosshair) return Vector3.Down;
		HasGroundAim = AimUtil.TryGetCrosshairGroundPoint(GetViewport(), OwnerUnit, MaxAimDistance, GroundMinNormalY, out Vector3 target);
		if (!HasGroundAim) return Vector3.Down;
		// 目标须低于角色；超出角度时仍保留同一水平方向。
		Vector3 delta = target - OwnerUnit.GlobalPosition;
		if (delta.Y >= -0.001f) { HasGroundAim = false; return Vector3.Down; }
		Vector3 planar = new(delta.X, 0, delta.Z);
		float maxRange = -delta.Y * Mathf.Tan(Mathf.DegToRad(Mathf.Clamp(MaxDiveAngleDegrees, 0, 85)));
		if (planar.Length() > maxRange) planar = planar.Normalized() * maxRange;
		LastAimPoint = OwnerUnit.GlobalPosition + new Vector3(planar.X, delta.Y, planar.Z);
		return (LastAimPoint - OwnerUnit.GlobalPosition).Normalized();
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
			if (OwnerUnit != null)
			{
				_hitbox?.SweepSphere(_previousPosition, OwnerUnit.GlobalPosition);
				_previousPosition = OwnerUnit.GlobalPosition;
			}
			// 落地判定：物理帧里 IsOnFloor 为真（下落攻击把垂直速度压到 -DiveSpeed，一碰地就为真）。
			if (Movement is { IsGrounded: true })
			{
				Land();
				return;
			}

			if (_diveTime > MaxDiveSeconds)
			{
				GameLog.Write("Combat", $"⚠ 下落攻击 {AbilityId} 超过 {MaxDiveSeconds:0.##}s 还没落地，收招（命中盒关掉）。");
				Cancel();
			}

			return;
		}
		if (!_landed && (Movement is { IsGrounded: true } || StateTime > EventWaitSeconds / Mathf.Max(0.01f, AnimSpeedScale)))
		{
			GameLog.Write("Combat", $"下落攻击：{AbilityId} 未等到Action或起动前已落地，取消，不派发落地伤害。");
			Cancel();
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
		Movement?.EndVelocityOverride(this);

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
		Movement?.EndVelocityOverride(this);
		base._ExitTree();
	}

	protected override void OnFinishOrCancel()
	{
		_diving = false;
		_hitbox?.Deactivate();
		Movement?.EndVelocityOverride(this);
	}
}

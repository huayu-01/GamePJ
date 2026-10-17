using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// **被动「落剑式」**（角色 B 武器 1「灵剑」武技3，设计文档第 112 行）：
///
/// <code>
/// 武技3[落剑式]{被动,cd:5}:你的下落攻击在落地时会造成额外的大范围攻击
///                          (不改变原有攻击,就是多一圈攻击判定+特效);
/// </code>
///
/// <para><b>"不改变原有攻击，就是多一圈"</b>的落地方式：本类**不动** <see cref="FallAttackAbility"/>
/// 的一个字，只在它抛出 <see cref="FallAttackAbility.Landed"/> 事件时**额外**开一圈自己的
/// 命中盒（范围/伤害/特效都是本行的表值 + 本节点下那个 <c>Attack</c> 命中体）。</para>
///
/// <para>CD 的语义按用户对"被动可以有 CD"的裁定（`docs/character-redesign.md` §1.7）：
/// 「当玩家连续使用下落攻击时，哪怕是每秒一次，也只会是在第一次和 5 秒后的那一次下落攻击触发
/// 这个技能」—— 所以 <see cref="Ability.IsReady"/> 由基类的冷却倒计时管，
/// <see cref="OnLanded"/> 里先判 <see cref="Ability.IsReady"/> 再出手。</para>
///
/// <para>它没有按键（<c>InputAction</c> 留空）→ 不由 <c>PlayerAbilityInput</c> 触发，
/// 完全靠"上游技能的事件"驱动 —— 这就是文档说的"被动"。</para>
/// </summary>
public partial class LandingBurstAbility : Ability
{
	protected override bool OccupiesAction => false;
	[ExportGroup("落地那一圈额外攻击")]
	/// <summary>**上游技能行 Id**（下落攻击那一行，例如 `skl_b1_fall`）。它落地时本被动出手。</summary>
	[Export] public string TriggerAbilityId { get; set; } = string.Empty;

	/// <summary>额外那一圈的**伤害倍率**（乘在上游技能本次的实际伤害上；1 = 一样痛）。</summary>
	[Export] public float DamageScale { get; set; } = 1f;

	/// <summary>判定持续（秒）。</summary>
	[Export] public float ActiveTime { get; set; } = 0.15f;

	/// <summary>额外那一圈的**击退**（文档「地爆」那是增幅的事，这里只是被动自己的默认）。</summary>
	[Export] public float Knockback { get; set; }

	[ExportGroup("运行期可读状态（自测）")]
	/// <summary>累计触发几次（自测取证：证明 CD 真的在拦住第二次）。</summary>
	public int TriggerCount { get; private set; }

	/// <summary>最近一次那一圈的伤害（自测取证）。</summary>
	public float LastBurstDamage { get; private set; }

	private AttackHitbox? _hitbox;
	private FallAttackAbility? _source;
	private int _stage;
	private float _activeTimer;

	protected override void OnReady()
	{
		_hitbox = GetNodeOrNull<AttackHitbox>("Attack");
	}

	/// <summary>被动：不由按键触发。</summary>
	protected override bool CanActivate(Unit? target) => false;

	protected override void OnActivate(Unit? target)
	{
	}

	/// <summary>每帧惰性绑定上游（表在第一个物理帧之后才套上来，见 `Ability.EnsureAnimEventHooked` 的说明）。</summary>
	private void BindSource()
	{
		if (_source != null && IsInstanceValid(_source))
		{
			return;
		}

		if (OwnerUnit == null || string.IsNullOrEmpty(TriggerAbilityId))
		{
			return;
		}

		if (Ability.FindByAbilityId(OwnerUnit, TriggerAbilityId) is not FallAttackAbility source)
		{
			return;
		}

		_source = source;
		source.Landed += OnLanded;
		GameLog.Write("Combat", $"被动·落剑式：{AbilityId} 已挂到下落攻击 {TriggerAbilityId} 的落地事件上（冷却 {EffectiveCooldown:0.##}s 就是它的触发间隔）。");
	}

	protected override void Tick(float delta)
	{
		if (!Enabled)
		{
			return;
		}

		BindSource();

		if (_stage == 0)
		{
			return;
		}

		_activeTimer -= delta;
		if (_activeTimer <= 0f)
		{
			_hitbox?.Deactivate();
			_stage = 0;
			Finish();
		}
	}

	/// <summary>上游落地：**额外一圈**（上游那一次攻击一个字节都没改）。</summary>
	private void OnLanded()
	{
		if (!Enabled || OwnerUnit == null || _hitbox == null || !IsReady)
		{
			return;
		}

		float basis = _source?.CurrentBasisDamage ?? _hitbox.Damage;
		LastBurstDamage = basis * DamageScale;
		TriggerCount++;

		var attack = new AttackInstance
		{
			Attacker = OwnerUnit,
			Ability = AbilityId,
			BaseDamage = LastBurstDamage,
			CritChance = _hitbox.CritChance,
			CritMultiplier = _hitbox.CritMultiplier,
			Knockback = Knockback > 0f ? Knockback : _hitbox.Knockback,
			Policy = HitPolicy.OncePerTarget,
			HitInterval = _hitbox.HitInterval,
			Tags = _hitbox.AttackTags,
			AbilityKind = AbilityId,
		};

		_hitbox.Activate(OwnerUnit, attack);
		BeginEventDrivenCast();
		_stage = 1;
		_activeTimer = Mathf.Max(0.02f, ActiveTime);

		GameLog.Write(
			"Combat",
			$"落剑式：{AbilityId} 在落地处多开一圈攻击（伤害 {LastBurstDamage:0.##} = 上游 {basis:0.##} ×{DamageScale:0.##}，"
			+ $"半径/形状来自本行命中体，第 {TriggerCount} 次触发）—— 上游下落攻击本身没有改动。");
	}

	protected override void OnFinishOrCancel()
	{
		_hitbox?.Deactivate();
		_stage = 0;
	}

	public override void _ExitTree()
	{
		if (_source != null && IsInstanceValid(_source))
		{
			_source.Landed -= OnLanded;
		}

		_source = null;
		base._ExitTree();
	}
}

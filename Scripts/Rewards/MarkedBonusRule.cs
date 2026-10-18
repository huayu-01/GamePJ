using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>
/// **「标记」类增幅**（J8）：命中给目标加「标记」，并按**这一击之前**目标身上已有的层数放大自己造成的伤害。
/// 一个类服务两条行 —— 差别全在数据里：
///
/// <list type="bullet">
/// <item>「发散」<c>evo_divergence</c>：只有 **能量掌武技1「波波」** 的命中加标记
///   （<see cref="MarkAbilityId"/> ← 行上的 <c>RequiredEventAbility</c> 资源引用 = <c>skl_a1_w1_1</c>），
///   并且**只提高「波波」**（<see cref="BonusAbilityId"/> 也指向它）——每层 <see cref="PerStackBonus"/>。
///   文档写的是 <c>{N}%</c>（N 未定）→ 表里是**预设 10%/层**，行 <c>Note</c> 写明。</item>
/// <item>「心眼」<c>evo_mindseye</c>：只有**箭**（弓武技1 <c>skl_a3_w1_1</c>）的命中加标记，
///   而加伤对**玩家的各种伤害**都生效（<see cref="BonusAbilityId"/> 留空 = 不限技能），固定 <see cref="FlatBonus"/> 50%
///   （文档给的就是 <c>{50}</c>）。</item>
/// </list>
///
/// <para><b>为什么不再复用 <c>BurnOnHitRule</c></b>（旧实现的病根）：那条规则的判据只有
/// "来源是自己 + 目标不是自己"，于是**任何**命中都会挂标记 —— 后撤、灵剑、能量掌武技2 全都算，
/// 这就是交接单说的"来源过滤尚不完整"。这里把两件事都收进<b>数据</b>：
/// 加标来源 = <see cref="MarkAbilityId"/>，加伤来源 = <see cref="BonusAbilityId"/>。</para>
///
/// <para><b>"命中前已有标记"</b>（用户 2026-10-17 裁定）：加伤是 <see cref="IOutgoingDamageScale"/>
/// 在 <see cref="AttackInstance.RollHit"/> 里被问的那一次算的 —— 那一刻 <c>Hit</c> 事件还没抛，
/// 所以<b>这一击自己刚加的标记不算数</b>：第一次加标这一击不加伤，从下一击起才加。</para>
///
/// <para><b>层数与过期</b>：层数不存在这里，存在**目标自己身上**（<c>Unit.ApplyStatus</c> 的
/// <see cref="StatusEffect.MaxStacks"/> 路径，读 <c>Unit.GetStatusStacks</c>），所以
/// ① 多个目标各记各的；② 层数封顶在效果表那一行（<c>eff_mark.MaxStacks = 5</c>）；
/// ③ 标记到期被 <c>Unit._PhysicsProcess</c> 清掉后，下一次命中读到的层数自然是 0。</para>
/// </summary>
public partial class MarkedBonusRule : TriggerRule, IOutgoingDamageScale
{
	/// <summary>挂哪一条状态（效果表行 Id；默认就是「标记」那一行 `eff_mark`）。</summary>
	public string StatusId { get; set; } = "Mark";

	/// <summary>标记时长（秒）。</summary>
	public float Duration { get; set; } = 6f;

	/// <summary>
	/// **只有这个技能行 Id 的命中会挂标记**（空 = 任何命中都挂，那是旧 `BurnOnHit` 的行为）。
	/// 由行上的 <c>RequiredEventAbility</c>（强类型 `AbilityConfig` 资源引用）编译而来 —— 表里是资源引用，不是字符串反射。
	/// </summary>
	public string MarkAbilityId { get; set; } = string.Empty;

	/// <summary>
	/// **只有这个技能行 Id 的伤害吃加成**（空 = 玩家的各种伤害都吃）。
	/// 由行上的 <c>BonusAgainstAbility</c> 资源引用编译而来。
	/// </summary>
	public string BonusAbilityId { get; set; } = string.Empty;

	/// <summary>固定加成（0.5 = +50%，「心眼」）。</summary>
	public float FlatBonus { get; set; }

	/// <summary>每层标记的加成（0.1 = 每层 +10%，「发散」）。</summary>
	public float PerStackBonus { get; set; }

	/// <summary>累计挂上过多少次标记（取证用）。</summary>
	public int MarkAppliedCount { get; private set; }

	/// <summary>最近一次结算读到的层数（取证用：它必须是"这一击之前"的层数）。</summary>
	public int LastStacksRead { get; private set; }

	/// <summary>最近一次结算给出的倍率（取证用）。</summary>
	public float LastScale { get; private set; } = 1f;

	private Unit? _owner;

	public override void _Ready()
	{
		base._Ready();
		_owner = GetParent<Unit>();
		_owner?.AddOutgoingDamageScale(this);
	}

	public override void _ExitTree()
	{
		_owner?.RemoveOutgoingDamageScale(this);
		_owner = null;
		base._ExitTree();
	}

	/// <summary>只有**自己打出去的命中**才挂标记；来源过滤按 <see cref="MarkAbilityId"/>（空 = 不限）。</summary>
	public override bool Matches(GameplayEvent gameplayEvent)
		=> gameplayEvent.Kind == GameplayEventKind.Hit
			&& _owner != null
			&& IsInstanceValid(_owner)
			&& gameplayEvent.Source == _owner
			&& gameplayEvent.Target != null
			&& gameplayEvent.Target != _owner
			&& (string.IsNullOrEmpty(MarkAbilityId) || gameplayEvent.Tag == MarkAbilityId);

	public override void Execute(GameplayEvent gameplayEvent)
	{
		if (gameplayEvent.Target is not { } target || StatusPresets.Create(StatusId, Duration) is not { } mark)
		{
			return;
		}

		target.ApplyStatus(mark, _owner, MarkAbilityId);
		MarkAppliedCount++;
	}

	/// <summary>
	/// 命中那一刻的倍率：<c>1 + FlatBonus + PerStackBonus × 目标身上（这一击之前）的层数</c>。
	///
	/// <para>层数为 0 → 返回 1（**第一次加标这一击不加伤**：那时目标身上还没有标记）。</para>
	/// </summary>
	public float ScaleAgainst(Unit target, AttackInstance attack)
	{
		if (target == null || !IsInstanceValid(target))
		{
			return 1f;
		}

		// 加伤来源过滤：「发散」只提高「波波」；「心眼」留空 = 玩家的各种伤害都提高。
		if (!string.IsNullOrEmpty(BonusAbilityId) && attack.Ability != BonusAbilityId)
		{
			return 1f;
		}

		int stacks = target.GetStatusStacks(StatusId);
		LastStacksRead = stacks;
		LastScale = stacks <= 0 ? 1f : 1f + FlatBonus + (PerStackBonus * stacks);
		return LastScale;
	}

	/// <summary>一条可读的状态串（自测/日志用）。</summary>
	public string DebugState()
		=> $"标记规则：状态「{StatusId}」时长 {Duration:0.##}s 加标来源「{(string.IsNullOrEmpty(MarkAbilityId) ? "（不限）" : MarkAbilityId)}」"
			+ $" 加伤来源「{(string.IsNullOrEmpty(BonusAbilityId) ? "（各种伤害）" : BonusAbilityId)}」"
			+ $" 固定 +{FlatBonus:0.##} 每层 +{PerStackBonus:0.##} 已加标 {MarkAppliedCount} 次"
			+ $"｜最近一次：读层数 {LastStacksRead} → ×{LastScale:0.####}";
}

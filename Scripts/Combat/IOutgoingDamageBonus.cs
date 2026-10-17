namespace ActRoguelike.Combat;

/// <summary>
/// **攻击者侧**的"对特定目标的**扁平加伤**"查询（J11：「巨人」符文用）。
///
/// <para><b>为什么不能复用 <see cref="IOutgoingDamageScale"/></b>：那个口子给的是**倍率**，
/// 而「巨人」是"攻击时造成最大生命值 {10}% 的**额外**伤害" —— 一笔与本次攻击伤害无关的固定量。
/// 用倍率硬凑只能写成 <c>1 + 0.1×生命 / 本次基础伤害</c>，但那会把暴击倍率也乘进这笔固定伤害里
/// （暴击时额外伤害翻倍），与文档口径不符。</para>
///
/// <para><b>计算位置</b>：<see cref="AttackInstance.RollHit"/> 里在既有的
/// <see cref="Unit.OutgoingDamageScaleAgainst"/> **之后**加一项。没有任何规则登记时那一项恒为 0，
/// 而 <c>x + 0f == x</c>（浮点逐位相等），所以**改动前行为逐位不变**。</para>
///
/// <para>实现方：<c>Rewards.RuneMaxHealthDamageRule</c>（登记在 <see cref="Unit.AddOutgoingDamageBonus"/>）。
/// 与 <see cref="IOutgoingDamageScale"/> 同一条归属纪律：增幅/符文是<b>玩家</b>拿到的，
/// 所以敌人的攻击天然问不到玩家的规则。</para>
/// </summary>
public interface IOutgoingDamageBonus
{
	/// <summary>这一次命中对 <paramref name="target"/> 的**额外伤害**（0 = 不变）。多条规则相加。</summary>
	float BonusAgainst(Unit target, AttackInstance attack);
}

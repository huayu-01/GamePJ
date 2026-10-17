namespace ActRoguelike.Combat;

/// <summary>
/// **攻击者侧**的"对特定目标的伤害缩放"查询（J8：增幅「心眼」「发散」共用）。
///
/// <para><b>为什么需要这一层</b>：<c>combat.md</c> 的纪律是"攻击侧属性在 <see cref="AttackInstance"/>
/// 创建时快照；需要**命中时读取**的效果属于 Trigger/Effect"。"目标身上有几层标记"正是**命中那一刻**
/// 才读的东西，所以它在 <see cref="AttackInstance.RollHit"/> 里问一句 —— 那一刻
/// <see cref="Unit.ReceiveHit"/> 还没进、<c>Hit</c> 事件也还没抛，
/// 于是"这一击自己刚加的标记"不会被算进这一击（用户裁定：第一次加标这一击不加伤，从下一击起才加）。</para>
///
/// <para><b>为什么挂在攻击者身上（不是目标）</b>：增幅是<b>玩家</b>拿到的，
/// 所以敌人的攻击 / 召唤物的攻击天然问不到玩家的规则 —— "其他单位的伤害不吃玩家的心眼加成"
/// 不是一条特例判断，而是这套归属的自然结果。</para>
///
/// <para>实现方：<c>Rewards.MarkedBonusRule</c>（登记在 <c>Unit.AddOutgoingDamageScale</c>）。
/// 没有任何实现方登记时 <see cref="Unit.OutgoingDamageScaleAgainst"/> 恒为 1 —— 改动前行为逐位不变。</para>
/// </summary>
public interface IOutgoingDamageScale
{
	/// <summary>这一次命中对 <paramref name="target"/> 的伤害倍率（1 = 不变）。多条规则相乘。</summary>
	float ScaleAgainst(Unit target, AttackInstance attack);
}

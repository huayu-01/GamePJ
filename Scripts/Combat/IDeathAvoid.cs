namespace ActRoguelike.Combat;

/// <summary>
/// **死亡避免**（J11：「生死」符文用）—— 受到致命伤的那一刻，在 <see cref="Unit.ReceiveHit"/>
/// 决定"死不死"之前问一遍登记过的规则。
///
/// <para><b>为什么需要一个新口子</b>：既有的 <c>Damaged</c> 事件是在
/// <c>bool killed</c> 算完之后才抛的，那时已经来不及阻止 <c>Die()</c>
/// （文档要的是「无效化这次伤害」，不是"死后复活"）。所以钩子必须落在
/// <c>killed</c> 判定与 <c>result</c> 构造之间。</para>
///
/// <para><b>默认行为</b>：没有任何实现方登记时 <c>Unit._deathAvoids.Count == 0</c>，
/// 那一段 <c>if</c> 根本进不去 —— 改动前"血量归零即死"的语义逐位不变。</para>
/// </summary>
public interface IDeathAvoid
{
	/// <summary>
	/// 尝试无效化这次致命伤。返回 <c>true</c> = 这次不死（实现方负责把生命值恢复到自己想要的值）。
	/// 返回 <c>false</c> = 这条规则管不了（次数用完），由下一条继续问。
	/// </summary>
	bool TryAvoidDeath(Unit unit);
}

using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 一次具体命中的结算数据（战斗规则说明 §10）。
/// 攻击侧数据在 <see cref="AttackInstance"/> 里快照，这里记录“这一次命中”的结果输入。
/// </summary>
public sealed class HitInstance
{
	public required Unit Target { get; init; }
	public Unit? Attacker { get; init; }
	public AttackInstance? Attack { get; init; }
	public required EventContext Context { get; init; }

	public float Damage { get; init; }
	public bool Critical { get; init; }
	public Vector3 Direction { get; init; }
	public Vector3 Position { get; init; }
	public float Knockback { get; init; }
	/// <summary>防御能力只无效化本次命中，不把规避扩散成一帧内的所有攻击无敌。</summary>
	public bool Negated { get; set; }
	/// <summary>主动招架事实；一次性效果护罩只写Negated，不冒充招架。</summary>
	public bool Parried { get; set; }
	/// <summary>命中体仍附着在出手单位上；远程、爆炸与脱手区域不使出手者停顿。</summary>
	public bool ContactFeedback { get; set; }

	/// <summary>
	/// **无视格挡**（文档：能量波"能穿透格挡（无视格挡效果）"，而剑气"可被格挡"）。
	///
	/// 它是"这一次命中"的性质，所以从这里读，而不是给某个子弹类型写死一条特例
	/// （见 <see cref="Unit.ResolveBlock"/>）。攻击侧在 <see cref="AttackInstance.BypassBlock"/> 上填，
	/// <see cref="AttackInstance.RollHit"/> 原样带过来。
	/// </summary>
	public bool BypassBlock { get; init; }
}

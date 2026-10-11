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
}

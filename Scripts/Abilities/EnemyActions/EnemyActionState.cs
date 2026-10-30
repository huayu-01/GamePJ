using ActRoguelike.Config;
using Godot;
using Kind = ActRoguelike.Config.EnemyActionStep.ActionKind;

namespace ActRoguelike.Abilities;

/// <summary>进入动作时冻结本次参数；共享定义不保存单位位置或计时，预警与执行读取同一快照。</summary>
public sealed record EnemyActionState
{
	public Kind Kind { get; init; }
	public EnemyActionDefinition? Action { get; init; }
	public Vector3 StartPosition { get; init; }
	public float WindupSeconds { get; init; }
	public float ActiveSeconds { get; init; }
	public float RecoverSeconds { get; init; }
	public float Damage { get; init; }
	public float Radius { get; init; }
	public float Speed { get; init; }
	public float TravelDistance { get; init; }
	public float FlightSeconds { get; init; }
	public float TurnDegreesPerSecond { get; init; }
	public bool Blocking { get; init; }
	public bool Centered { get; init; }
	public float LandingSeconds { get; init; } = .12f;
	public float BurrowMinDistance { get; init; } = 4;
	public float BurrowMaxDistance { get; init; } = 8;
	public float BurrowDepth { get; init; } = 1.5f;
	public PackedScene? ProjectileScene { get; init; }
	public static EnemyActionState From(EnemyActionStep step, Vector3 start)
	{
		var state = new EnemyActionState { Kind = step.Kind, Action = step.Action, StartPosition = start,
			WindupSeconds = step.WindupSeconds, ActiveSeconds = step.ActiveSeconds, RecoverSeconds = step.RecoverSeconds,
			Damage = step.Damage, Radius = step.Radius, Speed = step.Speed, TravelDistance = step.TravelDistance,
			FlightSeconds = step.FlightSeconds, TurnDegreesPerSecond = step.TurnDegreesPerSecond, Blocking = step.Blocking, Centered = step.Centered };
		return step.Action?.Resolve(state) ?? state;
	}
}

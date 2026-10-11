using System.Threading;
using Godot;

namespace ActRoguelike.Combat;

public enum Faction
{
	Player,
	Enemy,
	Neutral,
}

/// <summary>命中策略（战斗规则说明 §9）。由攻击本身决定，不写死在碰撞体上。</summary>
public enum HitPolicy
{
	OncePerTarget,
	IntervalPerTarget,
	OncePerPhase,
	MultiHit,
}

/// <summary>物理层约定，Unit 在 _Ready 里按阵营自动设置。</summary>
public static class CombatLayers
{
	public const uint World = 1 << 0;
	public const uint Player = 1 << 1;
	public const uint Enemy = 1 << 2;
	public const uint UnitMask = Player | Enemy;

	public static uint Of(Faction faction) => faction switch
	{
		Faction.Player => Player,
		Faction.Enemy => Enemy,
		_ => UnitMask,
	};
}

public struct DamageResult
{
	public bool Hit { get; init; }
	public float FinalDamage { get; init; }
	public float ArmorLost { get; init; }
	public float ShieldLost { get; init; }
	public float HealthLost { get; init; }
	public bool Invulnerable { get; init; }
	public bool Blocked { get; init; }
	public bool Critical { get; init; }
	public bool Killed { get; init; }

	public static DamageResult Miss => new();
}

/// <summary>
/// 事件链上下文（战斗规则说明 §23）。Depth 用于限制连锁触发，避免死亡爆炸互相递归。
/// </summary>
public sealed class EventContext
{
	public const int MaxDepth = 4;

	private static int _chainCounter;

	public int ChainId { get; init; }
	public int Depth { get; init; }
	public Unit? Source { get; init; }
	public Unit? OriginalSource { get; init; }
	public Unit? Target { get; init; }
	public Unit? OriginalTarget { get; init; }

	public static EventContext Create(Unit? source, Unit? target) => new()
	{
		ChainId = Interlocked.Increment(ref _chainCounter),
		Depth = 0,
		Source = source,
		OriginalSource = source,
		Target = target,
		OriginalTarget = target,
	};

	/// <summary>产生下一层事件；超过最大深度返回 null（调用方应放弃该次连锁）。</summary>
	public EventContext? Child(Unit? source, Unit? target)
	{
		if (Depth + 1 > MaxDepth)
		{
			return null;
		}

		return new EventContext
		{
			ChainId = ChainId,
			Depth = Depth + 1,
			Source = source,
			OriginalSource = OriginalSource,
			Target = target,
			OriginalTarget = OriginalTarget,
		};
	}
}

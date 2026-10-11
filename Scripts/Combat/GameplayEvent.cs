using System;
using Godot;

namespace ActRoguelike.Combat;

public enum GameplayEventKind
{
	Hit,
	Damaged,
	Died,
	Kill,
	ArmorBroken,
	ShieldBroken,
	AbilityActivated,
	WeaponFired,
	ReloadStarted,
	ReloadCompleted,
	Critical,
}

/// <summary>
/// 已经发生的游戏事实（执行文档 §4.1 Gameplay Event）。
/// Gameplay 只负责抛事实，Trigger/Condition/Effect 与表现层各自订阅。
/// </summary>
public readonly struct GameplayEvent
{
	public GameplayEventKind Kind { get; init; }
	public Unit? Source { get; init; }
	public Unit? Target { get; init; }
	public float Value { get; init; }

	/// <summary>攻击标签或技能 Id，用于按能力类型过滤，而不是按单位 Id。</summary>
	public string Tag { get; init; }

	public Vector3 Position { get; init; }
	public EventContext? Context { get; init; }
}

/// <summary>轻量事件总线。刻意不做队列与优先级，只用于跨系统广播（阶段 5 的触发效果）。</summary>
public static class GameplayEventBus
{
	public static event Action<GameplayEvent>? Raised;

    /// <summary>
    /// 广播事件。**逐个订阅者隔离异常** —— 之前直接 Invoke，
    /// 任何订阅者抛异常都会中断整条链，后面的订阅者收不到；
    /// 更糟的是调用方（如 Unit.ReceiveHit）会因此跳过后续逻辑，
    /// 曾造成「0 血敌人不死、战斗卡死」这种诡异故障。
    /// </summary>
    public static void Raise(in GameplayEvent gameplayEvent)
    {
        Delegate[]? handlers = Raised?.GetInvocationList();
        if (handlers == null)
        {
            return;
        }

        foreach (Delegate handler in handlers)
        {
            try
            {
                ((Action<GameplayEvent>)handler)(gameplayEvent);
            }
            catch (Exception ex)
            {
                GD.PushError($"[事件总线] 订阅者抛异常，已隔离（不影响其它订阅者与调用方）：{ex.Message}");
            }
        }
    }

	/// <summary>场景重载前清理静态订阅，避免持有已释放节点。</summary>
	public static void Clear() => Raised = null;
}
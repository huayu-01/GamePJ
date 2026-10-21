using System;
using Godot;

namespace ActRoguelike.Combat;

public enum GameplayEventKind
{
	Hit,
	Damaged,
	Died,
	Kill,

	/// <summary>
	/// **护甲被打光**那一刻（P6a 补上抛点）。抛点唯一：`Unit.ReceiveHit` 里 <c>ArmorBroke?.Invoke()</c> 那一处
	/// （扣甲之后 <c>Armor &lt;= 0</c>）。订它的增幅：如「能量护盾」的"护甲破碎 → 对周围敌人造成伤害"那一路。
	/// </summary>
	ArmorBroken,

	/// <summary>
	/// **护盾被打光**那一刻（P6a 补上抛点）。抛点唯一：`Unit.ReceiveHit` 里 <c>ShieldBroke?.Invoke()</c> 那一处。
	/// </summary>
	ShieldBroken,

	AbilityActivated,
	WeaponFired,

	/// <summary>
	/// **暴击命中**（P6a 补上抛点）。抛点唯一：`Unit.ReceiveHit` 里结算完这次命中之后，判据是
	/// <c>HitInstance.Critical</c>（攻击方按 `CritChance` 掷出来的那一次）且真的打出了伤害。
	/// 「是玩家的暴击才算」交给订阅方判（<c>TablePulseRule</c> 的 <c>mine</c> = Source == 玩家）。
	/// </summary>
	Critical,

	/// <summary>
	/// **格挡 / 架势成功**（P5 第二批新增，只加不删）：`GuardStanceAbility` 真的"架住"了那一次命中时抛。
	///
	/// <para>抛点只有一处（`GuardStanceAbility.OnOwnerHit` 里 <c>ParryCount++</c> 那里），
	/// 订阅方是增幅行「招架反击」（刷新技能 2 冷却）与「能量护盾」（对周围敌人造成伤害）——
	/// 两条都是**表驱动的** <c>TablePulseRule</c>。</para>
	/// </summary>
	Parried,
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
	public bool IsDamageOverTime { get; init; }
	public bool IsSecondary { get; init; }
	/// <summary>Hit／Damaged携带原始出手快照，追击类效果可在伤害结算后选择目标，不再反查当前技能。</summary>
	public AttackInstance? Attack { get; init; }

	/// <summary>攻击标签或技能 Id，用于按能力类型过滤，而不是按单位 Id。</summary>
	public string Tag { get; init; }
	/// <summary>来自攻击快照的连招信息；总段数0表示事件不属于连招。</summary>
	public int ComboStep { get; init; }
	public int ComboStepCount { get; init; }

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

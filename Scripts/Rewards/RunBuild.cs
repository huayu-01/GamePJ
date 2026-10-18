using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Combat;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>
/// 本局已获得的构筑（执行文档 §八 Run Build）。
/// 候选池在角色选定后建立一次，每次奖励只做抽取，不重新拼接（§8.4）。
/// </summary>
public sealed partial class RunBuild : RefCounted
{
	private readonly List<RewardDefinition> _acquired = new();
	private readonly Dictionary<string, int> _stacks = new();
	private readonly List<TriggerRule> _triggers = new();

	public IReadOnlyList<RewardDefinition> Acquired => _acquired;
	public IReadOnlyList<TriggerRule> Triggers => _triggers;

	public int StacksOf(string id) => _stacks.TryGetValue(id, out int count) ? count : 0;

	public bool Has(string id) => _stacks.ContainsKey(id);

	// ─────────────────────────────────────────────────────────────────────────
	// ⭐ J11：**符文 → 增幅前置** 的那一条通路（用户原话：「携带了这个雷之印等于满足它们的前置要求」）
	//
	// 设计上刻意**不进 `_stacks`**：符文不是"已经拥有的增幅"，不能让它出现在
	// `Acquired`（HUD 构筑列表）、也不能让它满足互斥（`Excludes`）—— 它只影响**前置**这一层。
	// 所以这里是一份独立的"被满足的前置字符串"小集合，读取点只有 `SatisfiesPrerequisite`，
	// 而 `SatisfiesPrerequisite` 的调用点只有 `RewardPool.HasPrerequisites`。
	//
	// 默认（不携带任何符文）→ 集合为空 → `SatisfiesPrerequisite` 退化成改动前的
	// `StacksOf(id) > 0`，判定行为**逐位不变**。
	// ─────────────────────────────────────────────────────────────────────────
	private readonly HashSet<string> _runeUnlocks = new();

	/// <summary>当前被符文满足的前置字符串（日志 / 自测取证用，顺序不保证）。</summary>
	public IReadOnlyCollection<string> RuneUnlocks => _runeUnlocks;

	/// <summary>
	/// **前置判定本体**：`id` 这个前置条件满足了吗。
	/// 两条来源：① 已经拥有该 Id 的增幅（改动前的唯一来源）；② 当前携带的符文把它满足了。
	/// </summary>
	public bool SatisfiesPrerequisite(string id)
		=> StacksOf(id) > 0 || _runeUnlocks.Contains(id);

	/// <summary>
	/// 换掉"当前携带的符文带来的前置满足名单"。传空 / null = 没有符文（回到逐位不变的判定）。
	///
	/// <para>名单里通常同时含**符文行 Id**（`rune_lei_seal`）与 `RuneConfig.UnlocksRequires` 里的
	/// 派系名（`雷之印`）—— 前者让"表里按符文行 Id 写前置"也能用，后者对应文档里的写法。</para>
	/// </summary>
	public void SetRuneUnlocks(IEnumerable<string>? tokens)
	{
		_runeUnlocks.Clear();
		if (tokens == null)
		{
			return;
		}

		foreach (string token in tokens)
		{
			if (!string.IsNullOrEmpty(token))
			{
				_runeUnlocks.Add(token);
			}
		}
	}

	/// <summary>当前符文解锁了哪些前置（一行可读串，日志/自测用）。</summary>
	public string DescribeRuneUnlocks()
		=> _runeUnlocks.Count == 0 ? "（没有携带符文）" : "[" + string.Join("、", _runeUnlocks) + "]";

	public void Add(RewardDefinition definition, RewardContext context)
	{
		int next = StacksOf(definition.Id) + 1;
		_stacks[definition.Id] = next;
		if (next == 1)
		{
			_acquired.Add(definition);
		}

		definition.Apply(context);

		// 异化/进化带来的触发规则在这里登记，统一由 TickTriggers 驱动冷却。
		foreach (Node child in context.Player.GetChildren())
		{
			if (child is TriggerRule rule && rule.RewardId == definition.Id)
			{
				_triggers.Add(rule);
			}
		}
	}

	public void TickTriggers(float delta)
	{
		foreach (TriggerRule rule in _triggers)
		{
			rule.Tick(delta);
		}
	}

	/// <summary>
	/// **自测专用**：只记层数、**不套用效果**。
	///
	/// <para>P5 的三层判定探针要造出"已经拥有 X"的构筑，但 <see cref="Add"/> 会把 X 的效果真的挂到玩家身上
	/// （改 StatSheet / 加 TriggerRule），那一局的数值就被探针污染了。所以单开这一条路：
	/// 与 <c>AbilityStore.DebugSelfTestLines</c> 同一类"只给自测用的口子"（不进任何正式流程）。</para>
	/// </summary>
	public void DebugGrantWithoutApplying(RewardDefinition definition)
	{
		int next = StacksOf(definition.Id) + 1;
		_stacks[definition.Id] = next;
		if (next == 1)
		{
			_acquired.Add(definition);
		}
	}

	/// <summary>当前构筑的简要列表，供 HUD 显示。</summary>
	public string Describe()
	{
		if (_acquired.Count == 0)
		{
			return "构筑：无";
		}

		IEnumerable<string> parts = _acquired.Select(d =>
		{
			int stacks = StacksOf(d.Id);
			return stacks > 1 ? $"{d.Name}×{stacks}" : d.Name;
		});
		return "构筑：" + string.Join(" / ", parts);
	}
}

/// <summary>
/// 触发规则（战斗规则说明 §21-24）：订阅 Gameplay 事件，满足条件就执行效果。
/// 带冷却、每链次数上限与链深度上限，防止死亡爆炸互相递归。
/// </summary>
public abstract partial class TriggerRule : Node
{
	public string RewardId { get; set; } = string.Empty;
	public float Cooldown { get; set; }
	public int MaxTriggersPerChain { get; set; } = 4;
	/// <summary>默认保持旧行为：DoT只广播伤害／击杀事实，不再次触发主攻击命中规则。</summary>
	public bool IncludeDamageOverTimeHits { get; set; }

	private float _cooldownRemaining;
	private int _chainId = -1;
	private int _chainCount;

	public bool IsReady => _cooldownRemaining <= 0f;

	public override void _Ready()
	{
		GameplayEventBus.Raised += OnRaised;
	}

	public override void _ExitTree()
	{
		GameplayEventBus.Raised -= OnRaised;
	}

	public void Tick(float delta) => _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - delta);

	public abstract bool Matches(GameplayEvent gameplayEvent);

	public abstract void Execute(GameplayEvent gameplayEvent);

	private void OnRaised(GameplayEvent gameplayEvent)
	{
		if (gameplayEvent.IsDamageOverTime && gameplayEvent.Kind == GameplayEventKind.Hit && !IncludeDamageOverTimeHits) return;
		if (!IsReady || !Matches(gameplayEvent))
		{
			return;
		}

		EventContext? context = gameplayEvent.Context;
		int chainId = context?.ChainId ?? -1;
		if (chainId != _chainId)
		{
			_chainId = chainId;
			_chainCount = 0;
		}

		if (context != null && context.Depth >= EventContext.MaxDepth)
		{
			return;
		}

		if (_chainCount >= MaxTriggersPerChain)
		{
			return;
		}

		_chainCount++;
		_cooldownRemaining = Cooldown;
		Execute(gameplayEvent);
	}
}

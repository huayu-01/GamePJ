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

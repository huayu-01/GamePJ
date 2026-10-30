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
	internal void RegisterTrigger(TriggerRule rule) { if (!_triggers.Contains(rule)) _triggers.Add(rule); }
	internal void UnregisterTrigger(TriggerRule rule) => _triggers.Remove(rule);

	public int StacksOf(string id) => _stacks.TryGetValue(id, out int count) ? count : 0;

	public bool Has(string id) => _stacks.ContainsKey(id);
	public string DisplayName(RewardDefinition definition, bool offered = false)
	{
		int level = StacksOf(definition.Id) + (offered ? 1 : 0);
		return definition.ShowUpgradeLevel && level > 1
			? ActRoguelike.Config.ContentText.Get("ui.reward_upgrade_level", "升级：{Name} [{Level}级]").Replace("{Name}", definition.Name).Replace("{Level}", level.ToString())
			: definition.Name;
	}

	// ─────────────────────────────────────────────────────────────────────────
	// ⭐ J11：**符文 → 增幅前置** 的那一条通路（用户原话：「携带了这个雷之印等于满足它们的前置要求」）
	//
	// 符文不进 `_stacks` 或 `Acquired`，避免冒充已获得的增幅。
	// 其稳定身份参与前置和显式 Excludes 资格判断，例如生命强化排除雷之印。
	//
	// 默认（不携带任何符文）→ 集合为空 → `SatisfiesPrerequisite` 退化成改动前的
	// `StacksOf(id) > 0`，判定行为**逐位不变**。
	// ─────────────────────────────────────────────────────────────────────────
	private readonly HashSet<string> _runeUnlocks = new();

	/// <summary>当前被符文满足的稳定前置 Id（旧 token 已归一化；顺序不保证）。</summary>
	public IReadOnlyCollection<string> RuneUnlocks => _runeUnlocks;

	/// <summary>
	/// **前置判定本体**：`id` 这个前置条件满足了吗。
	/// 两条来源：① 已经拥有该 Id 的增幅（改动前的唯一来源）；② 当前携带的符文把它满足了。
	/// </summary>
	public bool SatisfiesPrerequisite(string id)
		=> StacksOf(id) > 0 || _runeUnlocks.Contains(ActRoguelike.Config.ContentIdentity.Prerequisite(id));

	/// <summary>
	/// 换掉"当前携带的符文带来的前置满足名单"。传空 / null = 没有符文（回到逐位不变的判定）。
	///
	/// <para>符文行 Id 与声明的解锁 Id 入集合；旧派系 token 通过 ContentIdentity 兼容，显示名不参与判定。</para>
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
					_runeUnlocks.Add(ActRoguelike.Config.ContentIdentity.Prerequisite(token));
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

		try { definition.Apply(context); }
		catch
		{
			if (next == 1) { _stacks.Remove(definition.Id); _acquired.Remove(definition); }
			else _stacks[definition.Id] = next - 1;
			throw;
		}

		// 新级应用成功后才撤销前级实例；历史拥有记录继续满足前置，但构筑只展示当前级。
		if (!string.IsNullOrEmpty(definition.ReplacesId))
		{
			context.Player.GetNodeOrNull<RewardEffectHost>(RewardEffectHost.NodeName)?.ReleaseReward(this, definition.ReplacesId);
			_acquired.RemoveAll(row => row.Id == definition.ReplacesId);
		}
		// 异化/进化带来的触发规则在这里登记，统一由 TickTriggers 驱动冷却。
		foreach (Node child in context.Player.GetChildren())
		{
			if (child is TriggerRule rule && rule.RewardId == definition.Id)
			{
				RegisterTrigger(rule);
			}
		}
	}

	public void TickTriggers(float delta)
	{
		_triggers.RemoveAll(rule => !GodotObject.IsInstanceValid(rule) || !rule.IsInsideTree() || rule.IsStopped);
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

		return "构筑：" + string.Join(" / ", _acquired.Select(d => d.Name));
	}
}

/// <summary>
/// 触发规则（战斗规则说明 §21-24）：订阅 Gameplay 事件，满足条件就执行效果。
/// 带冷却、每链次数上限与链深度上限，防止死亡爆炸互相递归。
/// </summary>
public abstract partial class TriggerRule : Node, IStoppableRewardRule
{
	public string RewardId { get; set; } = string.Empty;
	public float Cooldown { get; set; }
	public int MaxTriggersPerChain { get; set; } = 4;
	/// <summary>默认保持旧行为：DoT只广播伤害／击杀事实，不再次触发主攻击命中规则。</summary>
	public bool IncludeDamageOverTimeHits { get; set; }

	private float _cooldownRemaining;
	private int _chainId = -1;
	private int _chainCount;
	public bool IsStopped { get; protected set; }

	public bool IsReady => _cooldownRemaining <= 0f;

	public override void _Ready()
	{
		IsStopped = false;
		GameplayEventBus.Raised += OnRaised;
	}

	public override void _ExitTree()
	{
		Stop();
	}
	public virtual void Stop() { IsStopped = true; GameplayEventBus.Raised -= OnRaised; }

	public void Tick(float delta) => _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - delta);

	public abstract bool Matches(GameplayEvent gameplayEvent);

	public abstract void Execute(GameplayEvent gameplayEvent);

	private void OnRaised(GameplayEvent gameplayEvent)
	{
		if (IsStopped) return;
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

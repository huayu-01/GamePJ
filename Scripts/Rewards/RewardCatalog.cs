using System;
using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>
/// 奖励内容目录（策划文档 §9-10）。
/// 强化改参数、进化改武器核心行为、异化加新规则；通用效果不依赖具体角色 Id。
/// 新增内容只加一行定义，池与抽取代码不动。
///
/// <para><b>⚠️ P5：这里原来那份 19 条「兜底目录」已经删掉</b>（调研报告 §2.2 点名的硬编码）。理由见
/// <see cref="All"/> 的注释 —— 一句话：它是同一批 19 行的**第二份真源**，而且已经漂移
/// （`dmg_up` vs `upg_dmg_up`），漂移的直接后果就是"跳弹 × 贯穿弹头"的互斥在兜底路径上**静默失效**。</para>
/// </summary>
public static class RewardCatalog
{
	/// <summary>
	/// 本局实际使用的增幅项目录：**唯一真源 = 表**（`Config/Tables/Upgrades/*.tres` + `ConfigIndex.tres` 的
	/// <c>Upgrades</c> 数组），由 <c>ConfigIndex</c> 在 <c>_Ready</c> 里读表并编译好（见 <c>RewardTable.Build</c>）。
	///
	/// <para><b>为什么不留 C# 兜底</b>（P5 决策，替换掉原 `Fallback` 的 19 行硬编码）：</para>
	/// <list type="number">
	/// <item>它是同一批 19 行的**第二份真源** —— 表在编辑器里改、C# 数组改不动，必然漂移
	///   （`config-schema.md` §1.4-① 那条纪律）。实测已经漂了：兜底写 `dmg_up` / `pierce_round`，
	///   表写 `upg_dmg_up` / `upg_pierce_round` → 兜底路径上"跳弹 × 贯穿弹头"的互斥**静默失效**。</item>
	/// <item>**加内容这件事本身要求它消失**：新设计 60+ 条增幅如果兜底也要一份，就得写两遍，
    ///   与「加一条增幅 = 加一行表」（`character-redesign.md` §1.5）直接冲突。</item>
	/// <item>**它其实护不住任何东西**：实测（`RunController.ResolveStages` 的注释）删/改一个被引用的行文件会让
	///   **整份** `ConfigIndex.tres` 加载失败 —— 那时所有数值一起回退到代码默认值（玩家生命 250→160），
	///   游戏已经不可玩了；"只有增幅表读不到"这个状态在实际路径上到不了。</item>
	/// </list>
	/// <para><b>回退语义仍然保留</b>（只是回退的终点从"一份会漂的名单"改成"空目录 + 大声留痕"）：
	/// 表为空 → <c>RewardTable.Build</c> 打一条明确的日志，此时池子是空的、互动奖励节点给不出东西
	/// （不会崩、不会死锁：`StageController` 收到 <c>false</c> 就只是这次交互不生效）。</para>
	/// </summary>
	public static RewardDefinition[] All { get; private set; } = Array.Empty<RewardDefinition>();

	/// <summary>换成本局目录（<c>ConfigIndex</c> 读表后调用）。传 null / 空数组 → 空目录（见 <see cref="All"/>）。</summary>
	public static void UseTable(RewardDefinition[]? rows)
		=> All = rows ?? Array.Empty<RewardDefinition>();

	public static RewardDefinition? Find(string id) => Array.Find(All, d => d.Id == id);
}

/// <summary>击杀触发连锁闪电：从死者位置找最近的敌人放电，逐跳传递。</summary>
public partial class ChainLightningRule : TriggerRule
{
	public float Damage { get; set; } = 14f;
	public float Range { get; set; } = 7f;
	public int Jumps { get; set; } = 3;

	private readonly HashSet<ulong> _hit = new();

	public override bool Matches(GameplayEvent gameplayEvent)
		=> gameplayEvent.Kind == GameplayEventKind.Kill
			&& gameplayEvent.Source is { Faction: Faction.Player };

	public override void Execute(GameplayEvent gameplayEvent)
	{
		Unit? source = gameplayEvent.Source;
		if (source == null)
		{
			return;
		}

		_hit.Clear();
		if (gameplayEvent.Target != null)
		{
			_hit.Add(gameplayEvent.Target.GetInstanceId());
		}

		Vector3 from = gameplayEvent.Position;
		for (int i = 0; i < Jumps; i++)
		{
			Unit? next = FindNearest(from);
			if (next == null)
			{
				return;
			}

			_hit.Add(next.GetInstanceId());
			var attack = new AttackInstance
			{
				Attacker = source,
				Ability = "ChainLightning",
				BaseDamage = Damage,
				Policy = HitPolicy.OncePerTarget,
				Tags = new[] { "Lightning" },
			};
			next.ReceiveHit(attack.RollHit(next, (next.GlobalPosition - from).Normalized(), next.GlobalPosition));
			from = next.GlobalPosition;
		}
	}

	private Unit? FindNearest(Vector3 from)
	{
		Unit? best = null;
		float bestDistance = Range * Range;

		foreach (Node node in GetTree().GetNodesInGroup("enemy_units"))
		{
			if (node is not Unit candidate || !candidate.IsValidTarget || _hit.Contains(candidate.GetInstanceId()))
			{
				continue;
			}

			float distance = from.DistanceSquaredTo(candidate.GlobalPosition);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = candidate;
			}
		}

		return best;
	}
}

/// <summary>吸血：玩家造成伤害后按比例回血。</summary>
public partial class LifestealRule : TriggerRule
{
	public float Ratio { get; set; } = 0.12f;

	private Unit? _owner;

	public override void _Ready()
	{
		base._Ready();
		_owner = GetParent<Unit>();
	}

	public override bool Matches(GameplayEvent gameplayEvent)
		=> gameplayEvent.Kind == GameplayEventKind.Damaged
			&& gameplayEvent.Source != null
			&& gameplayEvent.Source == _owner
			&& gameplayEvent.Target != _owner
			&& gameplayEvent.Value > 0f;

	public override void Execute(GameplayEvent gameplayEvent) => _owner?.Heal(gameplayEvent.Value * Ratio);
}

/// <summary>灼烧：命中给目标挂 Burn 状态（与伤害本体分离）。</summary>
public partial class BurnOnHitRule : TriggerRule
{
	public float Duration { get; set; } = 3f;

	/// <summary>挂哪个状态（对应 `Config/Tables/Effects/` 里的状态行 Id）。</summary>
	public string StatusId { get; set; } = "Burn";

	private Unit? _owner;

	public override void _Ready()
	{
		base._Ready();
		_owner = GetParent<Unit>();
	}

	public override bool Matches(GameplayEvent gameplayEvent)
		=> gameplayEvent.Kind == GameplayEventKind.Hit
			&& gameplayEvent.Source == _owner
			&& gameplayEvent.Target != null
			&& gameplayEvent.Target != _owner;

	public override void Execute(GameplayEvent gameplayEvent)
	{
		if (StatusPresets.Create(StatusId, Duration) is { } burn)
		{
			gameplayEvent.Target?.ApplyStatus(burn);
		}
	}
}

/// <summary>战地补给：击杀立刻补满弹匣。</summary>
public partial class AmmoOnKillRule : TriggerRule
{
	private Unit? _owner;
	private Weapon? _weapon;

	public override void _Ready()
	{
		base._Ready();
		_owner = GetParent<Unit>();
		_weapon = _owner == null ? null : NodeSearch.FindInSubtree<Weapon>(_owner);
	}

	public override bool Matches(GameplayEvent gameplayEvent)
		=> gameplayEvent.Kind == GameplayEventKind.Kill && gameplayEvent.Source == _owner;

	public override void Execute(GameplayEvent gameplayEvent) => _weapon?.RefillMagazine();
}

/// <summary>亡者随从：击杀时召唤一架无人机（有数量上限）。</summary>
public partial class SummonOnKillRule : TriggerRule
{
	public PackedScene? SummonScene { get; set; }
	public int MaxSummons { get; set; } = 2;

	private Unit? _owner;
	private int _summoned;

	public override void _Ready()
	{
		base._Ready();
		_owner = GetParent<Unit>();
	}

	public override bool Matches(GameplayEvent gameplayEvent)
		=> gameplayEvent.Kind == GameplayEventKind.Kill
			&& gameplayEvent.Source == _owner
			&& _summoned < MaxSummons;

	public override void Execute(GameplayEvent gameplayEvent)
	{
		if (SummonScene == null || _owner == null || !IsInstanceValid(_owner))
		{
			return;
		}

		var drone = SummonScene.Instantiate<Unit>();
		Node parent = GetTree().CurrentScene ?? GetTree().Root;
		parent.AddChild(drone);
		float angle = (float)GD.RandRange(0d, Mathf.Tau);
		drone.GlobalPosition = _owner.GlobalPosition + new Vector3(Mathf.Cos(angle) * 2f, 0.6f, Mathf.Sin(angle) * 2f);
		_summoned++;
	}
}

using System;
using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Core;
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

/// <summary>
/// **表驱动的「战斗事件 → 效果」规则**（P5 第二批新增，只加）：
/// **一个类服务所有"订阅某种事件、按表里的数值出手"的增幅行**。
///
/// <list type="bullet">
/// <item>`evo_stall_kill` 乱金柝：击杀时**刷新滞空**（`HoverSeconds` + `RequireHover`）；</item>
/// <item>`evo_dash_reset` 闪身步：技能 1 击杀时**冷却缩短 80%**（`AbilityId` + `CooldownFraction`）；</item>
/// <item>`evo_parry_refresh` 招架反击：架住时**刷新技能 2 冷却**（`Trigger=Parried` + `RefreshAbility`）；</item>
/// <item>`evo_salvation` 普渡：击杀时**回血**（`Heal`）；</item>
/// <item>`mut_stomp_kill` 践踏死亡：击杀时**获得额外空中跳跃次数**（`ExtraAirJumps` + `JumpCap`）；</item>
/// <item>`mut_energy_shield` 能量护盾：架住时**对周围敌人造成伤害**（`Radius` + `Damage`）。</item>
/// </list>
///
/// <para><b>为什么合成一个类</b>（`docs/upgrades.md` §2.3 那条"一个类服务所有同类行"）：这 6 条的差别**只有
/// "订阅哪一种事件 + 出手时做什么 + 数值多少"**，三样全是行里的键值对。一条行一个类会让"再加一条这类增幅"
/// 变成改 C#，与 `character-redesign.md` §1.5「加一条增幅 = 加一行表」冲突；
/// 而 6 个几乎一样的类正是 `docs/refactor-survey.md` 点名的复制粘贴。</para>
///
/// <para>数值**没有一个字面量**：`Trigger` / `AbilityId` / `RequireHover` / `CooldownFraction` /
/// `CooldownSeconds` / `RefreshAbility` / `Heal` / `HoverSeconds` / `ExtraAirJumps` / `JumpCap` /
/// `Radius` / `Damage` 全部由 `RewardEffectRegistry` 从行里读出来赋进来。</para>
///
/// <para>冷却、每链次数上限、链深度上限由基类 <see cref="TriggerRule"/> 统一管（与连锁闪电同一套）。</para>
/// </summary>
public partial class TablePulseRule : TriggerRule
{
	/// <summary>
	/// 订阅哪一种（或哪几种）事件：`Kill` / `Parried` / `Critical` / `ArmorBroken` / `Damaged`。
	///
	/// <para>P6a：**支持逗号分隔的多个事件名**（任一命中即出手）。为什么需要：
	/// 「能量护盾」的文档语义是「当**格挡 / 护甲**破碎时对周围敌人造成伤害」—— 两件事都要能触发同一条增幅，
	/// 而它是**一行**增幅（一个 <c>Trigger</c> 列）。写法：<c>Trigger = "Parried,ArmorBroken"</c>。</para>
	/// </summary>
	public string Trigger { get; set; } = "Kill";

	/// <summary>一次性 CDR 的目标（**技能行 Id**，不是节点名）：走 `Ability.FindByAbilityId`。</summary>
	public string AbilityId { get; set; } = string.Empty;

	/// <summary>乱金柝：只有**当前有滞空**时才出手（"当角色有滞空时"是文档的条件）。</summary>
	public bool RequireHover { get; set; }

	/// <summary>一次性 CDR：剩余冷却 ×(1 - 本值)（闪身步 0.8）。</summary>
	public float CooldownFraction { get; set; }

	/// <summary>一次性 CDR：剩余冷却直接减这么多秒。</summary>
	public float CooldownSeconds { get; set; }

	/// <summary>一次性 CDR：冷却清零 + 层数补满（招架反击）。</summary>
	public bool RefreshAbility { get; set; }

	/// <summary>回血点数（普渡）。</summary>
	public float Heal { get; set; }

	/// <summary>刷新滞空到这么多秒（乱金柝）。</summary>
	public float HoverSeconds { get; set; }

	/// <summary>每次触发获得几次额外空中跳跃（践踏死亡）。</summary>
	public int ExtraAirJumps { get; set; }

	/// <summary>额外空中跳跃的**上限**（文档：最多 N 次）。</summary>
	public int JumpCap { get; set; } = 3;

	/// <summary>对周围敌人的作用半径（能量护盾；0 = 不做这一段）。</summary>
	public float Radius { get; set; }

	/// <summary>对周围敌人的伤害（能量护盾）。</summary>
	public float Damage { get; set; }

	/// <summary>践踏死亡累计触发了几次（= 拿到了几次额外空中跳跃）。</summary>
	public int TriggerTally { get; private set; }

	private Unit? _owner;

	public override void _Ready()
	{
		base._Ready();
		_owner = GetParent<Unit>();
	}

	public override bool Matches(GameplayEvent gameplayEvent)
	{
		if (_owner == null || !IsInstanceValid(_owner))
		{
			return false;
		}

		bool mine = gameplayEvent.Source == _owner;

		// `Trigger` 是逗号分隔的事件名列表（P6a：「能量护盾」的"格挡 / 护甲破碎"两件事共用一个 Trigger 列）。
		// 任一条命中即通过 —— 单值时行为与改前逐位一致。
		bool kindOk = false;
		foreach (string trigger in Triggers)
		{
			if (MatchesKind(trigger, gameplayEvent, mine))
			{
				kindOk = true;
				break;
			}
		}

		if (!kindOk)
		{
			return false;
		}

		// 「当角色有滞空时，击杀会刷新滞空时间」—— 条件放在 Matches 里（放 Execute 会白白吃掉一次冷却）。
		return !RequireHover || (NodeSearch.FindInSubtree<AirHoverController>(_owner)?.HoverRemaining ?? 0f) > 0f;
	}

	/// <summary>
	/// 单个事件名是否命中。**每一条的判据与 P5/P5b 落地时逐字一致**（只是从 switch 搬进了方法）：
	/// <list type="bullet">
	/// <item>`Kill` / `Parried` / `Critical` / `Damaged`：只看 <c>Source == 自己</c>（<c>mine</c>）。</item>
	/// <item>`ArmorBroken`：<c>mine</c> **或** <c>Target == 自己</c> —— 因为"自己的甲被打碎"时
	///   <c>Source</c> 是**敌人**、<c>Target</c> 才是自己（这条判据是 P5b 写好的，P6a 起才真的收得到事件）。</item>
	/// </list>
	/// </summary>
	private bool MatchesKind(string trigger, in GameplayEvent gameplayEvent, bool mine) => trigger switch
	{
		"Kill" => gameplayEvent.Kind == GameplayEventKind.Kill && mine,
		"Parried" => gameplayEvent.Kind == GameplayEventKind.Parried && mine,
		"Critical" => gameplayEvent.Kind == GameplayEventKind.Critical && mine,
		"ArmorBroken" => gameplayEvent.Kind == GameplayEventKind.ArmorBroken && (mine || gameplayEvent.Target == _owner),
		"Damaged" => gameplayEvent.Kind == GameplayEventKind.Damaged && mine,
		_ => false,
	};

	/// <summary>`Trigger` 拆出来的事件名列表（惰性缓存一次；`Trigger` 构造后不再改）。</summary>
	private string[] Triggers => _triggers ??= Trigger.Split(
		',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

	private string[]? _triggers;

	public override void Execute(GameplayEvent gameplayEvent)
	{
		if (_owner == null || !IsInstanceValid(_owner))
		{
			return;
		}

		TriggerTally++;

		// ① 一次性 CDR —— 走的正是 abilities.md §4 通路③ 那三个接口（比例 / 秒数 / 刷新）。
		if (!string.IsNullOrEmpty(AbilityId)
			&& (CooldownFraction > 0f || CooldownSeconds > 0f || RefreshAbility))
		{
			if (Ability.FindByAbilityId(_owner, AbilityId) is { } ability)
			{
				if (RefreshAbility)
				{
					ability.RefreshCooldown();
				}

				if (CooldownFraction > 0f)
				{
					ability.ReduceCooldownFraction(CooldownFraction);
				}

				if (CooldownSeconds > 0f)
				{
					ability.ReduceCooldown(CooldownSeconds);
				}
			}
			else
			{
				GD.PushWarning($"[Reward] 增幅 {RewardId} 要改的技能行 {AbilityId} 不在这位玩家身上，这一次没有生效。");
			}
		}

		// ② 回血（普渡）。
		if (Heal > 0f)
		{
			_owner.Heal(Heal);
		}

		// ③ 刷新滞空（乱金柝）。
		if (HoverSeconds > 0f && NodeSearch.FindInSubtree<AirHoverController>(_owner) is { } hover)
		{
			hover.Grant(HoverSeconds);
		}

		// ④ 额外空中跳跃（践踏死亡）：**累加**授予。文档原话「每次击杀都会获得额外跳跃的次数
		// （额外跳跃次数最多 N 次）」，所以是在**当前值**之上加，由表里的 `JumpCap` 封顶。
		//
		// ⚠️ P6a 修掉一个**潜伏 bug**：原来写的是 `SetExtraAirJumps(TriggerTally * ExtraAirJumps, JumpCap)`
		// —— 那是「把总数**覆盖**成 自己触发次数 × 每次给多少」，会把**别人**已经授予的那几次吃掉。
		// 受害的正是角色 A 的被动「灵动」（`AirJumpPassiveAbility` 授予 1 次）：拿了「践踏死亡」之后
		// 击杀一次，额外跳跃反而从 1 掉回 1（没有增长）。旧 3 个角色都没有这个被动，所以一直没显形；
		// P6a 把自测基准换成角色 A［猎手］之后它立刻暴露（`P5b·实测异化规则` 那一行 1 → 1）。
		//
		// 为什么"累加"是安全的：规则实例由 `_build.Add(...)` 在**选到这条增幅时**建一次，
		// `RunBuild` 是 `RunController` 的 `readonly` 字段（一局一个），所以不会因为换角色/换武器
		// 被重复套用而重复授予。（覆盖写法原本是为了防这件事，代价是吃掉别人的授予。）
		if (ExtraAirJumps != 0 && _owner.Movement is { } movement)
		{
			movement.SetExtraAirJumps(movement.ExtraAirJumps + ExtraAirJumps, JumpCap);
		}

		// ⑤ 对周围敌人造成伤害（能量护盾）。
		if (Radius > 0f && Damage > 0f)
		{
			PulseAroundOwner();
		}

		GameLog.Write("Reward", $"增幅·事件规则：{RewardId} 触发（{Trigger}，第 {TriggerTally} 次）");
	}

	private void PulseAroundOwner()
	{
		if (_owner == null)
		{
			return;
		}

		Vector3 from = _owner.GlobalPosition;
		float best = Radius * Radius;
		foreach (Node node in GetTree().GetNodesInGroup("enemy_units"))
		{
			if (node is not Unit candidate || !candidate.IsValidTarget)
			{
				continue;
			}

			Vector3 toCandidate = candidate.GlobalPosition - from;
			if (toCandidate.LengthSquared() > best)
			{
				continue;
			}

			var attack = new AttackInstance
			{
				Attacker = _owner,
				Ability = RewardId,
				AbilityKind = RewardId,
				BaseDamage = Damage,
				Policy = HitPolicy.OncePerTarget,
				Tags = new[] { "Pulse" },
			};
			candidate.ReceiveHit(attack.RollHit(candidate, toCandidate.Normalized(), candidate.GlobalPosition));
		}
	}
}

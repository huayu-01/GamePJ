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
			PulseDamage.Around(this, _owner, Radius, Damage, RewardId);
		}

		GameLog.Write("Reward", $"增幅·事件规则：{RewardId} 触发（{Trigger}，第 {TriggerTally} 次）");
	}
}

/// <summary>
/// 「以自己为圆心、对半径内所有敌人各造成一次伤害」—— 能量护盾（<see cref="TablePulseRule"/>）与
/// 杀戮践踏（<see cref="StompOnLandRule"/>，P6b 新增）**共用这一份实现**。
///
/// <para>抽出来的理由：这是"命中规则"，两条增幅各写一遍迟早会漂移（一个改半径判据、另一个忘改）。
/// 行为与 P5b 落地时逐位一致（组 `enemy_units` / <see cref="Unit.IsValidTarget"/> / 平方距离比较 /
/// `HitPolicy.OncePerTarget` / 标签 `Pulse`）。</para>
/// </summary>
internal static class PulseDamage
{
	/// <summary>围绕 <paramref name="owner"/> 自己（= 它的位置）打一圈。</summary>
	public static int Around(Node tree, Unit owner, float radius, float damage, string abilityId)
		=> Around(tree, owner, owner?.GlobalPosition ?? Vector3.Zero, radius, damage, abilityId);

	/// <summary>
	/// 围绕**给定的世界点**打一圈（返回打到了几个敌人，自测取证用）。
	///
	/// <para>P6d 新增的只是"圆心可以不是施法者自己"这一点：「落日剑」的剑是落在**目标头上**
	/// 掉下来的，圆心是那个敌人当时的位置，而归属（<c>Attacker</c>）仍然是玩家 ——
	/// 用原来那个签名会把圆心算成玩家的位置，等于在玩家脚下打一圈（错的）。</para>
	///
	/// <para>旧签名的行为**逐位不变**（它只是把 <c>owner.GlobalPosition</c> 转交给这一个）。</para>
	/// </summary>
	public static int Around(Node tree, Unit owner, Vector3 center, float radius, float damage, string abilityId)
	{
		if (radius <= 0f || damage <= 0f || !GodotObject.IsInstanceValid(owner) || tree?.GetTree() is not { } sceneTree)
		{
			return 0;
		}

		int hits = 0;
		Vector3 from = center;
		float best = radius * radius;
		foreach (Node node in sceneTree.GetNodesInGroup("enemy_units"))
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
				Attacker = owner,
				Ability = abilityId,
				AbilityKind = abilityId,
				BaseDamage = damage,
				Policy = HitPolicy.OncePerTarget,
				Tags = new[] { "Pulse" },
			};
			candidate.ReceiveHit(attack.RollHit(candidate, toCandidate.Normalized(), candidate.GlobalPosition));
			hits++;
		}

		return hits;
	}
}

/// <summary>
/// 异化「杀戮践踏」（**P6b 新增**）—— 文档 `角色设计.txt` 第 169 行：
/// 「杀戮践踏:跳跃时会对周围{n}码敌人造成{n}伤害,空中跳跃同样享受这个效果;」。
///
/// <para><b>判据取的是"落地那一刻"</b>（行名就是"践踏"）：<see cref="CharacterMovement"/> 里
/// "空中跳跃"与"地面跳跃"共用同一条 <c>TryJump</c>，落地判据天然把两者都覆盖到 ——
/// 正是文档那句「空中跳跃同样享受这个效果」。</para>
///
/// <para><b>为什么是轮询、不是订阅事件</b>：<c>CharacterMovement</c> 是**共用战斗类**（P6b 硬约束：
/// 本批不许碰），它没有抛"起跳 / 落地"事件。这里改为每帧读它**已经公开**的
/// <see cref="CharacterMovement.IsGrounded"/> / <see cref="CharacterMovement.FallHeight"/> 做边沿检测，
/// 一行共用代码都不用动。⚠️ 文档只写了 `n`（码数 / 伤害都没给），表里填的是**预设值**。</para>
/// </summary>
public partial class StompOnLandRule : TriggerRule
{
	/// <summary>作用半径（米）。</summary>
	public float Radius { get; set; } = 3f;

	/// <summary>对半径内每个敌人的伤害。</summary>
	public float Damage { get; set; } = 15f;

	/// <summary>要从多高落下才算（米；0 = 任何落地都算）。</summary>
	public float MinFallHeight { get; set; }

	/// <summary>触发几次（自测取证）。</summary>
	public int TriggerTally { get; private set; }

	private Unit? _owner;
	private bool _initialized;
	private bool _wasGrounded = true;
	private float _sinceTrigger;

	/// <summary>本规则**不是事件驱动**的（跳跃/落地在共用战斗类里没有事件，见类注释）——
	/// 永远不匹配任何 <see cref="GameplayEvent"/>，出手完全由 <see cref="_Process"/> 的边沿检测发起。</summary>
	public override bool Matches(GameplayEvent gameplayEvent) => false;

	public override void _Ready()
	{
		base._Ready();
		_owner = GetParent<Unit>();
	}

	public override void _Process(double delta)
	{
		if (!GodotObject.IsInstanceValid(_owner) || _owner.Movement is not { } movement)
		{
			return;
		}

		_sinceTrigger += (float)delta;
		bool grounded = movement.IsGrounded;
		if (!_initialized)
		{
			// 第一帧只记基线：出生就站在地上不算"落地"。
			_initialized = true;
			_wasGrounded = grounded;
			return;
		}

		bool landed = grounded && !_wasGrounded;
		_wasGrounded = grounded;
		if (!landed || _sinceTrigger < Cooldown || movement.FallHeight < MinFallHeight)
		{
			return;
		}

		_sinceTrigger = 0f;
		Execute(default);
	}

	public override void Execute(GameplayEvent gameplayEvent)
	{
		if (!GodotObject.IsInstanceValid(_owner))
		{
			return;
		}

		TriggerTally++;
		int hits = PulseDamage.Around(this, _owner!, Radius, Damage, RewardId);
		GameLog.Write(
			"Reward",
			$"增幅·事件规则：{RewardId} 触发（落地践踏，第 {TriggerTally} 次，半径 {Radius:0.#} 伤害 {Damage:0.#} → 命中 {hits} 个）");
	}
}

/// <summary>
/// 异化「巡飞弹」（**P6b 新增**）—— 文档 `角色设计.txt` 第 182 行：
/// 「巡飞弹:你的暴击伤害会使你发射一发追踪附近敌人的飞弹,伤害{50};」。
///
/// <para>复用的是 **P3 已经做好并自测过的追踪弹通路**（<c>Projectile.HomingStatusId</c> +
/// <c>HomingTurnRateDegrees</c>，见 <c>ArrowVolleyAbility</c> 与 <c>docs/abilities.md</c> §7）：
/// 这里只是把"锁定目标"换成"先给最近的敌人挂一个**专用信标状态**，再让飞弹追这个状态"。</para>
///
/// <para>⚠️ 信标**故意不复用「标记」**（<c>Mark</c>）：那样会和「心眼」（对被标记目标 +50% 伤害）、
/// 「发散」（每层标记加技能 1 伤害）产生非预期的联动。所以另立一行效果表
/// （`Config/Tables/Effects/eff_missile_beacon.tres`，无任何数值修正的纯信标）。</para>
///
/// <para>暴击事件的抛点在 P6a 已补（唯一）：`Unit.ReceiveHit` 结算完这次命中、`HitInstance.Critical`
/// 且真的打出了伤害。这里再按 <c>Source == 自己</c> 过滤，所以"敌人的暴击"不会触发。</para>
/// </summary>
public partial class HomingMissileOnCritRule : TriggerRule
{
	/// <summary>飞弹预制体（表列 `ProjectileScene`，路径字符串）。</summary>
	public PackedScene? MissileScene { get; set; }

	/// <summary>飞弹伤害（文档 50）。</summary>
	public float Damage { get; set; } = 50f;

	/// <summary>飞弹速度（米/秒）。</summary>
	public float Speed { get; set; } = 60f;

	/// <summary>索敌 / 追踪半径（米）。</summary>
	public float SeekRange { get; set; } = 18f;

	/// <summary>飞弹存活时间（秒）。</summary>
	public float Lifetime { get; set; } = 4f;

	/// <summary>信标状态 Id（表列 `BeaconStatusId`）。</summary>
	public string BeaconStatusId { get; set; } = string.Empty;

	/// <summary>信标持续时间（秒）。</summary>
	public float BeaconSeconds { get; set; } = 1.5f;

	/// <summary>追踪转向速度（度/秒）。</summary>
	public float TurnRateDegrees { get; set; } = 360f;

	/// <summary>发射几发（自测取证）。</summary>
	public int FireCount { get; private set; }

	private Unit? _owner;

	public override void _Ready()
	{
		base._Ready();
		_owner = GetParent<Unit>();
	}

	public override bool Matches(GameplayEvent gameplayEvent)
		=> gameplayEvent.Kind == GameplayEventKind.Critical && gameplayEvent.Source == _owner;

	public override void Execute(GameplayEvent gameplayEvent)
	{
		if (!GodotObject.IsInstanceValid(_owner) || MissileScene == null || _owner!.IsDead)
		{
			return;
		}

		Unit? target = Targeting.FindNearestHostile(_owner, _owner, SeekRange);
		if (target == null)
		{
			return;   // 附近没有敌人 → 这一发不发射（不空放，也不吃掉冷却以外的东西）
		}

		// 信标：让飞弹"追得到"（Projectile 的追踪判据是"目标身上有这个状态"）。
		if (!string.IsNullOrEmpty(BeaconStatusId)
			&& StatusPresets.Create(BeaconStatusId, BeaconSeconds) is { } beacon)
		{
			target.ApplyStatus(beacon);
		}

		Vector3 origin = _owner.GlobalPosition + (Vector3.Up * 1.2f);
		Vector3 toTarget = (target.GlobalPosition + (Vector3.Up * 0.9f)) - origin;
		Vector3 direction = toTarget.LengthSquared() < 0.0001f ? -_owner.GlobalTransform.Basis.Z : toTarget.Normalized();

		var attack = new AttackInstance
		{
			Attacker = _owner,
			Ability = RewardId,
			AbilityKind = RewardId,
			BaseDamage = Damage,
			CanCrit = false,
			Policy = HitPolicy.OncePerTarget,
			Lifetime = Lifetime,
			Tags = new[] { "Projectile", "Missile" },
		};

		var missile = MissileScene.Instantiate<Projectile>();
		(GetTree().CurrentScene ?? GetTree().Root).AddChild(missile);
		missile.GlobalPosition = origin;
		missile.Speed = Speed;
		missile.MaxLifetime = Lifetime;
		missile.HomingStatusId = BeaconStatusId;
		missile.HomingTurnRateDegrees = TurnRateDegrees;
		missile.HomingRange = SeekRange;
		missile.Launch(attack, direction);
		FireCount++;

		GameLog.Write(
			"Reward",
			$"增幅·事件规则：{RewardId} 触发（暴击 → 巡飞弹，第 {FireCount} 发，追 {target.Name}，伤害 {Damage:0.#} 弹速 {Speed:0.#}）");
	}
}

/// <summary>
/// 异化「护卫」（**P6d 新增**）—— 文档 `角色设计.txt` 第 172 行：
/// 「护卫:奔跑时获得一次格挡,格挡破碎{5}秒后才可再次获取;」。
///
/// <para><b>触发条件</b>：<see cref="SprintController.IsSprinting"/>（P6d 新增的"奔跑"状态，
/// 文档第 19 行「所有角色通用的机制」）—— 奔跑中、手上没有格挡、且距上次破碎已过
/// <see cref="TriggerRule.Cooldown"/> 秒（文档给的 <c>{5}</c> 就是它）→ 获得一次格挡。</para>
///
/// <para><b>格挡是什么</b>：走**既有的** <see cref="BlockComponent"/>（战斗规则说明 §14 的独立格挡模块），
/// 规则自己在玩家身上建一个名为 <c>Block</c> 的子节点（**只在选到这条增幅时才建**，所以不选 = 逐位不变），
/// 数值（<see cref="BlockDamageMultiplier"/> / <see cref="FrontDotThreshold"/>）全部来自行。
/// 获得格挡 = <c>Blocking = true</c>；格挡破碎 = 一次命中真的被挡掉（<see cref="DamageResult.Blocked"/>）。</para>
///
/// <para><b>为什么不用"无敌帧"那条路</b>（<c>GuardStanceAbility</c> 的写法）：文档这里说的是**格挡**，
/// 而格挡在本项目里有正式定义（独立防御机制、可部分减伤、可判正面）—— 用 <c>IsInvulnerable</c> 反而会把
/// "格挡"与"无敌"两件事混成一件（那正是 <c>GuardStanceAbility</c> 的注释里写清楚的区分）。
/// 顺带：<c>Unit.ReceiveHit</c> 的结算顺序里格挡在减伤**之前**，所以这里不需要动任何共用战斗类。</para>
///
/// <para><b>为什么是轮询 + 订阅 <c>Unit.Damaged</c>、而不是订阅事件总线</b>：与
/// <see cref="StompOnLandRule"/> 同一条纪律 —— 事件总线上没有"被格挡了"这个种类，
/// 而 <c>Unit.Damaged</c>（C# 事件）**已经**带着 <c>DamageResult.Blocked</c>，直接读它是最小改动。</para>
/// </summary>
public partial class GuardOnSprintRule : TriggerRule
{
	/// <summary>格挡后剩余的伤害比例（0 = 完全格挡）。来自行参数 <c>BlockDamageMultiplier</c>。</summary>
	public float BlockDamageMultiplier { get; set; }

	/// <summary>正面判定阈值（1 = 任意方向都算挡到，-1 = 只有正面）。来自行参数 <c>FrontDotThreshold</c>。</summary>
	public float FrontDotThreshold { get; set; } = 1f;

	/// <summary>现在手上有没有一次格挡（自测 / HUD）。</summary>
	public bool HasGuard { get; private set; }

	/// <summary>本局累计获得过几次格挡（自测取证）。</summary>
	public int GuardGranted { get; private set; }

	/// <summary>本局累计被打碎几次（= 真的挡掉了几次命中）。</summary>
	public int BlockedCount { get; private set; }

	/// <summary>距下次可获取还剩几秒（0 = 已经可以再获取）。</summary>
	public float RechargeRemaining { get; private set; }

	/// <summary>最近一次挡掉的伤害（自测取证）。</summary>
	public float LastBlockedDamage { get; private set; }

	private Unit? _owner;
	private BlockComponent? _block;
	private bool _subscribed;
	private bool _warnedNoSprint;

	/// <summary>本规则**不是**事件总线驱动的（"格挡成功"没有事件种类）——
	/// 永远不匹配任何 <see cref="GameplayEvent"/>，出手完全由 <see cref="_Process"/> 的轮询发起。</summary>
	public override bool Matches(GameplayEvent gameplayEvent) => false;

	public override void _Ready()
	{
		base._Ready();
		_owner = GetParent<Unit>();
		if (_owner != null)
		{
			_owner.Damaged += OnOwnerDamaged;
			_subscribed = true;
		}
	}

	public override void _ExitTree()
	{
		if (_subscribed && _owner != null && GodotObject.IsInstanceValid(_owner))
		{
			_owner.Damaged -= OnOwnerDamaged;
		}

		_subscribed = false;
		base._ExitTree();
	}

	public override void _Process(double delta)
	{
		PollOnce((float)delta);
	}

	/// <summary>
	/// 轮询一次：扣重取计时 → 手上没格挡且计时到了且在奔跑 → 获得一次格挡。
	/// **`_Process` 与自测探针走的是同一个入口**（证明"奔跑 → 获得格挡"这条链路本身，
	/// 而不是探针自己另写一遍条件）。
	/// </summary>
	public void PollOnce(float delta)
	{
		if (!GodotObject.IsInstanceValid(_owner) || _owner!.IsDead)
		{
			return;
		}

		RechargeRemaining = Mathf.Max(0f, RechargeRemaining - delta);
		if (HasGuard || RechargeRemaining > 0f)
		{
			return;
		}

		SprintController? sprint = NodeSearch.FindInSubtree<SprintController>(_owner);
		if (sprint == null)
		{
			if (!_warnedNoSprint)
			{
				_warnedNoSprint = true;
				GameLog.Write("Reward", $"⚠ 增幅 {RewardId}（护卫）：这个单位身上没有 SprintController → 永远进不了「奔跑」，这一条不会生效。");
			}

			return;
		}

		if (!sprint.IsSprinting)
		{
			return;
		}

		Execute(default);
	}

	/// <summary>
	/// 授予一次格挡。**入口只有这一处**：<see cref="_Process"/> 的轮询（奔跑中）与自测探针的直接调用，
	/// 走的都是它 —— 与 <see cref="StompOnLandRule"/> 同一个口径（"出手入口唯一"）。
	/// </summary>
	public override void Execute(GameplayEvent gameplayEvent) => Grant();

	/// <summary>奔跑中 → 获得一次格挡（格挡组件按需建，只在**拿到这条增幅之后**才存在）。</summary>
	private void Grant()
	{
		BlockComponent block = EnsureBlock();
		block.Blocking = true;
		HasGuard = true;
		GuardGranted++;
		GameLog.Write(
			"Reward",
			$"增幅·事件规则：{RewardId} 触发（奔跑 → 获得格挡，第 {GuardGranted} 次；挡后剩伤 {BlockDamageMultiplier:0.##} 正面阈值 {FrontDotThreshold:0.##}）");
	}

	/// <summary>被命中：这一下真的被格挡挡掉（<see cref="DamageResult.Blocked"/>）→ 格挡破碎 + 进入 {5} 秒重取间隔。</summary>
	private void OnOwnerDamaged(DamageResult result, HitInstance hit)
	{
		if (!HasGuard || !result.Blocked)
		{
			return;
		}

		if (_block != null && GodotObject.IsInstanceValid(_block))
		{
			_block.Blocking = false;
		}

		HasGuard = false;
		BlockedCount++;
		LastBlockedDamage = hit.Damage;
		RechargeRemaining = Cooldown;
		GameLog.Write(
			"Reward",
			$"增幅·事件规则：{RewardId} 触发（格挡破碎，挡下 {hit.Damage:0.##} 点，累计 {BlockedCount} 次）→ {Cooldown:0.##}s 后才可再次获取");
	}

	/// <summary>
	/// 拿格挡组件（没有就在玩家身上建一个名为 <c>Block</c> 的子节点）。
	/// ⚠️ <c>Unit.Block</c> 是惰性查的（<c>_block ??= GetNodeOrNull&lt;BlockComponent&gt;("Block")</c>），
	/// 所以运行期建出来的这个节点**会被 <c>Unit.ReceiveHit</c> 正常读到**，不需要改 <c>Unit</c>。
	/// </summary>
	private BlockComponent EnsureBlock()
	{
		if (_block != null && GodotObject.IsInstanceValid(_block))
		{
			return _block;
		}

		_block = _owner!.GetNodeOrNull<BlockComponent>("Block");
		if (_block == null)
		{
			_block = new BlockComponent { Name = "Block", Blocking = false };
			_owner.AddChild(_block);
		}

		_block.BlockDamageMultiplier = BlockDamageMultiplier;
		_block.FrontDotThreshold = FrontDotThreshold;
		_block.BreakThreshold = 0f;
		return _block;
	}

	/// <summary>探针用：一行的实际状态。</summary>
	public string DebugState()
		=> $"护卫：手上有格挡={HasGuard} 累计获得={GuardGranted} 被打碎={BlockedCount} 重取剩余={RechargeRemaining:0.##}s"
			+ $"｜数值 挡后剩伤={BlockDamageMultiplier:0.##} 正面阈值={FrontDotThreshold:0.##} 间隔={Cooldown:0.##}s";
}

/// <summary>
/// 异化「落日剑」（**P6d 新增**）—— 文档 `角色设计.txt` 第 187-188 行：
/// 「落日剑:武技1会对没有落日剑的敌人施加落日剑;（落日剑:为目标头顶悬挂一柄剑,一段时间后剑会落下造成伤害）」。
///
/// <list type="bullet">
/// <item><b>触发条件</b>：一次 <see cref="GameplayEventKind.Hit"/>，且 ① 来源是自己、
///   ② 目标是敌对单位、③ **这一击来自"武技1"**、④ 目标身上**还没有**落日剑
///   （<see cref="SunsetSwordMarker.NodeName"/> 那个子节点不存在 —— 这正是文档那句
///   「对**没有**落日剑的敌人施加」）。</item>
/// <item><b>"武技1"怎么判</b>（不写死技能 Id）：拿**按键动作**（<see cref="TriggerInputAction"/>，来自行）去问
///   <c>PlayerAbilityInput.FindByInputAction</c> —— 它读的是技能行的 <c>InputAction</c> 列，
///   所以"哪个技能在武技1 上"是**配置**；再把这**一次命中**的 <c>Tag</c>（= <c>AttackInstance.Ability</c>，
///   在 <c>Unit.ReceiveHit</c> 里写进事件）与它比。换武器 / 加武器都不用改代码。</item>
/// <item><b>效果</b>：给目标挂一个 <see cref="SunsetSwordMarker"/> 子节点（头顶一把剑），
///   它自己在 <c>HoverSeconds</c> 后落下，落地按 <c>Radius</c>/<c>Damage</c> 打一圈 + 画一圈
///   （复用 P6c 的 <c>LandingShockwave</c>）。</item>
/// </list>
///
/// <para>⚠️ 文档只写了"一段时间后"与"造成伤害"：<c>HoverSeconds</c> / <c>FallSeconds</c> /
/// <c>Radius</c> / <c>Damage</c> / <c>HoverHeight</c> 全是**预设值**（行 `Note` 里逐条写明）。</para>
/// </summary>
public partial class SunsetSwordRule : TriggerRule
{
	/// <summary>哪个按键动作算"武技1"（来自行；空 = 这一条永远不会出手）。</summary>
	public string TriggerInputAction { get; set; } = "weapon_skill_1";

	/// <summary>头顶悬挂多久之后落下（秒）。</summary>
	public float HoverSeconds { get; set; } = 1.5f;

	/// <summary>下落耗时（秒）。</summary>
	public float FallSeconds { get; set; } = 0.35f;

	/// <summary>落地伤害的作用半径（米）。</summary>
	public float Radius { get; set; } = 2.5f;

	/// <summary>落地伤害。</summary>
	public float Damage { get; set; } = 40f;

	/// <summary>悬挂高度（米）。</summary>
	public float HoverHeight { get; set; } = 3.2f;

	/// <summary>本局一共施加了几次（自测取证）。</summary>
	public int ApplyCount { get; private set; }

	private Unit? _owner;
	private bool _warnedNoAbility;

	public override void _Ready()
	{
		base._Ready();
		_owner = GetParent<Unit>();
	}

	public override bool Matches(GameplayEvent gameplayEvent)
	{
		Unit? owner = _owner;
		if (gameplayEvent.Kind != GameplayEventKind.Hit
			|| owner == null
			|| !GodotObject.IsInstanceValid(owner)
			|| gameplayEvent.Source != owner
			|| gameplayEvent.Target is not Unit victim
			|| victim == owner
			|| victim.IsDead
			|| !victim.IsHostileTo(owner))
		{
			return false;
		}

		// 「对**没有**落日剑的敌人施加」—— 已经有了就不再施加（剑在头顶挂着）。
		if (victim.GetNodeOrNull<SunsetSwordMarker>(SunsetSwordMarker.NodeName) != null)
		{
			return false;
		}

		return IsWeaponSkill1(gameplayEvent.Tag);
	}

	/// <summary>这一击是不是"武技1"打出来的（按**按键动作**反查技能行，不写死技能 Id）。</summary>
	private bool IsWeaponSkill1(string? abilityId)
	{
		Unit? owner = _owner;
		if (owner == null || string.IsNullOrEmpty(abilityId) || string.IsNullOrEmpty(TriggerInputAction))
		{
			return false;
		}

		Ability? weaponSkill1 = ActRoguelike.Player.PlayerAbilityInput.FindByInputAction(owner, TriggerInputAction);
		if (weaponSkill1 == null)
		{
			if (!_warnedNoAbility)
			{
				_warnedNoAbility = true;
				GameLog.Write(
					"Reward",
					$"⚠ 增幅 {RewardId}（落日剑）：这位玩家身上没有绑在「{TriggerInputAction}」上的技能 → 这一条不会生效（检查武器行的武技1 技能行与它的 InputAction 列）。");
			}

			return false;
		}

		return weaponSkill1.AbilityId == abilityId;
	}

	public override void Execute(GameplayEvent gameplayEvent)
	{
		Unit? owner = _owner;
		if (owner == null
			|| !GodotObject.IsInstanceValid(owner)
			|| gameplayEvent.Target is not Unit victim
			|| !GodotObject.IsInstanceValid(victim))
		{
			return;
		}

		if (victim.GetNodeOrNull<SunsetSwordMarker>(SunsetSwordMarker.NodeName) != null)
		{
			return;
		}

		var marker = new SunsetSwordMarker
		{
			Name = SunsetSwordMarker.NodeName,
			Attacker = owner,
			AbilityId = RewardId,
			HoverSeconds = HoverSeconds,
			FallSeconds = FallSeconds,
			Radius = Radius,
			Damage = Damage,
			HoverHeight = HoverHeight,
		};
		victim.AddChild(marker);
		ApplyCount++;

		GameLog.Write(
			"Reward",
			$"增幅·事件规则：{RewardId} 施加落日剑（第 {ApplyCount} 次，目标 {victim.Name}：悬挂 {HoverSeconds:0.##}s → 落下 {FallSeconds:0.##}s → 半径 {Radius:0.##}m / 伤害 {Damage:0.##}）");
	}

	/// <summary>探针用：一行的实际状态。</summary>
	public string DebugState()
		=> $"落日剑规则：施加={ApplyCount}｜触发按键「{TriggerInputAction}」｜悬挂={HoverSeconds:0.##}s 落下={FallSeconds:0.##}s"
			+ $" 半径={Radius:0.##}m 伤害={Damage:0.##} 高度={HoverHeight:0.##}m 间隔={Cooldown:0.##}s";
}

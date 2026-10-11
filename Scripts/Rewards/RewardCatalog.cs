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
/// </summary>
public static class RewardCatalog
{
	/// <summary>
	/// 代码内置目录 —— **兜底**。表（`Config/Tables/Upgrades/upg_*.tres` + `Balance.tres` 的
	/// <c>Upgrades</c> 数组）读得到就用表；读不到才用这里的 C# 数组。
	/// 这是"读不到表就用自身默认值"这条回退语义在增幅项上的落地。
	/// </summary>
	public static readonly RewardDefinition[] Fallback =
	{
		// ───────── 强化（改参数，可叠层） ─────────
		Enhance("dmg_up", "增伤弹头", "伤害 +15%", 3,
			c => Mod(c, "DamageMultiplier", ModifierOperation.Multiply, 1.15f)),
		Enhance("rate_up", "高速扳机", "射速 +12%", 3,
			c => Mod(c, "AttackSpeedMultiplier", ModifierOperation.Multiply, 1.12f)),
		Enhance("boots", "战术靴", "移动速度 +10%", 3,
			c => Mod(c, "MoveSpeedMultiplier", ModifierOperation.Multiply, 1.10f)),
		Enhance("vitality", "强化肌体", "最大生命 +25", 3,
			c => Mod(c, "MaxHealthAdd", ModifierOperation.Add, 25f)),
		Enhance("scope", "精准准星", "暴击率 +8%", 3,
			c => Mod(c, "CritChanceAdd", ModifierOperation.Add, 0.08f)),
		Enhance("lethal", "致命打击", "暴击伤害 +35%", 2,
			c => Mod(c, "CritMultiplierAdd", ModifierOperation.Add, 0.35f)),
		Enhance("fast_reload", "快速装填", "装填速度 +25%", 2,
			c => Mod(c, "ReloadSpeedMultiplier", ModifierOperation.Multiply, 1.25f)),
		Enhance("ext_mag", "扩容弹匣", "弹匣 +3", 2,
			c => Mod(c, "MagazineSizeAdd", ModifierOperation.Add, 3f)),
		Enhance("stance", "稳固站姿", "受到的伤害 -8%", 3,
			c => Mod(c, "DamageReductionAdd", ModifierOperation.Add, 0.08f)),
		Enhance("pierce_round", "贯穿弹头", "子弹可多穿透 1 个目标", 2,
			c => Mod(c, "PierceAdd", ModifierOperation.Add, 1f), excludes: new[] { "evo_ricochet" }),

		// ───────── 进化（武器限定，改核心行为） ─────────
		Evolution("evo_twin", "双管齐下", "每次射击额外发射一发子弹", "Gun", 1,
			c => Mod(c, "ProjectileCountAdd", ModifierOperation.Add, 1f)),
		Evolution("evo_ap", "穿甲弹", "子弹额外穿透 2 个目标", "Gun", 1,
			c => Mod(c, "PierceAdd", ModifierOperation.Add, 2f), excludes: new[] { "evo_ricochet" }),
		Evolution("evo_ricochet", "跳弹", "命中后弹射到附近的另一个敌人", "Gun", 1,
			c => Mod(c, "RicochetAdd", ModifierOperation.Add, 1f), excludes: new[] { "pierce_round", "evo_ap" }),
		Evolution("evo_blast", "爆裂弹头", "子弹命中时产生小范围爆炸", "Gun", 1, c =>
		{
			Mod(c, "ExplosionRadiusAdd", ModifierOperation.Override, 3.5f);
			Mod(c, "ExplosionDamageScale", ModifierOperation.Override, 0.6f);
		}),

		// ───────── 异化（加新规则，通过 Trigger/Effect 实现） ─────────
		Mutation("mut_chain", "连锁闪电", "击杀敌人时对附近敌人放电", 1, c =>
		{
			c.Player.AddChild(new ChainLightningRule
			{
				RewardId = "mut_chain",
				Cooldown = 0.12f,
				Damage = 14f,
				Range = 7f,
				Jumps = 3,
			});
		}),
		Mutation("mut_lifesteal", "吸血", "造成伤害的 12% 回复为生命", 1, c =>
		{
			c.Player.AddChild(new LifestealRule { RewardId = "mut_lifesteal", Ratio = 0.12f });
		}),
		Mutation("mut_burn", "灼烧弹", "命中使目标燃烧 3 秒", 1, c =>
		{
			c.Player.AddChild(new BurnOnHitRule { RewardId = "mut_burn", Duration = 3f });
		}),
		Mutation("mut_summon", "亡者随从", "每击杀 1 个敌人召唤 1 架无人机（最多 2 架）", 1, c =>
		{
			c.Player.AddChild(new SummonOnKillRule
			{
				RewardId = "mut_summon",
				SummonScene = GD.Load<PackedScene>("res://Scenes/Summons/Drone.tscn"),
				MaxSummons = 2,
			});
		}, requiresSummoner: true),
		Mutation("mut_supply", "战地补给", "击杀敌人立刻补满弹匣", 1, c =>
		{
			c.Player.AddChild(new AmmoOnKillRule { RewardId = "mut_supply" });
		}),
	};

	/// <summary>
	/// 本局实际使用的增幅项目录：**表优先**，表为空 / 未加载 → 回退到 <see cref="Fallback"/>。
	/// 由 <c>Balance</c> 在 <c>_Ready</c> 里读表并编译好（见 <c>RewardTable.Build</c>），
	/// 所以调用方（`RewardPool` / `RunController`）一行都不用改。
	/// </summary>
	public static RewardDefinition[] All { get; private set; } = Fallback;

	/// <summary>换成本局目录（<c>Balance</c> 读表后调用；传 null 等于回到兜底目录）。</summary>
	public static void UseTable(RewardDefinition[]? rows)
		=> All = rows is { Length: > 0 } ? rows : Fallback;

	public static RewardDefinition? Find(string id) => Array.Find(All, d => d.Id == id);

	/// <summary>
	/// 兜底目录的三个工厂。⚠️ 阶段 4 删掉了它们的 <c>tag</c> 参数：那个参数只喂给已删除的死字段
	/// <c>PoolTags</c>（"这条属于哪个池"现在由**专属池引用** + 武器标签两层表达，见
	/// <c>RewardDefinition.AllowsWeapon</c> 的三级回退）。
	/// </summary>
	private static RewardDefinition Enhance(string id, string name, string description, int maxStacks, Action<RewardContext> apply, string[]? excludes = null, bool requiresSummoner = false)
		=> new()
		{
			Id = id,
			Name = name,
			Description = description,
			Kind = RewardKind.Enhancement,
			MaxStacks = maxStacks,
			Apply = apply,
			Excludes = excludes ?? System.Array.Empty<string>(),
			RequiresSummoner = requiresSummoner,
		};

	private static RewardDefinition Evolution(string id, string name, string description, string weaponTag, int maxStacks, Action<RewardContext> apply, string[]? excludes = null)
		=> new()
		{
			Id = id,
			Name = name,
			Description = description,
			Kind = RewardKind.Evolution,
			MaxStacks = maxStacks,
			RequiredWeaponTags = new[] { weaponTag },
			Apply = apply,
			Excludes = excludes ?? System.Array.Empty<string>(),
		};

	private static RewardDefinition Mutation(string id, string name, string description, int maxStacks, Action<RewardContext> apply, string[]? excludes = null, bool requiresSummoner = false)
		=> new()
		{
			Id = id,
			Name = name,
			Description = description,
			Kind = RewardKind.Mutation,
			MaxStacks = maxStacks,
			Apply = apply,
			Excludes = excludes ?? System.Array.Empty<string>(),
			RequiresSummoner = requiresSummoner,
		};

	private static void Mod(RewardContext context, string stat, ModifierOperation operation, float value)
	{
		context.Player.Stats.AddModifier(new StatModifier
		{
			Stat = stat,
			Operation = operation,
			Value = value,
			Source = context.Build,
		});
		context.Player.RefreshStats();
	}
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

using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>
/// **符文规则集合（J11）** —— 「死铠 / 生死 / 狙神 / 巨人 / 小精灵」这 5 枚符文里
/// "需要一个行为"的那几件，全部由 <c>RewardEffectRegistry</c> 的 **`RuneEffect`** 处理器用
/// <c>new</c> 建出来（与 `MarkedBonusRule` / `StompOnLandRule` 同一条纪律）。
///
/// <para><b>一个类服务所有同类符文行</b>：数值全在符文行的 `HandlerParams` 里，代码里一个数字都没有。
/// 这些类没有任何 `.tscn` 挂载（全部运行期 `new`），所以同文件放多个类是安全的
/// （`architecture.md` 第 10 条限制的是"能被场景挂载"的脚本类）。</para>
///
/// <para>⚠️ 符文的**纯数值效果**（雷之印 / 战神印 / 风之印 / 行军 / 大力符 / 飞翼）根本不进这里 ——
/// 它们走既有的 `StatSet` 处理器 + `Modifiers` 列，一条 C# 都不用加。这里只放
/// "改 StatSheet 表达不了"的那几件（击杀计数、死亡避免、按距离/最大生命值加伤、轨道实体）。</para>
/// </summary>
public partial class RuneArmorOnKillRule : TriggerRule
{
	/// <summary>每多少击杀加一次甲（0 = 每次击杀都加）。</summary>
	public int KillsPerTrigger { get; set; } = 10;

	/// <summary>每次加多少甲。</summary>
	public float ArmorPerTrigger { get; set; } = 1f;

	/// <summary>累计击杀（取证用）。</summary>
	public int KillTally { get; private set; }

	/// <summary>累计加甲次数（取证用）。</summary>
	public int ArmorGrantTally { get; private set; }

	private Unit? _owner;

	public override void _Ready()
	{
		_owner = GetParent<Unit>();
		// 加甲只记录已发生的击杀，不生成攻击；合法链末层和第五杀也必须计数。
		GameplayEventBus.Raised += OnKill;
	}

	public override void _ExitTree()
	{
		GameplayEventBus.Raised -= OnKill;
		_owner = null;
	}

	private void OnKill(GameplayEvent fact) { if (Matches(fact)) Execute(fact); }

	/// <summary>只认**自己打出来的击杀**（与「连锁闪电」「灵魂收割」同一条判据）。</summary>
	public override bool Matches(GameplayEvent gameplayEvent)
		=> gameplayEvent.Kind == GameplayEventKind.Kill
			&& _owner != null
			&& IsInstanceValid(_owner)
			&& gameplayEvent.Source == _owner;

	public override void Execute(GameplayEvent gameplayEvent)
	{
		if (_owner == null || !IsInstanceValid(_owner))
		{
			return;
		}

		KillTally++;
		int every = Mathf.Max(0, KillsPerTrigger);
		if (every > 0 && KillTally % every != 0)
		{
			return;
		}

		_owner.GrantArmor(ArmorPerTrigger);
		ArmorGrantTally++;
		GameLog.Write(
			"Rune",
			$"符文·击杀加甲：第 {KillTally} 杀 → 护甲 +{ArmorPerTrigger:0.##}（现在 {_owner.Armor:0.##}，累计加甲 {ArmorGrantTally} 次）");
	}

	/// <summary>一行可读状态（自测/日志用）。</summary>
	public string DebugState()
		=> $"击杀加甲规则：每 {KillsPerTrigger} 杀 +{ArmorPerTrigger:0.##} 甲｜已击杀 {KillTally} · 已加甲 {ArmorGrantTally} 次";
}

/// <summary>
/// **死亡避免**（符文「生死」）：致命伤那一下把它无效化，并按比例恢复生命。
///
/// <para>钩子在 <see cref="Unit.ReceiveHit"/> 的"死不死"判定处（<see cref="IDeathAvoid"/> 的注释里写了
/// 为什么不能事后靠 <c>Damaged</c> 事件复活）。次数与恢复比例全部来自符文行。</para>
/// </summary>
public partial class RuneDeathAvoidRule : Node, IDeathAvoid
{
	/// <summary>还能无效化几次致命伤。</summary>
	public int Charges { get; set; } = 1;

	/// <summary>触发时恢复到最大生命值的这个比例（0.3 = 30%）。</summary>
	public float HealFraction { get; set; } = 0.3f;

	/// <summary>累计触发次数（取证用）。</summary>
	public int AvoidTally { get; private set; }

	/// <summary>最近一次触发时恢复到多少生命（取证用）。</summary>
	public float LastRestoredHealth { get; private set; }

	private Unit? _owner;

	public override void _Ready()
	{
		_owner = GetParent<Unit>();
		_owner?.AddDeathAvoid(this);
	}

	public override void _ExitTree()
	{
		_owner?.RemoveDeathAvoid(this);
		_owner = null;
	}

	public bool TryAvoidDeath(Unit unit)
	{
		if (Charges <= 0)
		{
			return false;
		}

		Charges--;
		AvoidTally++;
		float restored = Mathf.Max(1f, unit.MaxHealth * HealFraction);
		unit.RestoreHealthForDeathAvoid(restored);
		LastRestoredHealth = unit.Health;
		GameLog.Write(
			"Rune",
			$"符文·死亡避免：无效化这次致命伤 → 生命恢复到 {restored:0.##}（最大 {unit.MaxHealth:0.##} 的 {HealFraction:0.##}），剩余次数 {Charges}");
		return true;
	}

	/// <summary>一行可读状态（自测/日志用）。</summary>
	public string DebugState()
		=> $"死亡避免规则：剩余 {Charges} 次 · 恢复 {HealFraction:0.##} 生命｜已触发 {AvoidTally} 次"
			+ $"（最近一次恢复到 {LastRestoredHealth:0.##}）";
}

/// <summary>
/// **按距离加伤**（符文「狙神」）：离得越远伤害越高，近距离只有 50%。
///
/// <para>实现完全落在 J8 建好的攻击者侧查询口 <see cref="IOutgoingDamageScale"/> 上
/// （与「心眼」「发散」同一套机制）—— 命中在 <see cref="AttackInstance.RollHit"/> 里结算，
/// **没有新增任何战斗钩子**。</para>
///
/// <para>距离取的是"命中那一刻双方世界坐标的直线距离"，在 <see cref="NearRange"/> 与
/// <see cref="FarRange"/> 之间线性插值，两端外各自夹住（文档只给了近端 50%，
/// 远端与两个距离阈值是预设值，见行 `Note`）。</para>
/// </summary>
public partial class RuneDistanceDamageRule : Node, IOutgoingDamageScale
{
	/// <summary>近距离判定的距离（米）。</summary>
	public float NearRange { get; set; } = 3f;

	/// <summary>近距离倍率（0.5 = 只剩 50%）。</summary>
	public float NearScale { get; set; } = 0.5f;

	/// <summary>远端判定的距离（米）。</summary>
	public float FarRange { get; set; } = 15f;

	/// <summary>远端倍率（1.5 = +50%）。</summary>
	public float FarScale { get; set; } = 1.5f;

	/// <summary>最近一次结算读到的距离（取证用）。</summary>
	public float LastDistance { get; private set; } = -1f;

	/// <summary>最近一次给出的倍率（取证用）。</summary>
	public float LastScale { get; private set; } = 1f;

	private Unit? _owner;

	public override void _Ready()
	{
		_owner = GetParent<Unit>();
		_owner?.AddOutgoingDamageScale(this);
	}

	public override void _ExitTree()
	{
		_owner?.RemoveOutgoingDamageScale(this);
		_owner = null;
	}

	public float ScaleAgainst(Unit target, AttackInstance attack)
	{
		if (_owner == null || !IsInstanceValid(_owner) || target == null || !IsInstanceValid(target))
		{
			return 1f;
		}

		float distance = _owner.GlobalPosition.DistanceTo(target.GlobalPosition);
		LastDistance = distance;
		float span = FarRange - NearRange;
		float t = span > 0.0001f ? Mathf.Clamp((distance - NearRange) / span, 0f, 1f) : (distance >= FarRange ? 1f : 0f);
		LastScale = Mathf.Lerp(NearScale, FarScale, t);
		return LastScale;
	}

	/// <summary>一行可读状态（自测/日志用）。</summary>
	public string DebugState()
		=> $"按距离加伤规则：{NearRange:0.##}m ×{NearScale:0.##} → {FarRange:0.##}m ×{FarScale:0.##}"
			+ $"｜最近一次：距离 {LastDistance:0.##}m → ×{LastScale:0.####}";
}

/// <summary>
/// **按最大生命值加伤**（符文「巨人」= 每次攻击额外造成玩家最大生命值 10% 的伤害）。
///
/// <para>走 J11 新加的扁平加伤口 <see cref="IOutgoingDamageBonus"/>（为什么不能复用倍率口，
/// 见那个接口的注释）。读的是**玩家当前**的 <c>MaxHealth</c>，所以与「雷之印」的 30 上限叠加时
/// 按夹过之后的值算（口径一致：都是"最大生命值"）。</para>
/// </summary>
public partial class RuneMaxHealthDamageRule : Node, IOutgoingDamageBonus
{
	/// <summary>额外伤害 = 最大生命值 × 这个比例。</summary>
	public float Ratio { get; set; } = 0.1f;

	/// <summary>最近一次给出的额外伤害（取证用）。</summary>
	public float LastBonus { get; private set; }

	private Unit? _owner;

	public override void _Ready()
	{
		_owner = GetParent<Unit>();
		_owner?.AddOutgoingDamageBonus(this);
	}

	public override void _ExitTree()
	{
		_owner?.RemoveOutgoingDamageBonus(this);
		_owner = null;
	}

	public float BonusAgainst(Unit target, AttackInstance attack)
	{
		if (_owner == null || !IsInstanceValid(_owner))
		{
			return 0f;
		}

		LastBonus = Mathf.Max(0f, _owner.MaxHealth * Ratio);
		return LastBonus;
	}

	/// <summary>一行可读状态（自测/日志用）。</summary>
	public string DebugState()
		=> $"最大生命值加伤规则：×{Ratio:0.###}｜最近一次额外伤害 {LastBonus:0.##}";
}

/// <summary>
/// **小精灵**（符文「小精灵」）：一个绕玩家转的实体，每 N 秒攻击半径内**一个**敌人。
///
/// <para><b>为什么不是 Area3D / 不是独立单位</b>：与「灵魂收割」的 <c>SoulPickup</c> 同一条纪律 ——
/// 不进物理世界、不进 <c>enemy_units</c>/<c>player_units</c>/<c>summon_units</c> 任何分组，
/// 所以清场、目标选择、单位计数都碰不到它。索敌是**每帧距离轮询**，命中走
/// <see cref="AttackInstance.RollHit"/> + <see cref="Unit.ReceiveHit"/> 的统一结算 ——
/// 于是它天然吃玩家的伤害/暴击/「狙神」/「巨人」等全部修正，不自成一套。</para>
///
/// <para>⚠️ 没有模型/动画资源：本批用一颗自发光小球当**占位表现**（行 `Note` 已登记）。</para>
/// </summary>
public partial class PixieCompanion : Node3D
{
	/// <summary>攻击间隔（秒）。</summary>
	public float IntervalSeconds { get; set; } = 5f;

	/// <summary>索敌半径（米）。</summary>
	public float SearchRadius { get; set; } = 8f;

	/// <summary>每次攻击的伤害。</summary>
	public float Damage { get; set; } = 20f;

	/// <summary>绕玩家的半径（米）。</summary>
	public float OrbitRadius { get; set; } = 1.6f;

	/// <summary>绕行角速度（度/秒）。</summary>
	public float OrbitDegreesPerSecond { get; set; } = 90f;

	/// <summary>离地高度（米）。</summary>
	public float HoverHeight { get; set; } = 1.2f;

	/// <summary>攻击代号（写进攻击标签与日志，便于取证区分）。</summary>
	public string AbilityId { get; set; } = "rune_pixie";

	/// <summary>累计攻击次数（取证用）。</summary>
	public int AttackTally { get; private set; }

	/// <summary>最近一次的目标名（取证用）。</summary>
	public string LastTargetName { get; private set; } = "（无）";

	/// <summary>最近一次打出的伤害（取证用）。</summary>
	public float LastDamage { get; private set; }

	private Unit? _owner;
	private float _angle;
	private float _sinceAttack;

	public override void _Ready()
	{
		_owner = GetParent<Unit>();
		// 占位表现：一颗自发光小球（0 碰撞体、0 物理体、0 光，纯 MeshInstance）。
		var mesh = new MeshInstance3D
		{
			Name = "Placeholder",
			Mesh = new SphereMesh { Radius = 0.18f, Height = 0.36f },
		};
		var material = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.55f, 0.95f, 1f),
			EmissionEnabled = true,
			Emission = new Color(0.55f, 0.95f, 1f),
			EmissionEnergyMultiplier = 2.5f,
		};
		mesh.MaterialOverride = material;
		AddChild(mesh);
		_sinceAttack = 0f;
		GameLog.Write(
			"Rune",
			$"符文·小精灵：绕行半径 {OrbitRadius:0.##}m / 角速度 {OrbitDegreesPerSecond:0.#}°/s / 每 {IntervalSeconds:0.##}s 攻击 {SearchRadius:0.##}m 内一个敌人（伤害 {Damage:0.##}）");
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (_owner == null || !IsInstanceValid(_owner))
		{
			return;
		}

		_angle = Mathf.Wrap(_angle + Mathf.DegToRad(OrbitDegreesPerSecond) * dt, 0f, Mathf.Tau);
		Vector3 offset = new(Mathf.Cos(_angle) * OrbitRadius, HoverHeight, Mathf.Sin(_angle) * OrbitRadius);
		GlobalPosition = _owner.GlobalPosition + offset;

		_sinceAttack += dt;
		if (IntervalSeconds <= 0f || _sinceAttack < IntervalSeconds)
		{
			return;
		}

		_sinceAttack = 0f;
		Strike();
	}

	/// <summary>挑半径内**最近的一个**有效敌人打一次（"周围的某个敌人"）。</summary>
	private void Strike()
	{
		if (_owner == null || !IsInstanceValid(_owner) || GetTree() is not { } tree)
		{
			return;
		}

		Unit? best = null;
		float bestDistance = SearchRadius * SearchRadius;
		foreach (Node node in tree.GetNodesInGroup("enemy_units"))
		{
			if (node is not Unit candidate || !candidate.IsValidTarget)
			{
				continue;
			}

			float distance = (_owner.GlobalPosition - candidate.GlobalPosition).LengthSquared();
			if (distance > bestDistance)
			{
				continue;
			}

			best = candidate;
			bestDistance = distance;
		}

		if (best == null)
		{
			return;
		}

		var attack = new AttackInstance
		{
			Attacker = _owner,
			Ability = AbilityId,
			AbilityKind = AbilityId,
			BaseDamage = Damage,
			Policy = HitPolicy.OncePerTarget,
			Tags = new[] { "Pixie" },
		};
		Vector3 from = GlobalPosition;
		Vector3 direction = (best.GlobalPosition - from).Normalized();
		DamageResult result = best.ReceiveHit(attack.RollHit(best, direction, best.GlobalPosition));
		AttackTally++;
		LastTargetName = best.Name;
		LastDamage = result.FinalDamage;
		GameLog.Write("Rune", $"符文·小精灵出手：第 {AttackTally} 次 → {best.Name} 伤害 {result.FinalDamage:0.##}");
	}

	/// <summary>一行可读状态（自测/日志用）。</summary>
	public string DebugState()
		=> $"小精灵：每 {IntervalSeconds:0.##}s / 半径 {SearchRadius:0.##}m / 伤害 {Damage:0.##} / 绕行 {OrbitRadius:0.##}m"
			+ $"｜已出手 {AttackTally} 次（最近目标 {LastTargetName} 伤害 {LastDamage:0.##}）";
}

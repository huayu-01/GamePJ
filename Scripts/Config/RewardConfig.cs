using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 增幅或符文的一行：身份与资格、完整文本键，以及可组合效果资源。
/// 生产内容仅执行Effects；旧ApplyHandler和顶层专用参数通过兼容层读取，编辑器隐藏且不重复保存。
/// </summary>
[Tool]
[GlobalClass]
public partial class RewardConfig : ConfigRow
{
	[Export] public RewardConfig? Replaces { get; set; }
	[Export] public bool ShowUpgradeLevel { get; set; }
	/// <summary>空为通用；非空仅在指定模式可获取，使用稳定Id而非显示名称。</summary>
	[Export] public string[] RequiredModeIds { get; set; } = System.Array.Empty<string>();
	[Export] public string IconName { get; set; } = string.Empty;
	[ExportGroup("身份与出率（强化 / 进化 / 异化；能叠几层；抽中概率）")]
	/// <summary>强化 / 进化 / 异化（对应 <c>Rewards.RewardKind</c>）。</summary>
	[Export] public Rewards.RewardKind RewardKind { get; set; } = Rewards.RewardKind.Enhancement;

	/// <summary>最多叠几层（0 = 不限；1 = 只能拿一次）。</summary>
	[Export] public int MaxStacks { get; set; } = 1;

	/// <summary>抽中概率权重（与 <c>BossPerk</c> 共用 <see cref="WeightedPick"/>；0表示不抽取）。</summary>
	[Export] public float Weight { get; set; } = 1f;

	[ExportGroup("效果")]
	/// <summary>组合效果列表；非空时作为唯一效果入口，旧ApplyHandler保留兼容。</summary>
	[Export] public RewardEffectDefinition[] Effects { get; set; } = System.Array.Empty<RewardEffectDefinition>();
	/// <summary>历史处理器Id，仅由RewardEffectCompatibility翻译；新内容使用Effects。</summary>
	[Export] public string ApplyHandler { get; set; } = "StatSet";
	/// <summary>只接受该技能造成的事件；资源引用在编译时转为运行期技能Id。</summary>
	[Export] public AbilityConfig? RequiredEventAbility { get; set; }
	/// <summary>事件必须来自攻击快照中的末段连招；默认不限定（普渡）。</summary>
	[Export] public bool RequireFinalComboStep { get; set; }
	/// <summary>
	/// **只对这个技能的伤害加成**（J8：「发散」只提高「波波」而「心眼」提高玩家的各种伤害）。
	/// 留空 = 不按技能过滤。与 <see cref="RequiredEventAbility"/> 分工：
	/// 那个管「哪一击才加标」，这个管「加伤只算哪些技能」—— 两条都是**强类型资源引用**，不是字符串反射。
	/// </summary>
	[Export] public AbilityConfig? BonusAgainstAbility { get; set; }
	/// <summary>点按连射进化；参数及目标技能均为强类型资源。</summary>
	private VolleyRampTuning? _volleyRamp;
	[Export] public VolleyRampTuning? VolleyRamp { get => _volleyRamp ?? FindTuning<VolleyRampTuning>(); set => _volleyRamp = value; }

	/// <summary>蓄力阈值弹进化（J9「龙之箭」）：蓄力上限 / 阈值 / 尺寸 / 穿透数及目标技能均为强类型资源。</summary>
	private ChargedShotTuning? _chargedShot;
	[Export] public ChargedShotTuning? ChargedShot { get => _chargedShot ?? FindTuning<ChargedShotTuning>(); set => _chargedShot = value; }
	/// <summary>只改变指定技能的蓄力、单箭与分配追踪，不写全局数值。</summary>
	private ChargeBehaviorTuning? _chargeBehavior;
	[Export] public ChargeBehaviorTuning? ChargeBehavior { get => _chargeBehavior ?? FindTuning<ChargeBehaviorTuning>(); set => _chargeBehavior = value; }

	/// <summary>近战每段真实 ATK 额外出弹（金光术）。</summary>
	private MeleeProjectileTuning? _meleeProjectile;
	[Export] public MeleeProjectileTuning? MeleeProjectile { get => _meleeProjectile ?? FindTuning<MeleeProjectileTuning>(); set => _meleeProjectile = value; }
	/// <summary>只调整目标技能自身（灵能汲取／神降术等）。</summary>
	private SkillEnhancementTuning? _skillEnhancement;
	[Export] public SkillEnhancementTuning? SkillEnhancement { get => _skillEnhancement ?? FindTuning<SkillEnhancementTuning>(); set => _skillEnhancement = value; }
	/// <summary>飞出武器的周期索敌（喜爱）。</summary>
	private WeaponSeekTuning? _weaponSeek;
	[Export] public WeaponSeekTuning? WeaponSeek { get => _weaponSeek ?? FindTuning<WeaponSeekTuning>(); set => _weaponSeek = value; }
	/// <summary>长枪落地召回与蓄力缩时（气贯长虹）。</summary>
	private WeaponRecallTuning? _weaponRecall;
	[Export] public WeaponRecallTuning? WeaponRecall { get => _weaponRecall ?? FindTuning<WeaponRecallTuning>(); set => _weaponRecall = value; }
	/// <summary>特化机制；数值共读HandlerParams，None不建立规则。</summary>
	[Export] public SpecializationKind Specialization { get; set; }
	/// <summary>已迁移特化的参数与运行组件；留空兼容旧枚举及HandlerParams。</summary>
	private SpecializationBehaviorDefinition? _specializationBehavior;
	[Export] public SpecializationBehaviorDefinition? SpecializationBehavior
	{
		get => _specializationBehavior ?? System.Linq.Enumerable.FirstOrDefault(System.Linq.Enumerable.OfType<SpecializationRewardEffect>(Effects))?.Definition;
		set => _specializationBehavior = value;
	}
	/// <summary>特化绑定的技能资源（雷池的角色技能2、无情及下落攻击）。</summary>
	[Export] public AbilityConfig[] SpecializationTargets { get; set; } = System.Array.Empty<AbilityConfig>();
	/// <summary>特化发射的投射物预制体。</summary>
	[Export] public PackedScene? SpecializationProjectile { get; set; }
	/// <summary>战斗之心转换的奖励行，资源引用覆盖普通／+／++且不吞其他减伤。</summary>
	[Export] public RewardConfig[] ConvertedRewards { get; set; } = System.Array.Empty<RewardConfig>();

	/// <summary>
	/// 处理器参数：<c>StatSet</c> 用 <c>Modifiers</c>；其余处理器用这里的键值对
	/// （例：<c>Damage</c> / <c>Range</c> / <c>Jumps</c> / <c>Ratio</c> / <c>Duration</c> / <c>MaxSummons</c>）。
	/// </summary>
	[Export] public ParamEntry[] HandlerParams { get; set; } = System.Array.Empty<ParamEntry>();

	[ExportGroup("池过滤（三级回退：① 专属池 → ② 武器标签 → ③ 通用）")]
	/// <summary>要求武器具备的标签；空 = 不限武器（**第二层**：现有 4 条进化走这层）。</summary>
	[Export] public string[] RequiredWeaponTags { get; set; } = System.Array.Empty<string>();

	/// <summary>
	/// **限定（角色层）**：只有**当前角色行 Id**（`chr_hunter` / `chr_weapon_master` …）在这里面时才进池。
	/// 空 = 不限角色（**默认**，所以既有 19 行逐位不变）。
	///
	/// <para><b>这一列回答"&lt;A限定&gt; / &lt;A1限定&gt; 到底怎么写"</b>（用户 2026-10-12 澄清）：
	/// 池 = 哈迪斯的祝福清单，**按「角色 + 武器」过滤候选**，不另建池子。所以：</para>
	/// <list type="bullet">
	/// <item><c>&lt;A限定&gt;</c>（限定角色 A、**不限其武器**）= 本列填 <c>chr_hunter</c>，<b>不填</b> <see cref="Pool"/>；</item>
	/// <item><c>&lt;A1限定&gt;</c>（角色 A **+** 武器 1）= 本列填 <c>chr_hunter</c> <b>并且</b> <see cref="Pool"/> 指向该武器行
	///   <c>Pool</c> 指向的那个池（武器是**资源引用**，改名/删文件会被 Godot 置 null 从而能检测，见 <see cref="UpgradePoolConfig"/>）。</item>
	/// </list>
	///
	/// <para>⚠️ **不知道角色是谁就不给**（`RewardDefinition.AllowsCharacter` 对空 Id 返回 false）：
	/// 这正是用户要的语义 ——「角色 A 拿了角色 B 限定的增幅，不会产生任何效果，这并不正确」，
	/// 正确行为是**根本不进候选列表**。</para>
	/// </summary>
	[Export] public string[] RequiredCharacterIds { get; set; } = System.Array.Empty<string>();

	/// <summary>
	/// **前置**：必须**已经拥有**这些增幅 Id 里的**每一个**，本项才进池（不满足 = 根本不出现，不是"拿了没效果"）。
	/// 空 = 没有前置（**默认**，既有 19 行逐位不变）。
	///
	/// <para>文档里 <c>弹射+</c>（前置"弹射"）/ <c>穿透++</c>（前置"穿透+"）/ <c>烈阳+</c> / <c>冰晶+</c> /
	/// <c>增生+</c> / <c>大刀阔斧+</c> / <c>剑突+</c> / <c>速攻+</c> 都靠这一列。</para>
	/// </summary>
	[Export] public string[] Requires { get; set; } = System.Array.Empty<string>();

	/// <summary>
	/// 本选项**专属的升级池**（`Config/Tables/UpgradePools/pool_*.tres`）—— **第一层**。
	/// 填了 = 只有 <c>Weapon.PoolId</c> 等于它的武器能拿到这一条，**此时不再看 <see cref="RequiredWeaponTags"/>**；
	/// 留空 = 落到第二层（标签匹配）与第三层（通用池：现有 10 条强化 + 5 条异化）。
	///
	/// **选项行是"池成员"的唯一真源**：池行不反过来列选项（一个选项占一行 + 一个引用列；
	/// "一栏里塞多个选项"的数组形态恰恰是编辑器拿不到的，见 docs/archive/design-notes/config-tooling-yard.md §4.3）。
	/// </summary>
	[Export] public UpgradePoolConfig? Pool { get; set; }

	// ⚠️ P6a **删掉了** `RequiresSummoner`（"只有召唤控制角色才能拿到"）：它唯一的用户是
	// `mut_summon`（亡者随从），而那个角色与那条行都在 P6a 清掉了。角色限定现在统一走
	// `RequiredCharacterIds`（资源不可用时的字符串名单，见下），不需要第二个只认"召唤师"的布尔。

	/// <summary>互斥的增幅项 Id（双向判定）。</summary>
	[Export] public string[] Excludes { get; set; } = System.Array.Empty<string>();

	/// <summary>
	/// **单向**互斥：已经拥有这些 Id 里的任意一个时，本项不再进池
	/// （与 <see cref="Excludes"/> 的双向判定互补；<c>RewardPool.Roll</c> 读它）。
	/// 阶段 4 之前 <c>RewardDefinition.ConflictsWith</c> 在编译时**没有任何填入口**，所以永远是空数组；
	/// 现在表里能填了（默认空 = 行为不变）。
	/// </summary>
	[Export] public string[] ConflictsWith { get; set; } = System.Array.Empty<string>();

	/// <summary>
	/// 属性修正（<c>StatSet</c> 处理器用）。取代 `RewardCatalog` 里 <c>Mod(c, "DamageMultiplier", Multiply, 1.15f)</c> 这类闭包。
	/// </summary>
	private StatOverride[] _modifiers = System.Array.Empty<StatOverride>();
	[Export] public StatOverride[] Modifiers
	{
		get => _modifiers.Length > 0 || Effects.Length == 0 ? _modifiers : System.Linq.Enumerable.ToArray(System.Linq.Enumerable.SelectMany(System.Linq.Enumerable.OfType<StatRewardEffect>(Effects), e => e.Modifiers));
		set => _modifiers = value;
	}
	private T? FindTuning<T>() where T : Resource => System.Linq.Enumerable.FirstOrDefault(System.Linq.Enumerable.OfType<T>(System.Linq.Enumerable.Select(System.Linq.Enumerable.OfType<AbilityTuningRewardEffect>(Effects), e => e.Tuning)));
	public override void _ValidateProperty(Godot.Collections.Dictionary property)
	{
		base._ValidateProperty(property);
		if (Effects.Length == 0) return;
		string name = property["name"].AsString();
		if (name is "VolleyRamp" or "ChargedShot" or "ChargeBehavior" or "MeleeProjectile" or "SkillEnhancement" or "WeaponSeek" or "WeaponRecall" or "Modifiers" or "SpecializationBehavior" or "SpecializationTargets" or "SpecializationProjectile" or "ConvertedRewards" or "ApplyHandler" or "HandlerParams" or "RequiredEventAbility" or "BonusAgainstAbility" or "RequireFinalComboStep")
			property["usage"] = (long)((PropertyUsageFlags)property["usage"].AsInt64() & ~(PropertyUsageFlags.Storage | PropertyUsageFlags.Editor));
	}

	/// <summary>该处理器是否已注册（读表时用来抓拼错的 handler id）。</summary>
	public bool HasKnownHandler => Effects.Length > 0 || Rewards.RewardEffectRegistry.IsRegistered(ApplyHandler);
	/// <summary>统一说明与校验的数据视图，不要求制作人员把数值复制到两处。</summary>
	public System.Collections.Generic.IEnumerable<StatOverride> DeclaredModifiers
	{
		get
		{
			if (Effects.Length == 0) { foreach (var modifier in Modifiers) yield return modifier; yield break; }
			foreach (var effect in Effects) if (effect is StatRewardEffect stats) foreach (var modifier in stats.Modifiers) yield return modifier;
		}
	}
}

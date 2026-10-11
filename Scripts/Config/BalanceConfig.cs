using Godot;

namespace ActRoguelike.Config;

/// <summary>可选武器（开局配置与武器池节点用）。数值都在 BalanceConfig 里，不写死进预制体。</summary>
public enum WeaponPresetKind
{
	Pistol,
	Sniper,
	Rifle,
}

/// <summary>可选角色（策划文档 §7）。</summary>
public enum PlayerCharacterKind
{
	/// <summary>角色 1：远程枪手（三把枪）。</summary>
	Gunner,

	/// <summary>角色 2：近战与重型防御（左键近战重击）。</summary>
	Heavy,

	/// <summary>角色 3：召唤与控制（G 键主动召唤无人机）。</summary>
	Summoner,
}

/// <summary>
/// 全局平衡配置（数值唯一入口 <see cref="Balance"/> 的持有者 + **分表索引**）。
///
/// **2026-10 配置表阶段 2 之后，这里只保留两类字段：**
/// ① **跨表生效的全局开关**（敌人/Boss 全局倍率、节奏倍率、Boss 压力曲线、表现与 HUD、节点/关卡约束）——
///    它们是"难度总开关"，不属于任何单张表；
/// ② **各分表的索引数组**（<c>Weapons</c> / <c>Characters</c> / <c>Enemies</c> / <c>Effects</c> /
///    <c>Nodes</c> / <c>Upgrades</c> / <c>MetaUpgrades</c> / <c>Stages</c>）。
///
/// 已经搬进表的数值（玩家/角色/武器/商店价目等）**不再在这里留一份** —— 留两份必然漂移
/// （上一轮的关卡表就是这么坏的，见 docs/config-schema.md §1.4-①）。
/// 真正的数值在 `Config/Tables/&lt;分组&gt;/*.tres`，一行一个文件。
///
/// 代码里不要写死数值 —— 需要新的可调项就在对应的表行类上加一个 `[Export]`，然后在套用它的地方读表。
/// </summary>
[GlobalClass]
public partial class BalanceConfig : Resource
{
	[ExportGroup("敌人 · 全局倍率")]
	/// <summary>
	/// 所有普通敌人的生命倍率（回退值）。⚠️ **必须与 `Config/Tables/Enemies/enemy_global.tres` 的值一致** ——
	/// 优先来源是那一行；这里只是"表读不到"时的兜底。两个值不一样就会出现**没有日志的静默换挡**
	/// （旧代码这里是 1f / 0.8f，表一丢敌人伤害直接 ×2.67，见 config-tables-inventory.md §5-④）。
	/// </summary>
	[Export] public float EnemyHealthMultiplier { get; set; } = 0.75f;

	/// <summary>所有普通敌人的伤害倍率（难度总开关；回退值，语义见上一条）。</summary>
	[Export] public float EnemyDamageMultiplier { get; set; } = 0.3f;

	[Export] public float EnemyMoveSpeedMultiplier { get; set; } = 1f;
	[Export] public float EliteChanceMultiplier { get; set; } = 1f;

	[ExportGroup("节奏 · Stage")]
	[Export] public float NodeBudgetMultiplier { get; set; } = 1f;
	[Export] public float SpawnIntervalMultiplier { get; set; } = 1f;

	[ExportGroup("Boss")]
	[Export] public float BossHealthMultiplier { get; set; } = 1f;
	[Export] public float BossDamageMultiplier { get; set; } = 0.9f;
	[Export] public float BossPressureInterval { get; set; } = 12f;
	[Export] public float BossPressureDamagePerLevel { get; set; } = 0.08f;
	[Export] public float BossPressureAttackSpeedPerLevel { get; set; } = 0.06f;

	[ExportGroup("关卡序列")]
	/// <summary>本局的 Stage 顺序；留空时使用代码内置的三关默认序列。</summary>
	[Export] public StageDefinition[] Stages { get; set; } = System.Array.Empty<StageDefinition>();

	// ─────────────────────────────────────────────────────────────────────────
	// 分表索引（配置表阶段 2，设计文档 config-schema.md §2.3 / §3）
	//
	// 每一张表在磁盘上是 `Config/Tables/<分组>/` 下一堆"一行一个文件"的 `.tres`，
	// 这里只放**索引数组**：元素指向那些行文件的独立 `ExtResource`。
	// 为什么是显式数组而不是扫目录：① 只被目录扫描引用的资源在导出时可能掉出打包范围；
	// ② 扫目录会让"Balance 是唯一数值入口"这条约定失效（见 docs/todo-handoff.md）。
	//
	// ⚠️ 操作纪律（上一轮实测的坑，见 config-schema.md §5 阶段 1）：
	// **不要单独删/改被引用到的行文件** —— 只要 Balance.tres 引用的任何一个行文件缺失/改名，
	// Godot 会**整份 Balance.tres 加载失败**（不是只丢那一行），于是所有数值静默回到代码默认值。
	// 要下线一行，就把这里的数组项与顶部对应的 `[ext_resource]` **一起**删掉。
	//
	// 回退语义：数组为空 / 整份 Balance.tres 没加载 → 各系统读不到行 → 使用**自身** `[Export]` 默认值，
	// 并且每个系统都会写一条带"来源：表 / 代码默认值"的日志留痕。
	// ─────────────────────────────────────────────────────────────────────────

	[ExportGroup("表索引 · 武器")]
	/// <summary>武器表（`Config/Tables/Weapons/wpn_*.tres`）。空 = 用 <c>Weapon</c> 组件自身的默认值。</summary>
	[Export] public WeaponConfig[] Weapons { get; set; } = System.Array.Empty<WeaponConfig>();

	[ExportGroup("表索引 · 角色")]
	/// <summary>角色表（`Config/Tables/Characters/chr_*.tres`）。空 = 用玩家场景里的原始数值。</summary>
	[Export] public CharacterConfig[] Characters { get; set; } = System.Array.Empty<CharacterConfig>();

	[ExportGroup("表索引 · 敌人")]
	/// <summary>敌人表（`Config/Tables/Enemies/enemy_*.tres`）：1 行全局倍率 + 每个敌种一行。</summary>
	[Export] public EnemyConfig[] Enemies { get; set; } = System.Array.Empty<EnemyConfig>();

	[ExportGroup("表索引 · 首领")]
	/// <summary>首领表（`Config/Tables/Bosses/boss_*.tres`）。空 = 用 Boss.tscn 场景里的值。</summary>
	[Export] public BossConfig[] Bosses { get; set; } = System.Array.Empty<BossConfig>();

	[ExportGroup("表索引 · 召唤物")]
	/// <summary>召唤物表（`Config/Tables/Summons/summon_*.tres`）。空 = 用 Drone.tscn 场景里的值。</summary>
	[Export] public SummonConfig[] Summons { get; set; } = System.Array.Empty<SummonConfig>();

	[ExportGroup("表索引 · 技能")]
	/// <summary>
	/// 技能表（`Config/Tables/Skills/skl_*.tres`）。单位表用 `Skills` 以资源引用指过来；
	/// 里面 <c>ApplyHandler</c> 非空的那几行是**首领行为词条**（Boss Build）。
	/// </summary>
	[Export] public AbilityConfig[] Skills { get; set; } = System.Array.Empty<AbilityConfig>();

	[ExportGroup("表索引 · Pressure 数值强化池")]
	/// <summary>首领**数值型**强化池（`Config/Tables/Pressure/pmod_*.tres`）。行为型在技能表里，两者不得合并（§7.1）。</summary>
	[Export] public PressureModConfig[] PressureMods { get; set; } = System.Array.Empty<PressureModConfig>();

	[ExportGroup("表索引 · 效果（击退 / 燃烧 / 减速 / 眩晕 / 爆炸 / 吸血 / 召唤）")]
	/// <summary>效果参数表（`Config/Tables/Effects/eff_*.tres`）。空 = 用 <c>StatusPresets.Fallback</c> 的代码内置值。</summary>
	[Export] public EffectConfig[] Effects { get; set; } = System.Array.Empty<EffectConfig>();

	[ExportGroup("表索引 · 节点")]
	/// <summary>节点表（`Config/Tables/Nodes/node_*.tres`）。空 = 用 <c>RewardService</c> / <c>NodePlacement</c> 里的原 switch。</summary>
	[Export] public NodeConfig[] Nodes { get; set; } = System.Array.Empty<NodeConfig>();

	[ExportGroup("表索引 · 增幅项")]
	/// <summary>增幅项表（`Config/Tables/Upgrades/upg_*.tres`）。空 = 用 <c>RewardCatalog.Fallback</c>。</summary>
	[Export] public RewardConfig[] Upgrades { get; set; } = System.Array.Empty<RewardConfig>();

	[ExportGroup("表索引 · 局外成长")]
	/// <summary>局外成长表（`Config/Tables/Meta/meta_*.tres`）。空 = 用 <c>MetaProgress</c> 里的代码内置系数。</summary>
	[Export] public MetaUpgradeConfig[] MetaUpgrades { get; set; } = System.Array.Empty<MetaUpgradeConfig>();

	[ExportGroup("成长")]
	[Export] public int RewardChoiceCount { get; set; } = 3;

	[ExportGroup("表现 · 伤害跳字")]
	[Export] public bool ShowDamageNumbers { get; set; } = true;
	[Export] public float DamageNumberLifetime { get; set; } = 0.8f;
	[Export] public float DamageNumberRise { get; set; } = 70f;
	[Export] public int DamageNumberFontSize { get; set; } = 24;

	[ExportGroup("表现 · 血条")]
	[Export] public bool ShowEnemyHealthBars { get; set; } = true;
	[Export] public float EnemyHealthBarWidth { get; set; } = 64f;
	[Export] public float EnemyHealthBarHeight { get; set; } = 7f;
	/// <summary>血条相对单位原点的垂直偏移（现在沿 -Up 使用，即放在脚下）。</summary>
	[Export] public float EnemyHealthBarTopOffset { get; set; } = 0.25f;

	/// <summary>「白板首领副本」的血量倍率（相对普通首领，关卡核心文档 L231/L315）。</summary>
	[Export(PropertyHint.Range, "0.05,1,0.05")] public float EliteBossHealthScale { get; set; } = 0.35f;

	/// <summary>节点之间的最小间距（关卡核心文档 L106）。</summary>
	[Export] public float MinNodeSpacing { get; set; } = 8f;

	/// <summary>完成一次高价值节点挑战增加的 Pressure 点数（关卡核心文档 L352）。</summary>
	[Export] public int PressurePerHighValueNode { get; set; } = 150;

	/// <summary>进化奖励对 Pressure 增加值的额外比例（关卡核心文档 L356）。</summary>
	[Export] public float PressureEvolutionBonus { get; set; } = 1.25f;

	/// <summary>异化奖励对 Pressure 增加值的额外比例（关卡核心文档 L356）。</summary>
	[Export] public float PressureMutationBonus { get; set; } = 1.5f;

	/// <summary>每多少点 Pressure 转换成 1 点首领压力；余数清零（关卡核心文档 L370）。</summary>
	[Export] public int PressurePerPoint { get; set; } = 100;

	/// <summary>战斗型回复节点先免费恢复的生命比例（关卡核心文档 L180-183）。</summary>
	[Export(PropertyHint.Range, "0,1,0.05")] public float NodeHealRatio { get; set; } = 0.3f;

	/// <summary>免费挑战额度：初始值（关卡核心文档 L409）。</summary>
	[Export] public int FreeChallengeBaseQuota { get; set; } = 1;

	/// <summary>免费挑战额度上限（关卡核心文档 L409-416）。</summary>
	[Export] public int FreeChallengeMaxQuota { get; set; } = 2;

	/// <summary>地图下方的区域杀高度：玩家 Y 低于它直接被击杀（用户需求）。</summary>
	[Export] public float KillPlaneY { get; set; } = -8f;

	/// <summary>与首领的距离小于它时，屏幕上方显示完整血条；否则只显示首领头像。</summary>
	[Export] public float BossBarFullRange { get; set; } = 22f;
	[Export] public float BossHealthBarScale { get; set; } = 4f;
}

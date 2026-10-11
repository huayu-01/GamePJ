using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 增幅项表的一行 —— 一个 `.tres` 文件就是一行（`Config/Tables/Upgrades/upg_*.tres`）。
/// 索引用 <see cref="BalanceConfig.Upgrades"/>。
///
/// **两层结构**（设计文档 config-schema.md §2.2）：磁盘行（本类）→ 运行期视图
/// （<c>RewardDefinition</c>，带 <c>Action&lt;RewardContext&gt;</c> 委托）。
/// 委托不可能序列化进 `.tres`，所以效果用 <see cref="ApplyHandler"/> 字符串 +
/// C# 处理器注册表（<c>RewardEffectRegistry</c>）表达 —— 表仍是**可校验的数据**，
/// 读表时就能把"handler 未注册"抓出来并警告。
/// </summary>
[Tool]
[GlobalClass]
public partial class RewardConfig : ConfigRow
{
	[ExportGroup("身份与出率（强化 / 进化 / 异化；能叠几层；抽中概率）")]
	/// <summary>强化 / 进化 / 异化（对应 <c>Rewards.RewardKind</c>）。</summary>
	[Export] public Rewards.RewardKind RewardKind { get; set; } = Rewards.RewardKind.Enhancement;

	/// <summary>最多叠几层（0 = 不限；1 = 只能拿一次）。</summary>
	[Export] public int MaxStacks { get; set; } = 1;

	/// <summary>抽中概率（与 <c>BossPerk</c> 共用 <see cref="WeightedPick"/>；当前 19 条都是 1.0 = 等概率）。</summary>
	[Export] public float Weight { get; set; } = 1f;

	[ExportGroup("效果")]
	/// <summary>处理器 id（见 <c>Rewards.RewardEffectRegistry</c> 的注册表）。未注册会在读表时警告并跳过这一行。</summary>
	[Export] public string ApplyHandler { get; set; } = "StatSet";

	/// <summary>
	/// 处理器参数：<c>StatSet</c> 用 <c>Modifiers</c>；其余处理器用这里的键值对
	/// （例：<c>Damage</c> / <c>Range</c> / <c>Jumps</c> / <c>Ratio</c> / <c>Duration</c> / <c>MaxSummons</c>）。
	/// </summary>
	[Export] public ParamEntry[] HandlerParams { get; set; } = System.Array.Empty<ParamEntry>();

	[ExportGroup("池过滤（三级回退：① 专属池 → ② 武器标签 → ③ 通用）")]
	/// <summary>要求武器具备的标签；空 = 不限武器（**第二层**：现有 4 条进化走这层）。</summary>
	[Export] public string[] RequiredWeaponTags { get; set; } = System.Array.Empty<string>();

	/// <summary>
	/// 本选项**专属的升级池**（`Config/Tables/UpgradePools/pool_*.tres`）—— **第一层**。
	/// 填了 = 只有 <c>Weapon.PoolId</c> 等于它的武器能拿到这一条，**此时不再看 <see cref="RequiredWeaponTags"/>**；
	/// 留空 = 落到第二层（标签匹配）与第三层（通用池：现有 10 条强化 + 5 条异化）。
	///
	/// **选项行是"池成员"的唯一真源**：池行不反过来列选项（一个选项占一行 + 一个引用列；
	/// "一栏里塞多个选项"的数组形态恰恰是编辑器拿不到的，见 docs/config-tooling-yard.md §4.3）。
	/// </summary>
	[Export] public UpgradePoolConfig? Pool { get; set; }

	/// <summary>只有召唤控制角色才能拿到。</summary>
	[Export] public bool RequiresSummoner { get; set; }

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
	[Export] public StatOverride[] Modifiers { get; set; } = System.Array.Empty<StatOverride>();

	/// <summary>该处理器是否已注册（读表时用来抓拼错的 handler id）。</summary>
	public bool HasKnownHandler => Rewards.RewardEffectRegistry.IsRegistered(ApplyHandler);
}

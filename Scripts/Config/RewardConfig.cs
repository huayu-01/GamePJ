using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 增幅项表的一行 —— 一个 `.tres` 文件就是一行（`Config/Tables/Upgrades/upg_*.tres`）。
/// 索引用 <see cref="ConfigIndexData.Upgrades"/>。
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

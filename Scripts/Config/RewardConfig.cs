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
	/// <summary>强化 / 进化 / 异化（对应 <c>Rewards.RewardKind</c>）。</summary>
	[Export] public Rewards.RewardKind RewardKind { get; set; } = Rewards.RewardKind.Enhancement;

	/// <summary>最多叠几层（0 = 不限）。</summary>
	[Export] public int MaxStacks { get; set; } = 1;

	/// <summary>抽中概率（与 <c>BossPerk</c> 共用 <see cref="WeightedPick"/>）。</summary>
	[Export] public float Weight { get; set; } = 1f;

	[ExportGroup("效果")]
	/// <summary>处理器 id（见 <c>Rewards.RewardEffectRegistry</c> 的注册表）。未注册会在读表时警告并跳过这一行。</summary>
	[Export] public string ApplyHandler { get; set; } = "StatSet";

	/// <summary>
	/// 处理器参数：<c>StatSet</c> 用 <c>Modifiers</c>；其余处理器用这里的键值对
	/// （例：<c>Damage</c> / <c>Range</c> / <c>Jumps</c> / <c>Ratio</c> / <c>Duration</c> / <c>MaxSummons</c>）。
	/// </summary>
	[Export] public ParamEntry[] HandlerParams { get; set; } = System.Array.Empty<ParamEntry>();

	[ExportGroup("池过滤")]
	/// <summary>要求武器具备的标签；空 = 不限武器（取代 <c>RequiredWeaponTags</c>）。</summary>
	[Export] public string[] RequiredWeaponTags { get; set; } = System.Array.Empty<string>();

	/// <summary>只有召唤控制角色才能拿到。</summary>
	[Export] public bool RequiresSummoner { get; set; }

	/// <summary>互斥的增幅项 Id（双向判定）。</summary>
	[Export] public string[] Excludes { get; set; } = System.Array.Empty<string>();

	/// <summary>池标签。</summary>
	[Export] public string[] PoolTags { get; set; } = System.Array.Empty<string>();

	/// <summary>
	/// 属性修正（<c>StatSet</c> 处理器用）。取代 `RewardCatalog` 里 <c>Mod(c, "DamageMultiplier", Multiply, 1.15f)</c> 这类闭包。
	/// </summary>
	[Export] public StatOverride[] Modifiers { get; set; } = System.Array.Empty<StatOverride>();

	/// <summary>该处理器是否已注册（读表时用来抓拼错的 handler id）。</summary>
	public bool HasKnownHandler => Rewards.RewardEffectRegistry.IsRegistered(ApplyHandler);
}

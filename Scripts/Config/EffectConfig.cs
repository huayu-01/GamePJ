using System.Collections.Generic;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Config;

/// <summary>效果类型。<see cref="EffectConfig"/> 的行为由它决定（表里不再放代码，只放数值）。</summary>
public enum EffectKind
{
	/// <summary>击退：命中时给目标一个冲量。参数 <c>Multiplier</c>（全局缩放）/<c>ResistanceCap</c>（抗性上限）。</summary>
	Knockback,

	/// <summary>持续伤害（燃烧这类）：每 <c>TickSeconds</c> 秒扣 <c>DamagePerSecond × TickSeconds</c>。</summary>
	DamageOverTime,

	/// <summary>减速 / 定身：改 <c>MoveSpeedMultiplier</c>。</summary>
	Slow,

	/// <summary>削伤害：改 <c>DamageMultiplier</c>。</summary>
	Weaken,

	/// <summary>增伤 / 增攻速（正面状态）。</summary>
	Empower,

	/// <summary>眩晕：移速 + 攻速一起砍。</summary>
	Stun,

	/// <summary>爆炸：命中后按半径造成范围伤害。</summary>
	Explosion,

	/// <summary>吸血：把造成伤害的一定比例回血。</summary>
	Lifesteal,

	/// <summary>命中挂状态：命中时给目标挂一条状态行（<c>StatusId</c>）。</summary>
	ApplyStatusOnHit,

	/// <summary>击杀召唤：击杀敌人时召唤一个单位。</summary>
	SummonOnKill,

	/// <summary>其他 / 以后扩展。</summary>
	Other,
}

/// <summary>
/// **效果参数表**的一行 —— 一个 `.tres` 文件就是一行（`Config/Tables/Effects/eff_*.tres`）。
/// 索引用 <see cref="ConfigIndexData.Effects"/>。
///
/// 为什么单独立表（用户点名要求）：击退值、燃烧值、减速/眩晕倍率这类"效果"参数原先散落在三处 ——
/// ① 武器表里的 <c>Knockback</c>；② `StatusEffect.cs` 的 <c>switch</c> 里 6 个硬编码十进制；
/// ③ `RewardCatalog.cs` 里吸血 0.12 / 燃烧 3 秒 / 召唤上限 2 这些闭包常量。
/// 它们都是"效果"而不是"谁的数值"，所以集中到这一张表；**每一条都能单独调，且不写死进 C#**。
///
/// ⚠️ 与**状态行**的区别：这里描述的是"效果本身的参数"（一次击退的力量、一层燃烧每秒烧多少），
/// 状态行（<see cref="EffectKind.DamageOverTime"/> / <see cref="EffectKind.Slow"/> …）描述的是
/// "挂上去之后持续改什么"。两者共用本表，靠 <see cref="Kind"/> 区分，避免出现两张同构的表。
/// </summary>
[Tool]
[GlobalClass]
public partial class EffectConfig : ConfigRow
{
	[ExportGroup("身份（这一类效果 + 图标）")]
	/// <summary>这是哪一类效果 —— 决定这一行的数值怎么被使用（见 <see cref="EffectKind"/> 每个取值的中文说明）。</summary>
	[Export] public EffectKind Kind { get; set; } = EffectKind.Other;

	/// <summary>图标（界面/Debug 用；空 = 无图标）。</summary>
	[Export(PropertyHint.File, "*.png,*.svg")] public string IconPath { get; set; } = string.Empty;

	[ExportGroup("通用数值（按 Kind 选用，用不到的就留默认值）")]
	/// <summary>
	/// 状态类效果的默认持续时间（秒；0 = 由施加方决定，例如命中体的 <c>StatusDuration</c>）。
	/// ⚠️ 这是**第二优先**：调用方给了时长就用调用方的（`ToStatus(duration)` 的规则）。
	/// </summary>
	[Export] public float Duration { get; set; }

	[ExportGroup("状态修正（状态类效果用；取代 StatusEffect.cs 里硬编码的 Modifiers）")]
	/// <summary>挂上之后持续生效的属性修正（例：<c>MoveSpeedMultiplier × 0.55</c> = 减速）。空 = 不改属性（纯持续伤害就是这种）。</summary>
	[Export] public StatOverride[] Modifiers { get; set; } = System.Array.Empty<StatOverride>();

	/// <summary>每秒伤害（0 = 没有持续伤害）。等价于原来 <c>StatusEffect.DamagePerSecond</c>；燃烧当前 = 6 伤害/秒。</summary>
	[Export] public float DamagePerSecond { get; set; }

	/// <summary>
	/// **最多叠几层**（1 = 原来的行为：同名只刷新时间、不叠加）。
	///
	/// <para>设计文档的「标记」：「可叠加且不限定单个目标的 buff」→ 这一列填 &gt; 1（例如 5）。
	/// 层数由 <c>Unit.ApplyStatus</c> 记在单位自己身上（每个目标各一份），所以天然不限单个目标；
	/// 修正器按层数放大（每层一份），层数也能被增幅读（`Unit.GetStatusStacks`）。</para>
	///
	/// <para>⚠️ 这一列的值是**表**里说了算，代码里没有层数上限。</para>
	/// </summary>
	[Export] public int MaxStacks { get; set; } = 1;

	/// <summary>
	/// 本行是不是一条"可被挂到单位身上的状态"（有 Modifiers / 有持续伤害 / 或**是可叠加的纯标记**）。
	///
	/// ⚠️ 第三个条件是「标记」这类**没有数值修正**的纯计数 buff 所需要的：它没有任何 Modifier，
	/// 却必须能被挂上（心眼那条增幅读的就是层数）。
	/// </summary>
	public bool IsStatus => Modifiers.Length > 0 || DamagePerSecond > 0f || MaxStacks > 1;

	/// <summary>
	/// 转成运行期的 <see cref="StatusEffect"/>（状态类效果用）。
	/// 读不到本行时调用方应当完全跳过这个转换，单位不会挂上任何状态。
	/// </summary>
	public StatusEffect ToStatus(float duration)
	{
		var modifiers = new List<StatModifier>(Modifiers.Length);
		foreach (StatOverride entry in Modifiers)
		{
			modifiers.Add(entry.ToRuntime());
		}

		return new StatusEffect
		{
			Id = Id,
			Duration = duration > 0f ? duration : Duration,
			Modifiers = modifiers.ToArray(),
			DamagePerSecond = DamagePerSecond,
			MaxStacks = Mathf.Max(1, MaxStacks),
		};
	}
}

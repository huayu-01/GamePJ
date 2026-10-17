using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// **蓄力阈值弹**进化的强类型参数（J9「龙之箭」）：用技能资源寻址，
/// 蓄力上限 / 阈值 / 尺寸倍率 / 穿透数**全是表里的数字** —— 代码里一个数字都没有，
/// 也不做 <c>Godot.Get/Set("属性名")</c> 那种字符串反射设值（architecture.md 第 12 条）。
///
/// <para>语义：**松手那一刻**的蓄力秒数 ≥ <see cref="ThresholdSeconds"/> → 这一发就是"大箭"
/// （投射物放大到 <see cref="SizeScale"/> 倍 + 额外穿透 <see cref="PierceCount"/> 个目标）。
/// 判定在松手时冻结，释放段重播 / 等真实 ATK 期间不会再变（见 <c>ArrowVolleyAbility.FireVolley</c>）。</para>
///
/// <para>⚠️ **3 秒上限 vs 2 秒阈值的口径**（交接单「下一步顺序」第 3 条点名的歧义）：
/// 档位仍按技能行自己的 <c>ChargeStepSeconds</c> 每档递进，<see cref="MaxChargeSeconds"/> 只决定"能按多久"。
/// 基础弓 0.5s + 每 0.1s 一档 → 满 3 秒 = 25 档 ×8.5。
/// 要改成"2 秒后不再加档"只需把 <see cref="MaxSteps"/> 填 15（= floor((2-0.5)/0.1)），**不改 C#**。</para>
/// </summary>
[Tool]
[GlobalClass]
public partial class ChargedShotTuning : Resource
{
	/// <summary>改哪一条技能行（资源引用；运行期按行 Id 找到玩家身上那个 <c>Ability</c>）。</summary>
	[Export] public AbilityConfig? TargetAbility { get; set; }

	/// <summary>蓄力总时长上限（秒；≤0 = 不动技能行自己的 <c>ChargeMaxSeconds</c>）。原文「现在可以蓄力3秒」就是它。</summary>
	[Export] public float MaxChargeSeconds { get; set; } = 3f;

	/// <summary>
	/// 后置蓄力**档数**上限（0 = 不限，由 <see cref="MaxChargeSeconds"/> 封顶）。
	/// 填 15 = "2 秒后不再加档"那一种解释；默认 0 = "继续加档到 3 秒"。
	/// </summary>
	[Export] public int MaxSteps { get; set; }

	/// <summary>松手时蓄力 ≥ 这个秒数 → 这一发是"大箭"（≤0 = 整条阈值弹逻辑不启用）。</summary>
	[Export] public float ThresholdSeconds { get; set; } = 2f;

	/// <summary>大箭的投射物整体缩放（与技能行 <c>ProjectileSizeScale</c> 同一个单位；≤0 = 不放大）。</summary>
	[Export] public float SizeScale { get; set; } = 1f;

	/// <summary>大箭**额外**可穿透的目标数（0 = 不穿透；99 = 实际意义上的"全穿透"）。</summary>
	[Export] public int PierceCount { get; set; }
}

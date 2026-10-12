using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 一张能力卡**激活后怎么生效**（<see cref="AbilityCardConfig.Effect"/>）。
///
/// 只做三种是**故意的**：需求要的是"最小可用闭环 + 位置先占住"，
/// 每多一种效果就要多一处局内接线（数字一下滑回去看代码）。
/// 加第四种的做法写在 `docs/ability-cards.md`（一个 enum 值 + 一个 switch 分支 + 一张卡行）。
/// </summary>
public enum AbilityCardEffectKind
{
	/// <summary>加法修饰：<c>Stat += PerLevel × 等级</c>（例：最大生命 +20/级）。</summary>
	StatAdd,

	/// <summary>乘法修饰：<c>Stat ×= 1 + PerLevel × 等级</c>（例：伤害 +6%/级）。</summary>
	StatMultiply,

	/// <summary>授予一个技能：把 <see cref="AbilityCardConfig.Skill"/> 指的技能行套到玩家对应能力节点上并**启用**它
	/// （例：枪手拿到"无人机协议"就能按 G 召唤）。<see cref="AbilityCardConfig.PerLevel"/> = 每级（从 2 级起）减少的冷却秒数。</summary>
	GrantSkill,
}

/// <summary>
/// **能力卡表**的一行 —— 一个 `.tres` 文件就是一张卡（`Config/Tables/AbilityCards/card_*.tres`）。
/// 索引用 <see cref="BalanceConfig.AbilityCards"/>。
///
/// <para>📌 <b>它与 <c>Config/Tables/Skills/</c> 的关系（本轮第一件要判断的事）</b>：
/// 两者是**两层不同的东西**，所以**没有混进同一张表**：</para>
/// <list type="bullet">
/// <item><b>技能表（`Skills/skl_*.tres`，25 行）= 局内的东西</b>：描述"某个单位身上的某个能力节点怎么配"
///   （冷却/前摇/后摇/朝向/命中体/子类参数），由单位表的 `Skills` 以资源引用指过来
///   （`chr_*.tres → skl_*.tres`）。它是**战斗管线的一部分**，一局之内不变。</item>
/// <item><b>能力卡表（本类，`AbilityCards/card_*.tres`）= 局外的东西</b>：解锁 / 升级 / 激活开关 /
///   占用多少额度点数。它**跨局存在**（存 `user://ability_cards.cfg`），只在本局**开局**时把效果套到玩家身上。</item>
/// </list>
/// <para>两者的**唯一接触点**是 <see cref="Skill"/> 这一列（<b>资源引用</b>，不是字符串 id）：
/// 一张"授予技能"的卡指向技能表里的一行 —— 卡说"你有这个技能"，技能行说"这个技能的具体参数"。
/// 因此**能力卡没有复制任何技能参数**，改技能参数仍然只改 `Skills/` 一行。</para>
///
/// <para>⚠️ 纪律与其它表完全一致：读不到行 → 调用方什么都不做；行文件被删会让**整份** `Balance.tres`
/// 加载失败（所以下线一张卡要把数组项与 `[ext_resource]` 一起删）。</para>
/// </summary>
[Tool]
[GlobalClass]
public partial class AbilityCardConfig : ConfigRow
{
	[ExportGroup("卡牌 · 图标与额度")]
	/// <summary>
	/// 图标文件名（不含目录与扩展名）—— 到 `Assets/UI/Icons/&lt;它&gt;.svg` 找图。
	/// 留空 = 不显示图标（只显示名字），与"读不到图返回 null"同一条纪律。
	/// </summary>
	[Export] public string IconName { get; set; } = string.Empty;

	/// <summary>激活时占用的**额度点数**（玩家总额度有限 → 必须取舍）。0 = 不计额度。</summary>
	[Export] public int QuotaCost { get; set; } = 1;

	/// <summary>等级上限（**解锁后等级 = 1**，所以上限 <c>&gt;= 1</c>）。等级 0 = 未解锁，
	/// 边框颜色按等级走：0 灰 / 1 白 / 2 绿 / 3 蓝 / 4 紫 / 5 红。</summary>
	[Export] public int MaxLevel { get; set; } = 5;

	[ExportGroup("价格（局外碎片，付给 MetaProgress）")]
	/// <summary>解锁价格（等级 0 → 1）。</summary>
	[Export] public int UnlockCost { get; set; } = 30;

	/// <summary>第 1 次升级（1 → 2）的价格。</summary>
	[Export] public int UpgradeBaseCost { get; set; } = 20;

	/// <summary>每多一级的涨价幅度（线性：<c>Base + Growth × (当前等级 - 1)</c>）。</summary>
	[Export] public int UpgradeCostGrowth { get; set; } = 15;

	[ExportGroup("激活后的效果（只在本局开局套用一次）")]
	/// <summary>效果种类，见 <see cref="AbilityCardEffectKind"/>。</summary>
	[Export] public AbilityCardEffectKind Effect { get; set; } = AbilityCardEffectKind.StatAdd;

	/// <summary>改哪个属性（与 <c>StatSheet</c> 的键一致，例：<c>MaxHealthAdd</c> / <c>DamageMultiplier</c>）。
	/// 只有 <see cref="AbilityCardEffectKind.StatAdd"/> 与 <see cref="AbilityCardEffectKind.StatMultiply"/> 读它。</summary>
	[Export] public string Stat { get; set; } = string.Empty;

	/// <summary>
	/// 每级的量：加法/乘法效果的每级数值；<see cref="AbilityCardEffectKind.GrantSkill"/> 时
	/// = **每级（从 2 级起）减少的冷却秒数**。
	/// </summary>
	[Export] public float PerLevel { get; set; }

	/// <summary>
	/// 授予哪个技能 —— **资源引用**指向 `Config/Tables/Skills/skl_*.tres` 的一行
	/// （与 `chr_*.tres → skl_*.tres` 同一种写法：改技能参数只改技能行，卡不复制参数）。
	/// 只有 <see cref="AbilityCardEffectKind.GrantSkill"/> 读它。
	/// </summary>
	[Export] public AbilityConfig? Skill { get; set; }

	/// <summary>升级到 <paramref name="level"/>+1 要多少碎片（<paramref name="level"/> 是**当前**等级，1 起）。</summary>
	public int UpgradeCostOf(int level) => UpgradeBaseCost + (UpgradeCostGrowth * Mathf.Max(0, level - 1));

	/// <summary>从等级 <paramref name="level"/> **重置回 1 级**时应归还的碎片总额
	/// （= 已经把 1 级以上花掉的升级费全额退还；解锁费不退）。</summary>
	public int RefundFor(int level)
	{
		int total = 0;
		for (int current = 1; current < level; current++)
		{
			total += UpgradeCostOf(current);
		}

		return total;
	}

	/// <summary>效果文本（日志 / 悬停提示共用一处，避免两处各写一遍）。</summary>
	public string EffectText(int level)
	{
		int clamped = Mathf.Max(0, level);
		return Effect switch
		{
			AbilityCardEffectKind.StatAdd =>
				$"{Stat} +{PerLevel * clamped:0.##}",
			AbilityCardEffectKind.StatMultiply =>
				$"{Stat} ×{1f + (PerLevel * clamped):0.###}",
			AbilityCardEffectKind.GrantSkill =>
				Skill == null
					? "授予技能（行里没填技能 → 不生效）"
					: $"授予技能 {Skill.Id}（冷却 {GrantedCooldown(level):0.##}s）",
			_ => "—",
		};
	}

	/// <summary>"授予技能"卡在 <paramref name="level"/> 级时的实际冷却（基础值来自技能行）。</summary>
	public float GrantedCooldown(int level)
	{
		float baseCooldown = Skill?.Cooldown ?? 0f;
		if (baseCooldown <= 0f)
		{
			return 0f;
		}

		return Mathf.Max(1f, baseCooldown - (PerLevel * Mathf.Max(0, level - 1)));
	}
}

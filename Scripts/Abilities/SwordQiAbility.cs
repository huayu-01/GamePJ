using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// **剑气**（角色 B 武器 1「灵剑」武技2「拔刀式」，设计文档第 110 / 114 行）：
///
/// <code>
/// 武技2[拔刀式]{cd:2}:基础蓄力{1}秒后朝准星方向发射一发剑气,对路径上的敌人造成伤害;
/// 剑气:能穿透路径上的敌人,但飞行路径短,可被格挡,剑气大小会等比攻击盒大小;
/// </code>
///
/// <para>它就是"一支箭"（<see cref="ArrowVolleyAbility"/> 的表驱动参数全部适用）：直线、**可被格挡**
/// （<see cref="ArrowVolleyAbility.BypassBlock"/> = false，与能量波相反）、**穿透路径上的敌人**
/// （<see cref="ArrowVolleyAbility.Pierce"/>）、**飞行路径短**（<see cref="ArrowVolleyAbility.MaxLifetime"/>
/// 给一个小值）。蓄力走基类三段状态机（`ChargeBaseSeconds = 1`、押住松手才发射）。</para>
///
/// <para><b>「剑气大小会等比攻击盒大小」</b>：<see cref="SizeSourceAbilityId"/> 指向武技1 那一行，
/// 剑气按它**当前那一段**的命中盒尺寸等比缩放（<see cref="ArrowVolleyAbility.ProjectileSizeScale"/> +
/// <see cref="SizeRatio"/>）。源技能只读不写；读不到源技能时退化成 <c>SizeRatio</c> 一个常量
/// （不让"两把武器换装顺序"影响能不能放技能）。</para>
/// </summary>
public partial class SwordQiAbility : ArrowVolleyAbility
{
	[ExportGroup("剑气（大小等比攻击盒大小）")]
	/// <summary>**尺寸基准**：哪个技能行的命中盒尺寸（武技1 = `skl_b1_w1_1`）。留空 = 用 <see cref="SizeRatio"/>。</summary>
	[Export] public string SizeSourceAbilityId { get; set; } = string.Empty;

	/// <summary>等比系数：剑气大小 = 源技能当前那段命中盒尺寸 × 这个系数。</summary>
	[Export] public float SizeRatio { get; set; } = 0.5f;

	/// <summary>源技能找不到时用的绝对尺寸（米；0 = 不缩放，用预制体自己的大小）。</summary>
	[Export] public float FallbackSize { get; set; } = 1.2f;

	/// <summary>本次剑气实际用的尺寸（自测取证）。</summary>
	public float LastSize { get; private set; }

	/// <summary>尺寸的来源说明（自测取证："等比的是谁的盒子"）。</summary>
	public string LastSizeSource { get; private set; } = string.Empty;
	private float _enhancedSize = 1f;
	private (int Hits, float Radius)? _originalSweep;
	public void ConfigureEnhancement(float damageScale, float sizeScale, int sweepMaxHits, float sweepRadius)
	{
		Damage *= Mathf.Max(0f, damageScale);
		_enhancedSize *= Mathf.Max(0.01f, sizeScale);
		_originalSweep ??= (SweepMaxHits, SweepRadius);
		SweepMaxHits = sweepMaxHits;
		SweepRadius = sweepRadius;
	}
	protected override void OnLoadoutReset()
	{
		base.OnLoadoutReset();
		_enhancedSize = 1f;
		if (_originalSweep is { } sweep) { SweepMaxHits = sweep.Hits; SweepRadius = sweep.Radius; }
		_originalSweep = null;
	}

	protected override void OnActivate(Unit? target)
	{
		ResolveSize();
		base.OnActivate(target);
	}

	protected override void PrepareShot() => ResolveSize();
	private void ResolveSize()
	{
		Vector3 source = Vector3.Zero;
		if (!string.IsNullOrEmpty(SizeSourceAbilityId) && OwnerUnit != null
			&& Ability.FindByAbilityId(OwnerUnit, SizeSourceAbilityId) is MeleeComboAbility melee)
		{
			source = melee.CurrentStepBoxSize;
			LastSizeSource = $"武技1 {SizeSourceAbilityId} 当前第 {melee.ComboStep + 1} 段的命中盒 {source}";
		}

		if (source == Vector3.Zero)
		{
			LastSize = FallbackSize;
			LastSizeSource = string.IsNullOrEmpty(SizeSourceAbilityId)
				? "（没填 SizeSourceAbilityId → 用 FallbackSize）"
				: $"（找不到 {SizeSourceAbilityId} → 用 FallbackSize）";
		}
		else
		{
			// "等比"：取源盒子的**最大边**做基准（剑气的可视长度本来就该随那把剑的攻击盒走）。
			float longest = Mathf.Max(source.X, Mathf.Max(source.Y, source.Z));
			LastSize = longest * SizeRatio;
		}

		LastSize *= _enhancedSize;
		ProjectileSizeScale = LastSize > 0f ? LastSize : 1f;
	}
}

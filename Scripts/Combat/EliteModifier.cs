using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 强化型精英（战斗规则说明 §29）：普通敌人 + 一组 Modifier，不需要为精英复制敌人脚本。
/// 由 Encounter 在生成时按概率挂上，也可以在预制体里直接放一个。
/// </summary>
public partial class EliteModifier : Node, IInitialArmorBonus
{
	[Export] public float HealthMultiplier { get; set; } = 2.5f;

	/// <summary>
	/// 精英额外护甲的最低值；保留旧导出名以兼容场景。常规精英的下限不低于120。
	/// 不代表护甲上限，比例部分按关卡及特性完成后的最大生命计算。
	/// </summary>
	[Export] public float ArmorAdd { get; set; } = 120f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float ArmorHealthRatio { get; set; } = .3f;
	/// <summary>仅用于已有的技能护卫身份标记；该场景已有独立基础护甲，不重复授予随机精英加甲。</summary>
	[Export] public bool GrantArmorBonus { get; set; } = true;
	public float ArmorFor(Unit unit) => GrantArmorBonus ? Mathf.Max(120f, Mathf.Max(ArmorAdd, unit.MaxHealth * Mathf.Max(0, ArmorHealthRatio))) : 0f;
	[Export] public float DamageMultiplier { get; set; } = 1.4f;
	[Export] public float MoveSpeedMultiplier { get; set; } = 0.9f;
	[Export] public float KnockbackResistance { get; set; } = 0.6f;

	/// <summary>体型与配色（表现层，用来说明“这是精英”）。</summary>
	[Export] public float ModelScale { get; set; } = 1.35f;
	[Export] public Color Tint { get; set; } = new(0.85f, 0.3f, 0.9f);

	private bool _applied;

	/// <summary>由 Encounter 在运行时添加时也能生效。</summary>
	public override void _Ready() => Apply();

	public void Apply()
	{
		if (_applied || GetParent() is not Unit unit)
		{
			return;
		}

		_applied = true;
		unit.Stats.AddModifier(new StatModifier { Stat = "MaxHealthMultiplier", Operation = ModifierOperation.Multiply, Value = HealthMultiplier, Source = this });

		unit.Stats.AddModifier(new StatModifier { Stat = "DamageMultiplier", Operation = ModifierOperation.Multiply, Value = DamageMultiplier, Source = this });
		unit.Stats.AddModifier(new StatModifier { Stat = "MoveSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = MoveSpeedMultiplier, Source = this });
		unit.KnockbackResistance = Mathf.Max(unit.KnockbackResistance, KnockbackResistance);
		unit.RefreshStats();
		unit.AddInitialArmorBonus(this);
		unit.AddToGroup("elite_units");

		if (!Mathf.IsEqualApprox(ModelScale, 1f))
		{
			unit.Scale = new Vector3(ModelScale, ModelScale, ModelScale);
		}

		// 精英保留模型原材质，以紫色半透明轮廓和体型识别，不再整身刷成不透明紫块。
		TintRecursive(unit, UnitRimOverlay.Create(Tint, .42f, 1.0f));
	}
	public override void _ExitTree()
	{
		if (GetParent() is Unit unit) unit.RemoveInitialArmorBonus(this);
		UnitRimOverlay.RemoveSource(this);
	}

	/// <summary>
	/// 递归给身体网格添加精英边缘光，仍保留原贴图与表面材质。
	/// 换用 .glb 模型后，网格藏在 Model/&lt;角色&gt;/Skeleton3D/... 下面，
	/// 只遍历直接子节点会一个网格都找不到 —— 精英怪不再变紫，而且**不报错**（静默失效）。
	/// </summary>
	private void TintRecursive(Node node, Material material)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is SkillIndicator or EnemyAttackWarning or StatusEffectFx or ChargeBodyFx or ActRoguelike.Visual.UnitMotionFx || child.Name == "ArmorAura") continue;
			if (child is MeshInstance3D mesh)
			{
				UnitRimOverlay.Set(mesh, this, material, 10);
			}

			TintRecursive(child, material);
		}
	}
}

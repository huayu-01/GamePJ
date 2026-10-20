using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 强化型精英（战斗规则说明 §29）：普通敌人 + 一组 Modifier，不需要为精英复制敌人脚本。
/// 由 Encounter 在生成时按概率挂上，也可以在预制体里直接放一个。
/// </summary>
public partial class EliteModifier : Node
{
	[Export] public float HealthMultiplier { get; set; } = 2.5f;

	/// <summary>
	/// 给精英**直接加多少护甲**（用户 C1：护甲没有上限，所以这是纯粹的"加甲"，
	/// 不再是"抬高护甲上限" —— 原先这里是 <c>MaxArmorAdd</c> 修正器）。
	/// 走 <see cref="Unit.GrantArmor"/>，进树前后调用都能正确结算。
	/// </summary>
	[Export] public float ArmorAdd { get; set; } = 120f;
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

		// 护甲没有上限（用户 C1）：直接加甲，不再经 StatSheet 的 MaxArmorAdd 抬高"上限"。
		unit.GrantArmor(ArmorAdd);
		unit.Stats.AddModifier(new StatModifier { Stat = "DamageMultiplier", Operation = ModifierOperation.Multiply, Value = DamageMultiplier, Source = this });
		unit.Stats.AddModifier(new StatModifier { Stat = "MoveSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = MoveSpeedMultiplier, Source = this });
		unit.KnockbackResistance = Mathf.Max(unit.KnockbackResistance, KnockbackResistance);
		unit.RefreshStats();
		unit.AddToGroup("elite_units");

		if (!Mathf.IsEqualApprox(ModelScale, 1f))
		{
			unit.Scale = new Vector3(ModelScale, ModelScale, ModelScale);
		}

		var material = new StandardMaterial3D
		{
			AlbedoColor = Tint,
			EmissionEnabled = true,
			Emission = Tint,
			EmissionEnergyMultiplier = 0.35f,
		};

		if (unit.GetNodeOrNull<UnitPresentation>("Presentation") is { } presentation && presentation.IsNodeReady()) presentation.SetBaseMaterial(material);
		else TintRecursive(unit, material);
	}

	/// <summary>
	/// 递归给所有网格上色。
	/// 换用 .glb 模型后，网格藏在 Model/&lt;角色&gt;/Skeleton3D/... 下面，
	/// 只遍历直接子节点会一个网格都找不到 —— 精英怪不再变紫，而且**不报错**（静默失效）。
	/// </summary>
	private static void TintRecursive(Node node, Material material)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is MeshInstance3D mesh)
			{
				mesh.MaterialOverride = material;
			}

			TintRecursive(child, material);
		}
	}
}

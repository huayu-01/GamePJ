using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 强化型精英（战斗规则说明 §29）：普通敌人 + 一组 Modifier，不需要为精英复制敌人脚本。
/// 由 Encounter 在生成时按概率挂上，也可以在预制体里直接放一个。
/// </summary>
public partial class EliteModifier : Node
{
	[Export] public float HealthMultiplier { get; set; } = 2.5f;
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
		unit.Stats.AddModifier(new StatModifier { Stat = "MaxArmorAdd", Operation = ModifierOperation.Add, Value = ArmorAdd, Source = this });
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

		foreach (Node child in unit.GetChildren())
		{
			if (child is MeshInstance3D mesh)
			{
				mesh.MaterialOverride = material;
			}
		}
	}
}

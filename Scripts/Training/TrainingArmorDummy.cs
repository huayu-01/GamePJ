using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Training;

/// <summary>训练木桩：周期性补充护甲，训练专用上限，不修改生产护甲规则。</summary>
public partial class TrainingArmorDummy : Node
{
	[Export] public float ArmorPerTick { get; set; } = 20f;
	[Export] public float IntervalSeconds { get; set; } = 2.5f;
	[Export] public float MaxTrainingArmor { get; set; } = 100f;

	private Unit? _unit;
	private double _timer;

	public override void _Ready()
	{
		_unit = GetParent<Unit>();
		_timer = IntervalSeconds;
	}

	public override void _Process(double delta)
	{
		if (_unit == null || !IsInstanceValid(_unit) || _unit.IsDead) return;
		_timer -= delta;
		if (_timer > 0) return;
		_timer = IntervalSeconds;
		if (_unit.Armor >= MaxTrainingArmor) return;
		_unit.GrantArmor(Mathf.Min(ArmorPerTick, MaxTrainingArmor - _unit.Armor));
	}
}

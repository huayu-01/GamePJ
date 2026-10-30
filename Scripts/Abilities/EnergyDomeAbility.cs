using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Abilities;

public partial class EnergyDomeAbility : EvolvedAttackAbility
{
	[Export] public float Duration { get; set; } = 2;
	[Export] public float StartupSeconds { get; set; } = .15f;
	private EnergyDomeField? _field;
	public bool DomeActive => IsInstanceValid(_field) && !_field!.IsQueuedForDeletion();
	protected override void OnActivate(Unit? target)
	{
		Snapshot(); if (IsInstanceValid(_field)) _field!.QueueFree();
		_field = new EnergyDomeField { OwnerUnit = OwnerUnit!, Radius = Radius, Lifetime = Duration };
		OwnerUnit!.AddChild(_field); var attack = Attack(Damage);
		foreach (var enemy in Enemies(Targeting.AimPoint(OwnerUnit), Radius)) if (CanSee(enemy, Targeting.AimPoint(OwnerUnit))) Strike(enemy, attack);
	}
	protected override void Tick(float delta) { if (IsActive && StateTime >= StartupSeconds) Finish(); }
	protected override void OnLoadoutReset() { if (IsInstanceValid(_field)) _field!.QueueFree(); _field = null; }
	public override void _ExitTree() { OnLoadoutReset(); base._ExitTree(); }
}

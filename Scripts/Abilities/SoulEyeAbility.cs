using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Abilities;

public partial class SoulEyeAbility : EvolvedAttackAbility
{
	[Export] public float Duration { get; set; } = 10;
	[Export] public float MarkInterval { get; set; } = .2f;
	[Export] public float MarkDuration { get; set; } = 6;
	[Export] public float SearchRadius { get; set; } = 12;
	[Export] public float StartupSeconds { get; set; } = .2f;
	private SoulEyeCompanion? _eye;
	public SoulEyeCompanion? Eye => IsInstanceValid(_eye) ? _eye : null;
	protected override void OnActivate(Unit? target) => Snapshot();
	protected override void Tick(float delta)
	{
		if (!IsActive) return;
		if (StateTime < StartupSeconds) return;
		if (IsInstanceValid(_eye)) _eye!.QueueFree();
		_eye = new SoulEyeCompanion { OwnerUnit = OwnerUnit!, Lifetime = Duration, Interval = MarkInterval, MarkDuration = MarkDuration, Radius = SearchRadius, AbilityId = AbilityId };
		OwnerUnit!.AddChild(_eye); Finish();
	}
	protected override void OnLoadoutReset() { if (IsInstanceValid(_eye)) _eye!.QueueFree(); _eye = null; }
	public override void _ExitTree() { OnLoadoutReset(); base._ExitTree(); }
}

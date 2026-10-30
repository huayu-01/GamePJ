using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Run;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Rewards;

public partial class SupplyDropRule : Node, IStoppableRewardRule
{
	public float IntervalSeconds { get; set; } = 10;
	public float MinRadius { get; set; } = 6;
	public float MaxRadius { get; set; } = 10;
	public float PickupLifetime { get; set; } = 30;
	public int MaxOutstanding { get; set; } = 5;
	public SupplyOptionDefinition[] Options { get; set; } = System.Array.Empty<SupplyOptionDefinition>();
	public float EffectiveInterval => IntervalSeconds * Mathf.Max(.1f, 1 - (_owner?.Stats.Get("SupplyIntervalReduction") ?? 0));
	public int DropCount { get; private set; }
	public int CollectedCount { get; private set; }
	public IReadOnlyList<SupplyPickup> Pickups => _pickups;
	private readonly List<SupplyPickup> _pickups = new();
	private Unit? _owner; private float _time;
	public override void _Ready() { _owner = GetParent<Unit>(); SetProcess(false); }
	public SupplyOptionDefinition? RollOption(bool survival)
	{
		float weight = Options.Sum(o => survival ? o.SurvivalWeight : o.AdventureWeight); if (weight <= 0) return null;
		float roll = GD.Randf() * weight;
		foreach (var option in Options) { float w = survival ? option.SurvivalWeight : option.AdventureWeight; if (w > 0 && (roll -= w) <= 0) return option; }
		return Options.LastOrDefault(o => (survival ? o.SurvivalWeight : o.AdventureWeight) > 0);
	}
	public override void _PhysicsProcess(double delta)
	{
		if (_owner is not { IsDead: false }) { Stop(); return; }
		var run = RunController.Instance; if (run?.IsCombatActive != true) return;
		_time += (float)delta; if (_time < EffectiveInterval) return; _time = Mathf.Max(0, _time - EffectiveInterval);
		_pickups.RemoveAll(static p => !IsInstanceValid(p) || p.IsQueuedForDeletion()); if (_pickups.Count >= MaxOutstanding) return;
		if (MapManager.Instance is not { IsReady: true } map || !map.TryGroundAnchor(_owner.GlobalPosition, out var anchor, _owner)
			|| !map.TryRingPoint(anchor, MinRadius, MaxRadius, out var point, _owner, anchor)) return;
		var option = RollOption(run.Mode is SurvivalDirector); if (option == null) return;
		var pickup = new SupplyPickup { OwnerUnit = _owner, Option = option, Lifetime = PickupLifetime, GroundPoint = point };
		pickup.Collected += OnCollected; AddChild(pickup); _pickups.Add(pickup); DropCount++;
	}
	private void OnCollected(SupplyPickup pickup) { CollectedCount++; _pickups.Remove(pickup); pickup.Collected -= OnCollected; }
	public void Stop()
	{
		foreach (var pickup in _pickups) if (IsInstanceValid(pickup)) { pickup.Collected -= OnCollected; pickup.Stop(); }
		_pickups.Clear(); _owner = null; SetPhysicsProcess(false);
	}
	public override void _ExitTree() => Stop();
}

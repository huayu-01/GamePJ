using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Run;

public partial class SurvivalDirector
{
	private readonly List<RunShardPickup> _shardDrops = new();
	public int CollectedCombatShards { get; private set; }
	public int CollectedLootShards => CollectedCombatShards + (_run?.CollectedBonusShards ?? 0);
	public int DroppedCombatShards { get; private set; }
	public int PendingCombatShards { get; private set; }
	private void DropCombatShards(Unit unit, bool boss, bool milestone, Vector3 origin)
	{
		if (Definition == null || _player == null || unit.HasMeta("EliteSummoned")) return;
		bool elite = milestone || unit.IsInGroup("elite_units") || unit.HasTag("Elite");
		int amount = boss || unit.HasTag("Boss") ? Definition.BossKillShardDrop : elite ? Definition.EliteKillShardDrop : 0;
		if (amount <= 0) return;
		var scene = Definition.ShardPickupScene ?? RuntimeResources.Load<PackedScene>("res://Scenes/Combat/ShardPickup.tscn");
		var pickup = scene.Instantiate<RunShardPickup>();
		pickup.Player = _player; pickup.Amount = amount; pickup.PickupRadius = Definition.ShardPickupRadius;
		pickup.Collected += CollectCombatShards;
		AddChild(pickup); pickup.GlobalPosition = PickupDropLayout.Claim(unit, origin);
		_shardDrops.Add(pickup); DroppedCombatShards += amount; PendingCombatShards += amount;
	}
	private void CollectCombatShards(RunShardPickup pickup)
	{
		if (!_shardDrops.Remove(pickup)) return;
		pickup.Collected -= CollectCombatShards; PendingCombatShards = Mathf.Max(0, PendingCombatShards - pickup.Amount);
		if (State is PhaseState.Stopped or PhaseState.Dormant || _player is not { IsDead: false }) return;
		CollectedCombatShards += pickup.Amount;
	}
	private void ClearShardPickups()
	{
		foreach (var pickup in _shardDrops)
			if (IsInstanceValid(pickup)) { pickup.Collected -= CollectCombatShards; pickup.QueueFree(); }
		_shardDrops.Clear(); PendingCombatShards = 0;
	}
	public override int SettlementShards(bool victory) => base.SettlementShards(victory) + CollectedCombatShards;
	public override string DescribeSettlement(bool victory, string build, int earned, int total) =>
		base.DescribeSettlement(victory, build, earned, total) + "\n" +
		string.Format(ContentText.Get("ui.survival.shard_breakdown", "战利品碎片 +{0}（已包含在本局总收益中）"), CollectedCombatShards);
}

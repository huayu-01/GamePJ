using Godot;
using ActRoguelike.Config;

namespace ActRoguelike.Combat;

public enum EnemyCrowdMode { Physical, PassThrough, Avoidance }

/// <summary>地面敌人的可回退拥挤策略；不改攻击层、玩家阻挡、飞行或生成预算。</summary>
[Tool, GlobalClass]
public partial class EnemyCrowdConfig : Resource
{
	private EnemyCrowdMode _mode;
	[Export] public EnemyCrowdMode Mode { get => _mode; set { if (_mode == value) return; _mode = value; EmitChanged(); } }
	[Export] public float NeighborDistance { get; set; } = 6f;
	[Export] public int MaxNeighbors { get; set; } = 10;
	[Export] public float TimeHorizon { get; set; } = .65f;
	[Export] public float RadiusScale { get; set; } = 1.4f;
	[Export] public bool BossPushEnabled { get; set; } = true;
	[Export] public float BossPushSpeed { get; set; } = 8f;
	[Export] public float BossPushSideWeight { get; set; } = 1.3f;
	private static readonly EnemyCrowdConfig Fallback = new();
	public static EnemyCrowdConfig Current => ConfigIndex.Settings.EnemyCrowd ?? Fallback;
	public static bool IsGroundEnemy(Unit unit) => unit.Faction == Faction.Enemy && unit.Movement?.CanFly != true;
	public static uint BodyMaskFor(Unit unit) => IsGroundEnemy(unit) && Current.Mode != EnemyCrowdMode.Physical
		? CombatLayers.World | CombatLayers.Player : CombatLayers.BodyMask;
}

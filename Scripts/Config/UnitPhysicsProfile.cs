using Godot;

namespace ActRoguelike.Config;

/// <summary>身体运动配置；与阵营、受击层和视觉模型独立，运行时不修改共享资源。</summary>
[GlobalClass]
public partial class UnitPhysicsProfile : Resource
{
	[Export] public bool CanFly { get; set; }
	[Export(PropertyHint.Range, "0.01,100,0.01,or_greater")] public float Mass { get; set; } = 1f;
	[Export] public bool ControlImmune { get; set; }
	[Export] public bool CanBeKnockedDown { get; set; } = true;
	[ExportGroup("飞行（脚底到World地面的高度）")]
	[Export] public float HoverHeight { get; set; } = 3f;
	[Export] public float HeightResponse { get; set; } = 3f;
	[Export] public float LiftAcceleration { get; set; } = 24f;
	[Export] public float MaxVerticalSpeed { get; set; } = 6f;
	[Export] public float GroundProbeDistance { get; set; } = 50f;
	[Export] public float GroundRecoverySeconds { get; set; } = 2f;
}

using Godot;

namespace ActRoguelike.Visual;

/// <summary>建筑占地禁草体积；随建筑缩放和旋转，只在草地生成时读取，不参与碰撞。</summary>
[GlobalClass]
public partial class GrassExclusionZone : Node3D
{
	[Export] public bool Enabled { get; set; } = true;
	[Export] public Vector3 Size { get; set; } = new(4, 2, 4);
}

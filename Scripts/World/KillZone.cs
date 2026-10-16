using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.World;

/// <summary>
/// 地图下方的区域杀（用户要求：用碰撞产生，进入时才击杀，而不是每帧遍历单位组）。
///
/// 由 <see cref="MapManager"/> 在关卡加载后创建一个覆盖整张地图的薄区域，
/// 单位（玩家 / 敌人 / 首领 / 召唤物）落进去立即击杀。
/// 相比"每帧遍历 4 个组查 Y 坐标"，这是事件驱动的，且天然对所有单位生效。
/// </summary>
public partial class KillZone : Area3D
{
	/// <summary>区域厚度（够薄即可，反正只看"有没有落进来"）。</summary>
	[Export] public float Thickness { get; set; } = 20f;

	/// <summary>区域边长，覆盖整张地图。</summary>
	[Export] public float Extent { get; set; } = 300f;

	/// <summary>区域中心的 Y（低于地图底部）。</summary>
	[Export] public float CenterY { get; set; } = -60f;

	public override void _Ready()
	{
		CollisionLayer = 0;                 // 自己不参与碰撞
		CollisionMask = CombatLayers.UnitMask; // 只关心身体，攻击盒和投射物不触发区域杀。
		Monitoring = true;
		Monitorable = false;

		Position = new Vector3(0f, CenterY, 0f);

		var shape = new CollisionShape3D
		{
			Name = "Shape",
			Shape = new BoxShape3D { Size = new Vector3(Extent, Thickness, Extent) },
		};
		AddChild(shape);

		BodyEntered += OnBodyEntered;
		ActRoguelike.Core.GameLog.Write("Map", $"区域杀已就位：中心 Y={CenterY}、覆盖 {Extent}×{Extent}");
	}

	private void OnBodyEntered(Node3D body)
	{
		if (body is not Unit unit || !GodotObject.IsInstanceValid(unit) || unit.IsDead)
		{
			return;
		}

		ActRoguelike.Core.GameLog.Write("Combat", $"{unit.Name} 落出地图（进入区域杀）→ 击杀");
		unit.ApplyDirectDamage(999999f, null);
	}
}

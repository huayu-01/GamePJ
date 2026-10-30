using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Run;

public partial class SceneRuneMeteor : SceneRuneActor
{
	private MeshInstance3D _meteor = null!;
	private MeshInstance3D _warning = null!;
	private SphereShape3D _shape = null!;
	private PhysicsShapeQueryParameters3D _query = null!;
	private bool _exploded;
	public override void Initialize()
	{
		_warning = Ring(Data.Radius, Data.Tint);
		_meteor = new MeshInstance3D { Mesh = new SphereMesh { Radius = .32f, Height = .64f }, Position = Vector3.Up * Data.FallHeight,
			MaterialOverride = Material(new Color(1, .6f, .22f)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off }; AddChild(_meteor);
		_shape = new SphereShape3D { Radius = Data.Radius };
		_query = new PhysicsShapeQueryParameters3D { Shape = _shape, CollisionMask = CombatLayers.Enemy, CollideWithAreas = false };
	}
	public override void Tick(float dt)
	{
		if (Finished) return; Age += dt;
		if (_exploded) { _warning.Scale *= 1 + dt * 3; if (Age > Data.WarningSeconds + Data.FallHeight / Data.Speed + .2f) Finish(); return; }
		if (Age < Data.WarningSeconds) return;
		float height = Mathf.Max(0, Data.FallHeight - (Age - Data.WarningSeconds) * Data.Speed); _meteor.Position = Vector3.Up * height;
		if (height > 0) return;
		_exploded = true; _meteor.Visible = false;
		_query.Transform = new Transform3D(Basis.Identity, GlobalPosition + Vector3.Up * .5f);
		var hits = GetWorld3D().DirectSpaceState.IntersectShape(_query, 128);
		foreach (var result in hits)
			if (result["collider"].AsGodotObject() is Unit { Faction: Faction.Enemy, IsDead: false } unit) Controller.Hit(unit, Data.Damage);
	}
	public override void _ExitTree() { base._ExitTree(); _query?.Dispose(); _shape?.Dispose(); }
}

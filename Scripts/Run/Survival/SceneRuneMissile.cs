using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Run;

/// <summary>只扫玩家受击层，永远穿过地形与敌人；方向在发射时冻结，有出生预警。</summary>
public partial class SceneRuneMissile : SceneRuneActor
{
	private Vector3 _direction;
	private MeshInstance3D _body = null!;
	private SphereShape3D _shape = null!;
	private PhysicsShapeQueryParameters3D _query = null!;
	private bool _launched;
	public override void Initialize()
	{
		GlobalPosition += Vector3.Up;
		_shape = new SphereShape3D { Radius = .65f };
		_query = new PhysicsShapeQueryParameters3D { Shape = _shape, CollisionMask = CombatLayers.Player, CollideWithAreas = false };
		_body = new MeshInstance3D { Mesh = new SphereMesh { Radius = .6f, Height = 1.2f }, MaterialOverride = Material(Data.Tint), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off }; AddChild(_body);
		Ring(.85f, Data.Tint);
	}
	public override void Tick(float dt)
	{
		if (Finished) return; Age += dt;
		if (Age >= Data.WarningSeconds + Data.Lifetime) { Finish(); return; }
		if (Age < Data.WarningSeconds) { _body.Scale = Vector3.One * (.75f + .1f * Mathf.Sin(Age * 16)); return; }
		if (!_launched) { _direction = (Projectile.GetTargetCenter(Controller.Player) - GlobalPosition).Normalized(); _launched = true; _body.Scale = Vector3.One; }
		Vector3 motion = _direction * Data.Speed * dt;
		_query.Transform = new Transform3D(Basis.Identity, GlobalPosition); _query.Motion = motion;
		var space = GetWorld3D().DirectSpaceState;
		var overlap = space.IntersectShape(_query, 1);
		float[] fractions = space.CastMotion(_query);
		if (overlap.Count > 0 || fractions.Length > 0 && fractions[0] < 1)
		{
			Finish(); Controller.ApplyMissile(_direction); return;
		}
		GlobalPosition += motion;
	}
	public override void _ExitTree() { base._ExitTree(); _query?.Dispose(); _shape?.Dispose(); }
}

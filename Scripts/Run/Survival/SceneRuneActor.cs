using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Run;

/// <summary>环境实例没有敌人身份，不计刷怪费、击杀或经验，所有实例由唯一场景符文收回。</summary>
public abstract partial class SceneRuneActor : Node3D
{
	public SceneRuneController Controller { get; set; } = null!;
	protected SceneRuneDefinition Data => Controller.Definition;
	public bool Finished { get; private set; }
	protected float Age;
	public abstract void Initialize();
	public abstract void Tick(float dt);
	public virtual void Finish() { Finished = true; Visible = false; }
	public override void _ExitTree() => Finish();
	protected StandardMaterial3D Material(Color color) => new() { AlbedoColor = color, EmissionEnabled = true, Emission = new Color(color.R, color.G, color.B),
		EmissionEnergyMultiplier = .8f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
	protected MeshInstance3D Ring(float radius, Color color)
	{
		var mesh = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = radius * .93f, OuterRadius = radius, Rings = 40, RingSegments = 6 },
			Position = Vector3.Up * .08f, Scale = new Vector3(1, .25f, 1), MaterialOverride = Material(color), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
		AddChild(mesh); return mesh;
	}
	protected static bool Live(Unit unit) => IsInstanceValid(unit) && !unit.IsDead && !unit.IsQueuedForDeletion();
}

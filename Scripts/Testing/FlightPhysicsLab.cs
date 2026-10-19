using System;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>独立交互验证场；几何体只是物理占位，不替代角色模型或动画设计。</summary>
public partial class FlightPhysicsLab : Node3D
{
	private Unit _flyer = null!, _light = null!, _heavy = null!;
	private Label _status = null!;
	private UnitPhysicsProfile _profile = null!;
	private bool _capture;
	public override void _Ready()
	{
		_profile = RuntimeResources.Load<UnitPhysicsProfile>("res://Config/Physics/flight_standard.tres");
		var floor = new StaticBody3D { Position = new Vector3(0, -0.5f, 0), CollisionLayer = CombatLayers.World, CollisionMask = 0 };
		floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(28, 1, 22) } });
		floor.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(28, 1, 22), Material = Material(new Color(0.12f, 0.17f, 0.22f)) } }); AddChild(floor);
		var wall = new StaticBody3D { Position = new Vector3(8, 3, 0), CollisionLayer = CombatLayers.World, CollisionMask = 0 };
		wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.5f, 6, 8) } });
		wall.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.5f, 6, 8), Material = Material(new Color(0.3f, 0.36f, 0.42f)) } }); AddChild(wall);
		var light = new DirectionalLight3D { RotationDegrees = new Vector3(-55, -25, 0), ShadowEnabled = true }; AddChild(light);
		var camera = new Camera3D { Position = new Vector3(15, 15, 20), Current = true, Projection = Camera3D.ProjectionType.Orthogonal, Size = 23 }; AddChild(camera); camera.LookAt(new Vector3(0, 1.5f, 0));
		_flyer = Actor("飞行身体", new Vector3(-2, 0.05f, 0), new Color(0.2f, 0.85f, 0.95f), _profile);
		_light = Actor("地面质量1", new Vector3(-4, 0.05f, 4), new Color(0.95f, 0.7f, 0.25f), null);
		_heavy = Actor("地面质量3", new Vector3(1, 0.05f, 4), new Color(0.85f, 0.4f, 0.28f), RuntimeResources.Load<UnitPhysicsProfile>("res://Config/Physics/ground_heavy.tres"));
		var canvas = new CanvasLayer(); AddChild(canvas);
		var help = new Label { Position = new Vector2(24, 20), Text = "飞行与受力验证场\n方向键：移动飞行单位    1：向下冲量    2：压低高度3秒\n3：打落并着地恢复    4：相同力推动轻／重身体    5：复位", Theme = null }; help.AddThemeFontSizeOverride("font_size", 22); canvas.AddChild(help);
		_status = new Label { Position = new Vector2(24, 115) }; _status.AddThemeFontSizeOverride("font_size", 20); canvas.AddChild(_status);
		_capture = Array.IndexOf(OS.GetCmdlineUserArgs(), "--flight-lab-capture") >= 0;
		if (_capture) Capture();
	}
	private static StandardMaterial3D Material(Color color) => new() { AlbedoColor = color };
	private Unit Actor(string name, Vector3 position, Color color, UnitPhysicsProfile? profile)
	{
		var unit = new Unit { Name = name, Faction = Faction.Enemy };
		unit.AddChild(new CollisionShape3D { Position = Vector3.Up * 0.9f, Shape = new CapsuleShape3D { Radius = 0.45f, Height = 1.8f } });
		unit.AddChild(new MeshInstance3D { Name = "Model", Position = Vector3.Up * 0.9f, Mesh = new CapsuleMesh { Radius = 0.45f, Height = 1.8f, Material = Material(color) } });
		unit.AddChild(new CharacterMovement { Name = "Movement", PhysicsProfile = profile });
		unit.AddChild(new Label3D { Position = Vector3.Up * 2.3f, Text = name, FontSize = 48, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled });
		AddChild(unit); unit.GlobalPosition = position; return unit;
	}
	public override void _PhysicsProcess(double delta)
	{
		Vector3 intent = new((Input.IsPhysicalKeyPressed(Key.Right) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.Left) ? 1 : 0), 0,
			(Input.IsPhysicalKeyPressed(Key.Down) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.Up) ? 1 : 0));
		_flyer.Movement!.SetIntent(intent, 4);
		_status.Text = $"状态：{_flyer.Movement.FlightState}    脚底高度：{_flyer.GlobalPosition.Y:0.00} m    目标离地高度：{_flyer.Movement.EffectiveHoverHeight:0.0} m";
	}
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
		switch (key.PhysicalKeycode)
		{
			case Key.Key1: _flyer.ReceiveKnockback(Vector3.Down * 10); break;
			case Key.Key2: _flyer.ReceiveHeightSuppression(this, 0.5f, 3); break;
			case Key.Key3: _flyer.ReceiveKnockDown(12); break;
			case Key.Key4: _light.ReceiveForce(this, Vector3.Right * 30, 1); _heavy.ReceiveForce(this, Vector3.Right * 30, 1); break;
			case Key.Key5: Reset(_flyer, new Vector3(-2, 0.05f, 0), _profile); Reset(_light, new Vector3(-4, 0.05f, 4), null); Reset(_heavy, new Vector3(1, 0.05f, 4), _heavy.Movement!.PhysicsProfile); break;
		}
	}
	private static void Reset(Unit unit, Vector3 position, UnitPhysicsProfile? profile)
	{
		unit.Movement!.ConfigurePhysics(profile); unit.Movement.Stop(); unit.Velocity = Vector3.Zero; unit.GlobalPosition = position; unit.ResetPhysicsInterpolation();
	}
	private async void Capture()
	{
		for (int i = 0; i < 120; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Error result = GetViewport().GetTexture().GetImage().SavePng("res://docs/screenshots/j34_flight_lab.png");
		GD.Print($"[FlightLab] 截图={result}"); GameExit.Request(GetTree(), result == Error.Ok ? 0 : 1);
	}
}

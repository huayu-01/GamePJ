using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实玩家武器对照场；快捷键只改本次进程内配置，不写用户存档。</summary>
public partial class CombatFeedbackLab : Node3D
{
	private Unit _player = null!;
	private Label _label = null!;
	private readonly System.Collections.Generic.List<Unit> _targets = new();
	private bool _enabled, _pause;
	private float _camera;
	private readonly PlayerLoadout _loadout = new();
	private readonly WeaponPresetKind[] _weapons = { WeaponPresetKind.SpiritSword, WeaponPresetKind.Scythe, WeaponPresetKind.Spear, WeaponPresetKind.Bow, WeaponPresetKind.EnergyPalm, WeaponPresetKind.Crossbow };
	public override async void _Ready()
	{
		var p = CombatFeedback.Profile; _enabled = p.Enabled; _pause = p.LocalPauseEnabled; _camera = p.CameraStrength;
		var floor = new StaticBody3D { Position = Vector3.Down * .5f, CollisionLayer = CombatLayers.World, CollisionMask = 0 };
		floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(50, 1, 50) } });
		floor.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(50, 1, 50), Material = new StandardMaterial3D { AlbedoColor = new Color(.12f, .16f, .21f) } } }); AddChild(floor);
		AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-60, -25, 0), ShadowEnabled = true });
		var layer = new CanvasLayer(); AddChild(layer); _label = new Label { Position = new Vector2(22, 18) }; _label.AddThemeFontSizeOverride("font_size", 20); layer.AddChild(_label);
		while (ConfigRows.ById<EnemyConfig>("enemy_chaser") == null) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
		_player.GlobalPosition = new Vector3(0, .05f, 5); _player.IsInvulnerable = true; Equip(0); Spawn();
		NodeSearch.FindInSubtree<Camera3D>(_player.GetNode("CameraRig"))?.MakeCurrent();
	}
	private void Equip(int index) => _loadout.Apply(_player, this, index < 3 ? PlayerCharacterKind.WeaponMaster : PlayerCharacterKind.Hunter, _weapons[index]);
	private void Spawn()
	{
		foreach (var target in _targets) if (IsInstanceValid(target)) target.QueueFree(); _targets.Clear();
		for (int i = 0; i < 3; i++)
		{
			var unit = new Unit { Faction = Faction.Enemy, MaxHealth = 10000, StartingArmor = i == 1 ? 80 : 0 };
			unit.AddChild(new CollisionShape3D { Name = "Body", Position = Vector3.Up * .9f, Shape = new CapsuleShape3D { Radius = .45f, Height = 1.8f } });
			var model = new Node3D { Name = "Model" }; model.AddChild(new MeshInstance3D { Position = Vector3.Up * .9f, Mesh = new CapsuleMesh { Radius = .45f, Height = 1.8f, Material = new StandardMaterial3D { AlbedoColor = new Color(.5f, .55f, .6f) } } }); unit.AddChild(model);
			unit.AddChild(new UnitAnimator { Name = UnitAnimator.NodeName });
			unit.AddChild(new UnitPresentation { Name = "Presentation", RemoveOnDeath = false });
			if (i == 2) unit.AddChild(new BlockComponent { Name = "Block", BlockDamageMultiplier = 0 });
			AddChild(unit); unit.GlobalPosition = new Vector3((i - 1) * 4, .05f, -1); unit.RotationDegrees = new Vector3(0, 180, 0); _targets.Add(unit);
			unit.AddChild(new Label3D { Position = Vector3.Up * 2.5f, Text = new[] { "无甲／打肉", "80护甲／可破甲", "正面完全格挡／可绕背" }[i], FontSize = 38, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled });
		}
	}
	public override void _UnhandledInput(InputEvent input)
	{
		if (!IsInstanceValid(_player) || input is not InputEventKey { Pressed: true, Echo: false } key) return;
		if (key.PhysicalKeycode >= Key.F1 && key.PhysicalKeycode <= Key.F6) Equip((int)key.PhysicalKeycode - (int)Key.F1);
		var p = CombatFeedback.Profile;
		if (key.PhysicalKeycode == Key.F7) p.Enabled = !p.Enabled;
		if (key.PhysicalKeycode == Key.F8) p.LocalPauseEnabled = !p.LocalPauseEnabled;
		if (key.PhysicalKeycode == Key.F9) p.CameraStrength = p.CameraStrength > 0 ? 0 : 1;
		if (key.PhysicalKeycode == Key.Home) { Spawn(); _player.GlobalPosition = new Vector3(0, .05f, 5); _player.Velocity = Vector3.Zero; _player.ResetPhysicsInterpolation(); }
	}
	public override void _Process(double delta)
	{
		var p = CombatFeedback.Profile;
		_label.Text = $"打击反馈对照场（机制占位）\nF1 灵剑  F2 镰刀  F3 长枪  F4 弓  F5 能量掌  F6 飞刀\nF7 反馈 {p.Enabled}  F8 近战顿帧 {p.LocalPauseEnabled}  F9 镜头 {p.CameraStrength:0.0}  Home复位\n沿用WASD／鼠标／左键／右键／技能操作\n最近结果 {CombatFeedback.LastKind}  反馈 {CombatFeedback.PresentedCount}  镜头 {CombatFeedback.CameraCount}";
	}
	public override void _ExitTree() { var p = CombatFeedback.Profile; p.Enabled = _enabled; p.LocalPauseEnabled = _pause; p.CameraStrength = _camera; }
}

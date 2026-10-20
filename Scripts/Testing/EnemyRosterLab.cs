using System;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Run;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>敌人机制试玩场：F1到F11切换原型，F12生成有甲精英；数字键保留玩家技能。</summary>
public partial class EnemyRosterLab : Node3D
{
	private readonly string[] _keys = { "burrow_hunter", "spear_guard", "arc_thrower", "meteor_mage", "tracking_charger", "hover_diver", "heavy_slammer", "blade_spinner", "bomb_lobber", "arc_bomber", "combo_fighter" };
	private Unit _player = null!;
	private Unit? _enemy;
	private Label _status = null!;
	private int _selected;
	private bool _capture;
	public override async void _Ready()
	{
		var floor = new StaticBody3D { Position = Vector3.Down * .5f, CollisionLayer = CombatLayers.World, CollisionMask = 0 };
		floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(40, 1, 40) } });
		floor.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(40, 1, 40), Material = new StandardMaterial3D { AlbedoColor = new Color(.12f, .16f, .21f) } } }); AddChild(floor);
		AddChild(new MapManager { BakeDelaySeconds = .1f });
		AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-60, -25, 0), ShadowEnabled = true });
		var camera = new Camera3D { Position = new Vector3(12, 17, 19), Projection = Camera3D.ProjectionType.Orthogonal, Size = 22, Current = true };
		AddChild(camera); camera.LookAt(new Vector3(0, .8f, 0));
		var canvas = new CanvasLayer(); AddChild(canvas);
		var help = new Label { Position = new Vector2(22, 18), Text = "敌人机制原型（胶囊占位）\nF1 地鼠  F2 矛盾  F3 投掷  F4 落弹法师  F5 追踪冲锋  F6 俯冲\nF7 重击  F8 回旋  F9 抛弹  F10 抛弹投掷  F11 连招  F12 当前精英\nWASD移动／鼠标瞄准／沿用玩家技能；Home复位；玩家不掉血" };
		help.AddThemeFontSizeOverride("font_size", 20); canvas.AddChild(help);
		_status = new Label { Position = new Vector2(22, 174) }; _status.AddThemeFontSizeOverride("font_size", 20); canvas.AddChild(_status);
		_capture = Array.IndexOf(OS.GetCmdlineUserArgs(), "--roster-lab-capture") >= 0;
		// 首个PhysicsFrame可能早于autoload的延迟索引；按实际就绪状态等待。
		while (ConfigRows.ById<EnemyConfig>("enemy_burrow_hunter") == null)
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		if (_capture) { Showcase(); Capture(); return; }
		_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
		_player.GlobalPosition = new Vector3(0, .05f, 4); _player.IsInvulnerable = true;
		new PlayerLoadout().Apply(_player, this, PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
		var rig = _player.GetNode<Node3D>("CameraRig");
		NodeSearch.FindInSubtree<Camera3D>(rig)?.MakeCurrent(); Spawn(false);
	}
	private Unit Make(string key, Vector3 position, bool elite, bool brain)
	{
		var row = ConfigRows.ById<EnemyConfig>("enemy_" + key)!;
		var enemy = RuntimeResources.Load<PackedScene>(row.ScenePath).Instantiate<Unit>(); row.ApplyTo(enemy);
		if (elite) enemy.AddChild(new EliteModifier()); AddChild(enemy); enemy.GlobalPosition = position;
		enemy.GetNode("Brain").SetPhysicsProcess(brain);
		enemy.AddChild(new Label3D { Position = Vector3.Up * 2.5f, Text = row.DisplayName + "\n压力 " + row.SpawnResourceCost + (elite ? "／有甲精英" : ""), FontSize = 38, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled });
		return enemy;
	}
	private void Spawn(bool elite)
	{
		if (IsInstanceValid(_enemy)) _enemy!.QueueFree();
		foreach (var node in GetTree().GetNodesInGroup("projectiles")) node.QueueFree();
		_enemy = Make(_keys[_selected], new Vector3(0, .05f, -5), elite, true);
	}
	public override void _UnhandledInput(InputEvent input)
	{
		if (_capture || input is not InputEventKey { Pressed: true, Echo: false } key) return;
		if (key.PhysicalKeycode >= Key.F1 && key.PhysicalKeycode <= Key.F11) { _selected = (int)key.PhysicalKeycode - (int)Key.F1; Spawn(false); }
		else if (key.PhysicalKeycode == Key.F12) Spawn(true);
		else if (key.PhysicalKeycode == Key.Home)
		{
			_player.GlobalPosition = new Vector3(0, .05f, 4); _player.Velocity = Vector3.Zero; _player.ResetPhysicsInterpolation(); Spawn(false);
		}
	}
	public override void _Process(double delta)
	{
		if (_capture || !IsInstanceValid(_enemy)) return;
		var ability = _enemy!.GetNode<EnemyPlanAbility>("AbilityPlan");
		_status.Text = $"动作：{ability.CurrentKind}／执行中 {ability.IsBusy}  真实ATK {ability.ExecutedAttackCount}  护甲 {_enemy.Armor:0}  血量 {_enemy.Health:0}";
	}
	private void Showcase()
	{
		for (int i = 0; i < _keys.Length; i++) Make(_keys[i], new Vector3((i % 4 - 1.5f) * 4, .05f, (i / 4 - 1) * 4), false, false);
		_enemy = Make("heavy_slammer", new Vector3(6, .05f, 4), true, false);
		_status.Text = "11类地面敌人＋有甲精英对照；原型差异以动作与数值配置为准。";
	}
	private async void Capture()
	{
		await ToSignal(GetTree().CreateTimer(.8), SceneTreeTimer.SignalName.Timeout);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Error result = GetViewport().GetTexture().GetImage().SavePng("res://docs/screenshots/j36_enemy_roster.png");
		_enemy!.GetNode<UnitPresentation>("Presentation").FlashAttack();
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Error flash = GetViewport().GetTexture().GetImage().SavePng("res://docs/screenshots/j36_attack_warning.png");
		GD.Print($"[RosterLab] 截图={result}／闪色={flash}");
		GameExit.Request(GetTree(), result == Error.Ok && flash == Error.Ok ? 0 : 1);
	}
}

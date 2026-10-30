using System;
using System.Linq;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>重击敌人的独立美术展示与实战开关，只挂在自然美术样板中。</summary>
public partial class StoneSlammerShowcase : Node3D
{
	[Export] public PackedScene EnemyScene { get; set; } = null!;
	[Export] public EnemyConfig EnemyRow { get; set; } = null!;
	[Export] public Vector3 SpawnPosition { get; set; } = new(0, 0.08f, -1.8f);
	private Unit _enemy = null!;
	private EnemyPlanAbility _ability = null!;
	private PlayerController _player = null!;
	private Camera3D _camera = null!;
	private Label _label = null!;
	private CanvasLayer _hud = null!;
	private bool _combat;

	public override void _Ready()
	{
		_player = GetParent().GetNode<PlayerController>("Player");
		_enemy = EnemyScene.Instantiate<Unit>();
		EnemyRow.ApplyTo(_enemy);
		_enemy.Position = SpawnPosition;
		_enemy.Rotation = new Vector3(0, Mathf.Pi, 0);
		_enemy.GetNode("Brain").ProcessMode = ProcessModeEnum.Disabled;
		AddChild(_enemy);
		(GetParent() as NatureCombatLab)?.RegisterTestEnemy(_enemy);
		_ability = _enemy.GetNode<EnemyPlanAbility>("AbilityPlan");
		_camera = new Camera3D { Name = "SlammerCloseup", Fov = 54, Far = 140 };
		AddChild(_camera);
		_camera.GlobalPosition = new Vector3(5.8f, 3.25f, 4.5f);
		_camera.LookAt(SpawnPosition + Vector3.Up * 1.55f);
		_hud = new CanvasLayer { Name = "SlammerHud" };
		AddChild(_hud);
		_label = new Label { Position = new Vector2(24, 990), MouseFilter = Control.MouseFilterEnum.Ignore };
		_label.AddThemeFontSizeOverride("font_size", 22);
		_label.AddThemeColorOverride("font_outline_color", new Color(0.04f, 0.07f, 0.04f));
		_label.AddThemeConstantOverride("outline_size", 7);
		_hud.AddChild(_label);
		foreach (string argument in OS.GetCmdlineUserArgs())
		{
			if (argument.StartsWith("--slammer-capture=")) Capture(argument.Split('=')[1]);
			if (argument == "--slammer-smoke") Validate();
		}
	}

	public override void _Process(double delta)
	{
		if (!IsInstanceValid(_enemy))
		{
			_label.Text = "重击敌人已被击败 · F6 重置样板";
			return;
		}
		_label.Text = $"重击敌人 · 几何样件 · {(_combat ? "实战中" : "展示中")} · 生命 {_enemy.Health:0}/{_enemy.MaxHealth:0}\nF7 展示 / 实战 · F8 抡锤动作 · F9 近景 / 跟随 · F6 重置";
	}

	public override void _UnhandledInput(InputEvent input)
	{
		if (input is not InputEventKey { Pressed: true, Echo: false } key) return;
		if (key.PhysicalKeycode == Key.F7 && IsInstanceValid(_enemy) && !_enemy.IsDead)
		{
			SetCombat(!_combat);
			GetViewport().SetInputAsHandled();
		}
		if (key.PhysicalKeycode == Key.F8 && !_combat && IsInstanceValid(_enemy) && !_enemy.IsDead)
		{
			_enemy.GetNode<UnitAnimator>("Animator").PlaySkillAnimation("EnemyAction", 0.55f);
			GetViewport().SetInputAsHandled();
		}
		if (key.PhysicalKeycode == Key.F9)
		{
			bool closeup = !_camera.Current;
			if (closeup) _camera.MakeCurrent();
			else _player.GetNode<Camera3D>("CameraRig/SpringArm/Camera").MakeCurrent();
			Input.MouseMode = closeup ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
			GetViewport().SetInputAsHandled();
		}
	}

	private void SetCombat(bool enabled)
	{
		_combat = enabled;
		_enemy.GetNode("Brain").ProcessMode = enabled ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
		if (!enabled)
		{
			// 展示暂停使用公开生命周期入口，取消动作并重置冷却。
			_ability.ResetForLoadout();
			_enemy.Movement?.Stop();
			_enemy.Velocity = Vector3.Zero;
			_enemy.GlobalPosition = SpawnPosition;
			_enemy.Rotation = new Vector3(0, Mathf.Pi, 0);
			_enemy.ResetPhysicsInterpolation();
		}
	}

	private AnimationPlayer Playback => NodeSearch.FindInSubtree<AnimationPlayer>(_enemy.GetNode("Model"))!;

	private async void Capture(string view)
	{
		for (int frame = 0; frame < 100; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		if (view != "follow")
		{
			_camera.MakeCurrent();
			_hud.Visible = false;
			GetParent().GetNode<CanvasLayer>("LabHud").Visible = false;
		}
		if (view is "windup" or "impact")
		{
			_enemy.GetNode<UnitAnimator>("Animator").ProcessMode = ProcessModeEnum.Disabled;
			Playback.Play("slam", 0);
			Playback.Seek(view == "windup" ? 0.43 : 0.55, true);
			Playback.Advance(0);
			Playback.Pause();
			if (view == "windup")
			{
				_camera.GlobalPosition = new Vector3(6.5f, 4.0f, 5.5f);
				_camera.LookAt(SpawnPosition + Vector3.Up * 2);
			}
		}
		for (int frame = 0; frame < 24; frame++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		string path = $"res://docs/screenshots/stone_slammer_{view}.png";
		Error error = GetViewport().GetTexture().GetImage().SavePng(path);
		GD.Print($"[StoneSlammer] screenshot={error} path={path}");
		GameExit.Request(GetTree(), error == Error.Ok ? 0 : 1);
	}

	private async void Validate()
	{
		for (int frame = 0; frame < 90; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		var skeleton = NodeSearch.FindInSubtree<Skeleton3D>(_enemy.GetNode("Model"))!;
		bool animations = new[] { "idle", "walk", "sprint", "slam", "hit", "die", "skills/EnemyAction" }.All(name => Playback.HasAnimation(name));
		bool durationAligned = Math.Abs(Playback.GetAnimation("slam").Length - 0.9) < 0.01;
		Vector3 start = _enemy.GlobalPosition;
		SetCombat(true);
		for (int frame = 0; frame < 60; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		float movement = start.DistanceTo(_enemy.GlobalPosition);
		_player.GlobalPosition = _enemy.GlobalPosition - _enemy.GlobalBasis.Z * 2.2f;
		_player.Velocity = Vector3.Zero;
		_player.ResetPhysicsInterpolation();
		float before = _player.Health + _player.Armor + _player.Shield;
		int events = 0;
		void OnEvent(string name, ulong id) { if (name == "ATK") events++; }
		_enemy.GetNode<UnitAnimator>("Animator").SkillEvent += OnEvent;
		for (int frame = 0; frame < 150; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		_enemy.GetNode<UnitAnimator>("Animator").SkillEvent -= OnEvent;
		float damage = before - _player.Health - _player.Armor - _player.Shield;
		var body = _enemy.GetNode<CollisionShape3D>("Body").Shape as CapsuleShape3D;
		bool passed = animations && durationAligned && skeleton.GetBoneCount() == 23 && movement > 0.5f && events > 0 && _ability.ExecutedAttackCount > 0 && damage > 0 && body?.Height > 2.9f && MapManager.Instance?.IsReady == true;
		var report = new Godot.Collections.Dictionary
		{
			["passed"] = passed, ["bones"] = skeleton.GetBoneCount(), ["animations_present"] = animations,
			["animation_timing_aligned"] = durationAligned,
			["movement_meters"] = movement, ["attack_events"] = events, ["executed_slams"] = _ability.ExecutedAttackCount,
			["player_damage"] = damage, ["collision_height"] = body?.Height ?? 0,
			["navigation_ready"] = MapManager.Instance?.IsReady == true,
		};
		using var file = FileAccess.Open("res://docs/screenshots/stone_slammer_validation.json", FileAccess.ModeFlags.Write);
		file.StoreString(Json.Stringify(report, "  "));
		GD.Print("[StoneSlammer] " + Json.Stringify(report));
		GameExit.Request(GetTree(), passed ? 0 : 1);
	}
}

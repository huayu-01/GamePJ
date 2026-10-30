using System;
using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>自然场景和生成人形的独立战斗验收入口，复用正式移动、动画事件和命中逻辑。</summary>
public partial class NatureCombatLab : Node3D
{
	[Export] public PackedScene? PilotWeapon { get; set; }
	[Export] public Vector3 PilotWeaponRotation { get; set; } = Vector3.Zero;
	[Export] public float PilotWeaponScale { get; set; } = 0.7f;
	[Export] public string LabTitle { get; set; } = "林间战斗场 · 120×120 米 · 生成人形 / UAL 动作";
	[Export] public bool SpawnTrainingTargets { get; set; } = true;
	[Export] public float ArenaHalfSize { get; set; } = 60f;
	[Export] public string CapturePrefix { get; set; } = "nature_arena";
	[Export] public bool EnableArtAntialiasing { get; set; }
	[Export] public float EnemySpawnMinRadius { get; set; } = 10f;
	[Export] public float EnemySpawnMaxRadius { get; set; } = 18f;
	private PlayerController _player = null!;
	private MeleeComboAbility _sword = null!;
	private Camera3D _overview = null!;
	private Camera3D _follow = null!;
	private Label _status = null!;
	private float _statusTimer;
	private int _spawnSerial;
	private readonly List<Unit> _testEnemies = new();
	private bool _airPose;
	public void RegisterTestEnemy(Unit unit)
	{
		if (!_testEnemies.Contains(unit)) _testEnemies.Add(unit);
	}

	public override void _Ready()
	{
		if (EnableArtAntialiasing)
		{
			GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;
			GetViewport().UseTaa = true;
		}
		_player = GetNode<PlayerController>("Player");
		_player.GlobalPosition = GetNode<Marker3D>("NatureArena/CombatMarkers/PlayerStart").GlobalPosition;
		_player.ResetPhysicsInterpolation();
		_sword = _player.GetNode<MeleeComboAbility>("SwordAttack");
		_overview = GetNode<Camera3D>("Overview");
		_overview.LookAt(GetNode<Marker3D>("NatureArena/CombatMarkers/Center").GlobalPosition);
		_follow = _player.GetNode<Camera3D>("CameraRig/SpringArm/Camera");
		PrepareAttackEvent();
		_player.Movement!.JumpStarted += OnJumpStarted;
		AttachSword();
		BuildHelp();
		if (SpawnTrainingTargets)
		{
			SpawnDummy(new Vector3(-4, 0.05f, -3));
			SpawnDummy(new Vector3(4, 0.05f, -3));
		}
		foreach (string argument in OS.GetCmdlineUserArgs())
		{
			if (argument == "--nature-smoke") RunSmoke();
			if (argument.StartsWith("--nature-capture=")) Capture(argument.Split('=')[1]);
		}
	}

	private void PrepareAttackEvent()
	{
		AnimationPlayer playback = NodeSearch.FindInSubtree<AnimationPlayer>(_player.GetNode("Model"))!;
		var library = new AnimationLibrary();
		foreach (string name in new[] { "attack", "attack_b" })
		{
			Animation clip = (Animation)playback.GetAnimation(name).Duplicate(true);
			int track = clip.AddTrack(Animation.TrackType.Method);
			clip.TrackSetPath(track, new NodePath("."));
			clip.TrackInsertKey(track, name == "attack" ? 0.18 : 0.21, new Godot.Collections.Dictionary
			{
				["method"] = "anim_event", ["args"] = new Godot.Collections.Array { "ATK" }
			});
			// 仍由正式 UnitAnimator 读取事件帧，关闭引擎直接方法回调。
			clip.TrackSetEnabled(track, false);
			library.AddAnimation(name, clip);
		}
		playback.AddAnimationLibrary("skills", library);
		Animation jump = (Animation)playback.GetAnimation("jump").Duplicate(true);
		jump.LoopMode = Animation.LoopModeEnum.Linear;
		library.AddAnimation("jump", jump);
		_sword.AnimName = "skills/attack";
	}

	private async void AttachSword()
	{
		Skeleton3D skeleton = NodeSearch.FindInSubtree<Skeleton3D>(_player.GetNode("Model"))!;
		var attachment = new BoneAttachment3D { Name = "WeaponSocket", BoneName = "RightHand" };
		skeleton.AddChild(attachment);
		Node3D weapon = (PilotWeapon ?? RuntimeResources.Load<PackedScene>("res://Scenes/Weapons/Visuals/NaturePilotSword.tscn")).Instantiate<Node3D>();
		attachment.AddChild(weapon);
		weapon.Name = "PilotSword";
		weapon.Scale = Vector3.One * PilotWeaponScale;
		weapon.Position = new Vector3(0, 0.055f, 0);
		// 以待机姿势标定一次握持轴，之后保留局部变换，让剑随真实手骨挥动。
		for (int frame = 0; frame < 2; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (!IsInstanceValid(skeleton) || !IsInstanceValid(weapon)) return;
		Basis hand = (skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(skeleton.FindBone("RightHand"))).Basis;
		Vector3 blade = new Vector3(0, -0.6f, -0.8f).Normalized();
		var desired = new Basis(Vector3.Right, blade, Vector3.Right.Cross(blade));
		weapon.Basis = hand.Inverse() * _player.GlobalBasis * desired * Basis.FromEuler(PilotWeaponRotation * (Mathf.Pi / 180f)) * Basis.FromScale(Vector3.One * PilotWeaponScale);
	}

	private void BuildHelp()
	{
		var canvas = new CanvasLayer { Name = "LabHud" };
		AddChild(canvas);
		var panel = new PanelContainer { Position = new Vector2(22, 18), MouseFilter = Control.MouseFilterEnum.Ignore };
		var style = new StyleBoxFlat { BgColor = new Color(0.055f, 0.09f, 0.07f, 0.84f), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 12, ContentMarginBottom = 12 };
		panel.AddThemeStyleboxOverride("panel", style);
		canvas.AddChild(panel);
		_status = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
		_status.AddThemeFontSizeOverride("font_size", 19);
		panel.AddChild(_status);
	}

	public override void _Process(double delta)
	{
		_statusTimer -= (float)delta;
		if (_statusTimer > 0) return;
		_statusTimer = 0.15f;
		int alive = 0;
		foreach (Unit unit in _testEnemies) if (IsInstanceValid(unit) && !unit.IsDead) alive++;
		_status.Text = $"{LabTitle}\nWASD 移动 · 空格跳跃 · 左键挥剑 · F5 刷怪 · F6 重置 · Tab 总览 · Esc 鼠标\n生命 {_player.Health:0}/{_player.MaxHealth:0}   敌人 {alive}   有效挥击 {_sword.StrikeCount}   导航 {MapManager.Instance?.PolygonCount ?? 0}";
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_player.IsDead) return;
		UnitAnimator animator = _player.GetNode<UnitAnimator>(UnitAnimator.NodeName);
		if (_airPose && (_player.IsOnFloor() || _sword.IsBusy))
		{
			if (!_sword.IsBusy) animator.StopSkillAnimation();
			_airPose = false;
		}
	}

	private void OnJumpStarted(Vector3 origin)
	{
		if (_sword.IsBusy || _player.IsDead) return;
		_player.GetNode<UnitAnimator>(UnitAnimator.NodeName).PlaySkillAnimation("skills/jump", 1f);
		_airPose = true;
	}

	public override void _ExitTree()
	{
		if (IsInstanceValid(_player) && IsInstanceValid(_player.Movement)) _player.Movement!.JumpStarted -= OnJumpStarted;
	}

	public override void _UnhandledInput(InputEvent input)
	{
		if (input is not InputEventKey { Pressed: true, Echo: false } key) return;
		switch (key.PhysicalKeycode)
		{
			case Key.Tab: SetOverview(!_overview.Current); break;
			case Key.F5: SpawnWave(); break;
			case Key.F6: GetTree().ReloadCurrentScene(); break;
		}
	}

	private void SetOverview(bool active)
	{
		_overview.Current = active;
		_follow.Current = !active;
		Input.MouseMode = active ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
	}

	private Unit SpawnDummy(Vector3 position)
	{
		Unit unit = RuntimeResources.Load<PackedScene>("res://Scenes/Training/TrainingDummy.tscn").Instantiate<Unit>();
		unit.Name = "TrainingTarget" + _spawnSerial++;
		unit.Position = position;
		unit.AddChild(new UnitAnimator { Name = UnitAnimator.NodeName });
		AddChild(unit);
		_testEnemies.Add(unit);
		return unit;
	}

	private void SpawnWave()
	{
		for (int index = 0; index < 5; index++)
		{
			if (!TryFindEnemySpawn(out Vector3 spawn)) continue;
			Unit enemy = RuntimeResources.Load<PackedScene>("res://Scenes/Enemies/Chaser.tscn").Instantiate<Unit>();
			enemy.Name = "NatureChaser" + _spawnSerial++;
			ConfigRows.ById<EnemyConfig>("enemy_chaser")?.ApplyTo(enemy);
			enemy.Position = spawn;
			AddChild(enemy);
			_testEnemies.Add(enemy);
		}
	}

	private bool TryFindEnemySpawn(out Vector3 spawn)
	{
		// 坡地可能产生孤立导航面，测试场只在与玩家连通的位置刷怪。
		for (int attempt = 0; attempt < 24; attempt++)
		{
			spawn = MapManager.Instance!.SpawnPoint(_player.GlobalPosition, EnemySpawnMinRadius, EnemySpawnMaxRadius);
			Vector3[] path = NavigationServer3D.MapGetPath(GetWorld3D().NavigationMap, spawn, _player.GlobalPosition, true);
			if (path.Length > 0 && path[^1].DistanceTo(_player.GlobalPosition) < 1.5f) return true;
		}
		spawn = Vector3.Zero;
		return false;
	}

	private async void Capture(string view)
	{
		for (int index = 0; index < 90; index++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		if (view == "overview") SetOverview(true);
		else if (view == "follow") SetOverview(false);
		else if (GetNodeOrNull<Camera3D>($"NatureArena/ArtViews/{view}") is Camera3D artCamera)
		{
			artCamera.Current = true;
			GetNode<CanvasLayer>("LabHud").Visible = false;
		}
		else
		{
			_follow.Current = false;
			var camera = new Camera3D { Position = _player.GlobalPosition + new Vector3(4, 2.8f, -5), Current = true, Fov = 58 };
			AddChild(camera);
			camera.LookAt(_player.GlobalPosition + Vector3.Up);
		}
		// 留出新镜头的时域采样收敛时间，截图与实际停留后的画面一致。
		for (int frame = 0; frame < 24; frame++)
			await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		string path = $"res://Logs/evidence/screenshots/{CapturePrefix}_{view}.png";
		Error result = GetViewport().GetTexture().GetImage().SavePng(ActRoguelike.Testing.ValidationArtifacts.Path(path));
		GD.Print($"[NatureArena] screenshot={result} path={path}");
		GameExit.Request(GetTree(), result == Error.Ok ? 0 : 1);
	}

	private async void RunSmoke()
	{
		for (int index = 0; index < 90; index++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		Vector3 start = _player.GlobalPosition;
		Input.ActionPress("move_forward");
		for (int index = 0; index < 60; index++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		Input.ActionRelease("move_forward");
		float distance = _player.GlobalPosition.DistanceTo(start);
		for (int index = 0; index < 10; index++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		Unit target = SpawnDummy(_player.GlobalPosition - _player.GlobalBasis.Z * 2.2f);
		float before = target.Health + target.Armor + target.Shield;
		bool activated = _sword.TryPress(target);
		for (int index = 0; index < 16; index++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		_sword.TryPress(target);
		for (int index = 0; index < 90; index++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		float damage = before - target.Health - target.Armor - target.Shield;
		float baseline = _player.GlobalPosition.Y;
		float jumpPeak = baseline;
		Input.ActionPress("jump");
		for (int index = 0; index < 3; index++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		Input.ActionRelease("jump");
		for (int index = 0; index < 65; index++)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			jumpPeak = Mathf.Max(jumpPeak, _player.GlobalPosition.Y);
		}
		SpawnWave();
		var starts = new Dictionary<Unit, float>();
		foreach (Unit enemy in _testEnemies)
			if (IsInstanceValid(enemy) && enemy.Name.ToString().StartsWith("NatureChaser")) starts[enemy] = enemy.GlobalPosition.DistanceTo(_player.GlobalPosition);
		for (int index = 0; index < 120; index++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		float approach = 0;
		int reachableEnemies = 0;
		var enemyApproaches = new Godot.Collections.Array<float>();
		foreach (var pair in starts)
		{
			float progress = pair.Value - pair.Key.GlobalPosition.DistanceTo(_player.GlobalPosition);
			approach += progress;
			enemyApproaches.Add(progress);
			Vector3[] path = NavigationServer3D.MapGetPath(GetWorld3D().NavigationMap, pair.Key.GlobalPosition, _player.GlobalPosition, true);
			if (path.Length > 0 && path[^1].DistanceTo(_player.GlobalPosition) < 1.5f) reachableEnemies++;
		}
		approach /= Mathf.Max(1, starts.Count);
		bool navReady = MapManager.Instance?.IsReady == true;
		bool pathsClear = true;
		foreach (string marker in new[] { "North", "South", "West", "East" })
			pathsClear &= MapManager.Instance!.IsWalkable(GetNode<Marker3D>($"NatureArena/CombatMarkers/{marker}").GlobalPosition);
		var edgeRay = PhysicsRayQueryParameters3D.Create(new Vector3(ArenaHalfSize-2,1,0), new Vector3(ArenaHalfSize+1,1,0), 1);
		bool boundary = GetWorld3D().DirectSpaceState.IntersectRay(edgeRay).Count > 0;
		bool passed = navReady && pathsClear && boundary && distance > 4 && jumpPeak - baseline > 0.5f && activated && _sword.StrikeCount == 2 && damage > 0 && approach > 0.5f && starts.Count == 5 && reachableEnemies == starts.Count;
		var report = new Godot.Collections.Dictionary { ["passed"] = passed, ["navigation_ready"] = navReady, ["navigation_polygons"] = MapManager.Instance?.PolygonCount ?? 0, ["movement_meters"] = distance, ["jump_height"] = jumpPeak - baseline, ["attack_activated"] = activated, ["animation_event_strikes"] = _sword.StrikeCount, ["damage"] = damage, ["floor_height"] = _player.GlobalPosition.Y, ["cardinal_paths_clear"] = pathsClear, ["boundary_collision"] = boundary, ["spawned_enemies"] = starts.Count, ["reachable_enemies"] = reachableEnemies, ["enemy_approaches"] = enemyApproaches, ["enemy_approach_meters"] = approach };
		using var file = FileAccess.Open($"res://Logs/evidence/screenshots/{CapturePrefix}_validation.json", FileAccess.ModeFlags.Write);
		file.StoreString(Json.Stringify(report, "  "));
		GD.Print("[NatureArena] " + Json.Stringify(report));
		GameExit.Request(GetTree(), passed ? 0 : 1);
	}
}

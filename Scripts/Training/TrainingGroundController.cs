using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Interaction;
using ActRoguelike.Player;
using ActRoguelike.Run;
using ActRoguelike.Rewards;
using ActRoguelike.UI;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Training;

/// <summary>
/// 071 单区域训练场：召唤木桩/敌人、三选一增幅、重置、返回基地。
/// 不挂 RunController，不写存档；重置通过重载当前场景完成。
/// </summary>
public partial class TrainingGroundController : Node3D
{
	[Export] public NodePath PlayerPath { get; set; } = "Player";
	[Export] public NodePath SpawnPointPath { get; set; } = "SpawnPoint";
	[Export] public NodePath SpawnRootPath { get; set; } = "SpawnRoot";
	[Export] public NodePath SpawnConsolePath { get; set; } = "SpawnConsole/InteractionComponent";
	[Export] public NodePath BuffConsolePath { get; set; } = "BuffConsole/InteractionComponent";
	[Export] public NodePath ResetConsolePath { get; set; } = "ResetConsole/InteractionComponent";
	[Export] public NodePath ReturnConsolePath { get; set; } = "ReturnGate/InteractionComponent";

	private Unit _player = null!;
	private Node3D _spawnPoint = null!;
	private Node3D _spawnRoot = null!;
	private Weapon? _weapon;
	private RunBuild _build = new();
	private readonly RewardPool _pool = new();
	private readonly List<RewardDefinition> _pendingOptions = new();
	private RewardScreen? _rewardScreen;
	private TrainingSpawnScreen? _spawnScreen;
	private Label? _hud;
	private double _resetTimer = -1;
	private int _spawnSerial;

	public Unit Player => _player;
	public RunBuild Build => _build;
	public int PendingChoiceCount => _pendingOptions.Count;

	public override void _Ready()
	{
		_player = GetNode<Unit>(PlayerPath);
		_spawnPoint = GetNode<Node3D>(SpawnPointPath);
		_spawnRoot = GetNode<Node3D>(SpawnRootPath);
		SetupPlayer();

		GetNode<InteractionComponent>(SpawnConsolePath).Triggered += OpenSpawnPanel;
		GetNode<InteractionComponent>(BuffConsolePath).Triggered += OpenBuffPanel;
		GetNode<InteractionComponent>(ResetConsolePath).Triggered += ResetTraining;
		GetNode<InteractionComponent>(ReturnConsolePath).Triggered += ReturnToBase;

		_spawnScreen = new TrainingSpawnScreen { Name = "SpawnScreen" };
		AddChild(_spawnScreen);
		var rewardScene = RuntimeResources.Load<PackedScene>("res://Scenes/UI/RewardScreen.tscn");
		if (rewardScene != null)
		{
			_rewardScreen = rewardScene.Instantiate<RewardScreen>();
			AddChild(_rewardScreen);
			_rewardScreen.RewardChosen += OnRewardChosen;
		}
		BuildHud();
	}

	private async void SetupPlayer()
	{
		while (IsInsideTree() && ConfigRows.Count<CharacterConfig>() == 0)
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		if (!IsInsideTree() || _player == null) return;
		new PlayerLoadout().Apply(_player, this, RunSetup.Character, RunSetup.Weapon);
		_player.GlobalPosition = new Vector3(0, .1f, 14);
		_player.ResetPhysicsInterpolation();
		_weapon = NodeSearch.FindInSubtree<Weapon>(_player);
		if (_weapon != null)
		{
			string characterId = RunController.CharacterRow(RunSetup.Character)?.Id ?? string.Empty;
			_pool.Initialize(_weapon, characterId, "training_ground");
		}
		UpdateHud();
	}

	public void OpenSpawnPanel()
	{
		if (_spawnScreen == null) return;
		if (_spawnScreen.Visible) { _spawnScreen.Close(); return; }
		_spawnScreen.Open(this);
	}

	public void OpenBuffPanel()
	{
		if (_rewardScreen == null || _weapon == null)
		{
			_spawnScreen?.SetStatus("没有可用武器/奖励界面，无法打开增幅。");
			return;
		}
		_pendingOptions.Clear();
		_pendingOptions.AddRange(_pool.Roll(_build, 3));
		if (_pendingOptions.Count == 0)
		{
			_spawnScreen?.SetStatus("当前没有合法增幅候选；可先重置或继续召唤测试。");
			return;
		}
		_rewardScreen.Open(_pendingOptions, "测试场 · 选择增幅");
	}

	private void OnRewardChosen(int index)
	{
		if (_rewardScreen == null || _weapon == null || index < 0 || index >= _pendingOptions.Count) return;
		RewardDefinition row = _pendingOptions[index];
		try
		{
			_build.Add(row, new RewardContext { Player = _player, Weapon = _weapon, Build = _build });
		}
		catch (Exception ex)
		{
			GD.PushError($"训练场增幅应用失败：{row.Id}：{ex.Message}");
		}
		_pendingOptions.Clear();
		_rewardScreen.Close();
		UpdateHud();
	}

	public void SpawnDummy(string kind, int count, bool elite)
	{
		if (kind == "group")
		{
			SpawnGroup();
			_spawnScreen?.SetStatus("已召唤木桩群 ×5。");
			return;
		}
		count = Mathf.Clamp(count, 1, 5);
		for (int i = 0; i < count; i++) SpawnOneDummy(kind, i, count);
		_spawnScreen?.SetStatus($"已召唤 {DummyLabel(kind)} ×{count}。");
	}

	private void SpawnGroup()
	{
		const int count = 5;
		for (int i = 0; i < count; i++)
		{
			Unit? unit = MakeDummy("normal", 150f, 0f);
			if (unit == null) continue;
			Place(unit, SpawnPosition(i, count, 2.2f), "木桩群");
			unit.Scale = Vector3.One * .9f;
		}
	}

	private void SpawnOneDummy(string kind, int index, int count)
	{
		float hp = kind switch
		{
			"armor" => 500f,
			"block" => 500f,
			"invincible" => 600f,
			"shooter" => 300f,
			"area" => 400f,
			_ => 600f,
		};
		float armor = kind == "normal" ? 60f : 0f;
		Unit? unit = MakeDummy(kind, hp, armor);
		if (unit == null) return;
		Place(unit, SpawnPosition(index, count, 2.1f), DummyLabel(kind));
	}

	private Unit? MakeDummy(string kind, float hp, float armor)
	{
		string scenePath = kind switch
		{
			"shooter" => "res://Scenes/Training/TrainingShooter.tscn",
			"area" => "res://Scenes/Training/TrainingAttacker.tscn",
			_ => "res://Scenes/Training/TrainingDummy.tscn",
		};
		var scene = RuntimeResources.Load<PackedScene>(scenePath);
		if (scene == null) return null;
		Unit unit = scene.Instantiate<Unit>();
		unit.ApplyBaseStats(hp, armor, 0, 0);
		unit.ArmorDamageReduction = .3f;
		unit.KnockbackResistance = 1f;
		if (kind == "armor") unit.AddChild(new TrainingArmorDummy { Name = "TrainingArmorDummy" });
		if (kind == "block")
		{
			unit.AddChild(new BlockComponent
			{
				Name = "Block",
				Blocking = true,
				BlockDamageMultiplier = .2f,
				FrontDotThreshold = -.5f,
				BlockStunSeconds = 0f,
			});
			unit.AddChild(new TrainingBlockDummy { Name = "TrainingBlockDummy" });
		}
		if (kind == "invincible") unit.IsInvulnerable = true;
		if (kind == "area") ConfigureAreaDummy(unit);
		return unit;
	}

	private static void ConfigureAreaDummy(Unit unit)
	{
		var ability = unit.GetNodeOrNull<MeleeAttackAbility>("AbilityMelee");
		if (ability != null)
		{
			ability.AttackRange = 5f;
			ability.Cooldown = 2.5f;
			ability.Windup = .8f;
			ability.Recover = .6f;
		}
		var hitbox = unit.GetNodeOrNull<AttackHitbox>("AbilityMelee/Attack");
		if (hitbox != null)
		{
			hitbox.Position = new Vector3(0, .9f, -2.5f);
			if (hitbox.GetNodeOrNull<CollisionShape3D>("Shape")?.Shape is BoxShape3D box)
				box.Size = new Vector3(4f, 1.6f, 4f);
		}
	}

	private void Place(Unit unit, Vector3 position, string label)
	{
		UnitAnimator.Ensure(unit);
		_spawnRoot.AddChild(unit);
		unit.GlobalPosition = position;
		unit.AddToGroup("training_spawn");
		var text = new Label3D
		{
			Text = label,
			Position = new Vector3(0, 2.8f, 0),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FontSize = 36,
			OutlineSize = 9,
		};
		unit.AddChild(text);
		unit.LookAt(new Vector3(_player.GlobalPosition.X, unit.GlobalPosition.Y, _player.GlobalPosition.Z));
		unit.ResetPhysicsInterpolation();
	}

	private Vector3 SpawnPosition(int index, int count, float radius)
	{
		Vector3 center = _spawnPoint.GlobalPosition;
		if (count <= 1) return center + new Vector3(0, .05f, 0);
		float angle = Mathf.Tau * index / count;
		return center + new Vector3(Mathf.Cos(angle) * radius, .05f, Mathf.Sin(angle) * radius);
	}

	private static string DummyLabel(string kind) => kind switch
	{
		"armor" => "护甲刷新",
		"block" => "正面格挡",
		"invincible" => "无敌",
		"shooter" => "朝前射击",
		"area" => "前方区域",
		"group" => "木桩群",
		_ => "普通木桩",
	};

	public void SpawnEnemy(string id, int count, bool elite)
	{
		EnemyConfig? row = ConfigRows.ById<EnemyConfig>(id);
		if (row == null || row.IsGlobal) { _spawnScreen?.SetStatus("敌人 ID 不存在。"); return; }
		count = Mathf.Clamp(count, 1, 5);
		int spawned = 0;
		for (int i = 0; i < count; i++)
		{
			var scene = RuntimeResources.Load<PackedScene>(row.ScenePath);
			if (scene == null) continue;
			Unit unit = scene.Instantiate<Unit>();
			row.ApplyTo(unit);
			if (elite) unit.AddChild(new EliteModifier());
			Place(unit, SpawnPosition(i, count, 2.4f), elite ? $"{row.DisplayName}（精英）" : row.DisplayName);
			spawned++;
		}
		_spawnScreen?.SetStatus($"已召唤 {row.DisplayName} ×{spawned}{(elite ? "（精英）" : "")}。");
	}

	public void ClearSpawns()
	{
		foreach (Node child in _spawnRoot.GetChildren())
		{
			if (IsInstanceValid(child)) child.QueueFree();
		}
		foreach (Node node in GetTree().GetNodesInGroup("projectiles"))
			if (IsInstanceValid(node) && _spawnRoot.IsAncestorOf(node)) node.QueueFree();
		_spawnScreen?.SetStatus("已清空训练召唤物。");
	}

	public void ResetTraining()
	{
		_spawnScreen?.Close();
		if (_rewardScreen?.IsOpen == true) _rewardScreen.Close();
		ClearSpawns();
		_pendingOptions.Clear();
		if (_player != null && IsInstanceValid(_player))
		{
			_player.GetNodeOrNull<RewardEffectHost>(RewardEffectHost.NodeName)?.ReleaseBuild(_build);
			_build = new RunBuild();
			new PlayerLoadout().Apply(_player, this, RunSetup.Character, RunSetup.Weapon);
			_player.GlobalPosition = new Vector3(0, .1f, 14);
			_player.Velocity = Vector3.Zero;
			_player.ResetPhysicsInterpolation();
			_player.FillVitals();
			_weapon = NodeSearch.FindInSubtree<Weapon>(_player);
			if (_weapon != null)
			{
				string characterId = RunController.CharacterRow(RunSetup.Character)?.Id ?? string.Empty;
				_pool.Initialize(_weapon, characterId, "training_ground");
			}
		}
		UpdateHud();
		_spawnScreen?.SetStatus("已重置：增幅清空、玩家状态复位、召唤物清除。");
	}

	public void ReturnToBase()
	{
		_spawnScreen?.Close();
		UiRouter.Instance?.ResetForSceneChange();
		RuntimeResources.ChangeScene(GetTree(), "res://Scenes/Levels/Base.tscn");
	}

	public override void _Process(double delta)
	{
		if (_player == null || !IsInstanceValid(_player)) return;
		if (_resetTimer >= 0)
		{
			_resetTimer -= delta;
			if (_resetTimer <= 0) ResetTraining();
			return;
		}
		if (_player.IsDead || _player.GlobalPosition.Y < -15) _resetTimer = .8;
	}

	private void BuildHud()
	{
		var layer = new CanvasLayer { Name = "TrainingHud", Layer = 4 };
		AddChild(layer);
		_hud = new Label
		{
			Position = new Vector2(18, 18),
			Text = "",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(620, 0),
		};
		_hud.AddThemeFontSizeOverride("font_size", 17);
		layer.AddChild(_hud);
		UpdateHud();
	}

	private void UpdateHud()
	{
		if (_hud == null) return;
		string build = _build.Acquired.Count == 0
			? "未选增幅"
			: string.Join("、", _build.Acquired.Select(row => row.Name));
		_hud.Text = $"测试场 · 单区域训练\n" +
			$"F 召唤终端 / 增幅终端 / 重置 / 返回基地\n" +
			$"已选增幅 {_build.Acquired.Count}：{build}";
	}

	private void DebugSpawnAllDummies()
	{
		// 预留调试入口；正式验收通过面板逐个召唤，不自动摆场。
		_spawnSerial++;
	}
}

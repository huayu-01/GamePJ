using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.Training;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>071 训练场无头专项：场景、七类木桩、敌人召唤、增幅面板、清空。</summary>
public partial class TrainingGroundProbe : Node
{
	private int _pass, _fail;

	public override async void _Ready()
	{
		var scene = RuntimeResources.Load<PackedScene>("res://Scenes/Training/TrainingGround.tscn");
		if (scene == null) { Fail("训练场场景可加载"); Finish(); return; }
		var ground = scene.Instantiate<TrainingGroundController>();
		AddChild(ground);
		for (int i = 0; i < 20 && ConfigRows.Count<EnemyConfig>() == 0; i++)
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		for (int i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

		Check(ground.Player != null, "训练场有玩家");
		await Shot("overview");
		Check(ground.GetNodeOrNull<Node3D>("SpawnRoot") != null, "训练场有召唤根节点");
		Check(ground.GetNodeOrNull<Node3D>("SpawnConsole/InteractionComponent") != null, "召唤终端存在");
		Check(ground.GetNodeOrNull<Node3D>("BuffConsole/InteractionComponent") != null, "增幅终端存在");
		Check(ground.GetNodeOrNull<Node3D>("ResetConsole/InteractionComponent") != null, "重置终端存在");
		Check(ground.GetNodeOrNull<Node3D>("ReturnGate/InteractionComponent") != null, "返回终端存在");

		foreach ((string kind, int expected, string label) in new[]
		{
			("normal", 1, "普通"), ("armor", 1, "护甲"), ("block", 1, "格挡"),
			("invincible", 1, "无敌"), ("shooter", 1, "射击"), ("area", 1, "区域"), ("group", 5, "群组"),
		})
		{
			ground.ClearSpawns();
			ground.SpawnDummy(kind, 1, false);
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			int count = ground.GetNode<Node3D>("SpawnRoot").GetChildren().OfType<Unit>().Count();
			Check(count == expected, $"{label}木桩生成数量 {count}/{expected}");
		}

		ground.ClearSpawns();
		ground.SpawnDummy("armor", 1, false);
		ground.SpawnDummy("block", 1, false);
		ground.SpawnDummy("invincible", 1, false);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		Unit? armor = ground.GetNode<Node3D>("SpawnRoot").GetChildren().OfType<Unit>().FirstOrDefault(u => u.GetNodeOrNull<TrainingArmorDummy>("TrainingArmorDummy") != null);
		Unit? block = ground.GetNode<Node3D>("SpawnRoot").GetChildren().OfType<Unit>().FirstOrDefault(u => u.GetNodeOrNull<TrainingBlockDummy>("TrainingBlockDummy") != null);
		Unit? invincible = ground.GetNode<Node3D>("SpawnRoot").GetChildren().OfType<Unit>().FirstOrDefault(u => u.IsInvulnerable);
		Check(armor != null, "护甲木桩挂载刷新组件");
		Check(block != null && block.GetNodeOrNull<BlockComponent>("Block") is { Blocking: true }, "格挡木桩常驻正面格挡");
		Check(invincible != null, "无敌木桩 IsInvulnerable");

		ground.ClearSpawns();
		ground.SpawnDummy("group", 1, false);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		await Shot("dummies");

		ground.ClearSpawns();
		ground.SpawnEnemy("enemy_chaser", 1, false);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		Check(ground.GetNode<Node3D>("SpawnRoot").GetChildren().OfType<Unit>().Any(u => u.HasTag("Chaser")), "真实敌人可召唤");

		ground.ClearSpawns();
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		Check(ground.GetNode<Node3D>("SpawnRoot").GetChildCount() == 0, "清空删除训练召唤物");

		var spawnScreen = ground.GetNodeOrNull<TrainingSpawnScreen>("SpawnScreen");
		spawnScreen?.Open(ground);
		await ToSignal(GetTree().CreateTimer(.4, processAlways: true), SceneTreeTimer.SignalName.Timeout);
		await Shot("spawn-panel");
		spawnScreen?.Close();
		ground.OpenBuffPanel();
		await ToSignal(GetTree().CreateTimer(.6, processAlways: true), SceneTreeTimer.SignalName.Timeout);
		Check(ground.PendingChoiceCount > 0, "增幅面板有候选");
		await Shot("panel");
		var reward = ground.GetChildren().OfType<RewardScreen>().FirstOrDefault();
		if (reward != null)
		{
			reward.EmitSignal(RewardScreen.SignalName.RewardChosen, 0);
			for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			Check(ground.Build.Acquired.Count == 1, "增幅选择应用到 RunBuild");
			reward.Close();
		}
		for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		ground.ResetTraining();
		for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		Check(ground.Build.Acquired.Count == 0, "第一次重置清空增幅");
		ground.SpawnDummy("normal", 1, false);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		ground.ResetTraining();
		for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		Check(ground.Build.Acquired.Count == 0 && ground.GetNode<Node3D>("SpawnRoot").GetChildCount() == 0, "第二次重置清空增幅与召唤物");
		for (int i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

		Finish();
	}

	private void Check(bool ok, string text)
	{
		if (ok) _pass++; else _fail++;
		GD.Print($"[TrainingGround] {(ok ? "PASS" : "FAIL")} {text}");
	}
	private void Fail(string text) => Check(false, text);

	private async Task Shot(string name)
	{
		if (DisplayServer.GetName() == "headless") return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		string dir = ProjectSettings.GlobalizePath("res://Logs/evidence/training");
		DirAccess.MakeDirRecursiveAbsolute(dir);
		using var image = GetViewport().GetTexture().GetImage();
		Check(image.SavePng(Path.Combine(dir, $"071-{name}.png")) == Error.Ok, "真实渲染截图 " + name);
	}

	private void Finish()
	{
		GD.Print($"[TrainingGround] SUMMARY pass={_pass} fail={_fail}");
		GetTree().Quit(_fail == 0 ? 0 : 1);
	}
}

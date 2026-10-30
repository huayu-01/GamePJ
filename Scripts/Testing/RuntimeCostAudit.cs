using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>只读运行结构审计：逐个加载真实资源，不修改碰撞、调度或模型，不推断帧率收益。</summary>
public partial class RuntimeCostAudit : Node3D
{
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private static List<Node> Flatten(Node root)
	{
		var result = new List<Node>(); var pending = new Stack<Node>(); pending.Push(root);
		while (pending.Count > 0) { var node = pending.Pop(); result.Add(node); foreach (Node child in node.GetChildren()) pending.Push(child); }
		return result;
	}
	private static string? Callback(Node node, string method)
	{
		Type? owner = node.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public)?.DeclaringType;
		return owner?.Namespace?.StartsWith("ActRoguelike", StringComparison.Ordinal) == true ? owner.FullName : null;
	}
	private static object Snapshot(Node root, string id)
	{
		var nodes = Flatten(root); var meshes = nodes.OfType<MeshInstance3D>().Where(m => m.Mesh != null).ToArray();
		var materials = new HashSet<ulong>(); int surfaces = 0, nextPasses = 0;
		foreach (var mesh in meshes)
		{
			int count = mesh.Mesh!.GetSurfaceCount(); surfaces += count;
			for (int i = 0; i < count; i++) if (mesh.GetActiveMaterial(i) is { } material) { materials.Add(material.GetInstanceId()); if (material.NextPass != null) nextPasses++; }
		}
		var callbacks = nodes.Select(n => new { path = root.GetPathTo(n).ToString(), type = n.GetType().Name,
			process = Callback(n, "_Process"), physics = Callback(n, "_PhysicsProcess"), processEnabled = n.IsProcessing(), physicsEnabled = n.IsPhysicsProcessing(), canProcess = n.CanProcess() })
			.Where(n => n.process != null || n.physics != null).ToArray();
		return new { id, nodeCount = nodes.Count, meshes = meshes.Length, visibleMeshes = meshes.Count(m => m.IsVisibleInTree()),
			skinnedMeshes = meshes.Count(m => m.Skin != null), surfaces, distinctMaterials = materials.Count, surfacesWithNextPass = nextPasses,
			materialOverlays = meshes.Count(m => m.MaterialOverlay != null), shadowCasters = meshes.Count(m => m.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off),
			bones = nodes.OfType<Skeleton3D>().Sum(s => s.GetBoneCount()), skeletons = nodes.OfType<Skeleton3D>().Count(),
			animationPlayers = nodes.OfType<AnimationPlayer>().Select(p => new { path = root.GetPathTo(p).ToString(), playing = p.IsPlaying(), callbackMode = p.CallbackModeProcess.ToString() }).ToArray(),
			collisionShapes = nodes.OfType<CollisionShape3D>().Count(), enabledShapes = nodes.OfType<CollisionShape3D>().Count(s => !s.Disabled),
			areas = nodes.OfType<Area3D>().Count(), monitoringAreas = nodes.OfType<Area3D>().Count(a => a.Monitoring),
			navigationAgents = nodes.OfType<NavigationAgent3D>().Count(), multimeshes = nodes.OfType<MultiMeshInstance3D>().Count(),
			occluders = nodes.OfType<OccluderInstance3D>().Count(),
			abilities = nodes.OfType<Ability>().Select(a => new { a.AbilityId, type = a.GetType().Name, a.Enabled, a.IsActive, a.CooldownRemaining, physicsEnabled = a.IsPhysicsProcessing() }).ToArray(),
			processCallbacks = callbacks.Count(c => c.process != null && c.processEnabled && c.canProcess),
			physicsCallbacks = callbacks.Count(c => c.physics != null && c.physicsEnabled && c.canProcess), callbacks };
	}
	public override async void _Ready()
	{
		bool passed = false;
		try
		{
			await Wait(.15);
#if DEBUG
			MetaProgress.UseInMemoryStoreForSelfTest(); RuneCarry.UseInMemoryStoreForSelfTest();
#endif
			var samples = new List<object>();
			var arena = RuntimeResources.Load<PackedScene>("res://Scenes/Levels/SurvivalArena.tscn").Instantiate<Node3D>();
			AddChild(arena); await Wait(.5); var world = Snapshot(arena, "survival_arena"); arena.QueueFree(); await Wait(.1);
			var rows = ConfigIndex.Settings.Enemies.Where(e => !e.IsGlobal).Cast<UnitConfig>().Concat(ConfigIndex.Settings.Bosses).ToArray();
			foreach (var row in rows)
			{
				string path = row is EnemyConfig enemy ? enemy.ScenePath : ((BossConfig)row).ScenePath;
				var unit = RuntimeResources.Load<PackedScene>(path).Instantiate<Unit>(); row.ApplyTo(unit); AddChild(unit);
				unit.GlobalPosition = new Vector3(0, 20, 0); await Wait(.05);
				samples.Add(Snapshot(unit, row.Id)); unit.QueueFree(); await Wait(.04);
			}
			var report = new { date = DateTimeOffset.Now, engine = Engine.GetVersionInfo()["string"].AsString(),
				physicsEngine = ProjectSettings.GetSetting("physics/3d/physics_engine").AsString(), world, enemies = samples,
				note = "真实Ready后逐个原型的只读结构快照；不是战斗帧耗时或50／100敌人压力测试；不代表节点、骨骼或材质数量与耗时线性对应。" };
			string output = ValidationArtifacts.Path("res://Logs/evidence/performance-investigation/runtime-structure-20261026.json");
			System.IO.File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
			GD.Print($"[RuntimeCostAudit] COMPLETE enemies={samples.Count} physics={report.physicsEngine}"); passed = true;
		}
		catch (Exception error) { GD.Print("[RuntimeCostAudit] FAIL " + error); }
		GameExit.Request(GetTree(), passed ? 0 : 1);
	}
}

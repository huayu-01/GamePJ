using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>只在诊断中分离模型绘制与原生动画开销；不停止实战敌人的逻辑或改写美术源。</summary>
public partial class AnimationCostProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "AnimationCost";
	protected override async Task RunChecks()
	{
		if (DisplayServer.GetName() == "headless") throw new InvalidOperationException("模型成本诊断需要真实渲染。");
		var window = GetTree().Root; window.Mode = Window.ModeEnum.Windowed; window.Size = new Vector2I(3840, 2160);
		window.Scaling3DScale = 1; window.Msaa3D = Viewport.Msaa.Disabled;
		Engine.MaxFps = 0; DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
		var rows = new[] { ConfigRows.ById<EnemyConfig>("enemy_chaser")!, ConfigRows.ById<EnemyConfig>("enemy_ranged")!, ConfigRows.ById<EnemyConfig>("enemy_heavy_slammer")! };
		var models = new List<Node3D>(); var players = new List<AnimationPlayer>(); int bones = 0, surfaces = 0, meshes = 0;
		for (int i = 0; i < 100; i++)
		{
			var row = rows[i % rows.Length];
			var unit = RuntimeResources.Load<PackedScene>(row.ScenePath).Instantiate<Unit>();
			var model = unit.GetNode<Node3D>("Model"); unit.RemoveChild(model); unit.Free();
			model.Name = "ModelCost" + i; Mode.AddChild(model);
			model.GlobalPosition = new Vector3((i % 10 - 4.5f) * 3, 1f, (i / 10 - 4.5f) * 3);
			models.Add(model);
			var animations = new List<AnimationPlayer>(); NodeSearch.CollectInSubtree(model, animations);
			foreach (var player in animations)
			{
				string? clip = player.GetAnimationList().FirstOrDefault(n => n.EndsWith("idle", StringComparison.OrdinalIgnoreCase));
				if (clip == null) continue;
				player.Play(clip); players.Add(player);
			}
			var skeletons = new List<Skeleton3D>(); NodeSearch.CollectInSubtree(model, skeletons); bones += skeletons.Sum(s => s.GetBoneCount());
			var geometry = new List<MeshInstance3D>(); if (model is MeshInstance3D rootMesh) geometry.Add(rootMesh); NodeSearch.CollectInSubtree(model, geometry);
			meshes += geometry.Count; surfaces += geometry.Sum(m => m.Mesh?.GetSurfaceCount() ?? 0);
		}
		Check(models.Count == 100 && players.Count > 0 && bones > 0, $"100个当前敌人模型子树，无敌人AI／攻击脚本；动画播放器{players.Count}、骨骼{bones}、Mesh{meshes}、表面{surfaces}");
		var camera = new Camera3D(); Mode.AddChild(camera); camera.GlobalPosition = new Vector3(0, 40, 40); camera.LookAt(Vector3.Zero); camera.Current = true;
		var rid = GetViewport().GetViewportRid(); RenderingServer.ViewportSetMeasureRenderTime(rid, true);
		var results = new List<object>();
		foreach (var phase in new[] { (Name: "visible_animated", Visible: true, Animated: true), (Name: "visible_frozen", Visible: true, Animated: false),
			(Name: "hidden_animated", Visible: false, Animated: true), (Name: "hidden_frozen", Visible: false, Animated: false), (Name: "visible_animated_repeat", Visible: true, Animated: true) })
		{
			foreach (var model in models) model.Visible = phase.Visible;
			foreach (var player in players) player.Active = phase.Animated;
			await Wait(1.5);
			var frameTimes = new List<double>(2048); var gpuTimes = new List<double>(2048); var processTimes = new List<double>(2048); var draws = new List<double>(2048);
			ulong began = Time.GetTicksUsec(), previous = began;
			while (Time.GetTicksUsec() - began < 3_000_000)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				ulong now = Time.GetTicksUsec(); frameTimes.Add((now - previous) / 1000.0); previous = now;
				gpuTimes.Add(RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid)); processTimes.Add(Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000);
				draws.Add(Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame));
			}
			var sorted = frameTimes.OrderBy(t => t).ToArray();
			results.Add(new { phase = phase.Name, samples = sorted.Length, frameMs = frameTimes.Average(), p99FrameMs = sorted[(int)(sorted.Length * .99)], gpuMs = gpuTimes.Average(), processMs = processTimes.Average(), drawCalls = draws.Average() });
			Check(sorted.Length > 30, $"诊断阶段{phase.Name}有效采样{sorted.Length}帧，平均{frameTimes.Average():0.00}ms／GPU{gpuTimes.Average():0.00}ms");
		}
		RenderingServer.ViewportSetMeasureRenderTime(rid, false);
		foreach (var model in models) model.QueueFree();
		string path = ValidationArtifacts.Path($"res://Logs/evidence/performance-investigation/animation-cost-{DateTime.Now:yyyyMMdd_HHmmss}.json");
		System.IO.File.WriteAllText(path, JsonSerializer.Serialize(new { models = 100, animationPlayers = players.Count, bones, meshes, surfaces, phases = results,
			note = "诊断隔离模型子树，只播放idle；阶段停止动画／隐藏只作用于诊断副本，不代表实战可以停掉技能动画或逻辑；短时顺序试验，末尾重复首阶段检查漂移。" }, new JsonSerializerOptions { WriteIndented = true }));
	}
}

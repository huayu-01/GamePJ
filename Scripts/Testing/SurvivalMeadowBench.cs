using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Visual;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>
/// 正式生存场景（暮光回廊）里的草地接入实测：**绘制块 4 米 / 8 米 × 静态 / 战斗 × 有草 / 无草**，
/// 各暖场 1.5 秒、采样 3 秒，记录帧时间、GPU 时间、渲染 CPU 时间、draw call 与三角面数。
///
/// 跑的是**正式入口** `Scenes/Survival.tscn`（正式 HUD、跟随相机、丛林替换、导航与真实敌人 AI）。
/// 有草/无草只切 <see cref="SurvivalMeadow.SetGrassEnabled"/>；4/8 米只切
/// <see cref="SurvivalMeadow.Rebuild"/>（生成格固定 4 米，所以两种块大小的草**逐簇相同**）。
/// </summary>
public partial class SurvivalMeadowBench : EliteTraitProbeSupport
{
	protected override string ProbeTag => "MeadowBench";
	protected override bool SceneMeadowEnabled => true;

	/// <summary>战斗负载用的敌人数量（真实 Chaser，AI／导航／近战全部照常跑）。</summary>
	private const int CombatEnemies = 12;
	private const string EvidenceDir = "res://Logs/evidence/grass-production/";
	private static readonly float[] ChunkSizes = { 4f, 8f };

	private SurvivalMeadow _meadow = null!;
	private readonly List<Unit> _enemies = new();
	private int _hitsTaken;
	private int _referenceFull;

	protected override async Task RunChecks()
	{
		_meadow = Mode.GetNode<SurvivalMeadow>("Meadow");
		for (int i = 0; i < 900 && !_meadow.Built; i++) await Wait(.02);

		Check(_meadow.Built, $"正式场景草地已构建：{_meadow.BuildMilliseconds:0.0}ms，分配 {_meadow.BuildAllocatedMb:0.00}MB");
		if (!_meadow.Built || _meadow.Grass == null) return;
		ShaderGrassPatch grass = _meadow.Grass;
		Check(grass.NormalCount > 12000 && grass.ChunkCount > 0,
			$"草地规模：普通 {grass.NormalCount} 簇／{grass.ChunkCount} 个 {_meadow.GrassChunkSize:0} 米绘制块／{grass.ExclusionCount} 个禁草区（{ShaderGrassPatch.TrianglesPerClump} 三角面每簇）");
		Check(Mode.GetNode<Node3D>("Arena").GetNodeOrNull<Node>("Ground") != null,
			"arena 地面仍在，草地只做视觉，碰撞与导航由 Arena 提供");

		Player.Damaged += (_, _) => _hitsTaken++;

		if (DisplayServer.GetName() == "headless")
		{
			Check(true, "无头模式：只验证接入与规模，不做帧时间采样");
			return;
		}

		await Benchmark();
	}

	/// <summary>两种绘制块各跑一遍「静态/战斗 × 有草/无草」，顺序正反各一次抵消漂移。</summary>
	private async Task Benchmark()
	{
		Rid rid = GetViewport().GetViewportRid();
		// ⚠️ 显示器 160Hz + 垂直同步会把帧时间钉在 6.25ms，草地的开销只会藏在 GPU/CPU 里。
		// 基准必须关掉 vsync 与帧率上限，帧时间才反映真实开销；跑完恢复原设置。
		DisplayServer.VSyncMode originalVsync = DisplayServer.WindowGetVsyncMode();
		int originalMaxFps = Engine.MaxFps;
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
		Engine.MaxFps = 0;
		RenderingServer.ViewportSetMeasureRenderTime(rid, true);
		var phases = new (bool grass, bool combat)[]
		{
			(false, false), (true, false), (false, true), (true, true),
			(true, true), (false, true), (true, false), (false, false),
		};
		var results = new List<object>();
		var builds = new List<object>();
		try
		{
			Aabb referenceBounds = default;
			int referenceClumps = 0;
			foreach (float chunkSize in ChunkSizes)
			{
				_meadow.Rebuild(chunkSize);
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				ShaderGrassPatch grass = _meadow.Grass!;
				builds.Add(new
				{
					chunkSize,
					chunks = grass.ChunkCount,
					clumps = grass.NormalCount,
					exclusionZones = grass.ExclusionCount,
					excludedChunks = grass.FullyExcludedChunks,
					groundRaycasts = grass.GroundRaycastCount,
					buildMilliseconds = _meadow.BuildMilliseconds,
					buildAllocatedMb = _meadow.BuildAllocatedMb,
				});
				if (referenceClumps == 0) { referenceClumps = grass.NormalCount; referenceBounds = grass.InstanceBounds; _referenceFull = grass.FullCount; }
				else
				{
					// 比"全量簇数 + 包围盒"：显示簇数是按块取整的读数，块越大取整损失越少，允许差几十个。
					Check(grass.FullCount == _referenceFull && grass.InstanceBounds.Position.IsEqualApprox(referenceBounds.Position)
							&& grass.InstanceBounds.Size.IsEqualApprox(referenceBounds.Size),
						$"{chunkSize:0} 米绘制块的草地与 4 米**逐簇相同**（{grass.FullCount} 簇全量，显示档读数 {grass.NormalCount}，包围盒 {grass.InstanceBounds.Size}），只改了一次绘制的装载量");
				}
				Check(grass.ChunkCount > 0, $"{chunkSize:0} 米绘制块：{grass.ChunkCount} 个分块，构建 {_meadow.BuildMilliseconds:0}ms");

				// 空场景（无敌人、无草）先拍一张对照，证明两种块大小的画面一致。
				_meadow.SetGrassEnabled(false);
				ClearEnemies();
				for (int run = 0; run < phases.Length; run++)
				{
					(bool useGrass, bool combat) = phases[run];
					_meadow.SetGrassEnabled(useGrass);
					ClearEnemies();
					if (combat) SpawnCombat();

					string phase = $"c{chunkSize:0}_" + (combat ? "combat" : "static") + (useGrass ? "_grass" : "_ground");
					await Wait(1.5);
					var times = new List<double>();
					var gpu = new List<double>();
					var cpu = new List<double>();
					var draws = new List<double>();
					var primitives = new List<double>();
					ulong start = Time.GetTicksUsec(), previous = start;
					while (Time.GetTicksUsec() - start < 3_000_000)
					{
						await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
						ulong now = Time.GetTicksUsec();
						times.Add((now - previous) / 1000.0);
						previous = now;
						gpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid));
						cpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeCpu(rid));
						draws.Add(Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame));
						primitives.Add(Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame));
					}

					double[] sorted = times.OrderBy(value => value).ToArray();
					results.Add(new
					{
						run,
						chunkSize,
						phase,
						combat,
						grass = useGrass,
						enemies = _enemies.Count,
						samples = times.Count,
						meanFrameMs = times.Average(),
						medianFrameMs = sorted[sorted.Length / 2],
						p95FrameMs = sorted[(int)(sorted.Length * .95)],
						gpuMs = gpu.Average(),
						renderCpuMs = cpu.Average(),
						drawCalls = draws.Average(),
						primitives = primitives.Average(),
					});
					Check(times.Count > 30 && gpu.Any(value => value > 0),
						$"{phase}：{times.Count} 帧，平均 {times.Average():0.00}ms，p95 {sorted[(int)(sorted.Length * .95)]:0.00}ms，GPU {gpu.Average():0.00}ms，draw {draws.Average():0}，敌人 {_enemies.Count}");
					if (combat) Check(_hitsTaken > 0, $"{phase} 战斗负载真实：玩家在采样窗口内被命中 {_hitsTaken} 次");
					if (run == 1) await ShotTo(phase);
				}
			}

			string path = ValidationArtifacts.Path(EvidenceDir + "benchmark-chunks.json");
			System.IO.File.WriteAllText(path, JsonSerializer.Serialize(new
			{
				engine = Engine.GetVersionInfo()["string"].AsString(),
				gpu = RenderingServer.GetVideoAdapterName(),
				scene = "res://Scenes/Survival.tscn",
				resolution = $"{GetWindow().Size.X}x{GetWindow().Size.Y}",
				msaa = "2x",
				generationCell = _meadow.GrassGenerationCell,
				density = _meadow.Grass?.Density,
				windFrequency = _meadow.Grass?.WindFrequency,
				combatEnemies = CombatEnemies,
				note = "正式 Survival 场景；HUD／跟随相机／导航／真实敌人 AI 全部保留；绘制块 4 米与 8 米各跑静态/战斗×有草/无草，正反序各一次；生成格固定 4 米，所以两种块大小的草逐簇相同。",
				builds,
				phases = results,
			}, new JsonSerializerOptions { WriteIndented = true }));
			Check(System.IO.File.Exists(path), "基准 JSON 已写盘 " + path);
			foreach (var build in builds)
			{
				dynamic row = build;
				GD.Print($"[{ProbeTag}] BUILD chunk={row.chunkSize:0}m chunks={row.chunks} clumps={row.clumps} build={row.buildMilliseconds:0}ms rays={row.groundRaycasts}");
			}
		}
		finally
		{
			RenderingServer.ViewportSetMeasureRenderTime(rid, false);
			DisplayServer.WindowSetVsyncMode(originalVsync);
			Engine.MaxFps = originalMaxFps;
			ClearEnemies();
			_meadow.SetGrassEnabled(true);
		}
	}

	/// <summary>真实敌人：正常敌人行、AI 与导航都开，围绕玩家 6.5 米站位后自行追击。</summary>
	private void SpawnCombat()
	{
		_hitsTaken = 0;
		var scene = RuntimeResources.Load<PackedScene>("res://Scenes/Enemies/Chaser.tscn");
		for (int i = 0; i < CombatEnemies; i++)
		{
			var unit = scene.Instantiate<Unit>();
			ConfigRows.ByScenePath<EnemyConfig>(scene.ResourcePath)?.ApplyTo(unit);
			Mode.AddChild(unit);
			float angle = Mathf.Tau * i / CombatEnemies;
			unit.GlobalPosition = Player.GlobalPosition + new Vector3(Mathf.Cos(angle) * 6.5f, .1f, Mathf.Sin(angle) * 6.5f);
			unit.ResetPhysicsInterpolation();
			_enemies.Add(unit);
		}
	}

	private void ClearEnemies()
	{
		foreach (Unit enemy in _enemies)
		{
			if (!IsInstanceValid(enemy)) continue;
			Unit unit = enemy;
			unit.GetParent()?.RemoveChild(unit);
			unit.QueueFree();
		}
		_enemies.Clear();
	}

	private async Task ShotTo(string name)
	{
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		using var image = GetViewport().GetTexture().GetImage();
		Error error = image.SavePng(ValidationArtifacts.Path(EvidenceDir + name + ".png"));
		Check(error == Error.Ok, "真实渲染截图 " + name);
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实单位材质/骨骼兼容及4K百单位开关对照，不修改美术源和生产人数。</summary>
public partial class CombatArtProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "CombatArt";
	protected override async Task RunChecks()
	{
		var ids = new[] { "enemy_chaser", "enemy_heavy_slammer", "enemy_meteor_mage", "1339" };
		var units = new List<Unit>();
		int count = DisplayServer.GetName() == "headless" ? 8 : 100;
		for (int i = 0; i < count; i++)
		{
			var row = ConfigRows.ById<EnemyConfig>(ids[i % ids.Length])!;
			var unit = RuntimeResources.Load<PackedScene>(row.ScenePath).Instantiate<Unit>(); row.ApplyTo(unit); Mode.AddChild(unit);
			unit.GlobalPosition = Player.GlobalPosition + new Vector3((i % 10 - 4.5f) * 2.5f, 0, -8 - i / 10 * 2.5f);
			unit.GetNode("Brain").ProcessMode = ProcessModeEnum.Disabled; unit.Movement!.SetPhysicsProcess(false);
			foreach (Node child in unit.GetChildren()) if (child is Ability ability) ability.Enabled = false;
			units.Add(unit);
		}
		await Wait(.2);
		var styles = units.Select(u => u.GetNode<UnitArtStyle>("ArtStyle")).ToArray();
		Check(styles.All(s => s.Enabled && s.StyledSurfaceCount > 0), "052普通/重型/法师/蘑菇均已接入卡通材质");
		Check(styles.All(s => !s.IsProcessing() && !s.IsPhysicsProcessing()), "052材质组件不进行逐帧轮询");
		var meshes = new List<MeshInstance3D>(); NodeSearch.CollectInSubtree(units[0].GetNode("Model"), meshes);
		if (units[0].GetNode("Model") is MeshInstance3D rootMesh) meshes.Add(rootMesh);
		var material = meshes.Select(m => m.MaterialOverride ?? m.GetSurfaceOverrideMaterial(0)).OfType<ShaderMaterial>().First();
		Check(material.NextPass is ShaderMaterial ink && Mathf.IsEqualApprox(ink.GetShaderParameter("ink_width").AsSingle(), .006f), "052不透明身体使用6毫米细描边且保持原深度测试");
		styles[0].SetEnabled(false); Check(styles[0].StyledSurfaceCount == 0, "052关闭时恢复原始材质");
		styles[0].SetEnabled(true); Check(styles[0].StyledSurfaceCount > 0, "052重开后重新绑定，不改源材质");
		var playerStyle = Player.GetNode<UnitArtStyle>("ArtStyle");
		Check(playerStyle.Enabled && playerStyle.StyledSurfaceCount > 0, "052玩家当前模型与武器同样进入卡通表现");
		await CheckDynamicGlow();
		if (DisplayServer.GetName() != "headless") await Measure(units, styles);
		foreach (var unit in units) unit.QueueFree();
		await Wait(.1); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); await Wait(.1);
		Check(!IsInstanceValid(units[0]), "052批量退出与完整GC后节点回收");
	}
	private async Task CheckDynamicGlow()
	{
		var model = RuntimeResources.Load<PackedScene>("res://Scenes/Visual/CloverWayfarer.tscn").Instantiate<Node3D>();
		Player.AddChild(model);
		var style = new UnitArtStyle(); Player.AddChild(style); style.Bind(model);
		await Wait(.1);
		var glow = model.GetNode<ActRoguelike.Visual.UnitCastGlow>("CastGlow");
		var mesh = (MeshInstance3D)model.FindChild("CW_ForeheadClover", true, false);
		Check(style.StyledSurfaceCount > 0 && glow.TargetCount == 5 && mesh.GetActiveMaterial(0) is StandardMaterial3D,
			"052角色身体卡通上色，同时五处动态施法纹样保留可驱动的原材质");
		var ability = Player.GetNode<EnergyWaveAbility>("AbilityWeaponSkill1");
		ability.ResetForLoadout();
		Check(ability.TryPress(null), "052实际发动当前槽位能量掌");
		await Wait(.17);
		Check(glow.PulseCount > 0 && glow.Strength > 0 && ((StandardMaterial3D)mesh.GetActiveMaterial(0)).EmissionEnergyMultiplier > 0,
			"052真实能力事件仍点亮施法纹样，卡通材质没有吞掉脉冲");
		style.SetEnabled(false); style.SetEnabled(true);
		Check(glow.TargetCount == 5 && mesh.GetActiveMaterial(0) is StandardMaterial3D, "052重复开关仍保留动态纹样材质");
		model.QueueFree(); style.QueueFree(); await Wait(.1);
		// 缺少配置表面的模型不曾订阅单位信号，退出不得解绑不存在的连接。
		var empty = new Node3D(); empty.AddChild(new ActRoguelike.Visual.UnitCastGlow { MeshName = "MissingSurface" });
		Player.AddChild(empty); await Wait(.08); empty.QueueFree(); await Wait(.08);
		Check(!IsInstanceValid(empty), "052未绑定表面的发光组件正常退出，严格stderr检查没有信号解绑错误");
	}
	private async Task Measure(List<Unit> units, UnitArtStyle[] styles)
	{
		var window = GetTree().Root; window.Mode = Window.ModeEnum.Windowed; window.Size = new Vector2I(3840, 2160);
		window.Scaling3DScale = 1; window.Msaa3D = Viewport.Msaa.Disabled;
		Engine.MaxFps = 0; DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
		var rig = Player.GetNode<Node3D>("CameraRig"); rig.SetPhysicsProcess(false); rig.SetProcessInput(false);
		var camera = new Camera3D { Current = true }; Mode.AddChild(camera); var origin = Player.GlobalPosition;
		camera.GlobalPosition = origin + new Vector3(0, 27, 13); camera.LookAt(origin + new Vector3(0, 0, -17)); camera.ResetPhysicsInterpolation();
		var rid = GetViewport().GetViewportRid(); RenderingServer.ViewportSetMeasureRenderTime(rid, true);
		var results = new List<object>();
		foreach (var phase in new[] { (Name: "original", Styled: false), (Name: "toon_outline", Styled: true), (Name: "original_repeat", Styled: false) })
		{
			foreach (var style in styles) style.SetEnabled(phase.Styled);
			await Wait(1.5);
			var frame = new List<double>(); var gpu = new List<double>(); var draws = new List<double>();
			ulong began = Time.GetTicksUsec(), previous = began;
			while (Time.GetTicksUsec() - began < 3_000_000)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				ulong now = Time.GetTicksUsec(); frame.Add((now - previous) / 1000d); previous = now;
				gpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid)); draws.Add(Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame));
			}
			var sorted = frame.Order().ToArray();
			results.Add(new { phase = phase.Name, samples = frame.Count, averageFrameMs = frame.Average(), p95FrameMs = sorted[(int)(sorted.Length * .95)], maximumFrameMs = sorted[^1], gpuMs = gpu.Average(), drawCalls = draws.Average() });
			Check(frame.Count > 30, $"052百单位4K {phase.Name}：均帧{frame.Average():0.00}ms，GPU{gpu.Average():0.00}ms，draws={draws.Average():0}");
			if (phase.Styled) await Shot("052-toon-outline-100");
		}
		RenderingServer.ViewportSetMeasureRenderTime(rid, false);
		string path = ValidationArtifacts.Path("res://Logs/evidence/combat-art-20261029.json");
		System.IO.File.WriteAllText(path, JsonSerializer.Serialize(new { resolution = new[] { 3840, 2160 }, units = units.Count, phases = results,
			note = "真实死战中混合当前四类单位，仅禁用本诊断的AI和主动移动；动画/身体/环境保持。只比较材质开关，含末尾原版重复，不代表完整12分钟性能或百敌60帧验收。" }, new JsonSerializerOptions { WriteIndented = true }));
	}
}

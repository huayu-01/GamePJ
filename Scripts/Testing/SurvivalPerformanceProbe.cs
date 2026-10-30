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

/// <summary>实渲染基准只报告实测，不以“采样完成”冒充达到帧率目标。</summary>
public partial class SurvivalPerformanceProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag=>"SurvivalPerformance";
	protected override async Task RunChecks()
	{
		GD.Seed(4262026);
		var options=OS.GetCmdlineUserArgs();
		int enemyCount=int.TryParse(options.FirstOrDefault(a=>a.StartsWith("--perf-count="))?.Split('=')[1],out int count)?count:100;
		if(enemyCount is not (50 or 100))throw new InvalidOperationException("基准只接受50或100敌人。");
		EnemyCrowdMode crowd=Enum.TryParse(options.FirstOrDefault(a=>a.StartsWith("--perf-crowd="))?.Split('=')[1],out EnemyCrowdMode mode)?mode:EnemyCrowdMode.Physical;
		EnemyCrowdConfig.Current.Mode=crowd;
		if(DisplayServer.GetName()=="headless")throw new InvalidOperationException("性能基准必须使用真实GPU渲染。");
		var window=GetTree().Root; window.Mode=Window.ModeEnum.Windowed; window.Size=new Vector2I(3840,2160);
		window.Scaling3DScale=1; window.Msaa3D=Viewport.Msaa.Disabled;
		Engine.MaxFps=0; DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
		Player.IsInvulnerable=true;
		var settings=Director.Definition!;
		var enemies=new[]{ConfigRows.ById<EnemyConfig>("enemy_chaser")!,ConfigRows.ById<EnemyConfig>("enemy_ranged")!,ConfigRows.ById<EnemyConfig>("enemy_heavy_slammer")!};
		foreach(var phase in settings.Phases){phase.Enemies=enemies;phase.MaxAlive=enemyCount;phase.MaxAlivePressure=1000;phase.BudgetPerSecond=600;phase.EliteChance=0;}
		settings.SpawnInterval=.02f; settings.SpawnWarningSeconds=.1f;
		var map=ActRoguelike.World.MapManager.Instance!;
		if(map.TryValidPoint(new Vector3(0,.1f,20),Player.GlobalPosition,out var position,Player)) Player.GlobalPosition=position;
		Player.ResetPhysicsInterpolation();
		var camera=new Camera3D(); Mode.AddChild(camera); camera.GlobalPosition=Player.GlobalPosition+new Vector3(0,30,28);
		camera.LookAt(Player.GlobalPosition+Vector3.Up); camera.Current=true;
		Director.SetPhysicsProcess(true);
		var prepared=new HashSet<Unit>();
		for(int i=0;i<1800 && Director.AliveCount<enemyCount;i++)
		{
			foreach(var unit in Director.LivingEnemies)
				if(prepared.Add(unit)){unit.Stats.AddModifier(new StatModifier{Stat="MaxHealthAdd",Operation=ModifierOperation.Add,Value=50000,Source=this});unit.RefreshStats();unit.FillVitals();}
			await Wait(.05);
		}
		foreach(var unit in Director.LivingEnemies)
			if(prepared.Add(unit)){unit.Stats.AddModifier(new StatModifier{Stat="MaxHealthAdd",Operation=ModifierOperation.Add,Value=50000,Source=this});unit.RefreshStats();unit.FillVitals();}
		Check(Director.AliveCount==enemyCount,$"正式导演生成{enemyCount}名真实敌人，保留AI、导航、动画和攻击，拥挤模式{crowd}");
		if(Director.AliveCount!=enemyCount)throw new InvalidOperationException("敌人没有达到基准数量。");
		await Wait(3);
		// 拉伸视口的Texture.GetSize可包含额外缩放；使用真实GPU帧图核对物理像素。
		await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
		using var measuredImage=GetViewport().GetTexture().GetImage();
		Vector2I textureSize=measuredImage.GetSize();
		Check(textureSize==new Vector2I(3840,2160) && Mathf.IsEqualApprox(window.Scaling3DScale,1),$"实际纹理{textureSize}，3D比例为1，无降采样");
		var rid=GetViewport().GetViewportRid(); RenderingServer.ViewportSetMeasureRenderTime(rid,true);
		var times=new List<double>(4096); var gpu=new List<double>(4096); var renderCpu=new List<double>(4096); var process=new List<double>(4096); var physics=new List<double>(4096); var draws=new List<double>(4096);
		var frameMemory=new List<(double FrameMs,long AllocatedBytes,int Gc0,int Gc1,int Gc2)>(4096);
		int minAlive=enemyCount,maxAlive=enemyCount,requests=0; float next=0, overlapTimer=0;
		var attack=Player.GetNode<EnergyWaveAbility>("AbilityWeaponSkill1");
		Unit[] targets=Director.LivingEnemies;
		var positions=new Vector3[targets.Length]; var radii=targets.Select(u=>ActRoguelike.World.MapManager.BodySize(u).Radius).ToArray();
		var overlaps=new List<int>(128);
		RuntimeFrameProfiler.Reset(); RuntimeFrameProfiler.Active=true;
		ulong physicsBegan=Engine.GetPhysicsFrames();
		ulong began=Time.GetTicksUsec(), previous=began;
		long allocatedBefore=GC.GetTotalAllocatedBytes(); int[] collectionsBefore={GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
		long previousAllocated=allocatedBefore; int previousGc0=collectionsBefore[0],previousGc1=collectionsBefore[1],previousGc2=collectionsBefore[2];
		while(Time.GetTicksUsec()-began<12_000_000)
		{
			await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
			ulong now=Time.GetTicksUsec(); double ms=(now-previous)/1000.0;previous=now;
			times.Add(ms); gpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid)); renderCpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeCpu(rid));
			process.Add(Performance.GetMonitor(Performance.Monitor.TimeProcess)*1000);physics.Add(Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess)*1000);
			draws.Add(Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame));
			long frameAllocated=GC.GetTotalAllocatedBytes(); int gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);
			frameMemory.Add((ms,frameAllocated-previousAllocated,gc0-previousGc0,gc1-previousGc1,gc2-previousGc2));
			previousAllocated=frameAllocated;previousGc0=gc0;previousGc1=gc1;previousGc2=gc2;
			minAlive=Math.Min(minAlive,Director.AliveCount);maxAlive=Math.Max(maxAlive,Director.AliveCount);
			next-=(float)(ms/1000);
			overlapTimer-=(float)(ms/1000);
			if(overlapTimer<=0)
			{
				overlapTimer=.2f;int pairs=0;
				for(int i=0;i<targets.Length;i++)positions[i]=targets[i].GlobalPosition;
				for(int i=0;i<targets.Length;i++)for(int j=i+1;j<targets.Length;j++)
				{ Vector3 difference=positions[i]-positions[j]; if(Mathf.Abs(difference.Y)>1)continue;difference.Y=0;float radius=(radii[i]+radii[j])*.8f;if(difference.LengthSquared()<radius*radius)pairs++; }
				overlaps.Add(pairs);
			}
			if(next<=0)
			{
				next=.3f; var target=targets[requests%targets.Length];
				camera.LookAt(Projectile.GetTargetCenter(target));
				if(attack.TryPress(target))requests++;
			}
		}
		RenderingServer.ViewportSetMeasureRenderTime(rid,false);
		RuntimeFrameProfiler.Active=false;
		ulong physicsFrames=Engine.GetPhysicsFrames()-physicsBegan;
		var ordered=times.OrderBy(t=>t).ToArray(); int slow=Math.Max(1,(int)Math.Ceiling(times.Count*.01));
		double averageFps=1000/times.Average(),low1Fps=1000/ordered.TakeLast(slow).Average();
		bool goal=textureSize==new Vector2I(3840,2160) && minAlive>=enemyCount && averageFps>=60 && low1Fps>=60;
		string label=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--perf-label="))?.Split('=')[1]??"sample";
		var report=new{label,goalMet=goal,gpu=RenderingServer.GetVideoAdapterName(),cpu=OS.GetProcessorName(),engine=Engine.GetVersionInfo()["string"].AsString(),
			width=textureSize.X,height=textureSize.Y,renderScale=window.Scaling3DScale,msaa=window.Msaa3D.ToString(),minEnemies=minAlive,maxEnemies=maxAlive,playerAttackRequests=requests,
			crowdMode=crowd.ToString(),averageDeepOverlapPairs=overlaps.Count>0?overlaps.Average():0,maxDeepOverlapPairs=overlaps.Count>0?overlaps.Max():0,
			sampleSeconds=times.Sum()/1000,samples=times.Count,averageFps,low1Fps,p95FrameMs=ordered[Math.Min(ordered.Length-1,(int)(ordered.Length*.95))],p99FrameMs=ordered[Math.Min(ordered.Length-1,(int)(ordered.Length*.99))],maxFrameMs=ordered[^1],
			gpuMs=gpu.Average(),renderCpuMs=renderCpu.Average(),processMs=process.Average(),physicsMs=physics.Average(),drawCalls=draws.Average(),physicsFrames,
			frameSetupCpuMs=RenderingServer.GetFrameSetupTimeCpu(),
			allocatedMb=(GC.GetTotalAllocatedBytes()-allocatedBefore)/1048576.0,gc0=GC.CollectionCount(0)-collectionsBefore[0],gc1=GC.CollectionCount(1)-collectionsBefore[1],gc2=GC.CollectionCount(2)-collectionsBefore[2],
			profile=RuntimeFrameProfiler.Snapshot(physicsFrames,times.Count),
			memoryFrames=frameMemory.Select((f,i)=>new{frame=i,frameMs=f.FrameMs,allocatedBytes=f.AllocatedBytes,gc0=f.Gc0,gc1=f.Gc1,gc2=f.Gc2}).ToArray(),
			note="真实生产场景与导演，敌人额外生命避免采样时人数下降；AI、动作和物理保持开启，玩家实际尝试发射能量掌；平均与1%低帧均达60才记goalMet。质量使用当前配置，强制原生4K、无MSAA。"};
		string path=ValidationArtifacts.Path($"res://Logs/evidence/update-fixes/performance-{label}-{DateTime.Now:yyyyMMdd_HHmmss}.json");
		System.IO.File.WriteAllText(path,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
		GD.Print($"[SurvivalPerformance] MEASURED avg={averageFps:0.0} low1={low1Fps:0.0} GPU={gpu.Average():0.00}ms CPU={process.Average():0.00}ms Physics={physics.Average():0.00}ms goal={goal}");
		Check(times.Count>=30 && requests>0 && minAlive==enemyCount,$"基准完成有效采样，{enemyCount}敌人期间实际发出玩家攻击请求");
		await Shot($"performance-4k-{enemyCount}-{crowd}");
	}
}

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>专用进程内控制终结器时序，复现资源缓存复活窗口。不可挂在正常游戏场景。</summary>
public partial class ResourceLifetimeProbe : Node
{
	private static readonly ManualResetEventSlim FinalizerEntered = new(false);
	private static readonly ManualResetEventSlim ReleaseFinalizer = new(false);
	private static bool _nativeWrapper;
	private static bool _contentValidator;
	private static volatile bool _barrierTimedOut;
	private static string FixturePath => _contentValidator ? "res://Config/Tables/Upgrades/evo_spare_dodge.tres" : _nativeWrapper ? "res://Scenes/Debug/resource_lifetime_native.tres" : "res://Scenes/Debug/resource_lifetime_fixture.tres";
	private sealed class FinalizerBarrier
	{
		~FinalizerBarrier()
		{
			FinalizerEntered.Set();
			if (!ReleaseFinalizer.Wait(TimeSpan.FromSeconds(30))) _barrierTimedOut = true;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ArmBarrier() => _ = new FinalizerBarrier();

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Resource Load(bool unsafeMode)
		=> unsafeMode
			? ResourceLoader.Load<Resource>(FixturePath)
			: _contentValidator ? ContentValidator.LoadAuthoredRow(FixturePath)!
			: RuntimeResources.Load<Resource>(FixturePath);

	private static string Describe(Resource resource) => resource is ParamEntry param
		? $"{param.Key}={param.Value}"
		: $"{resource.GetClass()}/{resource.ResourceName}";

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static WeakReference<Resource> LoadAndRelease(bool unsafeMode)
	{
		var resource = Load(unsafeMode);
		if (!ReferenceEquals(resource, Load(unsafeMode))) throw new InvalidOperationException("存活资源没有复用");
		GD.Print($"[Lifetime] 首次加载 {Describe(resource)} id={resource.GetInstanceId()} unsafe={unsafeMode} path={resource.ResourcePath} 存活包装器已复用");
		return new WeakReference<Resource>(resource);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool IsAlive(WeakReference<Resource> resource) => resource.TryGetTarget(out _);

	public override async void _Ready()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		bool unsafeMode = Array.IndexOf(OS.GetCmdlineUserArgs(), "--unsafe-cache") >= 0;
		_nativeWrapper = Array.IndexOf(OS.GetCmdlineUserArgs(), "--native-wrapper") >= 0;
		_contentValidator = Array.IndexOf(OS.GetCmdlineUserArgs(), "--content-validator") >= 0;
		try
		{
			if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--content-stress") >= 0)
			{
				for (int round = 1; round <= 8; round++)
				{
					var issues = ContentValidator.Validate(ConfigIndex.Settings, scanUnindexed: true);
					if (issues.Count != 0) throw new InvalidOperationException($"第{round}轮内容校验失败：{string.Join("；", issues)}");
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
					GC.WaitForPendingFinalizers();
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
					GD.Print($"[Lifetime] 内容压力轮次{round}/8：问题0，完整GC与终结器完成");
				}
				GD.Print("[Lifetime] 内容压力完成：8轮扫描、GC与终结器");
				GameExit.Request(GetTree());
				return;
			}
			ArmBarrier();
			GC.Collect();
			if (!FinalizerEntered.Wait(TimeSpan.FromSeconds(2))) throw new InvalidOperationException("终结器屏障未进入");
			// 首次读取也调用生产入口；原故障正是扫描把默认缓存对象留到了后续读取。
			var weak = LoadAndRelease(unsafeMode);
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
			bool alive = IsAlive(weak);
			GD.Print($"[Lifetime] 托管对象存活={alive}；终结器阻塞期间再次加载 unsafe={unsafeMode}");
			if (alive) throw new InvalidOperationException("托管包装器未被回收，复现条件不成立");
			Resource? again = null;
			if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--skip-reload") >= 0)
			{
				GD.Print("[Lifetime] 对照：跳过再次加载，只运行原对象终结器");
			}
			else if (_contentValidator && Array.IndexOf(OS.GetCmdlineUserArgs(), "--direct-reload") < 0)
			{
				var issues = ContentValidator.Validate(ConfigIndex.Settings, scanUnindexed: true);
				if (issues.Count != 0) throw new InvalidOperationException($"实际内容校验失败：{string.Join("；", issues)}");
				GD.Print("[Lifetime] 实际ContentValidator完成：生产索引与目录扫描问题0");
			}
			else
			{
				again = Load(unsafeMode);
				GD.Print($"[Lifetime] 再次加载 {Describe(again)} id={again.GetInstanceId()}");
			}
			if (_barrierTimedOut) throw new InvalidOperationException("终结器屏障超时，本次结果不能作为竞态覆盖证据。");
			ReleaseFinalizer.Set();
			GC.WaitForPendingFinalizers();
			GC.KeepAlive(again);
			GD.Print("[Lifetime] 完成：已覆盖收集后、终结前的再次加载");
			if (!unsafeMode && DisplayServer.GetName() != "headless")
			{
				Node scene = RuntimeResources.Load<PackedScene>("res://Scenes/Levels/Base.tscn").Instantiate();
				GetTree().Root.AddChild(scene);
				GetTree().CurrentScene = scene;
				await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
				var layer = new CanvasLayer { Layer = 120 };
				layer.AddChild(new Label { Text = "GC lifetime probe: collected wrapper / blocked finalizer / reload PASS", Position = new Vector2(24, 88) });
				AddChild(layer);
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				Error shot = GetViewport().GetTexture().GetImage().SavePng(ActRoguelike.Testing.ValidationArtifacts.Path("res://Logs/evidence/screenshots/i_lifetime_after_gc.png"));
				if (shot != Error.Ok) throw new InvalidOperationException($"截图失败：{shot}");
				GD.Print("[Lifetime] 截图 i_lifetime_after_gc.png：GC竞态探针后真实基地渲染正常");
			}
			GameExit.Request(GetTree());
		}
		catch (Exception ex)
		{
			ReleaseFinalizer.Set();
			GD.Print($"[Lifetime] FAIL {ex}");
			GameExit.Request(GetTree(), 1);
		}
	}
}

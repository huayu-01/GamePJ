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
	private static string FixturePath => _nativeWrapper ? "res://Scenes/Debug/resource_lifetime_native.tres" : "res://Scenes/Debug/resource_lifetime_fixture.tres";
	private sealed class FinalizerBarrier
	{
		~FinalizerBarrier()
		{
			FinalizerEntered.Set();
			ReleaseFinalizer.Wait(TimeSpan.FromSeconds(8));
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ArmBarrier() => _ = new FinalizerBarrier();

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Resource Load(bool unsafeMode)
		=> unsafeMode
			? ResourceLoader.Load<Resource>(FixturePath)
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
		try
		{
			ArmBarrier();
			GC.Collect();
			if (!FinalizerEntered.Wait(TimeSpan.FromSeconds(2))) throw new InvalidOperationException("终结器屏障未进入");
			var weak = LoadAndRelease(unsafeMode);
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
			bool alive = IsAlive(weak);
			GD.Print($"[Lifetime] 托管对象存活={alive}；终结器阻塞期间再次加载 unsafe={unsafeMode}");
			if (alive) throw new InvalidOperationException("托管包装器未被回收，复现条件不成立");
			Resource again = Load(unsafeMode);
			GD.Print($"[Lifetime] 再次加载 {Describe(again)} id={again.GetInstanceId()}");
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
				Error shot = GetViewport().GetTexture().GetImage().SavePng("res://docs/screenshots/i_lifetime_after_gc.png");
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

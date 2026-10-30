using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ActRoguelike.Core;

/// <summary>仅基准采样时启用；默认关闭，作用域为栈上结构，不为每帧创建托管对象。</summary>
public static class RuntimeFrameProfiler
{
	private sealed class Counter { public long Ticks,Calls,AllocatedBytes; }
	private static readonly Dictionary<string,Counter> Counters=new();
	public static bool Active { get; set; }
	public static void Reset()=>Counters.Clear();
	public static Sample Measure(string name)=>Active?new Sample(name):default;
	public readonly struct Sample : IDisposable
	{
		private readonly string? _name;
		private readonly long _started, _allocated;
		public Sample(string name){_name=name;_started=Stopwatch.GetTimestamp();_allocated=GC.GetAllocatedBytesForCurrentThread();}
		public void Dispose()
		{
			if(_name==null)return;
			long elapsed=Stopwatch.GetTimestamp()-_started, allocated=GC.GetAllocatedBytesForCurrentThread()-_allocated;
			if(!Counters.TryGetValue(_name,out var counter)){counter=new Counter();Counters.Add(_name,counter);}
			counter.Ticks+=elapsed;counter.Calls++;counter.AllocatedBytes+=allocated;
		}
	}
	public static Dictionary<string,object> Snapshot(ulong physicsFrames,int renderFrames)
	{
		var result=new Dictionary<string,object>();
		foreach(var entry in Counters)
		{
			double ms=entry.Value.Ticks*1000.0/Stopwatch.Frequency;
			// 包含作用域内的子调用；嵌套项不能相加当作总分配或总耗时。
			result[entry.Key]=new{calls=entry.Value.Calls,totalMs=ms,msPerPhysicsFrame=ms/Math.Max(1,physicsFrames),msPerRenderFrame=ms/Math.Max(1,renderFrames),allocatedBytes=entry.Value.AllocatedBytes,bytesPerCall=(double)entry.Value.AllocatedBytes/Math.Max(1,entry.Value.Calls)};
		}
		return result;
	}
}

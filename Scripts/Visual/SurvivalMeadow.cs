using System;
using Godot;

namespace ActRoguelike.Visual;

/// <summary>正式生存场景的丛林与草地入口；只增加视觉，原碰撞和导航仍由Arena提供。</summary>
public partial class SurvivalMeadow : Node3D
{
	[Export] public bool Enabled { get; set; } = true;
	[Export] public bool GrassEnabled { get; set; } = true;
	/// <summary>草地的**绘制块**大小（米）：只影响 draw call 数量，不影响草的位置。8 米约为 4 米的一半 draw call。</summary>
	[Export] public float GrassChunkSize { get; set; } = 8;
	/// <summary>草地的**生成格**（米）：决定草的位置；改它才会改变布局。</summary>
	[Export] public float GrassGenerationCell { get; set; } = 4;
	public bool Built { get; private set; }
	public double BuildMilliseconds { get; private set; }
	public double BuildAllocatedMb { get; private set; }
	public ShaderGrassPatch? Grass { get; private set; }
	public JungleGrassScenery? Scenery { get; private set; }

	public override async void _Ready()
	{
		if(!Enabled) return;
		// 场景刚进树时物理空间尚未同步，等碰撞进入空间后再采样草根。
		await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
		await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
		if(!IsInsideTree()||IsQueuedForDeletion()||!Enabled) return;
		BuildGrass();
		Built=true;
	}
	/// <summary>用新的绘制块大小重生成草地（草的位置不变，只有 MultiMesh 划分变化）。</summary>
	public void Rebuild(float chunkSize)
	{
		GrassChunkSize=chunkSize;
		if(Grass!=null&&IsInstanceValid(Grass)) { RemoveChild(Grass); Grass.Free(); Grass=null; }
		BuildGrass();
		Built=true;
	}
	private void BuildGrass()
	{
		ulong started=Time.GetTicksUsec(); long allocated=GC.GetTotalAllocatedBytes();
		var arena=GetParent().GetNode<Node3D>("Arena");
		if(Scenery==null)
		{
			Scenery=new JungleGrassScenery {Name="JungleScenery"}; AddChild(Scenery); Scenery.Build(arena);
		}
		Grass=new ShaderGrassPatch {Name="ShaderGrass",BuildReference=false,ChunkSize=GrassChunkSize,GenerationCell=GrassGenerationCell};
		AddChild(Grass);
		Grass.GlobalPosition=arena.GlobalPosition; Grass.RegisterSceneExclusions(arena,arena.GetNode("Ground")); Grass.Build();
		SetGrassEnabled(GrassEnabled);
		BuildMilliseconds=(Time.GetTicksUsec()-started)/1000.0;
		BuildAllocatedMb=(GC.GetTotalAllocatedBytes()-allocated)/1048576.0;
	}
	public void SetGrassEnabled(bool enabled)
	{
		GrassEnabled=enabled;
		Grass?.ApplyVariant(enabled?ShaderGrassPatch.Variant.Batched:ShaderGrassPatch.Variant.Ground);
	}
}

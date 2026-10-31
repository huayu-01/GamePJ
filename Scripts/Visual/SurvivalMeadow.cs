using System;
using Godot;

namespace ActRoguelike.Visual;

/// <summary>正式生存场景的丛林与草地入口；只增加视觉，原碰撞和导航仍由Arena提供。</summary>
public partial class SurvivalMeadow : Node3D
{
	[Export] public bool Enabled { get; set; } = true;
	[Export] public bool GrassEnabled { get; set; } = true;
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
		ulong started=Time.GetTicksUsec(); long allocated=GC.GetTotalAllocatedBytes();
		var arena=GetParent().GetNode<Node3D>("Arena");
		Scenery=new JungleGrassScenery {Name="JungleScenery"}; AddChild(Scenery); Scenery.Build(arena);
		Grass=new ShaderGrassPatch {Name="ShaderGrass",BuildReference=false}; AddChild(Grass);
		Grass.GlobalPosition=arena.GlobalPosition; Grass.RegisterSceneExclusions(arena,arena.GetNode("Ground")); Grass.Build();
		SetGrassEnabled(GrassEnabled);
		BuildMilliseconds=(Time.GetTicksUsec()-started)/1000.0;
		BuildAllocatedMb=(GC.GetTotalAllocatedBytes()-allocated)/1048576.0;
		Built=true;
	}
	public void SetGrassEnabled(bool enabled)
	{
		GrassEnabled=enabled;
		Grass?.ApplyVariant(enabled?ShaderGrassPatch.Variant.Batched:ShaderGrassPatch.Variant.Ground);
	}
}

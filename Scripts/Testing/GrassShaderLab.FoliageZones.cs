using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ActRoguelike.Core;
using ActRoguelike.Visual;
using Godot;

namespace ActRoguelike.Testing;

public partial class GrassShaderLab
{
	private static List<Transform3D> Roots(ShaderGrassPatch patch)=>patch.GetChildren().OfType<MultiMeshInstance3D>()
		.SelectMany(chunk=>Enumerable.Range(0,chunk.Multimesh!.InstanceCount).Select(i=>chunk.GlobalTransform*chunk.Multimesh.GetInstanceTransform(i))).ToList();
	private async Task CheckFoliageZones()
	{
		bool rendered=DisplayServer.GetName()!="headless";
		Check(_grass.ExclusionCount>0&&_grass.ExcludedCandidates>0,$"正式样板登记{_grass.ExclusionCount}个禁草体积，跳过{_grass.FullyExcludedChunks}整块／{_grass.ExcludedCandidates}候选，地面射线{_grass.GroundRaycastCount}");
		// 无头Dummy渲染器将MultiMesh变换回读为单位矩阵；实例位置必须在实渲染验证。
		if(rendered) Check(Roots(_grass).All(root=>!_grass.IsExcluded(root.Origin)),"实际MultiMesh内没有位于建筑禁草体积中的草根");
		Check(_scenery.StyledLeafSurfaces>20&&_scenery.FoliageStyle.SharedMaterialCount<=2,$"{_scenery.StyledLeafSurfaces}个树叶表面共享{_scenery.FoliageStyle.SharedMaterialCount}份柔和材质");
		var fieldBefore=new ShaderGrassPatch { Visible=false }; Mode.AddChild(fieldBefore); fieldBefore.Build();
		Check(_grass.GroundRaycastCount<fieldBefore.GroundRaycastCount,$"同种子全场生成射线：{fieldBefore.GroundRaycastCount}→{_grass.GroundRaycastCount}");
		ShaderGrassPatch CreatePatch()
		{
			var patch=new ShaderGrassPatch { FieldSize=new Vector2(12,12) }; Mode.AddChild(patch); patch.GlobalPosition=new Vector3(12,0,35); return patch;
		}
		var baseline=CreatePatch(); baseline.Build(); await Wait(.08); var baselineRoots=Roots(baseline); baseline.Visible=false;
		var full=CreatePatch(); full.RegisterExclusionBox(new Transform3D(Basis.Identity,full.GlobalPosition),new Vector3(12,2,12)); full.Build();
		Check(full.FullyExcludedChunks==9&&full.FullCount==0&&full.ChunkCount==0&&full.GroundRaycastCount==0&&!full.IsProcessing(),"12×12米完整占地：跳过9块、0实例、0地面射线、无逐帧更新");
		var partial=CreatePatch(); var transform=new Transform3D(new Basis(Vector3.Up,Mathf.Pi*.25f),partial.GlobalPosition);
		partial.RegisterExclusionBox(transform,new Vector3(5,2,8)); partial.Build(); await Wait(.08); var remaining=Roots(partial); partial.Visible=false;
		Check(partial.FullCount>0&&partial.FullCount<baseline.FullCount,"旋转45度占地保留外围草，并减少实例数");
		if(rendered)
		{
			Check(remaining.All(root=>!partial.IsExcluded(root.Origin)),"旋转45度的禁草区内部没有实际草实例");
			var expected=baselineRoots.Where(root=>!partial.IsExcluded(root.Origin)).ToHashSet();
			Check(expected.SetEquals(remaining),"加入禁草区后，区域外每簇位置、旋转、尺寸与原来完全一致");
		}
		Check(partial.GroundRaycastCount<baseline.GroundRaycastCount,$"旋转占地节省地面射线：{baseline.GroundRaycastCount}→{partial.GroundRaycastCount}");
		var authored=new Node3D(); Mode.AddChild(authored);
		var roof=new StaticBody3D { Position=new Vector3(12,4,35) }; authored.AddChild(roof); roof.AddChild(new CollisionShape3D { Shape=new BoxShape3D { Size=new Vector3(8,1,8) } });
		var elevated=CreatePatch(); elevated.RegisterSceneExclusions(authored,Mode.GetNode("Arena/Ground")); elevated.Build(); await Wait(.08); elevated.Visible=false;
		Check(elevated.ExclusionCount==0&&elevated.FullCount==baseline.FullCount,"架空建筑没有自动清空可见地面草");
		if(rendered) Check(Roots(elevated).SequenceEqual(baselineRoots),"架空建筑下实际草簇变换保持一致");
		// 显式禁草区域不依赖建筑碰撞或材质，适合装饰建筑和不规则占地的多个盒子。
		authored.AddChild(new GrassExclusionZone { Position=new Vector3(12,0,35),Size=new Vector3(12,2,12) });
		var explicitZone=CreatePatch(); explicitZone.RegisterSceneExclusions(authored,Mode.GetNode("Arena/Ground")); explicitZone.Build();
		Check(explicitZone.FullyExcludedChunks==9&&explicitZone.FullCount==0,"显式禁草组件可控制无贴地碰撞的建筑占地");
		System.IO.File.WriteAllText(ValidationArtifacts.Path("res://Logs/evidence/foliage-zones/zones.json"),JsonSerializer.Serialize(new {
			field=new{_grass.NormalCount,_grass.ChunkCount,_grass.ExclusionCount,_grass.ExcludedCandidates,_grass.FullyExcludedChunks,_grass.GroundRaycastCount},
			fieldWithoutExclusions=new{fieldBefore.NormalCount,fieldBefore.ChunkCount,fieldBefore.GroundRaycastCount},
			baseline=new{baseline.FullCount,baseline.GroundRaycastCount},rotated=new{partial.FullCount,partial.GroundRaycastCount},full=new{full.FullCount,full.ChunkCount,full.GroundRaycastCount,full.FullyExcludedChunks},
			foliage=new{_scenery.StyledLeafSurfaces,_scenery.FoliageStyle.SharedMaterialCount}
		},new JsonSerializerOptions {WriteIndented=true}));
		if(rendered)
		{
			_grass.Visible=false; partial.Visible=true; _caption.Visible=false;
			_camera.GlobalPosition=new Vector3(12,14,22); _camera.LookAt(partial.GlobalPosition); _camera.ResetPhysicsInterpolation();
			await Wait(.2); await ZoneShot("rotated-exclusion");
			_grass.Visible=true; partial.Visible=false;
		}
		foreach(var patch in new[]{fieldBefore,baseline,full,partial,elevated,explicitZone}) patch.QueueFree(); authored.QueueFree(); await Wait(.1);
		var trees=new Node3D(); Mode.AddChild(trees);
		var prototype=RuntimeResources.Load<PackedScene>("res://Scenes/Levels/NatureArena/Prefabs/CommonTree_3.tscn");
		var tree=prototype.Instantiate<Node3D>(); trees.AddChild(tree); tree.GlobalPosition=new Vector3(0,0,6);
		foreach(var collider in Walk(tree).OfType<CollisionObject3D>().ToArray()) {collider.GetParent().RemoveChild(collider);collider.Free();}
		var surfaces=Walk(tree).OfType<MeshInstance3D>().SelectMany(mesh=>Enumerable.Range(0,mesh.Mesh!.GetSurfaceCount()).Select(i=>(Mesh:mesh,Index:i,Original:mesh.GetActiveMaterial(i)))).ToArray();
		var style=new SoftFoliageStyle();
		int applied=style.Apply(tree);
		Check(applied>0&&surfaces.Where(s=>s.Original is StandardMaterial3D m&&!m.ResourceName.StartsWith("Leaves",StringComparison.Ordinal)).All(s=>s.Mesh.GetActiveMaterial(s.Index)==s.Original),"柔和方案只替换叶面，树干保留原材质");
		foreach(string name in new[]{"Pine_3","TwistedTree_3"})
		{
			var other=RuntimeResources.Load<PackedScene>($"res://Scenes/Levels/NatureArena/Prefabs/{name}.tscn").Instantiate<Node3D>();
			Check(style.Apply(other)>0,$"替换{name}模型仍能识别叶面并自动计算树冠范围"); other.Free();
		}
		if(DisplayServer.GetName()!="headless")
		{
			_caption.Visible=false; _grass.SetProcess(false);
			Vector3 target=tree.GlobalPosition+new Vector3(0,4,0);
			for(int angle=0;angle<=90;angle+=45)
			{
				_camera.GlobalPosition=target+new Vector3(0,3,15).Rotated(Vector3.Up,Mathf.DegToRad(angle)); _camera.LookAt(target); _camera.ResetPhysicsInterpolation();
				foreach(var s in surfaces) s.Mesh.SetSurfaceOverrideMaterial(s.Index,s.Original);
				await Wait(.25); await ZoneShot("tree-before-"+angle);
				style.Apply(tree); await Wait(.25); await ZoneShot("tree-after-"+angle);
			}
			trees.QueueFree(); await Wait(.1);
			SetView(true); await Wait(.3); await ZoneShot("overview");
			SetView(false); await Wait(.3); await ZoneShot("follow");
			_camera.GlobalPosition=new Vector3(-18,6,23); _camera.LookAt(new Vector3(-17,.2f,14)); _camera.ResetPhysicsInterpolation();
			await Wait(.3); await ZoneShot("building-edge");
			_grass.SetProcess(true); _caption.Visible=true;
		}
		else trees.QueueFree();
	}
	private async Task ZoneShot(string name)
	{
		await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
		using var image=GetViewport().GetTexture().GetImage();
		Check(image.SavePng(ValidationArtifacts.Path($"res://Logs/evidence/foliage-zones/{name}.png"))==Error.Ok,"真实渲染 "+name);
	}
}

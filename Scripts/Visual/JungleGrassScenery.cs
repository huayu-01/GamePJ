using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Visual;

/// <summary>将生存庭院视觉改为丛林；正式入口与草地样板复用，保留原碰撞和导航几何。</summary>
public partial class JungleGrassScenery : Node3D
{
	[Export] public PackedScene? TreePrototype { get; set; }
	[Export] public SoftFoliageStyle FoliageStyle { get; set; } = new();
	public int StyledLeafSurfaces { get; private set; }
	public int ReplacedStructures { get; private set; }
	public int BoundaryRockCount { get; private set; }
	public int FernCount { get; private set; }
	private ShaderMaterial _stone=null!,_bark=null!;
	private StandardMaterial3D _leaf=null!;
	private Mesh _rock=null!,_leafMesh=null!;
	private CylinderMesh _vine=null!;
	private RandomNumberGenerator _random=new() { Seed=20261029 };
	private static IEnumerable<Node> Walk(Node root) { yield return root; foreach(Node child in root.GetChildren()) foreach(var node in Walk(child)) yield return node; }
	public void Build(Node3D arena)
	{
		_stone=new ShaderMaterial { Shader=RuntimeResources.Load<Shader>("res://Assets/Effects/jungle_surface.gdshader") };
		_bark=(ShaderMaterial)_stone.Duplicate(); _bark.SetShaderParameter("bark",true);
		_leaf=new StandardMaterial3D { AlbedoColor=new Color("507934"),Roughness=1,CullMode=BaseMaterial3D.CullModeEnum.Disabled };
		_leafMesh=LeafMesh(); _vine=new CylinderMesh { TopRadius=.8f,BottomRadius=1,Height=1,RadialSegments=7,Rings=1 };
		var source=RuntimeResources.Load<PackedScene>("res://Scenes/Levels/NatureArena/Prefabs/Rock_Medium_1.tscn").Instantiate<Node3D>();
		_rock=Walk(source).OfType<MeshInstance3D>().First().Mesh!; source.Free();
		arena.GetNode<MeshInstance3D>("Ground/Mesh").MaterialOverride=new ShaderMaterial { Shader=RuntimeResources.Load<Shader>("res://Assets/Effects/jungle_ground.gdshader") };
		foreach(Node child in arena.GetChildren())
		{
			string name=child.Name;
			if(name.StartsWith("CrossPath",StringComparison.Ordinal)||name.StartsWith("OuterPath",StringComparison.Ordinal)||name.StartsWith("RuinCap",StringComparison.Ordinal))
				((Node3D)child).Visible=false;
			else if(name.StartsWith("Boundary",StringComparison.Ordinal)) child.GetNode<MeshInstance3D>("Mesh").Visible=false;
			else if(name.StartsWith("OuterTree",StringComparison.Ordinal))
			{
				var tree=(Node3D)child;
				foreach(var mesh in Walk(tree).OfType<MeshInstance3D>()) mesh.Visible=false;
				var broad=TreeVisual(); AddChild(broad); broad.GlobalTransform=tree.GlobalTransform;
			}
			else if(name.StartsWith("RuinWall",StringComparison.Ordinal))
			{
				var body=(Node3D)child; body.GetNode<MeshInstance3D>("Mesh").Visible=false;
				Vector3 center=body.GlobalPosition;
				for(int i=-1;i<=1;i++) Rock(center+new Vector3(i*2.8f,0,0),new Vector3(3.2f,1.85f+_random.Randf()*.18f,1.15f));
				DecorateWall(center,8.4f,1.8f); ReplacedStructures++;
			}
			else if(name.StartsWith("Pillar",StringComparison.Ordinal))
			{
				var body=(Node3D)child; body.GetNode<MeshInstance3D>("Mesh").Visible=false;
				Vector3 p=body.GlobalPosition; Rock(p,new Vector3(1.55f,3.05f,1.5f),_random.Randf());
				Segment(p+new Vector3(-.5f,-1.4f,.76f),p+new Vector3(.15f,1.55f,.75f),.08f);
				Fern(p+new Vector3(.35f,1.45f,0),.9f); ReplacedStructures++;
			}
			else if(name.StartsWith("NorthArchPillar",StringComparison.Ordinal))
			{
				var body=(Node3D)child; body.GetNode<MeshInstance3D>("Mesh").Visible=false;
				Vector3 p=body.GlobalPosition;
				Segment(p-Vector3.Up*2.6f,p+new Vector3(.12f,2.9f,.12f),.65f);
				for(int root=0;root<4;root++)
				{
					Vector3 outward=new Vector3(Mathf.Cos(root*1.6f),0,Mathf.Sin(root*1.6f));
					Segment(p-Vector3.Up*1.4f,p-Vector3.Up*2.5f+outward*1.05f,.18f);
				}
				Fern(p+Vector3.Up*2.65f,2.0f); ReplacedStructures++;
			}
			else if(name.StartsWith("NorthArchTop",StringComparison.Ordinal))
			{
				var mesh=(MeshInstance3D)child; mesh.Visible=false;
				Vector3 p=mesh.GlobalPosition;
				Segment(p-Vector3.Right*3.4f,p+Vector3.Right*3.4f,.38f);
				for(int leaf=0;leaf<7;leaf++) Fern(p+new Vector3(leaf-.5f*6,0,0),1.15f);
			}
			else if(child is MeshInstance3D mesh&&name.StartsWith("WellEdge",StringComparison.Ordinal))
			{
				mesh.Visible=false; Vector3 p=mesh.GlobalPosition;
				Vector3 direction=name.StartsWith("WellEdgeX",StringComparison.Ordinal)?Vector3.Forward:Vector3.Right;
				Segment(p-direction*2.5f,p+direction*2.5f,.2f);
			}
			else if(child is StaticBody3D platform&&name is not "Ground"&&platform.GetNodeOrNull<MeshInstance3D>("Mesh") is { } platformMesh)
				platformMesh.MaterialOverride=_stone;
		}
		for(int edge=0;edge<4;edge++)
		for(float distance=-50;distance<=50;distance+=3.5f)
		{
			Vector3 p=edge switch {0=>new(distance,2.0f,-52),1=>new(distance,2.0f,52),2=>new(-52,2.0f,distance),_=>new(52,2.0f,distance)};
			Rock(p,new Vector3(4.1f,4.0f+_random.RandfRange(-.25f,.6f),1.8f),edge<2?0:Mathf.Pi*.5f); BoundaryRockCount++;
			Fern(p-Vector3.Up*1.6f+new Vector3(_random.RandfRange(-.5f,.5f),0,_random.RandfRange(-.5f,.5f)),1.7f);
			if(edge<2)
			{
				Segment(p+new Vector3(-1.5f,-1.5f,edge==0?1:-1),p+new Vector3(.6f,1.7f,edge==0?1:-1),.1f);
				Fern(p+Vector3.Up*1.7f,1.3f);
			}
		}
		foreach(Vector3 center in new[]{new Vector3(-29,0,-22),new(29,0,-22),new(-29,0,24),new(29,0,24),new(-37,0,2),new(37,0,2)})
		{
			for(int i=0;i<3;i++)
			{
				var tree=TreeVisual(); AddChild(tree); tree.Position=center+new Vector3(i*2.2f-2.2f,0,(i%2)*3.6f);
				tree.Scale=Vector3.One*_random.RandfRange(.85f,1.15f); tree.Rotation=Vector3.Up*_random.Randf()*Mathf.Tau;
			}
		}
		for(int i=0;i<155;i++)
		{
			Vector3 p=new(_random.RandfRange(-48,48),.02f,_random.RandfRange(-48,48));
			// 大叶植物避开主要移动区域；全地面短草不受此限制。
			if(Mathf.Abs(p.X)<7 || p.Length()<8) continue;
			Fern(p,_random.RandfRange(.8f,1.4f));
		}
		var light=arena.GetNode<DirectionalLight3D>("Moonlight"); light.LightColor=new Color(.97f,.98f,.90f); light.LightEnergy=.9f;
		var world=arena.GetNode<WorldEnvironment>("WorldEnvironment");
		var environment=(Godot.Environment)world.Environment.Duplicate(true); world.Environment=environment;
		environment.BackgroundColor=new Color("637b6b"); environment.AmbientLightColor=new Color(.62f,.72f,.58f); environment.AmbientLightEnergy=.55f;
		environment.FogEnabled=true; environment.FogLightColor=new Color("718d77"); environment.FogDensity=.0012f;
	}
	public void SetOverview(bool enabled)
	{
		foreach(var mesh in Walk(this).OfType<MeshInstance3D>().Where(mesh=>mesh.Mesh==_leafMesh)) mesh.VisibilityRangeEnd=enabled?0:80;
	}
	private Node3D TreeVisual()
	{
		var root=(TreePrototype??RuntimeResources.Load<PackedScene>("res://Scenes/Levels/NatureArena/Prefabs/CommonTree_3.tscn")).Instantiate<Node3D>();
		foreach(var collider in Walk(root).OfType<CollisionObject3D>().ToArray()) { collider.GetParent().RemoveChild(collider); collider.Free(); }
		StyledLeafSurfaces+=FoliageStyle.Apply(root);
		return root;
	}
	private void Rock(Vector3 center,Vector3 size,float angle=0)
	{
		var bounds=_rock.GetAabb(); Vector3 scale=size/bounds.Size;
		var rotation=new Basis(Vector3.Up,angle);
		var basis=rotation*new Basis(new Vector3(scale.X,0,0),new Vector3(0,scale.Y,0),new Vector3(0,0,scale.Z));
		AddChild(new MeshInstance3D { Mesh=_rock,MaterialOverride=_stone,Transform=new Transform3D(basis,center-basis*bounds.GetCenter()) });
	}
	private void Segment(Vector3 from,Vector3 to,float radius)
	{
		Vector3 direction=(to-from).Normalized(); Vector3 guide=Mathf.Abs(direction.Dot(Vector3.Forward))>.97f?Vector3.Right:Vector3.Forward;
		Vector3 x=direction.Cross(guide).Normalized(), z=x.Cross(direction).Normalized();
		var basis=new Basis(x*radius,direction*from.DistanceTo(to),z*radius);
		AddChild(new MeshInstance3D { Mesh=_vine,MaterialOverride=_bark,Transform=new Transform3D(basis,(from+to)*.5f) });
	}
	private void Fern(Vector3 at,float size)
	{
		FernCount++;
		var basis=new Basis(Vector3.Up,_random.Randf()*Mathf.Tau).Scaled(Vector3.One*size);
		AddChild(new MeshInstance3D { Mesh=_leafMesh,MaterialOverride=_leaf,Transform=new Transform3D(basis,at),
			CastShadow=GeometryInstance3D.ShadowCastingSetting.Off,VisibilityRangeEnd=80 });
	}
	private void DecorateWall(Vector3 center,float width,float height)
	{
		for(int i=0;i<4;i++)
		{
			float x=-width*.41f+i*width*.27f,phase=_random.Randf()*Mathf.Tau;
			Vector3 Point(float t)=>center+new Vector3(x+.16f*Mathf.Sin(t*6+phase),height*(.55f-t),.64f+.06f*Mathf.Sin(t*9+phase));
			for(int part=0;part<10;part++) Segment(Point(part*.1f),Point((part+1)*.1f),Mathf.Lerp(.035f,.017f,part*.1f));
			Fern(Point(0),.65f); Fern(Point(.42f),.38f);
		}
	}
	private static ArrayMesh LeafMesh()
	{
		using var tool=new SurfaceTool(); tool.Begin(Mesh.PrimitiveType.Triangles);
		Vector3[] vertices={Vector3.Zero,new(-.17f,.04f,.32f),new(0,.13f,.36f),new(.17f,.04f,.32f),new(0,.035f,.86f)};
		for(int leaf=0;leaf<9;leaf++)
		{
			var basis=new Basis(Vector3.Up,leaf*2.39996f)*new Basis(Vector3.Right,leaf<6?-.30f:-.82f);
			foreach(int i in new[]{0,1,2,0,2,3,1,4,2,2,4,3}) { tool.SetNormal(basis*Vector3.Up); tool.AddVertex(basis*vertices[i]); }
		}
		return tool.Commit();
	}
}

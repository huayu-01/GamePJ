using System;
using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Visual;

/// <summary>独立草地样板：固定种子、地面采样、分块合批与同量独立网格对照；不参与战斗判定。</summary>
public partial class ShaderGrassPatch : Node3D
{
	public enum Variant { Ground, Batched, Individual, Dense }
	[Export] public Vector2 FieldSize { get; set; } = new(100,100);
	[Export] public Vector2 FieldCenter { get; set; } = Vector2.Zero;
	[Export] public float ChunkSize { get; set; } = 4;
	[Export] public float Density { get; set; } = 6;
	[Export] public float WindFrequency { get; set; } = .45f;
	[Export] public int Seed { get; set; } = 29102026;
	[Export] public bool BuildReference { get; set; }
	public int FullCount { get; private set; }
	public int NormalCount { get; private set; }
	public int ChunkCount => _chunks.Count;
	public Aabb InstanceBounds { get; private set; }
	public Variant CurrentVariant { get; private set; }
	public float WindTime { get; private set; }
	public ShaderMaterial GrassMaterial { get; private set; } = null!;
	private readonly List<(MultiMeshInstance3D Batch, Node3D? Singles, int Count)> _chunks = new();
	private ArrayMesh _mesh = null!;

	public void Build()
	{
		if(_chunks.Count>0) throw new InvalidOperationException("草地已经生成");
		if(!float.IsFinite(ChunkSize)||ChunkSize<=0||!FieldSize.IsFinite()||FieldSize.X<=0||FieldSize.Y<=0||!float.IsFinite(Density)||Density<=0)
			throw new ArgumentOutOfRangeException(nameof(ChunkSize),"草地大小、分块和密度必须为有效正数");
		ProcessMode=ProcessModeEnum.Pausable;
		GrassMaterial=new ShaderMaterial { Shader=RuntimeResources.Load<Shader>("res://Assets/Effects/grass_patch.gdshader") };
		GrassMaterial.SetShaderParameter("wind_frequency",WindFrequency);
		_mesh=BladeMesh();
		var random=new RandomNumberGenerator { Seed=(ulong)Seed };
		var space=GetWorld3D().DirectSpaceState;
		// 节点原点指定地面基准，不能拿中心井台的顶面作为整片地面的高度。
		bool firstPoint=true;
		for(float z=FieldCenter.Y-FieldSize.Y*.5f;z<FieldCenter.Y+FieldSize.Y*.5f;z+=ChunkSize)
		for(float x=FieldCenter.X-FieldSize.X*.5f;x<FieldCenter.X+FieldSize.X*.5f;x+=ChunkSize)
		{
			float width=Mathf.Min(ChunkSize,FieldCenter.X+FieldSize.X*.5f-x), depth=Mathf.Min(ChunkSize,FieldCenter.Y+FieldSize.Y*.5f-z);
			var instances=new List<(Transform3D Transform, Color Color)>();
			for(int i=0;i<width*depth*Density;i++)
			{
				float px=x+random.Randf()*width,pz=z+random.Randf()*depth;
				float patch=.55f+.3f*Mathf.Sin(px*.67f)*Mathf.Sin(pz*.53f);
				float edge=Mathf.Min(FieldSize.X*.5f-Mathf.Abs(px-FieldCenter.X),FieldSize.Y*.5f-Mathf.Abs(pz-FieldCenter.Y));
				patch*=Mathf.SmoothStep(0,2,edge);
				if(random.Randf()>patch) continue;
				Vector3 point=ToGlobal(new Vector3(px,0,pz));
				using var query=PhysicsRayQueryParameters3D.Create(point+Vector3.Up*4,point-Vector3.Up*4,CombatLayers.World);
				var hit=space.IntersectRay(query);
				if(hit.Count==0 || hit["normal"].AsVector3().Y<.85f) continue;
				Vector3 local=ToLocal(hit["position"].AsVector3());
				if(Mathf.Abs(local.Y)>.4f) continue;
				local.Y+=.012f;
				InstanceBounds=firstPoint?new Aabb(local,Vector3.Zero):InstanceBounds.Expand(local); firstPoint=false;
				float scale=random.RandfRange(.65f,1.1f),height=random.RandfRange(.65f,1.15f);
				var basis=new Basis(Vector3.Up,random.Randf()*Mathf.Tau).Scaled(new Vector3(scale,height,scale));
				instances.Add((new Transform3D(basis,local-new Vector3(x,0,z)),new Color(random.Randf(),1,1,1)));
			}
			if(instances.Count==0) continue;
			var multi=new MultiMesh { TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,Mesh=_mesh,InstanceCount=instances.Count };
			var batch=new MultiMeshInstance3D { Name="GrassChunk"+_chunks.Count,Multimesh=multi,Position=new Vector3(x,0,z),MaterialOverride=GrassMaterial,
				CastShadow=GeometryInstance3D.ShadowCastingSetting.Off,VisibilityRangeEnd=80,
				CustomAabb=new Aabb(new Vector3(-.5f,-.5f,-.5f),new Vector3(width+1,1.8f,depth+1)) };
			Node3D? singles=BuildReference?new Node3D { Name="ReferenceChunk"+_chunks.Count,Position=batch.Position,Visible=false }:null;
			AddChild(batch); if(singles!=null) AddChild(singles);
			for(int i=0;i<instances.Count;i++)
			{
				var instance=instances[i]; multi.SetInstanceTransform(i,instance.Transform);
				if(singles==null) continue;
				// 独立网格共享同一网格、材质；颜色与风动都由世界坐标计算，保持公平对照。
				var single=new MeshInstance3D { Mesh=_mesh,Transform=instance.Transform,MaterialOverride=GrassMaterial,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off,VisibilityRangeEnd=80 };
				singles.AddChild(single);
			}
			FullCount+=instances.Count; NormalCount+=(int)(instances.Count*.65f);
			_chunks.Add((batch,singles,instances.Count));
		}
		ApplyVariant(Variant.Batched);
	}
	public void ApplyVariant(Variant variant)
	{
		if(variant==Variant.Individual&&!BuildReference) throw new InvalidOperationException("独立草簇只在对照样板启用");
		CurrentVariant=variant;
		foreach(var chunk in _chunks)
		{
			int visible=(int)(chunk.Count*.65f);
			chunk.Batch.Visible=variant is Variant.Batched or Variant.Dense;
			chunk.Batch.Multimesh!.VisibleInstanceCount=variant==Variant.Dense?-1:visible;
			if(chunk.Singles!=null)
			{
				chunk.Singles.Visible=variant==Variant.Individual;
				for(int i=0;i<chunk.Count;i++) ((Node3D)chunk.Singles.GetChild(i)).Visible=i<visible;
			}
		}
		SetProcess(variant!=Variant.Ground);
	}
	public void SetOverview(bool enabled)
	{
		foreach(var chunk in _chunks) chunk.Batch.VisibilityRangeEnd=enabled?0:80;
		GrassMaterial.SetShaderParameter("draw_distance",enabled?0.0f:80.0f);
	}
	public override void _Process(double delta)
	{
		WindTime+=(float)delta;
		GrassMaterial.SetShaderParameter("wind_time",WindTime);
	}
	private static ArrayMesh BladeMesh()
	{
		using var surface=new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
		for(int blade=0;blade<4;blade++)
		{
			float angle=blade*2.39996f, height=.34f+blade*.035f;
			Vector3 side=new(Mathf.Cos(angle),0,Mathf.Sin(angle)),forward=new(-side.Z,0,side.X);
			Vector3 center=forward*.025f;
			Vector3[] vertices={center-side*.033f,center+side*.033f,center+Vector3.Up*height*.55f-side*.022f+forward*.025f,
				center+Vector3.Up*height*.55f+side*.022f+forward*.025f,center+Vector3.Up*height+forward*.08f};
			int[] order={0,1,2,1,3,2,2,3,4};
			foreach(int index in order) { surface.SetNormal(Vector3.Up); surface.SetUV(new Vector2(index%2,index==4?1:index>=2?.55f:0)); surface.AddVertex(vertices[index]); }
		}
		return surface.Commit();
	}
}

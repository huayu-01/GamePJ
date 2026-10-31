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
	public int ExclusionCount => _exclusions.Count;
	public int FullyExcludedChunks { get; private set; }
	public int ExcludedCandidates { get; private set; }
	public int GroundRaycastCount { get; private set; }
	public const int CardsPerClump=2;
	public const int TrianglesPerClump=CardsPerClump*6;
	public Aabb InstanceBounds { get; private set; }
	public Variant CurrentVariant { get; private set; }
	public float WindTime { get; private set; }
	public ShaderMaterial GrassMaterial { get; private set; } = null!;
	private readonly List<(MultiMeshInstance3D Batch, Node3D? Singles, int Count)> _chunks = new();
	private ArrayMesh _mesh = null!;
	private bool _built;
	private readonly List<ExclusionBox> _exclusions = new();
	private sealed record ExclusionBox(Transform3D Inverse, Vector3 HalfSize, Aabb Bounds)
	{
		public bool Contains(Vector3 world)
		{
			Vector3 local=(Inverse*world).Abs();
			return local.X<=HalfSize.X&&local.Y<=HalfSize.Y&&local.Z<=HalfSize.Z;
		}
	}
	public void RegisterExclusionBox(Transform3D transform, Vector3 size)
	{
		if(_built) throw new InvalidOperationException("禁草区必须在生成前登记");
		if(!size.IsFinite()||size.X<=0||size.Y<=0||size.Z<=0||!transform.IsFinite()||Mathf.Abs(transform.Basis.Determinant())<.000001f)
			throw new ArgumentOutOfRangeException(nameof(size),"禁草区尺寸与变换无效");
		_exclusions.Add(new ExclusionBox(transform.AffineInverse(),size*.5f,transform*new Aabb(-size*.5f,size)));
	}
	/// <summary>显式禁草节点优先；普通贴地盒形建筑自动取占地，架空物不自动登记。</summary>
	public void RegisterSceneExclusions(Node root, Node ground)
	{
		void Visit(Node node, bool hasZone)
		{
			if(node==ground) return;
			if(node is GrassExclusionZone zone&&zone.Enabled) RegisterExclusionBox(zone.GlobalTransform,zone.Size);
			// 给建筑挂禁草节点后由作者决定占地；禁用该节点可关闭该建筑的占地排除。
			foreach(Node child in node.GetChildren()) if(child is GrassExclusionZone) hasZone=true;
			if(!hasZone&&node is CollisionShape3D { Disabled:false, Shape:BoxShape3D box } shape&&shape.GetParent() is StaticBody3D)
			{
				Aabb bounds=shape.GlobalTransform*new Aabb(-box.Size*.5f,box.Size);
				if(bounds.Position.Y<=GlobalPosition.Y+.08f&&bounds.End.Y>=GlobalPosition.Y-.4f)
					RegisterExclusionBox(shape.GlobalTransform,box.Size+new Vector3(.12f,1,.12f));
			}
			foreach(Node child in node.GetChildren()) Visit(child,hasZone);
		}
		Visit(root,false);
	}
	public bool IsExcluded(Vector3 world) => _exclusions.Exists(box=>box.Contains(world));

	public void Build()
	{
		if(_built) throw new InvalidOperationException("草地已经生成");
		if(!float.IsFinite(ChunkSize)||ChunkSize<=0||!FieldSize.IsFinite()||FieldSize.X<=0||FieldSize.Y<=0||!float.IsFinite(Density)||Density<=0)
			throw new ArgumentOutOfRangeException(nameof(ChunkSize),"草地大小、分块和密度必须为有效正数");
		_built=true;
		ProcessMode=ProcessModeEnum.Pausable;
		GrassMaterial=new ShaderMaterial { Shader=RuntimeResources.Load<Shader>("res://Assets/Effects/grass_patch.gdshader") };
		GrassMaterial.SetShaderParameter("wind_frequency",WindFrequency);
		GrassMaterial.SetShaderParameter("blade_texture",RuntimeResources.Load<Texture2D>("res://Assets/Effects/grass_soft_blades.svg"));
		_mesh=BladeMesh();
		var space=GetWorld3D().DirectSpaceState;
		// 节点原点指定地面基准，不能拿中心井台的顶面作为整片地面的高度。
		bool firstPoint=true;
		for(float z=FieldCenter.Y-FieldSize.Y*.5f;z<FieldCenter.Y+FieldSize.Y*.5f;z+=ChunkSize)
		for(float x=FieldCenter.X-FieldSize.X*.5f;x<FieldCenter.X+FieldSize.X*.5f;x+=ChunkSize)
		{
			float width=Mathf.Min(ChunkSize,FieldCenter.X+FieldSize.X*.5f-x), depth=Mathf.Min(ChunkSize,FieldCenter.Y+FieldSize.Y*.5f-z);
			var bounds=GlobalTransform*new Aabb(new Vector3(x,-.4f,z),new Vector3(width,.8f,depth));
			var exclusions=_exclusions.FindAll(box=>box.Bounds.Intersects(bounds));
			bool ColumnInside(ExclusionBox box,float px,float pz)=>box.Contains(ToGlobal(new Vector3(px,-.4f,pz)))&&box.Contains(ToGlobal(new Vector3(px,.4f,pz)));
			if(exclusions.Exists(box=>ColumnInside(box,x,z)&&ColumnInside(box,x+width,z)&&ColumnInside(box,x,z+depth)&&ColumnInside(box,x+width,z+depth)))
			{
				FullyExcludedChunks++; ExcludedCandidates+=(int)Mathf.Ceil(width*depth*Density); continue;
			}
			// 各块独立种子、每候选固定随机步数，排除建筑不会让场外的草重新洗牌。
			int column=Mathf.RoundToInt((x-FieldCenter.X+FieldSize.X*.5f)/ChunkSize),row=Mathf.RoundToInt((z-FieldCenter.Y+FieldSize.Y*.5f)/ChunkSize);
			using var random=new RandomNumberGenerator { Seed=unchecked((ulong)(uint)Seed*73856093UL^(ulong)(uint)column*19349663UL^(ulong)(uint)row*83492791UL) };
			var instances=new List<(Transform3D Transform, Color Color)>();
			for(int i=0;i<width*depth*Density;i++)
			{
				float px=x+random.Randf()*width,pz=z+random.Randf()*depth;
				float sample=random.Randf(),scale=random.RandfRange(.65f,1.1f),height=random.RandfRange(.65f,1.15f),angle=random.Randf()*Mathf.Tau;
				float patch=.55f+.3f*Mathf.Sin(px*.67f)*Mathf.Sin(pz*.53f);
				float edge=Mathf.Min(FieldSize.X*.5f-Mathf.Abs(px-FieldCenter.X),FieldSize.Y*.5f-Mathf.Abs(pz-FieldCenter.Y));
				patch*=Mathf.SmoothStep(0,2,edge);
				if(sample>patch) continue;
				if(exclusions.Exists(box=>ColumnInside(box,px,pz))) { ExcludedCandidates++; continue; }
				Vector3 point=ToGlobal(new Vector3(px,0,pz));
				using var query=PhysicsRayQueryParameters3D.Create(point+Vector3.Up*4,point-Vector3.Up*4,CombatLayers.World);
				GroundRaycastCount++;
				var hit=space.IntersectRay(query);
				if(hit.Count==0 || hit["normal"].AsVector3().Y<.85f) continue;
				Vector3 local=ToLocal(hit["position"].AsVector3());
				if(Mathf.Abs(local.Y)>.4f) continue;
				if(exclusions.Exists(box=>box.Contains(hit["position"].AsVector3()))) { ExcludedCandidates++; continue; }
				local.Y+=.012f;
				InstanceBounds=firstPoint?new Aabb(local,Vector3.Zero):InstanceBounds.Expand(local); firstPoint=false;
				var basis=new Basis(Vector3.Up,angle).Scaled(new Vector3(scale,height,scale));
				instances.Add((new Transform3D(basis,local-new Vector3(x,0,z)),Colors.White));
			}
			if(instances.Count==0) continue;
			var multi=new MultiMesh { TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,Mesh=_mesh,InstanceCount=instances.Count };
			var batch=new MultiMeshInstance3D { Name="GrassChunk"+_chunks.Count,Multimesh=multi,Position=new Vector3(x,0,z),MaterialOverride=GrassMaterial,
				CastShadow=GeometryInstance3D.ShadowCastingSetting.Off,VisibilityRangeEnd=80,
				CustomAabb=new Aabb(new Vector3(-.8f,-.5f,-.8f),new Vector3(width+1.6f,1.8f,depth+1.6f)) };
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
		SetProcess(variant!=Variant.Ground&&_chunks.Count>0);
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
		for(int card=0;card<CardsPerClump;card++)
		{
			float angle=card*Mathf.Pi*.5f,height=.45f;
			Vector3 side=new(Mathf.Cos(angle),0,Mathf.Sin(angle)),forward=new(-side.Z,0,side.X);
			Vector3 Center(float t)=>new Vector3(0,height*t-.022f,0)+forward*(t*t*.035f);
			for(int segment=0;segment<3;segment++)
			{
				float lower=segment/3.0f,upper=(segment+1)/3.0f;
				Vector3[] vertices={Center(lower)-side*.55f,Center(lower)+side*.55f,Center(upper)-side*.55f,Center(upper)+side*.55f};
				foreach(int index in new[]{0,1,2,1,3,2})
				{
					surface.SetNormal(forward); surface.SetUV(new Vector2(card==0?index%2:1-index%2,index<2?lower:upper));
					surface.SetUV2(Vector2.Zero); surface.AddVertex(vertices[index]);
				}
			}
		}
		return surface.Commit();
	}
}

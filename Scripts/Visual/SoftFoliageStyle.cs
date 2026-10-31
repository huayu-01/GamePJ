using System;
using System.Collections.Generic;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Visual;

/// <summary>可换树模型的叶材质方案；网格和树干保持原样，同类叶面共享材质。</summary>
[GlobalClass]
public partial class SoftFoliageStyle : Resource
{
	[Export] public string MaterialPrefix { get; set; } = "Leaves";
	[Export(PropertyHint.Range,"0,1")] public float NormalRoundness { get; set; } = .78f;
	[Export(PropertyHint.Range,"0,1")] public float SideFade { get; set; } = .24f;
	[Export(PropertyHint.Range,"0,1")] public float WrapLight { get; set; } = .35f;
	private readonly Dictionary<(Mesh Mesh,int Surface,StandardMaterial3D Source),ShaderMaterial> _cache=new();
	public int SharedMaterialCount => _cache.Count;
	public int Apply(Node root)
	{
		int count=0;
		void Visit(Node node)
		{
			if(node is MeshInstance3D { Mesh:{ } mesh } instance)
			{
				for(int surface=0;surface<mesh.GetSurfaceCount();surface++)
				{
					if(instance.GetActiveMaterial(surface) is not StandardMaterial3D source||!source.ResourceName.StartsWith(MaterialPrefix,StringComparison.OrdinalIgnoreCase)) continue;
					if(!_cache.TryGetValue((mesh,surface,source),out var material))
					{
						var vertices=mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
						var bounds=vertices.Length==0?mesh.GetAabb():new Aabb(vertices[0],Vector3.Zero);
						foreach(var point in vertices) bounds=bounds.Expand(point);
						material=new ShaderMaterial { Shader=RuntimeResources.Load<Shader>("res://Assets/Effects/foliage_soft.gdshader") };
						material.SetShaderParameter("canopy_center",bounds.GetCenter());
						material.SetShaderParameter("canopy_radius",bounds.Size*.5f);
						material.SetShaderParameter("normal_roundness",NormalRoundness);
						material.SetShaderParameter("side_fade",SideFade);
						material.SetShaderParameter("wrap_light",WrapLight);
						material.SetShaderParameter("leaf_texture",source.AlbedoTexture);
						material.SetShaderParameter("has_texture",source.AlbedoTexture!=null);
						material.SetShaderParameter("use_vertex_color",source.VertexColorUseAsAlbedo);
						material.SetShaderParameter("leaf_color",source.AlbedoColor);
						material.SetShaderParameter("uv_scale",source.Uv1Scale);
						material.SetShaderParameter("uv_offset",source.Uv1Offset);
						material.SetShaderParameter("alpha_cutoff",Mathf.Clamp(source.AlphaScissorThreshold,.01f,.95f));
						_cache.Add((mesh,surface,source),material);
					}
					instance.SetSurfaceOverrideMaterial(surface,material); count++;
				}
			}
			foreach(Node child in node.GetChildren()) Visit(child);
		}
		Visit(root); return count;
	}
}

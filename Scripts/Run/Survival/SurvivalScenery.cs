using System;
using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Run;

/// <summary>仅调整本庭院树叶的近镜头显隐，树干与实体碰撞始终保留。</summary>
public partial class SurvivalScenery : Node3D
{
	public override void _Ready()
	{
		var copies = new Dictionary<StandardMaterial3D, StandardMaterial3D>();
		foreach (Node tree in GetChildren())
		{
			if (!tree.Name.ToString().StartsWith("OuterTree", StringComparison.Ordinal)) continue;
			foreach (Node child in tree.GetChildren())
			{
				if (child is not MeshInstance3D { Mesh: not null } mesh) continue;
				for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
				{
					if (mesh.GetActiveMaterial(i) is not StandardMaterial3D material || !material.ResourceName.StartsWith("Leaves_", StringComparison.Ordinal)) continue;
					if (!copies.TryGetValue(material, out var copy))
					{
						copy = (StandardMaterial3D)material.Duplicate();
						copy.DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.PixelDither;
						copy.DistanceFadeMinDistance = 9;
						copy.DistanceFadeMaxDistance = 15;
						copies[material] = copy;
					}
					mesh.SetSurfaceOverrideMaterial(i, copy);
				}
			}
		}
	}
}

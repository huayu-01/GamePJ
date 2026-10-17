using Godot;

namespace ActRoguelike.Visual;

/// <summary>只修改特效的私有材质副本，不改共享资源或战斗状态。</summary>
public static class FxPalette
{
	public static void Apply(Node node, Color color, float emission)
	{
		if (node is GeometryInstance3D geometry && geometry.MaterialOverride is ShaderMaterial source)
		{
			var material = (ShaderMaterial)source.Duplicate();
			material.SetShaderParameter("primary_color", color.Lightened(0.25f));
			material.SetShaderParameter("secondary_color", color.Darkened(0.35f));
			material.SetShaderParameter("emission", emission);
			geometry.MaterialOverride = material;
		}
		if (node is Light3D light) { light.LightColor = color; light.LightEnergy = 0.35f; }
		foreach (Node child in node.GetChildren()) Apply(child, color, emission);
	}
}

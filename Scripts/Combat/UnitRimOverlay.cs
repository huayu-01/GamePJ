using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>单位边缘表现共用叠加通道。预警＞受击＞精英＞蓄力，退出后恢复原叠加材质。</summary>
internal static class UnitRimOverlay
{
	private sealed class Channel
	{
		public Material? Original;
		public Material? Applied;
		public readonly Dictionary<object, (Material Material, int Priority)> Effects = new();
	}
	private static readonly Dictionary<MeshInstance3D, Channel> Channels = new();
	private static Shader? _shader;
	public static ShaderMaterial Create(Color color, float opacity, float energy)
	{
		_shader ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded, blend_mix, cull_back, depth_draw_never;
uniform vec4 rim_color : source_color = vec4(0.4, 0.7, 1.0, 1.0);
uniform float opacity = 0.55;
uniform float energy = 1.2;
void fragment() {
	float edge = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), 2.5);
	ALBEDO = rim_color.rgb;
	EMISSION = rim_color.rgb * energy;
	ALPHA = edge * clamp(opacity, 0.0, 0.72) * rim_color.a;
}" };
		var material = new ShaderMaterial { Shader = _shader };
		material.SetShaderParameter("rim_color", color);
		material.SetShaderParameter("opacity", opacity);
		material.SetShaderParameter("energy", energy);
		return material;
	}

	public static void Set(MeshInstance3D mesh, object owner, Material? material, int priority)
	{
		if (!GodotObject.IsInstanceValid(mesh)) return;
		if (!Channels.TryGetValue(mesh, out Channel? channel))
		{
			if (material == null) return;
			channel = new Channel { Original = mesh.MaterialOverlay };
			Channels.Add(mesh, channel);
		}
		if (material == null) channel.Effects.Remove(owner);
		else channel.Effects[owner] = (material, priority);
		Apply(mesh, channel);
	}

	public static void RemoveSource(object owner)
	{
		foreach (var pair in new List<KeyValuePair<MeshInstance3D, Channel>>(Channels))
		{
			pair.Value.Effects.Remove(owner);
			if (GodotObject.IsInstanceValid(pair.Key)) Apply(pair.Key, pair.Value);
			else Channels.Remove(pair.Key);
		}
	}

	private static void Apply(MeshInstance3D mesh, Channel channel)
	{
		Material? next = channel.Original;
		int priority = int.MinValue;
		foreach (var effect in channel.Effects.Values)
			if (effect.Priority > priority) { next = effect.Material; priority = effect.Priority; }
		// 外部模型系统在空闲时换过叠加材质，应记住新基底，不能还原到旧模型效果。
		if (mesh.MaterialOverlay != channel.Applied && channel.Applied != null)
			channel.Original = mesh.MaterialOverlay;
		if (channel.Effects.Count == 0) next = channel.Original;
		mesh.MaterialOverlay = next; channel.Applied = next;
		if (channel.Effects.Count == 0) Channels.Remove(mesh);
	}
}

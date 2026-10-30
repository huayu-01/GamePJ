using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>仅在绑定或换模型时处理表面；不改GLB，不占用受击/精英的叠加材质通道。</summary>
public partial class UnitArtStyle : Node
{
	private sealed record Binding(MeshInstance3D Mesh, int Surface, Material? Previous, Material Source, ShaderMaterial Styled);
	private static readonly Dictionary<(ulong Id, bool Outline, float Width, Color Ink), WeakReference<ShaderMaterial>> Cache = new();
	private static Dictionary<string, JsonElement>? _palette;
	private readonly List<Binding> _bindings = new();
	private CombatArtStyleConfig _profile = null!;
	private Node? _model;
	public int StyledSurfaceCount => _bindings.Count;
	public bool Enabled { get; private set; }
	public static UnitArtStyle Ensure(Unit unit)
	{
		if (unit.GetNodeOrNull<UnitArtStyle>("ArtStyle") is { } existing) return existing;
		var style = new UnitArtStyle { Name = "ArtStyle" }; unit.AddChild(style); return style;
	}
	public override void _Ready()
	{
		SetProcess(false); SetPhysicsProcess(false);
		_profile = RuntimeResources.Load<CombatArtStyleConfig>("res://Config/CombatArtStyle.tres"); Enabled = _profile.Enabled;
	}
	public void Bind(Node model)
	{
		Restore(); _model = model;
		if (Enabled) Apply(model);
	}
	public void SetEnabled(bool value)
	{
		if (Enabled == value) return; Enabled = value;
		Restore(); if (value && IsInstanceValid(_model)) Apply(_model!);
	}
	private void Restore()
	{
		foreach (var binding in _bindings)
			if (IsInstanceValid(binding.Mesh))
				if (binding.Surface < 0) binding.Mesh.MaterialOverride = binding.Previous;
				else binding.Mesh.SetSurfaceOverrideMaterial(binding.Surface, binding.Previous);
		_bindings.Clear();
	}
	private void Apply(Node root)
	{
		if (root is MeshInstance3D { Mesh: { } mesh } instance)
		{
			if (instance.MaterialOverride is BaseMaterial3D material) BindSurface(instance, -1, material, material);
			else if (instance.MaterialOverride == null)
				for (int i = 0; i < mesh.GetSurfaceCount(); i++)
				{
					Material? previous = instance.GetSurfaceOverrideMaterial(i);
					if ((previous ?? mesh.SurfaceGetMaterial(i)) is BaseMaterial3D source) BindSurface(instance, i, previous, source);
				}
		}
		foreach (Node child in root.GetChildren())
			if (child is not SkillIndicator && child is not EnemyAttackWarning && child is not StatusEffectFx && child.Name != "ArmorAura") Apply(child);
	}
	private void BindSurface(MeshInstance3D mesh, int surface, Material? previous, BaseMaterial3D source)
	{
		if (mesh.HasMeta("combat_dynamic_material") && mesh.GetMeta("combat_dynamic_material").AsBool()) return;
		// 透明外壳、发光特效与自定义shader保留，避免把眼睛、护罩或武器特效变成实体黑边。
		if (source.Transparency != BaseMaterial3D.TransparencyEnum.Disabled || source.ShadingMode == BaseMaterial3D.ShadingModeEnum.Unshaded) return;
		var key = (source.GetInstanceId(), _profile.OutlineEnabled, _profile.OutlineWidth, _profile.OutlineColor);
		if (!Cache.TryGetValue(key, out var weak) || !weak.TryGetTarget(out var material) || !IsInstanceValid(material))
		{
			material = CreateMaterial(source); Cache[key] = new WeakReference<ShaderMaterial>(material);
			if (Cache.Count > 256)
				foreach (var expired in Cache.Where(pair => !pair.Value.TryGetTarget(out var item) || !IsInstanceValid(item)).Select(pair => pair.Key).ToArray()) Cache.Remove(expired);
		}
		_bindings.Add(new Binding(mesh, surface, previous, source, material));
		if (surface < 0) mesh.MaterialOverride = material; else mesh.SetSurfaceOverrideMaterial(surface, material);
	}
	private ShaderMaterial CreateMaterial(BaseMaterial3D source)
	{
		_palette ??= JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Godot.FileAccess.GetFileAsString("res://Assets/Materials/EnemyAnime/palette.json"))!;
		string name = source.ResourceName.ToString().Split('.')[0];
		bool palette = _profile.UseApprovedPalette && _palette.TryGetValue(name, out var colors);
		var material = new ShaderMaterial { Shader = RuntimeResources.Load<Shader>(palette ? "res://Assets/Materials/EnemyAnime/anime_paint.gdshader" : "res://Assets/Effects/combat_toon.gdshader") };
		bool outline = _profile.OutlineEnabled;
		if (palette)
		{
			_palette.TryGetValue(name, out colors);
			foreach (var (field, parameter) in new[] { ("lit", "paint_color"), ("shade", "shade_color"), ("high", "light_color") }) material.SetShaderParameter(parameter, new Color(colors.GetProperty(field).GetString()!));
			foreach (var (field, parameter, fallback) in new[] { ("brush", "brush_amount", .035f), ("emission", "emissive_amount", 0f), ("rim", "edge_light", .045f) })
				material.SetShaderParameter(parameter, colors.TryGetProperty(field, out var amount) ? amount.GetSingle() : fallback);
			if (colors.TryGetProperty("mushroom", out var mushroom) && mushroom.GetBoolean())
			{ material.SetShaderParameter("mushroom_repaint", true); material.SetShaderParameter("region_texture", RuntimeResources.Load<Texture2D>("res://Assets/Materials/EnemyAnime/shroom_regions.png")); }
			if (colors.TryGetProperty("outline", out var hasOutline)) outline &= hasOutline.GetBoolean();
		}
		else
		{
			material.SetShaderParameter("base_color", source.AlbedoColor);
			material.SetShaderParameter("has_texture", source.AlbedoTexture != null);
			if (source.AlbedoTexture != null) material.SetShaderParameter("base_texture", source.AlbedoTexture);
			material.SetShaderParameter("vertex_color", source.VertexColorUseAsAlbedo);
			Color glow = source.EmissionEnabled ? source.Emission : Colors.Black;
			material.SetShaderParameter("glow_color", new Vector3(glow.R, glow.G, glow.B));
		}
		if (outline)
		{
			var ink = new ShaderMaterial { Shader = RuntimeResources.Load<Shader>("res://Assets/Materials/EnemyAnime/anime_outline.gdshader") };
			ink.SetShaderParameter("ink_width", _profile.OutlineWidth); ink.SetShaderParameter("ink_color", _profile.OutlineColor);
			material.NextPass = ink;
		}
		return material;
	}
	public override void _ExitTree() { _bindings.Clear(); _model = null; }
}

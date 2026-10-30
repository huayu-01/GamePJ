using System.Collections.Generic;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.UI;

/// <summary>共享磨砂背景与简化语义；不读写奖励资格、战斗或存档。</summary>
public static partial class GlassUi
{
	public static readonly Color Ink = new("102b48"), Paper = new("f5f8fc"), Secondary = new("486780");
	private static bool IsSpecialization(RewardDefinition row) => ConfigRows.ById<RewardConfig>(row.Id) is { } config && config.Specialization != default;
	public static string CategoryKey(RewardDefinition row) => IsSpecialization(row) ? "specializations" : row.Kind switch { RewardKind.Mutation => "mutations", RewardKind.Evolution => "evolutions", _ => "enhancements" };
	public static Color RewardColor(RewardDefinition row, bool dark = false) =>
		IsSpecialization(row) ? new Color(dark ? "b8c9ff" : "4d64a3") : row.Kind switch {
			RewardKind.Mutation => new(dark ? "dbb5ff" : "7953a6"),
			RewardKind.Evolution => new(dark ? "8fdfec" : "167783"),
			_ => new(dark ? "b1e5bb" : "34764b")
		};
	public static Texture2D? CategoryIcon(RewardDefinition row) => UiIcons.Named("Minimal/" +
		(IsSpecialization(row) ? "specialization" : row.Kind switch {
			RewardKind.Mutation => "mutation", RewardKind.Evolution => "evolution", _ => "enhancement"
		}));
	public static Theme Theme(bool dark = false)
	{
		var theme = new Theme { DefaultFont = Weight(500), DefaultFontSize = 23 };
		Color ink = dark ? Paper : Ink;
		foreach (string type in new[] { "Label", "Button", "LineEdit", "RichTextLabel" }) {
			theme.SetColor("font_color", type, ink); theme.SetColor("font_focus_color", type, ink);
			theme.SetColor("font_hover_color", type, ink); theme.SetColor("font_pressed_color", type, ink);
			theme.SetColor("default_color", type, ink);
		}
		theme.SetColor("font_placeholder_color", "LineEdit", Secondary);
		theme.SetColor("font_uneditable_color","LineEdit",ink);theme.SetStylebox("read_only","LineEdit",Tile(false,dark));
		foreach (string type in new[] { "Button", "LineEdit" }) {
			theme.SetStylebox("normal", type, Tile(false, dark)); theme.SetStylebox("hover", type, Tile(true, dark));
			theme.SetStylebox("pressed", type, Tile(true, dark)); theme.SetStylebox("focus", type, Tile(true, dark));
		}
		theme.SetTypeVariation("UiTitle", "Label"); theme.SetFontSize("font_size", "UiTitle", 42); theme.SetFont("font","UiTitle",Weight(700));
		theme.SetTypeVariation("UiCaption", "Label"); theme.SetColor("font_color", "UiCaption", dark ? new Color("c3d1da") : Secondary);
		theme.SetTypeVariation("UiKeyCap", "Label"); theme.SetFontSize("font_size", "UiKeyCap", 21); theme.SetColor("font_color", "UiKeyCap", ink);
		theme.SetTypeVariation("UiKeyCapBox", "PanelContainer"); theme.SetStylebox("panel", "UiKeyCapBox", KeyCapStyle(dark));
		theme.SetStylebox("separator","HSeparator",new StyleBoxLine { Color=new Color(.48f,.65f,.75f,.52f),Thickness=1 });
		theme.SetStylebox("separator","VSeparator",new StyleBoxLine { Color=new Color(.48f,.65f,.75f,.52f),Thickness=1,Vertical=true });
		var scroll = new StyleBoxFlat { BgColor=new Color(.22f,.40f,.53f,.30f),CornerRadiusTopLeft=5,CornerRadiusTopRight=5,CornerRadiusBottomLeft=5,CornerRadiusBottomRight=5 };
		theme.SetStylebox("grabber","VScrollBar",scroll); theme.SetStylebox("grabber_highlight","VScrollBar",scroll); theme.SetStylebox("scroll","VScrollBar",new StyleBoxEmpty()); theme.SetConstant("scrollbar_width","VScrollBar",9);
		theme.SetStylebox("panel", "PanelContainer", new StyleBoxEmpty());
		return theme;
	}
	public static StyleBoxFlat Tile(bool selected, bool dark = false) => new() {
		BgColor = selected ? new Color(1,1,1,.12f) : new Color(1,1,1,.14f),
		BorderColor = selected ? new Color("60edfa") : new Color(.94f,.98f,1f,.58f),
		BorderWidthLeft = selected ? 3 : 1, BorderWidthRight = selected ? 3 : 1,
		BorderWidthTop = selected ? 3 : 1, BorderWidthBottom = selected ? 3 : 1,
		CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
		ShadowColor = new Color(.20f,.91f,1f,selected ? .12f : 0), ShadowSize = selected ? 4 : 0,
		ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10
	};
	public static void Prepare(Control root, bool dark = false)
	{
		root.Theme = Theme(dark);
		var copy = new BackBufferCopy { Name = "GlassSceneCopy", CopyMode = BackBufferCopy.CopyModeEnum.Viewport };
		root.AddChild(copy); root.MoveChild(copy, 0);
		var backdrop = new ColorRect { Name="GlassBackdrop",MouseFilter=Control.MouseFilterEnum.Ignore,Material=new ShaderMaterial { Shader=RuntimeResources.Load<Shader>("res://Assets/UI/Shaders/frosted_glass.gdshader") } };
		((ShaderMaterial)backdrop.Material).SetShaderParameter("backdrop",true);((ShaderMaterial)backdrop.Material).SetShaderParameter("light_surface",!dark);
		root.AddChild(backdrop);root.MoveChild(backdrop,1);backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		if (root.GetNodeOrNull<ColorRect>("Dim") is { } dim) dim.Color = Colors.Transparent;
		StyleKeyHints(root,dark);
	}
	public static ShaderMaterial Background(Control host, bool light = true)
	{
		var material = new ShaderMaterial { Shader = RuntimeResources.Load<Shader>("res://Assets/UI/Shaders/frosted_glass.gdshader") };
		material.SetShaderParameter("light_surface", light);
		material.SetShaderParameter("frost_texture",RuntimeResources.Load<Texture2D>("res://Assets/UI/Textures/frost_variation.tres"));
		var background = new ColorRect { Name = "FrostedSurface", Color = Colors.White, Material = material, MouseFilter = Control.MouseFilterEnum.Ignore };
		// Node2D不参与Container排版，避免背景被当成列表／网格中的一个控件。
		var backing = new Node2D { Name="GlassBacking" }; host.AddChild(backing); host.MoveChild(backing,0); backing.AddChild(background);
		void Resize() { background.Size=host.Size; material.SetShaderParameter("panel_size",new Vector2(Mathf.Max(1,host.Size.X),Mathf.Max(1,host.Size.Y))); }
		host.Resized += Resize; Resize();
		return material;
	}
	public static void EmptyPanel(PanelContainer panel) => panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty { ContentMarginLeft=24,ContentMarginRight=24,ContentMarginTop=20,ContentMarginBottom=20 });
	public static void StyleKeyHints(Control root,bool dark=false)
	{
		if(root.GetNodeOrNull<Control>(OverlayScreen.KeyHintBarNodeName) is { } bar && !bar.HasNode("GlassBacking")) {
			var material = Background(bar,!dark);
			material.SetShaderParameter("hint_surface",true);
			material.SetShaderParameter("corner_radius",12f);
			bar.SetMeta("glass_hints",true);
			bar.SetMeta("glass_dark",dark);
			bar.OffsetLeft=dark?236:80;bar.OffsetRight=dark?-236:-80;bar.OffsetTop=-140;bar.OffsetBottom=-44;bar.AddThemeConstantOverride("separation",18);
			if(bar is BoxContainer box)box.Alignment=BoxContainer.AlignmentMode.Center;
		}
	}
	private static readonly Dictionary<ulong,(Node Node,bool Visible,bool Processing)> HiddenHud=new();
	public static void SyncWorldHud(Node root,bool hide)
	{
		bool IsVisible(Node node) => node is CanvasLayer layer ? layer.Visible : node is CanvasItem item && item.Visible;
		void SetVisible(Node node,bool visible) { if(node is CanvasLayer layer)layer.Visible=visible;else if(node is CanvasItem item)item.Visible=visible; }
		if(!hide) { foreach(var saved in HiddenHud.Values)if(GodotObject.IsInstanceValid(saved.Node)){SetVisible(saved.Node,saved.Visible);saved.Node.SetProcess(saved.Processing);}HiddenHud.Clear();return; }
		void Visit(Node node) {
			if(node is PlayerHud or WorldOverlay or ChallengeStatusPanel || node.GetType().Name=="SurvivalHud") {
				if(!HiddenHud.ContainsKey(node.GetInstanceId()))HiddenHud[node.GetInstanceId()]=(node,IsVisible(node),node.IsProcessing());SetVisible(node,false);node.SetProcess(false);
			}
			foreach(Node child in node.GetChildren())Visit(child);
		}
		Visit(root);
	}
	public static string RulesMarkup(string text)
	{
		var parts=text.Replace("[","[lb]").Split("\n\n");
		for(int i=0;i<parts.Length;i++) {
			int colon=parts[i].IndexOf('：'); if(colon<0) colon=parts[i].IndexOf(':'); if(colon<0) continue;
			string heading=parts[i][..colon]; string color=heading.Contains("前置") ? "a16822" : heading.Contains("互斥") ? "8551a5" : "386780";
			parts[i]=$"[color=#{color}][b]{parts[i][..(colon+1)]}[/b][/color]"+parts[i][(colon+1)..];
		}
		return string.Join("\n\n",parts);
	}
}

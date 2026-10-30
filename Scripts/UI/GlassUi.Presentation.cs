using System.Collections.Generic;
using Godot;

namespace ActRoguelike.UI;

public static partial class GlassUi
{
	private static readonly Dictionary<int, Font> Fonts = new();
	public static Font Weight(int weight)
	{
		if (!Fonts.TryGetValue(weight, out var font))
		{
			font = new FontVariation { BaseFont = Core.RuntimeResources.Load<FontFile>("res://Assets/Fonts/NotoSansSC/NotoSansSC.ttf"), VariationOpentype = new Godot.Collections.Dictionary { [2003265652] = (float)weight } };
			Fonts[weight] = font;
		}
		return font;
	}
	public static void Typography(Control control, int size, int weight = 500)
	{
		control.AddThemeFontSizeOverride("font_size", size);
		control.AddThemeFontOverride("font", Weight(weight));
	}
	public static void FrameTitle(Label title)
	{
		title.AddThemeColorOverride("font_color", Paper);
		var left = new ColorRect { Color = new Color(.83f,.94f,1f,.65f), MouseFilter = Control.MouseFilterEnum.Ignore };
		var right = new ColorRect { Color = left.Color, MouseFilter = Control.MouseFilterEnum.Ignore };
		title.AddChild(left); title.AddChild(right);
		void Resize()
		{
			float textWidth = title.GetThemeFont("font").GetStringSize(title.Text, fontSize: title.GetThemeFontSize("font_size")).X;
			float line = Mathf.Max(0, (title.Size.X - textWidth) * .5f - 40);
			left.Position = new Vector2(0,title.Size.Y*.5f); left.Size = new Vector2(line,1);
			right.Position = new Vector2(title.Size.X-line,title.Size.Y*.5f); right.Size = left.Size;
		}
		title.Resized += Resize; title.ItemRectChanged += Resize; Resize();
	}
	public static StyleBoxFlat KeyCapStyle(bool dark) => new()
	{
		BgColor = dark ? new Color(.13f,.21f,.30f,.65f) : new Color(.80f,.88f,1f,.72f),
		BorderColor = dark ? new Color(.83f,.93f,1f,.80f) : new Color(.47f,.66f,.86f,.78f),
		BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,
		CornerRadiusTopLeft=5,CornerRadiusTopRight=5,CornerRadiusBottomLeft=5,CornerRadiusBottomRight=5,
		ShadowSize=1,ShadowColor=new Color(1,1,1,.24f),
		ContentMarginLeft=9,ContentMarginRight=9,ContentMarginTop=7,ContentMarginBottom=7
	};
	public static HBoxContainer HintChip(KeyHint hint,bool dark)
	{
		var chip = new HBoxContainer { MouseFilter=Control.MouseFilterEnum.Ignore,SizeFlagsVertical=Control.SizeFlags.ShrinkCenter };
		chip.AddThemeConstantOverride("separation",10);
		if (hint.Keys.Contains("鼠标") || hint.Keys is "悬停" or "悬停／点击" or "滚轮")
		{
			chip.AddChild(new TextureRect { Texture=UiIcons.Named("Tabler/mouse"),CustomMinimumSize=new Vector2(40,52),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore,Modulate=dark ? Paper : Ink });
		}
		else
		{
			string[] keys=hint.Keys.Contains("方向键") ? new[]{"↑","↓","←","→"} : hint.Keys.Replace("Escape","Esc").Split(" / ");
			foreach(string key in keys)
			{
				var box = new PanelContainer { ThemeTypeVariation="UiKeyCapBox",MouseFilter=Control.MouseFilterEnum.Ignore,SizeFlagsVertical=Control.SizeFlags.ShrinkCenter };
				var cap = new Label { Text=key,ThemeTypeVariation="UiKeyCap",HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore,CustomMinimumSize=new Vector2(20,0) };
				box.AddChild(cap);chip.AddChild(box);
			}
		}
		var caption = new Label { Text=hint.Keys.Contains("鼠标左键") ? "左键选择" : hint.Keys is "悬停" or "悬停／点击" ? "悬停查看详情" : hint.Keys=="滚轮" ? "滚轮滚动" : hint.Caption,VerticalAlignment=VerticalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore };
		Typography(caption,22);
		chip.AddChild(caption);
		return chip;
	}
	public static void SelectionDot(Button tile)
	{
		var dot=new TextureRect { Name="SelectionDot",Texture=UiIcons.Named("Tabler/circle-dot"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,Modulate=new Color("19bfd4"),MouseFilter=Control.MouseFilterEnum.Ignore,Visible=false };
		tile.AddChild(dot);dot.Position=new Vector2(12,12);dot.Size=new Vector2(16,16);
	}
}

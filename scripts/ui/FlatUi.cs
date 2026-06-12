using Godot;

public static class FlatUi
{
    public static readonly Color Background = new(0.055f, 0.07f, 0.075f);
    public static readonly Color Surface = new(0.095f, 0.115f, 0.12f);
    public static readonly Color SurfaceAlt = new(0.125f, 0.15f, 0.155f);
    public static readonly Color Accent = new(0.16f, 0.72f, 0.48f);
    public static readonly Color AccentMuted = new(0.10f, 0.42f, 0.30f);
    public static readonly Color Text = new(0.90f, 0.94f, 0.92f);
    public static readonly Color MutedText = new(0.58f, 0.66f, 0.63f);
    public static readonly Color Danger = new(0.84f, 0.22f, 0.22f);

    public static StyleBoxFlat PanelStyle(Color? color = null, float radius = 8)
    {
        var style = new StyleBoxFlat
        {
            BgColor = color ?? Surface,
            CornerRadiusTopLeft = (int)radius,
            CornerRadiusTopRight = (int)radius,
            CornerRadiusBottomLeft = (int)radius,
            CornerRadiusBottomRight = (int)radius,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 10,
            ContentMarginBottom = 10
        };
        style.BorderColor = new Color(1, 1, 1, 0.08f);
        style.BorderWidthLeft = 1;
        style.BorderWidthTop = 1;
        style.BorderWidthRight = 1;
        style.BorderWidthBottom = 1;
        return style;
    }

    public static StyleBoxFlat ButtonStyle(Color color)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };
    }

    public static void StyleButton(Button button, Color? color = null)
    {
        button.AddThemeStyleboxOverride("normal", ButtonStyle(color ?? SurfaceAlt));
        button.AddThemeStyleboxOverride("hover", ButtonStyle((color ?? SurfaceAlt).Lightened(0.08f)));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle((color ?? SurfaceAlt).Darkened(0.08f)));
        button.AddThemeStyleboxOverride("disabled", ButtonStyle(new Color(0.13f, 0.14f, 0.14f, 0.65f)));
        button.AddThemeColorOverride("font_color", Text);
        button.AddThemeColorOverride("font_disabled_color", MutedText);
        button.FocusMode = Control.FocusModeEnum.None;
        button.CustomMinimumSize = new Vector2(96, 38);
    }

    public static Label Label(string text, int fontSize = 16, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeColorOverride("font_color", Text);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }

    public static Label MutedLabel(string text, int fontSize = 14)
    {
        var label = Label(text, fontSize);
        label.AddThemeColorOverride("font_color", MutedText);
        return label;
    }

    public static Panel Panel(string name = "Panel")
    {
        var panel = new Panel { Name = name };
        panel.AddThemeStyleboxOverride("panel", PanelStyle());
        return panel;
    }

    public static Button Button(string text, Color? color = null)
    {
        var button = new Button { Text = text };
        StyleButton(button, color);
        return button;
    }
}

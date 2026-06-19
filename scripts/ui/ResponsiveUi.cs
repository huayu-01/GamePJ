using Godot;

public readonly struct UiSafeMargins
{
    public UiSafeMargins(float left, float top, float right, float bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public float Left { get; }
    public float Top { get; }
    public float Right { get; }
    public float Bottom { get; }
}

public static class ResponsiveUi
{
    public static UiSafeMargins GetSafeMargins(Control control)
    {
        var viewportSize = control.GetViewportRect().Size;
        var windowSize = DisplayServer.WindowGetSize();
        var safeArea = DisplayServer.GetDisplaySafeArea();
        if (windowSize.X <= 0 || windowSize.Y <= 0 || safeArea.Size.X <= 0 || safeArea.Size.Y <= 0)
        {
            return new UiSafeMargins(0, 0, 0, 0);
        }

        var scaleX = viewportSize.X / windowSize.X;
        var scaleY = viewportSize.Y / windowSize.Y;
        var right = windowSize.X - safeArea.Position.X - safeArea.Size.X;
        var bottom = windowSize.Y - safeArea.Position.Y - safeArea.Size.Y;
        return new UiSafeMargins(
            Mathf.Max(0, safeArea.Position.X * scaleX),
            Mathf.Max(0, safeArea.Position.Y * scaleY),
            Mathf.Max(0, right * scaleX),
            Mathf.Max(0, bottom * scaleY));
    }

    public static float MarginFor(Vector2 viewportSize)
    {
        return Mathf.Clamp(Mathf.Min(viewportSize.X, viewportSize.Y) * 0.025f, 12f, 28f);
    }

    public static Vector2 FitPanel(Vector2 viewportSize, UiSafeMargins safe, float preferredWidth, float preferredHeight, float margin)
    {
        var width = Mathf.Max(240f, viewportSize.X - safe.Left - safe.Right - margin * 2f);
        var height = Mathf.Max(320f, viewportSize.Y - safe.Top - safe.Bottom - margin * 2f);
        return new Vector2(Mathf.Min(preferredWidth, width), Mathf.Min(preferredHeight, height));
    }

    public static void ApplySafeCenter(Control center, Control owner, float margin)
    {
        var safe = GetSafeMargins(owner);
        center.OffsetLeft = safe.Left + margin;
        center.OffsetTop = safe.Top + margin;
        center.OffsetRight = -(safe.Right + margin);
        center.OffsetBottom = -(safe.Bottom + margin);
    }

    public static void EnsureTouchTargets(Control root, float minimumPhysicalPixels = 48f)
    {
        var viewportSize = root.GetViewportRect().Size;
        var windowSize = DisplayServer.WindowGetSize();
        var physicalScale = viewportSize.Y > 0 && windowSize.Y > 0 ? windowSize.Y / viewportSize.Y : 1f;
        var logicalHeight = Mathf.Clamp(minimumPhysicalPixels / Mathf.Max(physicalScale, 0.01f), 48f, 72f);

        foreach (var node in root.FindChildren("*", "Control", true, false))
        {
            if (node is not Control control || control is not (BaseButton or LineEdit or SpinBox))
            {
                continue;
            }

            if (control.CustomMinimumSize.Y + 0.5f < logicalHeight)
            {
                control.CustomMinimumSize = new Vector2(control.CustomMinimumSize.X, logicalHeight);
            }
        }
    }
}

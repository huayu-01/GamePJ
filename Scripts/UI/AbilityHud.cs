using System;
using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.UI;

/// <summary>右下角技能栏；只读启用技能、配置行与InputMap，绝不修改技能状态。</summary>
public partial class AbilityHud : Control
{
	[Export] public float TileSize { get; set; } = 86;
	[Export] public float TileGap { get; set; } = 10;
	[Export] public float EdgeMargin { get; set; } = 26;
	[Export] public Color PanelColor { get; set; } = new(0.045f, 0.075f, 0.105f, 0.94f);
	[Export] public Color TextColor { get; set; } = new(0.91f, 0.96f, 0.94f);

	private sealed record Slot(Ability Ability, AbilityConfig Row, string InputAction);
	private readonly List<Slot> _slots = new();
	private readonly StyleBoxFlat _panel = new();
	private readonly StyleBoxFlat _key = new();
	private float _timer;
	public int SlotCount => _slots.Count;
	public string DisplayedIds => string.Join(",", _slots.ConvertAll(s => s.Ability.AbilityId));

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_panel.BgColor = PanelColor;
		_panel.BorderColor = new Color(0.45f, 0.65f, 0.65f, 0.5f);
		_panel.SetBorderWidthAll(1);
		_panel.SetCornerRadiusAll(12);
		_panel.ShadowColor = new Color(0, 0, 0, 0.28f);
		_panel.ShadowSize = 5;
		_key.BgColor = new Color(0.1f, 0.16f, 0.2f);
		_key.SetCornerRadiusAll(4);
	}
	public override void _Process(double delta)
	{
		_timer -= (float)delta;
		if (_timer > 0) return;
		_timer = 0.05f;
		_slots.Clear();
		if (PlayerLocator.Player is { IsDead: false } player)
		{
			foreach (var equipped in player.AbilitySlots.Equipped)
			{
				if (equipped.Ability is { Enabled: true } ability && equipped.Row is { HudIcon: not null } row)
				{
					_slots.Add(new Slot(ability, row, equipped.InputSlot));
				}
			}
			_slots.Sort((a, b) => a.Row.HudOrder != b.Row.HudOrder
				? a.Row.HudOrder.CompareTo(b.Row.HudOrder)
				: string.Compare(a.Row.Id, b.Row.Id, StringComparison.Ordinal));
		}
		QueueRedraw();
	}
	private int Columns => Mathf.Max(1, Mathf.Min(_slots.Count, (int)((Size.X - EdgeMargin * 2) / (TileSize + TileGap))));
	private Rect2 TileRect(int index)
	{
		int columns = Columns;
		int row = index / columns;
		int rows = (_slots.Count + columns - 1) / columns;
		int inRow = Mathf.Min(columns, _slots.Count - row * columns);
		float stride = TileSize + TileGap;
		return new Rect2(Size.X - EdgeMargin - inRow * stride + TileGap + index % columns * stride,
			Size.Y - EdgeMargin - rows * (stride + 20) + row * (stride + 20), TileSize, TileSize);
	}
	public override void _Draw()
	{
		Font font = GetThemeDefaultFont();
		for (int i = 0; i < _slots.Count; i++)
		{
			Slot slot = _slots[i];
			Ability ability = slot.Ability;
			Rect2 rect = TileRect(i);
			Vector2 center = rect.GetCenter() + new Vector2(0, -4);
			Color accent = slot.Row.HudColor;
			bool effectActive = ability.IsActive || ability.EffectRemainingSeconds > 0f;
			_panel.BorderColor = effectActive ? accent : new Color(accent, 0.35f);
			_panel.Draw(GetCanvasItem(), rect);
			float radius = TileSize * 0.34f;
			var iconRect = new Rect2(center - Vector2.One * radius * 0.72f, Vector2.One * radius * 1.44f);
			DrawTextureRect(slot.Row.HudIcon!, iconRect, false, ability.IsReady || effectActive ? accent : accent.Darkened(0.35f));
			float cooldown = Fraction(ability);
			DrawArc(center, radius, -Mathf.Pi / 2, Mathf.Tau - Mathf.Pi / 2, 64, new Color(accent, 0.14f), 2, true);
			if (cooldown > 0)
			{
				if (!ability.IsReady && !ability.IsActive) DrawCooldown(center, radius - 2, cooldown);
				DrawArc(center, radius, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * (1 - cooldown), 64, accent, 2.5f, true);
				if (!ability.IsReady && !ability.IsActive)
				{
					Text(font, center + new Vector2(-radius, 8), radius * 2, ability.CooldownRemaining.ToString("0.0"), 23, TextColor);
				}
			}
			else if (effectActive)
			{
				DrawArc(center, radius, -Mathf.Pi / 2, Mathf.Tau - Mathf.Pi / 2, 64, accent, 2.5f, true);
			}
			if (ability.IsCharging)
			{
				Rect2 track = new(rect.Position + new Vector2(8, TileSize - 10), new Vector2(TileSize - 16, 3));
				DrawRect(track, new Color(accent, 0.2f));
				DrawRect(new Rect2(track.Position, track.Size * new Vector2(ability.ChargeProgress, 1)), accent);
			}
			if (ability.IsMaintaining) Text(font, rect.Position + new Vector2(5, 18), TileSize - 10, "维持", 13, accent);
			if (ability.EffectRemainingSeconds > 0f) Text(font, rect.Position + new Vector2(5, 18), TileSize - 10, $"{ability.EffectRemainingSeconds:0.0}s", 13, accent);
			if (ability.MaxCharges > 1)
			{
				Text(font, rect.Position + new Vector2(TileSize - 43, 19), 36, $"{ability.ChargesRemaining}/{ability.MaxCharges}", 15, TextColor);
			}
			Rect2 keyRect = new(rect.Position + new Vector2(TileSize * 0.25f, TileSize - 17), new Vector2(TileSize * 0.5f, 22));
			_key.Draw(GetCanvasItem(), keyRect);
			Text(font, keyRect.Position + new Vector2(0, 16), keyRect.Size.X, KeyCaption(slot.InputAction), 13, TextColor);
			Text(font, rect.Position + new Vector2(-4, TileSize + 24), TileSize + 8, slot.Row.DisplayName, 14, TextColor);
		}
	}
	private void Text(Font font, Vector2 baseline, float width, string text, int size, Color color)
	{
		DrawStringOutline(font, baseline, text, HorizontalAlignment.Center, width, size, 3, new Color(0.015f, 0.025f, 0.04f, 0.8f));
		DrawString(font, baseline, text, HorizontalAlignment.Center, width, size, color);
	}
	private void DrawCooldown(Vector2 center, float radius, float fraction)
	{
		// 临近0/1时使用空扇形/整圆，避免浮点精度把多边形压成重合点而导致三角化失败。
		if (fraction <= 0.005f) return;
		if (fraction >= 0.995f)
		{
			DrawCircle(center, radius, new Color(0.012f, 0.025f, 0.04f, 0.82f));
			return;
		}
		int steps = Mathf.Max(1, Mathf.CeilToInt(64 * fraction));
		var points = new Vector2[steps + 2];
		points[0] = center;
		for (int i = 0; i <= steps; i++)
		{
			float angle = -Mathf.Pi / 2 + Mathf.Tau * fraction * i / steps;
			points[i + 1] = center + Vector2.FromAngle(angle) * radius;
		}
		DrawColoredPolygon(points, new Color(0.012f, 0.025f, 0.04f, 0.82f));
	}
	private static float Fraction(Ability ability)
	{
		float span = ability.MaxCharges > 1 ? ability.EffectiveRechargeSeconds : ability.EffectiveCooldown;
		return span <= 0 ? 0 : Mathf.Clamp(ability.CooldownRemaining / span, 0, 1);
	}
	public static string KeyCaption(string action)
	{
		if (string.IsNullOrEmpty(action)) return "被动";
		foreach (InputEvent input in InputMap.ActionGetEvents(action))
		{
			if (input is InputEventMouseButton mouse) return mouse.ButtonIndex switch
			{
				MouseButton.Left => "左键",
				MouseButton.Right => "右键",
				_ => $"鼠{(int)mouse.ButtonIndex}",
			};
			if (input is InputEventKey key) return OS.GetKeycodeString(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode);
		}
		return "未绑定";
	}
	public float CooldownFraction(string id) => _slots.Find(s => s.Ability.AbilityId == id) is { } slot ? Fraction(slot.Ability) : 0;
	public Rect2 BoundsOf(string id) => _slots.FindIndex(s => s.Ability.AbilityId == id) is int index && index >= 0 ? TileRect(index) : new Rect2();
	public string IconPathOf(string id) => _slots.Find(s => s.Ability.AbilityId == id)?.Row.HudIcon?.ResourcePath ?? string.Empty;
	public string DebugState() => $"技能栏{SlotCount}格 [{DisplayedIds}] 鼠标穿透={MouseFilter == MouseFilterEnum.Ignore}";
}

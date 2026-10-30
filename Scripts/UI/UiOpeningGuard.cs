using System.Collections.Generic;
using Godot;

namespace ActRoguelike.UI;

/// <summary>使用真实时钟保护新弹窗；开窗时或保护期内按住的确认输入必须先松开。</summary>
public sealed class UiOpeningGuard
{
	private static readonly Key[] ConfirmKeys = { Key.Enter, Key.KpEnter, Key.Space, Key.Key1, Key.Key2, Key.Key3, Key.Key4, Key.Kp1, Key.Kp2, Key.Kp3, Key.Kp4 };
	private readonly HashSet<Key> _heldKeys = new();
	private ulong _interactiveAt;
	private bool _heldMouse, _heldAccept;
	public bool IsBlocked
	{
		get
		{
			if (Time.GetTicksMsec() < _interactiveAt || _heldMouse && Input.IsMouseButtonPressed(MouseButton.Left)
				|| _heldAccept && Input.IsActionPressed("ui_accept")) return true;
			foreach (Key key in _heldKeys) if (Input.IsPhysicalKeyPressed(key) || Input.IsKeyPressed(key)) return true;
			return false;
		}
	}
	public void Begin(float seconds)
	{
		_interactiveAt = Time.GetTicksMsec() + (ulong)Mathf.CeilToInt(Mathf.Max(0, seconds) * 1000);
		_heldMouse = Input.IsMouseButtonPressed(MouseButton.Left);
		_heldAccept = Input.IsActionPressed("ui_accept");
		_heldKeys.Clear();
		foreach (Key key in ConfirmKeys) if (Input.IsPhysicalKeyPressed(key) || Input.IsKeyPressed(key)) _heldKeys.Add(key);
	}
	/// <summary>松手即使发生在上层窗口也清理；被保护的松手本身不交给GUI确认。</summary>
	public bool Filter(InputEvent input, bool active)
	{
		bool release = false;
		if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
		{
			if (!mouse.Pressed) { release = _heldMouse; _heldMouse = false; }
			else if (active && Time.GetTicksMsec() < _interactiveAt) _heldMouse = true;
		}
		if (input is InputEventKey key)
		{
			Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
			if (!key.Pressed) release |= _heldKeys.Remove(code);
			else if (active && Time.GetTicksMsec() < _interactiveAt && System.Array.IndexOf(ConfirmKeys, code) >= 0) _heldKeys.Add(code);
		}
		if (input.IsActionReleased("ui_accept")) { release |= _heldAccept; _heldAccept = false; }
		else if (active && input.IsActionPressed("ui_accept") && Time.GetTicksMsec() < _interactiveAt) _heldAccept = true;
		return active && (IsBlocked || release) && input is InputEventMouseButton or InputEventKey or InputEventJoypadButton or InputEventAction;
	}
}

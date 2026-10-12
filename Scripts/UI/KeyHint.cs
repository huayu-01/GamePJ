using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 一条「按键提示」：左边是按键（或一段短文本），右边是这一键在当前界面做什么。
///
/// 为什么是 struct 而不是 (Key, string) 元组：提示栏要显示的不只有键盘键 ——
/// 鼠标左右键、滚轮、「1~5」「长按左键」这类组合键都不是 <see cref="Key"/>。
/// 所以内部统一成「一段文本 + 一段说明」，键盘 / 鼠标各有一个工厂方法。
///
/// 用法（配合 <see cref="OverlayScreen.SetKeyHints"/>）：
/// <code>
/// SetKeyHints(
///     KeyHint.Of(Key.F, "使用"),
///     KeyHint.Text("1~5", "购买"),
///     KeyHint.Mouse(MouseButton.Right, "升级"));
/// </code>
/// 也支持元组写法（键盘 / 文本各有一个隐式转换）：
/// <code>
/// SetKeyHints((Key.F, "使用"), ("1~5", "购买"));
/// </code>
/// </summary>
public readonly record struct KeyHint(string Keys, string Caption)
{
	/// <summary>键盘键：文本取 <see cref="OS.GetKeycodeString"/>（"F" / "Enter" / "Escape" …）。</summary>
	public static KeyHint Of(Key key, string caption) => new(KeyText(key), caption);

	/// <summary>鼠标键（本项目有左键射击、右键瞄准）。</summary>
	public static KeyHint Mouse(MouseButton button, string caption) => new(MouseText(button), caption);

	/// <summary>任意文本键：组合键（"1~5"）、长按（"长按左键"）都用它。</summary>
	public static KeyHint Text(string keys, string caption) => new(keys, caption);

	/// <summary>空一条（占位用；<see cref="OverlayScreen.SetKeyHints"/> 会跳过 Keys 为空的条目）。</summary>
	public static KeyHint Empty => new(string.Empty, string.Empty);

	public static implicit operator KeyHint((Key Key, string Caption) hint) => Of(hint.Key, hint.Caption);

	public static implicit operator KeyHint((string Keys, string Caption) hint) => Text(hint.Keys, hint.Caption);

	/// <summary>按键 → 显示文本（唯一一处，界面与文档都对得上）。</summary>
	public static string KeyText(Key key) => OS.GetKeycodeString(key);

	/// <summary>鼠标键 → 中文显示文本。</summary>
	public static string MouseText(MouseButton button) => button switch
	{
		MouseButton.Left => "鼠标左键",
		MouseButton.Right => "鼠标右键",
		MouseButton.Middle => "鼠标中键",
		MouseButton.WheelUp => "滚轮上",
		MouseButton.WheelDown => "滚轮下",
		MouseButton.WheelLeft => "滚轮左",
		MouseButton.WheelRight => "滚轮右",
		_ => "鼠标",
	};
}

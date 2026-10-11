using System;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 弹出界面基类（UI 框架核心）。
///
/// 统一处理四件事，子类只管内容：
/// 1. **分层**（<see cref="UiLayer"/>）：HUD 不暂停游戏，面板/模态才暂停；
/// 2. **防误触**：打开后一段时间不收输入，长度取「保护期」与「缓动时长」的较大值；
/// 3. **开合动画**：淡入 + 轻微缩放，时长与起始缩放可配（0 = 关闭动画）；
/// 4. **Esc 关闭**：由 <see cref="UiRouter"/> 统一处理，关闭后自动恢复游戏与鼠标捕获。
/// </summary>
public abstract partial class OverlayScreen : CanvasLayer
{
	/// <summary>界面层：决定是否暂停游戏。</summary>
	public enum UiLayer
	{
		/// <summary>常驻 HUD：不暂停、不拦输入。</summary>
		Hud,

		/// <summary>普通面板：暂停游戏，可 Esc 关闭（基地的强化台 / 配置台 / 说明台）。</summary>
		Panel,

		/// <summary>模态：暂停游戏，通常必须做出选择（奖励三选一 / 商店）。</summary>
		Modal,
	}

	/// <summary>
	/// 本界面所属层（决定是否暂停游戏）。
	/// 注意别叫 Layer —— 那会遮蔽 CanvasLayer.Layer，Godot 按属性名绑定会产生歧义。
	/// </summary>

	/// <summary>本界面所属层（决定是否暂停游戏）。</summary>
	[Export] public UiLayer ScreenLayer { get; set; } = UiLayer.Panel;

	/// <summary>打开后的输入保护时间（秒），防误触。</summary>
	[Export] public float OpenGuardSeconds { get; set; } = 0.3f;

	/// <summary>开合缓动时长（秒）。0 = 不做动画。</summary>
	[Export] public float FadeSeconds { get; set; } = 0.16f;

	/// <summary>打开动画的起始缩放（1 = 不缩放）。</summary>
	[Export(PropertyHint.Range, "0.5,1,0.01")] public float OpenScaleFrom { get; set; } = 0.94f;

	/// <summary>兼容旧字段：曾经的"缓动时长"。</summary>
	public float OpeningDuration
	{
		get => FadeSeconds;
		set => FadeSeconds = value;
	}

	/// <summary>是否允许用 Esc 关闭。流程关键界面（必须选择/必须确认）设为 false。</summary>
	[Export] public bool CloseOnCancel { get; set; } = true;

	public bool IsOpen { get; private set; }

	/// <summary>保护期是否还没结束（true = 现在不接受输入）。</summary>
	public bool InputBlocked => Time.GetTicksMsec() < _interactiveAt;

	private ulong _interactiveAt;

	protected abstract Control Root { get; }

	/// <summary>子类在 _Ready 里调用一次：记录自己的根节点并保持可处理输入。</summary>
	protected void InitializeOverlay()
	{
		ProcessMode = ProcessModeEnum.Always;
		Root.Visible = false;
		Root.Modulate = new Color(1f, 1f, 1f, 0f);
	}

	protected void BeginOpen()
	{
		IsOpen = true;
		Root.Visible = true;
		_interactiveAt = Time.GetTicksMsec() + (ulong)(Mathf.Max(OpenGuardSeconds, FadeSeconds) * 1000f);

		// 先登记再播动画，保证暂停/鼠标模式立刻生效。
		UiRouter.Instance?.Push(this);
		PlayFade(open: true, onDone: null);
	}

	protected void BeginClose()
	{
		IsOpen = false;

		// 先出栈（恢复游戏与鼠标），再播关闭动画 —— 否则动画期间游戏还停着。
		UiRouter.Instance?.Pop(this);
		PlayFade(open: false, onDone: () => Root.Visible = false);
	}

	/// <summary>统一的淡入 + 缩放动画。</summary>
	private void PlayFade(bool open, Action? onDone)
	{
		if (FadeSeconds <= 0.01f)
		{
			Root.Modulate = new Color(1f, 1f, 1f, open ? 1f : 0f);
			Root.Scale = Vector2.One;
			onDone?.Invoke();
			return;
		}

		// 缩放围绕中心。
		Root.PivotOffset = Root.Size * 0.5f;

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(Root, "modulate:a", open ? 1f : 0f, FadeSeconds)
			.SetTrans(Tween.TransitionType.Sine);
		tween.TweenProperty(Root, "scale", open ? Vector2.One : Vector2.One * OpenScaleFrom, FadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(open ? Tween.EaseType.Out : Tween.EaseType.In);

		if (open)
		{
			Root.Scale = Vector2.One * OpenScaleFrom;
			Root.Modulate = new Color(1f, 1f, 1f, 0f);
		}
		else
		{
			tween.Chain().TweenCallback(Callable.From(() => onDone?.Invoke()));
		}
	}

	/// <summary>由 UiRouter 在 Esc 时调用；返回 true 表示这次按键被本界面消费。</summary>
	public bool TryCloseByCancel()
	{
		if (!IsOpen || !CloseOnCancel || InputBlocked || !CanCloseNow())
		{
			return false;
		}

		Close();
		return true;
	}

	/// <summary>子类可覆盖：还有子状态时不允许直接关闭。</summary>
	protected virtual bool CanCloseNow() => true;

	/// <summary>子类实现具体关闭逻辑（通常调用 BeginClose）。</summary>
	public abstract void Close();
}

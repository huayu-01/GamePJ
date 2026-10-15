using System;
using System.Collections.Generic;
using ActRoguelike.Audio;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 弹出界面基类（UI 框架核心）。
///
/// 统一处理五件事，子类只管内容：
/// 1. **分层**（<see cref="UiLayer"/>）：HUD 不暂停游戏，面板/模态才暂停；
/// 2. **防误触**：打开后一段时间不收输入，长度取「保护期」与「缓动时长」的较大值；
/// 3. **开合动画**：淡入 + 轻微缩放，时长与起始缩放可配（0 = 关闭动画）；
/// 4. **Esc 关闭**：由 <see cref="UiRouter"/> 统一处理，关闭后自动恢复游戏与鼠标捕获；
/// 5. **按键提示栏**（<see cref="SetKeyHints"/>）：屏幕底部一栏「键 + 说明」，
///    **结构永远存在**（<see cref="UiKeyHintBar"/>），界面可以选择不填 / 不显示。
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

	/// <summary>按键提示栏的节点名。场景里可以手写同名节点覆盖样式；没有就由基类建一份。</summary>
	public const string KeyHintBarNodeName = "KeyHintBar";

	/// <summary>提示栏高度（像素，锚在屏幕底部中央）。</summary>
	private const int KeyHintBarHeight = 62;

	private readonly List<KeyHint> _keyHints = new();
	private HBoxContainer? _keyHintBar;
	private bool _keyHintsEnabled = true;
	private bool _overlayInitialized;

	protected abstract Control Root { get; }

	// ───────────────────────── 按键提示栏（写在基类，第三批能力系统直接照用）─────────────────────────

	/// <summary>
	/// 提示栏的容器（<b>结构永远存在</b>）：屏幕底部整条、水平居中、背景完全透明。
	/// <list type="bullet">
	/// <item><b>层级最高</b>：它是 <see cref="Root"/> 的最后一个子节点，且 <c>ZIndex</c> 拉到 100 —— 同屏任何元素都挡不住它；</item>
	/// <item><b>不吃鼠标</b>：整条 + 每个键帽都是 <see cref="Control.MouseFilterEnum.Ignore"/>，
	///   绝不会像准星那样把鼠标事件抢走（见 architecture.md 第 7 条）；</item>
	/// <item><b>可以不显示</b>：没调 <see cref="SetKeyHints"/> 时它是 <c>Visible = false</c>，但节点一直在树里。</item>
	/// </list>
	/// </summary>
	protected HBoxContainer UiKeyHintBar
	{
		get
		{
			EnsureKeyHintBar();
			return _keyHintBar!;
		}
	}

	/// <summary>提示栏现在有没有内容（结构存在 ≠ 有内容）。</summary>
	public bool HasKeyHints => _keyHints.Count > 0;

	/// <summary>提示栏当前是否画出来（只读；改它用 <see cref="ShowKeyHints"/>）。</summary>
	public bool KeyHintsVisible => _keyHintBar is { Visible: true };

	/// <summary>提示栏里的条目数（验证用只读属性，GDScript 探针可以直接读）。</summary>
	public int KeyHintCount => _keyHints.Count;

	// ── 下面四个是"结构存在 / 层级最高 / 不遮挡"的**机器可判定形式**（GDScript 探针直接读，见 docs/ui.md）──

	/// <summary>提示栏节点真的在树里吗（"可以不显示，但不能没有"）。</summary>
	public bool HasKeyHintBar => _keyHintBar != null && IsInstanceValid(_keyHintBar) && _keyHintBar.IsInsideTree();

	/// <summary>提示栏是 <see cref="Root"/> 的**最后一个子节点**吗（层级最高的第一条保证）。</summary>
	public bool KeyHintBarTopmost =>
		_keyHintBar != null && IsInstanceValid(_keyHintBar)
		&& _keyHintBar.GetParent() == Root
		&& _keyHintBar.GetIndex() == Root.GetChildCount() - 1;

	/// <summary>提示栏的 <c>ZIndex</c>（层级最高的第二条保证；100 表示压在同屏元素之上）。</summary>
	public int KeyHintBarZIndex => _keyHintBar != null && IsInstanceValid(_keyHintBar) ? _keyHintBar.ZIndex : -1;

	/// <summary>提示栏完全不吃鼠标事件吗（"不遮挡其他元素"：背景透明 + 鼠标穿透）。</summary>
	public bool KeyHintBarIgnoresMouse => _keyHintBar is { MouseFilter: Control.MouseFilterEnum.Ignore };

	/// <summary>
	/// 验证 / 拍图用：塞一组示例提示（键盘 / 文本 / 鼠标三种形态各一条）。
	/// 放在基类里是有意的 —— 任何界面都能一行调它看提示栏长什么样。
	/// </summary>
	public void DebugSetSampleKeyHints()
	{
		SetKeyHints(
			KeyHint.Of(Key.F, "使用"),
			KeyHint.Text("1 ~ 5", "选择"),
			KeyHint.Mouse(MouseButton.Right, "升级"),
			(Key.Escape, "关闭"));
	}

	/// <summary>
	/// 设置整栏按键提示（**一次性替换**）。传空数组 = 清空（等价 <see cref="ClearKeyHints"/>）。
	/// <code>
	/// SetKeyHints(KeyHint.Of(Key.F, "使用"), KeyHint.Text("1~5", "购买"));
	/// SetKeyHints((Key.Escape, "关闭"), ("长按左键", "解锁"));   // 元组写法
	/// </code>
	/// </summary>
	public void SetKeyHints(params KeyHint[] hints)
	{
		_keyHints.Clear();
		foreach (KeyHint hint in hints ?? Array.Empty<KeyHint>())
		{
			if (!string.IsNullOrEmpty(hint.Keys))
			{
				_keyHints.Add(hint);
			}
		}

		RebuildKeyHints();
	}

	/// <summary>追加一条（键盘键）。</summary>
	public void AddKeyHint(Key key, string caption) => AddKeyHint(KeyHint.Of(key, caption));

	/// <summary>追加一条（任意文本键，例如「长按左键」「1~5」）。</summary>
	public void AddKeyHint(string keys, string caption) => AddKeyHint(KeyHint.Text(keys, caption));

	/// <summary>追加一条。</summary>
	public void AddKeyHint(KeyHint hint)
	{
		if (string.IsNullOrEmpty(hint.Keys))
		{
			return;
		}

		_keyHints.Add(hint);
		RebuildKeyHints();
	}

	/// <summary>清空提示栏（结构仍在，只是不画）。</summary>
	public void ClearKeyHints()
	{
		_keyHints.Clear();
		RebuildKeyHints();
	}

	/// <summary>整栏显示 / 隐藏（**只有"显示"这一层**；结构永远在，随手可以再打开）。</summary>
	public void ShowKeyHints(bool visible)
	{
		_keyHintsEnabled = visible;
		ApplyKeyHintVisibility();
	}

	/// <summary>建（或认领）提示栏节点，并把它压到 <see cref="Root"/> 的最上层。</summary>
	private void EnsureKeyHintBar()
	{
		if (_keyHintBar != null && IsInstanceValid(_keyHintBar))
		{
			return;
		}

		// 场景里手写了同名节点就用它（美术可以换样式），否则代码建一份 —— 两种都保证"结构存在"。
		_keyHintBar = Root.GetNodeOrNull<HBoxContainer>(KeyHintBarNodeName) ?? BuildKeyHintBar();
		if (_keyHintBar.GetParent() == null)
		{
			Root.AddChild(_keyHintBar);
		}

		// 层级：① 站到 Root 的最后一个子节点；② ZIndex 拉到 100（双保险，两个都不依赖对方）。
		Root.MoveChild(_keyHintBar, Root.GetChildCount() - 1);
		_keyHintBar.ZIndex = 100;
		_keyHintBar.Visible = false;
	}

	private static HBoxContainer BuildKeyHintBar()
	{
		var bar = new HBoxContainer
		{
			Name = KeyHintBarNodeName,
			// 背景透明：这条栏自己没有任何 Panel / ColorRect，只有几个键帽。
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Alignment = BoxContainer.AlignmentMode.End,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.Begin,
		};

		// 锚在屏幕底部整条（独立于父节点的布局，Root 铺满时它自己就在底部）。
		bar.AnchorLeft = 0f;
		bar.AnchorRight = 1f;
		bar.AnchorTop = 1f;
		bar.AnchorBottom = 1f;
		bar.OffsetLeft = 24f;
		bar.OffsetRight = -14f;
		bar.OffsetTop = -KeyHintBarHeight;
		bar.OffsetBottom = -10f;
		bar.AddThemeConstantOverride("separation", 22);
		return bar;
	}

	/// <summary>按当前条目重建键帽（内容变了才要调）。</summary>
	private void RebuildKeyHints()
	{
		// 提示可能在 _Ready 里比 InitializeOverlay() 更早设置：那时先存着，初始化时统一应用。
		if (!_overlayInitialized)
		{
			return;
		}

		EnsureKeyHintBar();
		foreach (Node child in _keyHintBar!.GetChildren())
		{
			// 先摘再删：QueueFree 要等帧末才生效，同一帧重建两次会看到旧键帽还挂在那儿。
			_keyHintBar.RemoveChild(child);
			child.QueueFree();
		}

		foreach (KeyHint hint in _keyHints)
		{
			_keyHintBar.AddChild(BuildChip(hint));
		}

		ApplyKeyHintVisibility();
	}

	/// <summary>一个键帽 = [按键] 说明。</summary>
	private static HBoxContainer BuildChip(KeyHint hint)
	{
		var chip = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		chip.AddThemeConstantOverride("separation", 8);

		var capBox = new PanelContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ThemeTypeVariation = "UiKeyCapBox",
		};
		var cap = new Label
		{
			Text = hint.Keys,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ThemeTypeVariation = "UiKeyCap",
		};
		capBox.AddChild(cap);

		var caption = new Label
		{
			Text = hint.Caption,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			VerticalAlignment = VerticalAlignment.Center,
		};

		chip.AddChild(capBox);
		chip.AddChild(caption);
		return chip;
	}

	private void ApplyKeyHintVisibility()
	{
		if (_keyHintBar == null || !IsInstanceValid(_keyHintBar))
		{
			return;
		}

		_keyHintBar.Visible = _keyHintsEnabled && _keyHints.Count > 0;
	}

	/// <summary>子类在 _Ready 里调用一次：记录自己的根节点并保持可处理输入。</summary>
	protected void InitializeOverlay()
	{
		ProcessMode = ProcessModeEnum.Always;
		Root.Visible = false;
		Root.Modulate = new Color(1f, 1f, 1f, 0f);

		// 按键提示栏：**在这里无条件建出来** —— 界面可以不填、不显示，但结构不能没有。
		_overlayInitialized = true;
		EnsureKeyHintBar();
		RebuildKeyHints();
	}

	protected void BeginOpen()
	{
		IsOpen = true;
		Root.Visible = true;
		_interactiveAt = Time.GetTicksMsec() + (ulong)(Mathf.Max(OpenGuardSeconds, FadeSeconds) * 1000f);

		// 先登记再播动画，保证暂停/鼠标模式立刻生效。
		UiRouter.Instance?.Push(this);

		// 界面打开音效：六个弹窗共用这一处，按 id 播（表里说了算）。按钮点击音在 AudioManager 里自动挂。
		AudioManager.PlayUi(AudioIds.UiOpen);
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

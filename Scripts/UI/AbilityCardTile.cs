using ActRoguelike.AbilityCards;
using ActRoguelike.Audio;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.UI;

/// <summary>能力矩阵元素上能做的四种操作（需求 §二 的四行交互）。</summary>
public enum AbilityCardAction
{
	/// <summary>左键**长按**（未解锁）→ 读条 → 解锁。</summary>
	Unlock,

	/// <summary>左键**单击**（已解锁）→ 激活 / 关闭。</summary>
	ToggleActive,

	/// <summary>右键**单击** → 升级。</summary>
	Upgrade,

	/// <summary>右键**长按**（已升级多次）→ 读条 → 重置升级进度并归还资源。</summary>
	ResetUpgrades,
}

/// <summary>
/// 矩阵元素的**宿主**：元素把"玩家按了什么"翻译成 <see cref="AbilityCardAction"/>，
/// 规则判定交给宿主；宿主返回一个 <see cref="AbilityCardResult"/>，元素**只按结果决定播哪个动画 + 哪个音**。
///
/// <para>为什么这样切：① 元素因此**完全不知道存档、碎片、额度**的存在，可以单独复制到任何界面；
/// ② 判定只有一处实现（<see cref="AbilityStore"/>），界面不会长出第二套规则；
/// ③ 自检/探针可以塞一个假宿主，把四种交互逐条跑一遍而不用真的点鼠标。</para>
/// </summary>
public interface IAbilityCardHost
{
	/// <summary>请求执行一次操作；返回结果（<see cref="AbilityCardResult.Ok"/> 之外的都算失败）。</summary>
	AbilityCardResult Request(AbilityCardTile tile, AbilityCardAction action);
}

/// <summary>
/// **能力矩阵元素**（需求 §二 的"做进基类"就是它）——一格 = 一张能力卡。
///
/// <para>它一个人负责：三个状态的视觉、四种交互、读条、震动、解锁/升级/重置的缓动、音效。
/// **新增一张卡不需要碰这个文件**（卡表里加一行 `.tres` 就够了），
/// 换界面也不需要（宿主是接口，<see cref="AbilityMatrixScreen"/> 只是它的第一个宿主）。
/// 契约见 `docs/ability-cards.md` §5。</para>
///
/// <list type="table">
/// <item><term>未解锁</term><description>内容**变暗** + 等级 0 的**灰**边框 + 状态行写"未解锁"</description></item>
/// <item><term>解锁未激活</term><description>**恢复颜色**（内容白色）+ 按等级着色的边框</description></item>
/// <item><term>激活</term><description>边框**外**亮起**一圈光圈**（<c>Glow</c> 的 StyleBoxFlat 阴影 + 呼吸），光圈颜色与边框**同色**</description></item>
/// </list>
///
/// <para>边框颜色 = 等级：**0 灰 &gt; 1 白 &gt; 2 绿 &gt; 3 蓝 &gt; 4 紫 &gt; 5 红**（<see cref="LevelColor"/> 是唯一一处）。</para>
///
/// <para>交互（鼠标事件走 <c>_GuiInput</c>，松手也轮询 <see cref="Input.IsMouseButtonPressed"/> 兜底 ——
/// 在元素外松手也能正常结算）：<br/>
/// ① 左键**长按**（<see cref="LongPressSeconds"/> 秒）→ 底部读条 → 完成即解锁；<br/>
/// ② 左键**单击** → 激活 / 关闭；<br/>
/// ③ 右键**单击** → 升级；<br/>
/// ④ 右键**长按** → 读条 → 重置升级进度并归还升级费。<br/>
/// 失败（资源不够 / 额度不够 / 未解锁 / 已满级 / 没得重置）一律 **震动元素 + 失败音效**。</para>
///
/// <para><b>占位卡</b>（用户 A5：「空的位置也有卡牌占着，但是会因为没有设定解锁条件而处于一直无法解锁」）：
/// <see cref="Card"/> 为 <c>null</c> 的那一格就是占位卡 —— **卡表里没有对应行**，所以它没有解锁条件、
/// <b>永远不会解锁 / 激活 / 升级</b>（宿主对它一律返回 <see cref="AbilityCardResult.NoCard"/>）。
/// 但它**不是死格子**：四种交互与失败反馈（读条、震动、失败音效）与真卡**走同一条代码路径**
/// （<see cref="RunAction"/> 只按结果决定动画与音效，不需要为占位卡写第二条分支）。</para>
/// </summary>
public partial class AbilityCardTile : Control
{
	/// <summary>长按读条时长（秒）。两者（解锁 / 重置）共用它。</summary>
	[Export(PropertyHint.Range, "0.2,2,0.05")] public float LongPressSeconds { get; set; } = 0.7f;

	/// <summary>占位卡边框的不透明度（<see cref="BorderColor"/> 与 <see cref="RefreshVisual"/> 共用同一个值）。</summary>
	public const float PlaceholderBorderAlpha = 0.55f;

	/// <summary>震动幅度（弧度）与时长（秒）。"失效/失败的交互都要震动对应元素"。</summary>
	[Export] public float ShakeRadians { get; set; } = 0.055f;

	/// <summary>震动时长（秒）。</summary>
	[Export] public float ShakeSeconds { get; set; } = 0.28f;

	/// <summary>这张卡（<c>null</c> = **占位卡**：卡表里还没有对应的行，所以它没有解锁条件）。</summary>
	public AbilityCardConfig? Card { get; private set; }

	/// <summary>这一格是占位卡吗（卡表里没有对应行 → 永远无法解锁 / 激活 / 升级，但交互反馈照旧）。</summary>
	public bool IsPlaceholder => Card == null;

	/// <summary>规则判定的宿主（由界面注入）。</summary>
	public IAbilityCardHost? Host { get; set; }

	// ── 只读状态（自检 / 探针直接读，不要另写一套判定）──

	/// <summary>当前等级（0 = 未解锁）。</summary>
	public int Level => AbilityStore.LevelOf(Card?.Id);

	/// <summary>激活中吗。</summary>
	public bool IsActive => AbilityStore.IsActive(Card?.Id);

	/// <summary>三状态之一（界面上的"变暗 / 恢复颜色 / 光圈"就是它）；占位卡是第四种展示。</summary>
	public string StateName => IsPlaceholder ? "占位卡" : Level <= 0 ? "未解锁" : IsActive ? "激活" : "解锁未激活";

	/// <summary>边框 / 光圈当前的颜色（= 等级色；占位卡是压暗一档的灰）。</summary>
	public Color BorderColor => IsPlaceholder
		? new Color(LevelColor(0).R, LevelColor(0).G, LevelColor(0).B, PlaceholderBorderAlpha)
		: LevelColor(Level);

	/// <summary>光圈现在亮着吗（激活才亮）。</summary>
	public bool HasGlow => _glow is { Visible: true };

	/// <summary>正在读条吗（探针可读）。</summary>
	public bool IsCharging => _holding;

	/// <summary>读条进度 0~1（探针可读）。</summary>
	public float ChargeProgress { get; private set; }

	/// <summary>本元素上最后一次操作的结果（探针可读）。</summary>
	public AbilityCardResult LastResult { get; private set; } = AbilityCardResult.NoCard;

	/// <summary>本元素一共成功执行了几次操作（探针可读）。</summary>
	public int SuccessCount { get; private set; }

	/// <summary>本元素一共失败（震动）了几次（探针可读）。</summary>
	public int FailCount { get; private set; }

	// ── 视觉节点（由 AbilityCardTile.tscn 提供；缺哪个都不崩）──

	private Panel? _glow;
	private PanelContainer? _frame;
	private Control? _content;
	private TextureRect? _icon;
	private Label? _name;
	private Label? _status;
	private Label? _hint;
	private ProgressBar? _progress;

	private bool _holding;
	private bool _longFired;
	private bool _syntheticHold;
	private MouseButton _holdButton = MouseButton.Left;
	private ulong _holdStart;
	private Tween? _glowTween;
	private Tween? _shakeTween;
	private Tween? _flashTween;

	public override void _Ready()
	{
		_glow = GetNodeOrNull<Panel>("Glow");
		_frame = GetNodeOrNull<PanelContainer>("Frame");
		_content = GetNodeOrNull<Control>("Frame/Pad/VBox");
		_icon = GetNodeOrNull<TextureRect>("Frame/Pad/VBox/Icon");
		_name = GetNodeOrNull<Label>("Frame/Pad/VBox/Name");
		_status = GetNodeOrNull<Label>("Frame/Pad/VBox/Status");
		_hint = GetNodeOrNull<Label>("Frame/Pad/VBox/Hint");
		_progress = GetNodeOrNull<ProgressBar>("Progress");

		MouseFilter = MouseFilterEnum.Stop;
		MouseDefaultCursorShape = CursorShape.PointingHand;
		Resized += UpdatePivot;
		UpdatePivot();
		RefreshVisual();
	}

	private void UpdatePivot() => PivotOffset = Size * 0.5f;

	/// <summary>
	/// 绑定一格的宿主（界面建格子时调一次）。
	/// <paramref name="card"/> 传 <c>null</c> = **占位卡**（"空位也放一张卡"），
	/// 除了不显示卡面内容、失败反馈照旧之外，与真卡**没有任何行为差异**。
	/// </summary>
	public void Bind(AbilityCardConfig? card, IAbilityCardHost host)
	{
		Card = card;
		Host = host;
		RefreshVisual();
	}

	// ───────────────────────── 三个状态的视觉 ─────────────────────────

	/// <summary>
	/// 按"等级 + 激活"重画这一格。**唯一一处**决定颜色 / 变暗 / 光圈 / 文案的地方，
	/// 所以任何状态变化之后只要调它，三个状态就不会各自漂移。
	/// </summary>
	public void RefreshVisual()
	{
		if (_frame == null)
		{
			return;
		}

		int level = Level;
		bool active = IsActive;
		bool locked = level <= 0;
		bool placeholder = IsPlaceholder;
		Color border = LevelColor(level);
		if (placeholder)
		{
			// 占位卡：用与"未解锁"同一档的**灰**，但压暗一档 —— 一眼能看出"这里还没有卡"，
			// 又不至于像坏掉的格子（用户要的是"填满的矩阵"，不是"死格子"）。
			border = new Color(border.R, border.G, border.B, PlaceholderBorderAlpha);
		}

		// ① 底板 + 边框：边框颜色 = 等级色；激活时边框加粗一档。
		var box = new StyleBoxFlat
		{
			BgColor = placeholder
				? new Color(0.06f, 0.07f, 0.10f, 0.72f)
				: locked
					? new Color(0.07f, 0.08f, 0.11f, 0.96f)
					: active
						? new Color(0.14f, 0.17f, 0.25f, 0.99f)
						: new Color(0.10f, 0.12f, 0.18f, 0.97f),
			BorderColor = border,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			AntiAliasing = true,
		};
		box.SetBorderWidthAll(active ? 3 : 2);
		_frame.AddThemeStyleboxOverride("panel", box);

		// ② 光圈：**在边框外**亮起（StyleBoxFlat 的 shadow 往外扩），颜色与边框一致；只有激活才亮。
		if (_glow != null)
		{
			var glow = new StyleBoxFlat
			{
				BgColor = new Color(0, 0, 0, 0),
				DrawCenter = false,
				BorderColor = new Color(border.R, border.G, border.B, 0.85f),
				ShadowColor = new Color(border.R, border.G, border.B, 0.55f),
				ShadowSize = 10,
				ShadowOffset = Vector2.Zero,
				CornerRadiusTopLeft = 12,
				CornerRadiusTopRight = 12,
				CornerRadiusBottomLeft = 12,
				CornerRadiusBottomRight = 12,
				AntiAliasing = true,
			};
			glow.SetBorderWidthAll(3);
			_glow.AddThemeStyleboxOverride("panel", glow);
			_glow.Visible = active;
			_glow.Modulate = new Color(1, 1, 1, active ? 1f : 0f);
			if (active)
			{
				StartGlowPulse();
			}
			else
			{
				StopGlowPulse();
			}
		}

		// ③ 内容：未解锁**变暗**；解锁后**恢复颜色**。
		if (_content != null)
		{
			_content.Modulate = locked ? new Color(0.40f, 0.42f, 0.48f, 1f) : Colors.White;
		}

		if (_icon != null)
		{
			_icon.Texture = UiIcons.ForAbilityCard(Card);
		}

		if (_name != null)
		{
			_name.Text = Card?.DisplayName ?? "占位卡";
		}

		if (_status != null)
		{
			_status.Text = StatusText(level, active);
		}

		if (_hint != null)
		{
			_hint.Text = HintText(level);
		}

		TooltipText = TooltipFor(level, active);
	}

	/// <summary>等级 → 边框颜色（**唯一一处**：0 灰 / 1 白 / 2 绿 / 3 蓝 / 4 紫 / 5 红）。</summary>
	public static Color LevelColor(int level) => level switch
	{
		0 => new Color(0.42f, 0.44f, 0.50f),
		1 => new Color(0.92f, 0.94f, 1.00f),
		2 => new Color(0.35f, 0.92f, 0.42f),
		3 => new Color(0.34f, 0.64f, 1.00f),
		4 => new Color(0.72f, 0.42f, 1.00f),
		_ => new Color(1.00f, 0.34f, 0.28f),
	};

	/// <summary>等级 → 中文名（图例与提示共用）。</summary>
	public static string LevelName(int level) => level switch
	{
		0 => "灰 0",
		1 => "白 1",
		2 => "绿 2",
		3 => "蓝 3",
		4 => "紫 4",
		_ => "红 5",
	};

	private string StatusText(int level, bool active)
	{
		if (Card == null)
		{
			// 占位卡（用户 A5）：没有解锁条件，所以永远停在"不可解锁"。
			return "占位卡 · 不可解锁";
		}

		if (level <= 0)
		{
			return "未解锁";
		}

		return active
			? $"Lv{level} · 激活中 · 占 {Card.QuotaCost} 点"
			: $"Lv{level} / {Card.MaxLevel} · 占 {Card.QuotaCost} 点";
	}

	private string HintText(int level)
	{
		if (Card == null)
		{
			return "未设定解锁条件";
		}

		if (level <= 0)
		{
			return $"长按左键解锁 · {Card.UnlockCost}";
		}

		if (level >= Card.MaxLevel)
		{
			return "已满级 · 长按右键重置";
		}

		return $"右键升级 · {Card.UpgradeCostOf(level)}";
	}

	private string TooltipFor(int level, bool active)
	{
		if (Card == null)
		{
			return "占位卡（矩阵空位）\n"
				+ "这一格在卡表里还没有对应行，所以**没有解锁条件** —— 它永远不会解锁、激活、升级。\n"
				+ "（交互反馈照旧：点它 / 长按它一样会震动 + 失败音效。）\n"
				+ "要把它变成真正的卡：在 Config/Tables/AbilityCards/ 加一行 .tres，"
				+ "再挂进 Config/Balance.tres 的 AbilityCards 数组。";
		}

		int next = level <= 0 ? 1 : Mathf.Min(Card.MaxLevel, level + 1);
		string state = level <= 0 ? "未解锁" : active ? "激活中" : "解锁未激活";
		return $"{Card.DisplayName}（{Card.Id}）\n"
			+ $"等级 {level} / {Card.MaxLevel}（{state}，{LevelName(level)}边框）\n"
			+ $"额度占用 {Card.QuotaCost} 点\n"
			+ $"效果（Lv{next}）：{Card.EffectText(next)}\n"
			+ (level <= 0
				? $"长按左键解锁：{Card.UnlockCost} 碎片"
				: level >= Card.MaxLevel
					? "已满级；长按右键可重置升级并归还升级费"
					: $"右键升级：{Card.UpgradeCostOf(level)} 碎片；左键{(active ? "关闭" : "激活")}")
			+ (string.IsNullOrEmpty(Card.DescriptionTemplate) ? string.Empty : $"\n{Card.DescriptionTemplate}");
	}

	// ───────────────────────── 四种交互 ─────────────────────────

	public override void _GuiInput(InputEvent @event)
	{
		// ⚠️ 这里**不**排除占位卡：用户要的是"填满的矩阵"而不是"死格子"，
		// 点它 / 长按它照常读条、照常震动 + 失败音效（占位卡的失败在宿主那一侧判定）。
		if (@event is not InputEventMouseButton button)
		{
			return;
		}

		if (button.Pressed)
		{
			if (button.ButtonIndex is not (MouseButton.Left or MouseButton.Right))
			{
				return;
			}

			BeginHold(button.ButtonIndex);
			AcceptEvent();
			return;
		}

		if (button.ButtonIndex == _holdButton && _holding)
		{
			FinishHold();
			AcceptEvent();
		}
	}

	public override void _Process(double delta)
	{
		if (!_holding)
		{
			return;
		}

		// 有读条的操作（左键解锁 / 右键重置）才走计时；单击的那两种不等读条。
		// 占位卡的等级恒为 0，所以它和"未解锁的真卡"走**同一条**读条路径（左键长按读条 → 失败）。
		bool charging = _holdButton == MouseButton.Left ? Level <= 0 : Level > 1;
		if (!charging)
		{
			// 单击的那两种：等松手（松手时结算），这里只补一条"在元素外松手"的兜底。
			ReleaseIfButtonUp();
			return;
		}

		float elapsed = (Time.GetTicksMsec() - _holdStart) / 1000f;
		ChargeProgress = Mathf.Min(1f, elapsed / Mathf.Max(0.05f, LongPressSeconds));
		if (_progress != null)
		{
			_progress.Visible = true;
			_progress.Value = ChargeProgress * 100f;
		}

		if (ChargeProgress >= 1f && !_longFired)
		{
			// 读条结束：**此刻才判定资源够不够**（需求："结束判定资源是否足够 → 不够则震动 → 够则解锁"）。
			_longFired = true;
			RunAction(_holdButton == MouseButton.Left ? AbilityCardAction.Unlock : AbilityCardAction.ResetUpgrades);
		}

		ReleaseIfButtonUp();
	}

	/// <summary>
	/// 兜底：鼠标在元素**外面**松开时 <c>_GuiInput</c> 收不到松手事件，
	/// 这里轮询真实鼠标状态补一次结算（否则读条会永远卡在那里）。
	/// 探针用代码读条（<see cref="DebugBeginCharge"/>）时跳过这条 —— 那时并没有真的按住鼠标。
	/// </summary>
	private void ReleaseIfButtonUp()
	{
		if (!_syntheticHold && !Input.IsMouseButtonPressed(_holdButton))
		{
			FinishHold();
		}
	}

	private void BeginHold(MouseButton button)
	{
		_holding = true;
		_longFired = false;
		_syntheticHold = false;
		_holdButton = button;
		_holdStart = Time.GetTicksMsec();
		ChargeProgress = 0f;
		if (_progress != null)
		{
			_progress.Visible = false;
			_progress.Value = 0f;
		}
	}

	private void FinishHold()
	{
		bool wasLong = _longFired;
		_holding = false;
		_longFired = false;
		ChargeProgress = 0f;
		if (_progress != null)
		{
			_progress.Visible = false;
		}

		if (wasLong)
		{
			return;   // 长按已经在读条结束时结算过了
		}

		RunAction(_holdButton == MouseButton.Left ? AbilityCardAction.ToggleActive : AbilityCardAction.Upgrade);
	}

	/// <summary>跑一次操作：判定交给宿主，**本元素按结果决定动画与音效**。</summary>
	public void RunAction(AbilityCardAction action)
	{
		// ⚠️ 占位卡也走这里：宿主对它返回 <see cref="AbilityCardResult.NoCard"/> →
		// 落到下面的失败分支（震动 + 失败音效），所以"不可解锁但**有反馈**"是**同一条代码路径**，
		// 不需要为占位卡写第二套交互。
		AbilityCardResult result = Host?.Request(this, action) ?? AbilityCardResult.NoCard;
		LastResult = result;
		if (result == AbilityCardResult.Ok)
		{
			SuccessCount++;
			AudioManager.PlayUi(action switch
			{
				AbilityCardAction.Unlock => AbilityAudioIds.Unlock,
				AbilityCardAction.Upgrade => AbilityAudioIds.Upgrade,
				AbilityCardAction.ResetUpgrades => AbilityAudioIds.Reset,
				_ => AbilityAudioIds.Click,
			});

			switch (action)
			{
				case AbilityCardAction.Unlock:
					PlayUnlockEffect();
					break;
				case AbilityCardAction.Upgrade:
					PlayUpgradeEffect();
					break;
				case AbilityCardAction.ResetUpgrades:
					PlayResetEffect();
					break;
				default:
					PlayActivatePulse();
					break;
			}

			return;
		}

		// 失败：震动 + 失败音效（需求："失效/失败的交互都震动对应元素"）。
		FailCount++;
		AudioManager.PlayUi(AbilityAudioIds.Fail);
		PlayFailShake();
	}

	// ───────────────────────── 缓动表现（全走 Tween）─────────────────────────

	/// <summary>解锁视觉：**略微放大 + 闪一下**（需求给的三种里挑了这两种；都是缓动）。</summary>
	public void PlayUnlockEffect()
	{
		KillTweens();
		Scale = Vector2.One;
		Modulate = new Color(1.9f, 1.9f, 1.9f, 1f);

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(this, "scale", Vector2.One * 1.16f, 0.16)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(this, "modulate", Colors.White, 0.34)
			.SetTrans(Tween.TransitionType.Sine);

		Tween back = CreateTween();
		back.TweenInterval(0.16);
		back.TweenProperty(this, "scale", Vector2.One, 0.22)
			.SetTrans(Tween.TransitionType.Back)
			.SetEase(Tween.EaseType.Out);
	}

	/// <summary>升级视觉：小一档的脉冲（不抢解锁那一下）。</summary>
	public void PlayUpgradeEffect()
	{
		KillTweens();
		Scale = Vector2.One;
		Modulate = new Color(1.45f, 1.45f, 1.45f, 1f);

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(this, "modulate", Colors.White, 0.24);
		tween.TweenProperty(this, "scale", Vector2.One * 1.07f, 0.12)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);

		Tween back = CreateTween();
		back.TweenInterval(0.12);
		back.TweenProperty(this, "scale", Vector2.One, 0.18).SetTrans(Tween.TransitionType.Sine);
	}

	/// <summary>重置视觉：先缩一下再弹回（"东西被拿下去又装回来"）。</summary>
	public void PlayResetEffect()
	{
		KillTweens();
		Scale = Vector2.One;
		Modulate = new Color(0.8f, 0.85f, 1f, 1f);

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(this, "modulate", Colors.White, 0.26);
		tween.TweenProperty(this, "scale", Vector2.One * 0.93f, 0.12).SetTrans(Tween.TransitionType.Sine);

		Tween back = CreateTween();
		back.TweenInterval(0.12);
		back.TweenProperty(this, "scale", Vector2.One, 0.2).SetTrans(Tween.TransitionType.Back);
	}

	/// <summary>激活 / 关闭：轻轻弹一下（不加额外音效文件，点它用的是 <c>ui_click</c>）。</summary>
	public void PlayActivatePulse()
	{
		KillTweens();
		Scale = Vector2.One;
		Tween tween = CreateTween();
		tween.TweenProperty(this, "scale", Vector2.One * 0.96f, 0.07).SetTrans(Tween.TransitionType.Sine);
		tween.TweenProperty(this, "scale", Vector2.One, 0.14).SetTrans(Tween.TransitionType.Back);
	}

	/// <summary>失败：**震动元素**（绕中心来回摆） + 一点红闪。**所有失败路径共用它**。</summary>
	public void PlayFailShake()
	{
		if (_shakeTween != null && _shakeTween.IsValid())
		{
			_shakeTween.Kill();
		}

		UpdatePivot();
		_flashTween?.Kill();
		Modulate = new Color(1.5f, 0.7f, 0.7f, 1f);
		Tween flash = CreateTween();
		flash.TweenProperty(this, "modulate", Colors.White, ShakeSeconds);
		_flashTween = flash;

		Tween shake = CreateTween();
		_shakeTween = shake;
		float step = ShakeSeconds / 6f;
		for (int i = 0; i < 6; i++)
		{
			float angle = (i % 2 == 0 ? 1f : -1f) * ShakeRadians * (1f - (i * 0.15f));
			shake.TweenProperty(this, "rotation", angle, step).SetTrans(Tween.TransitionType.Sine);
		}

		shake.TweenProperty(this, "rotation", 0f, step);
	}

	/// <summary>激活时的呼吸光圈（循环缓动；离开激活态就停）。</summary>
	private void StartGlowPulse()
	{
		if (_glowTween != null && _glowTween.IsValid())
		{
			return;
		}

		if (_glow == null)
		{
			return;
		}

		_glowTween = CreateTween().SetLoops();
		_glowTween.TweenProperty(_glow, "modulate:a", 0.55f, 0.9).SetTrans(Tween.TransitionType.Sine);
		_glowTween.TweenProperty(_glow, "modulate:a", 1.0f, 0.9).SetTrans(Tween.TransitionType.Sine);
	}

	private void StopGlowPulse()
	{
		if (_glowTween != null && _glowTween.IsValid())
		{
			_glowTween.Kill();
		}

		_glowTween = null;
	}

	private void KillTweens()
	{
		_flashTween?.Kill();
		_flashTween = null;
	}

	/// <summary>探针/拍摄台用：把四种交互各跑一次（**走的是同一条 <see cref="RunAction"/>**）。</summary>
	public AbilityCardResult DebugRunAction(AbilityCardAction action)
	{
		RunAction(action);
		return LastResult;
	}

	/// <summary>
	/// 探针 / 拍摄台用：用**代码**开始一次读条（不依赖真的按住鼠标，但读条与结算走的是同一条
	/// <c>_Process</c> → <see cref="RunAction"/> 路径，所以拍到的进度条与解锁动画都是真表现）。
	/// </summary>
	public void DebugBeginCharge(bool leftButton)
	{
		BeginHold(leftButton ? MouseButton.Left : MouseButton.Right);
		_syntheticHold = true;
	}

	/// <summary>探针用：结束这次读条（等价松手；长按已经在读条结束时结算过就什么都不做）。</summary>
	public void DebugReleaseCharge()
	{
		_syntheticHold = false;
		if (_holding)
		{
			FinishHold();
		}
	}

	/// <summary>
	/// 探针用的一行状态（GDScript 直接 <c>call("DebugState")</c> —— 返回 string 是 Variant 兼容的，
	/// 见 architecture.md 第 6 条：自定义 struct / 泛型方法不能给脚本调）。
	/// </summary>
	public string DebugState()
		=> $"{Card?.Id ?? "（占位卡）"} Lv{Level} {StateName} 边框#{BorderColor.ToHtml(false)} 光圈={HasGlow}"
			+ $" 读条={IsCharging}({ChargeProgress:0.00}) 成功={SuccessCount} 失败={FailCount} 末次={LastResult}";
}

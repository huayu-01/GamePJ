using System.Collections.Generic;
using ActRoguelike.AbilityCards;
using ActRoguelike.Audio;
using ActRoguelike.Config;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 能力卡画布：只铺真实卡，左到右换行，每页最多六列三行。
///
/// <list type="bullet">
/// <item>卡面宽高比固定，宽度由横向空间与三行高度共同决定；卡少时保持尺寸，不拉伸铺满。</item>
/// <item>新需求取代旧占位卡：空位留白，超过当前页面容量就翻页，窗口缩小时列数可减少。</item>
/// <item><b>元素是基类组件</b>：每一格都是 <see cref="AbilityCardTile"/>（三状态 + 四种交互 + 读条 + 震动 + 音效），
///   本界面只做三件事：建格子、<b>判定规则</b>（<see cref="IAbilityCardHost"/>）、以及额度栏 / 图例 / 文案。
///   换一个界面（比如以后要塞进基地界面）只要复用那个 .tscn + 实现一个宿主，交互一行都不用重写。</item>
/// <item><b>数据从哪来</b>：`ConfigIndex.tres → AbilityCards` 数组（顺序就是矩阵顺序），
///   加一张卡 = 加一行 .tres + 挂进索引三处；页面数量自动增加。</item>
/// </list>
///
/// 状态存盘与额度记账都在 <see cref="AbilityStore"/>；激活的效果在 <c>RunController.ApplyBalance</c> 里
/// 由 <see cref="AbilityEffects"/> 套到玩家身上（**下局开局生效**）。
/// </summary>
public partial class AbilityMatrixScreen : OverlayScreen, IAbilityCardHost
{
	/// <summary>默认快捷键（与 <c>docs/ability-cards.md</c>、基地界面的提示栏保持一致）。</summary>
	public const Key DefaultToggleKey = Key.K;

	/// <summary>打开 / 关闭本界面的按键。</summary>
	[Export] public Key ToggleKey { get; set; } = DefaultToggleKey;

	/// <summary>扩增额度上限的按键。</summary>
	[Export] public Key QuotaKey { get; set; } = Key.M;

	/// <summary>矩阵列数（默认 6 —— 需求点名过的 3×6 的横向版本，矩阵铺满时每格够大）。</summary>
	[Export(PropertyHint.Range, "1,6,1")] public int Columns { get; set; } = 6;

	/// <summary>每页行数上限；超出容量的卡进入下一页。</summary>
	[Export(PropertyHint.Range, "1,3,1")] public int Rows { get; set; } = 3;
	[Export] public float CardMinWidth { get; set; } = 128f;
	[Export] public float CardMaxWidth { get; set; } = 200f;
	[Export] public float CardSeparation { get; set; } = 28f;
	[Export] public float CardAspect { get; set; } = 0.625f;

	// ── 左侧额度进度条的配色 ──
	// 用户反馈：深色主题下**看不出已占用部分**（默认 ProgressBar 的填充与暗底几乎同色）。
	// 两个 StyleBox 在场景里（`theme_override_styles/fill` 与 `/background`），填充底色写 **白**，
	// 实际颜色由下面三个值在运行期写进 `StyleBoxFlat.BgColor`：
	// 正常 / 快满 / 满。三个颜色与两个阈值都是**场景上的导出值**（改配色不用改代码）。

	/// <summary>占用比例达到这个值 → 进度条填充换成 <see cref="QuotaWarnColor"/>。</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float QuotaWarnRatio { get; set; } = 0.8f;

	/// <summary>正常（没快满）时的填充色。</summary>
	[Export] public Color QuotaOkColor { get; set; } = new Color(0.36f, 0.72f, 0.95f);

	/// <summary>快满（≥ <see cref="QuotaWarnRatio"/>）时的填充色。</summary>
	[Export] public Color QuotaWarnColor { get; set; } = new Color(1f, 0.78f, 0.28f);

	/// <summary>装不下更多（占用 ≥ 额度上限）时的填充色。</summary>
	[Export] public Color QuotaFullColor { get; set; } = new Color(1f, 0.36f, 0.32f);

	protected override Control Root => _root;

	private Control _root = null!;
	private Label _shards = null!;
	private Label _quota = null!;
	private Button _quotaUpgrade = null!;
	private GridContainer _matrix = null!;
	private HBoxContainer _legend = null!;
	private Label _footer = null!;
	private Button _previous = null!;
	private Button _next = null!;
	private Label _pageLabel = null!;

	// 左侧窄额度条；资源和扩增操作统一留在顶栏。
	private ProgressBar? _quotaBar;
	private Label? _quotaText;

	private readonly List<AbilityCardTile> _tiles = new();

	private PackedScene? _tileScene;
	private bool _toggleHeld;
	private bool _quotaHeld;
	private int _builtCardCount = -1;
	private int _builtRows = -1;
	private int _builtPage = -1;
	private int _builtColumns = -1;
	private int _pageIndex;
	private int _pageRowLimit = 3;
	private float _cardWidth = 144f;
	private readonly List<AbilityCardConfig> _debugCards = new();
	private bool _debugCardList;
	private int _rows = 3;
	private bool _debugSample;

	/// <summary>本页实际生成的卡牌节点数，不含任何空位。</summary>
	public int SlotCount => _matrix.GetChildCount();

	/// <summary>其中真正的卡有几格。</summary>
	public int TileCount => _tiles.Count;

	public int PageIndex => _pageIndex;
	public int PageCapacity => Mathf.Clamp(Columns, 1, 6) * _pageRowLimit;
	public int PageCount => PagesFor(CardRowCount, PageCapacity);

	/// <summary>本页实际行数，空页为零。</summary>
	public int RowCount => _rows;

	/// <summary>按顺序取第 <paramref name="index"/> 个元素（自检 / 探针用）。</summary>
	public AbilityCardTile? TileAt(int index) => index >= 0 && index < _tiles.Count ? _tiles[index] : null;

	/// <summary>本界面读到的卡表行数（探针用）。</summary>
	public int CardRowCount => CardRows.Count;
	private IReadOnlyList<AbilityCardConfig> CardRows => _debugCardList ? _debugCards : AbilityStore.Rows;

	public override void _Ready()
	{
		_root = GetNode<Control>("Root");
		InitializeOverlay();
		_shards = GetNode<Label>("Root/Header/Shards");
		_quota = GetNode<Label>("Root/Header/Quota");
		_quotaUpgrade = GetNode<Button>("Root/Header/QuotaUpgrade");
		_matrix = GetNode<GridContainer>("Root/Panel/HBox/Matrix");
		_legend = GetNode<HBoxContainer>("Root/Legend");
		_footer = GetNode<Label>("Root/Footer");
		_quotaBar = GetNode<ProgressBar>("Root/Panel/HBox/QuotaRail/QuotaBar");
		_quotaText = GetNode<Label>("Root/Panel/HBox/QuotaRail/QuotaText");
		_previous = GetNode<Button>("Root/Pagination/Previous");
		_next = GetNode<Button>("Root/Pagination/Next");
		_pageLabel = GetNode<Label>("Root/Pagination/Page");
		_previous.Pressed += () => ChangePage(-1);
		_next.Pressed += () => ChangePage(1);

		_matrix.Columns = Columns;
		// ⚠️ 只在这里订阅一次：`RebuildIfNeeded` 里原来又订阅了一遍 → 每次改尺寸回调会被调两次。
		// ⚠️ 订阅的是 **Root**（跟着视口走）而不是 Matrix：矩阵自己的尺寸被最小尺寸顶住，
		// 窗口变窄时它**根本不会发出 `Resized`** → 列数会卡在拉宽后的值（实测：拉宽到 19 列，
		// 缩回 16:9 之后仍然是 19 列、矩阵区还报着 2342）。Root 是锚满视口的普通 Control，
		// 视口一变它一定变，只订阅 Root，避免矩阵重排引起重复重建。
		_root.Resized += OnMatrixResized;
		_quotaUpgrade.Pressed += BuyQuota;
		BuildLegend();
		CallDeferred(nameof(OnMatrixResized));
	}

	public override void _Process(double delta)
	{
		bool toggle = Input.IsPhysicalKeyPressed(ToggleKey);
		if (toggle && !_toggleHeld)
		{
			Toggle();
		}

		_toggleHeld = toggle;

		if (!IsOpen)
		{
			return;
		}

		bool quota = Input.IsPhysicalKeyPressed(QuotaKey);
		if (quota && !_quotaHeld)
		{
			BuyQuota();
		}

		_quotaHeld = quota;
	}

	private void Toggle()
	{
		if (IsOpen)
		{
			Close();
		}
		else
		{
			Open();
		}
	}

	/// <summary>打开界面（按键或 <c>UiService.OpenAbilityMatrix()</c> 都走这里）。</summary>
	public void Open()
	{
		if (IsOpen)
		{
			return;
		}

		// 提示栏照 `docs/ui.md` §10.3 写：鼠标左右键的两种用法 + 扩增额度 + 关闭。
		SetKeyHints(
			KeyHint.Mouse(MouseButton.Left, "激活 / 长按解锁"),
			KeyHint.Mouse(MouseButton.Right, "升级 / 长按重置升级"),
			KeyHint.Of(QuotaKey, "扩增额度"),
			KeyHint.Text("鼠标悬停", "看效果与价格"),
			KeyHint.Text("PgUp / PgDn", "翻页"),
			(Key.Escape, "关闭"));

		BeginOpen();
		OnMatrixResized();
		RebuildIfNeeded();
		RefreshAll();
	}

	public override void Close()
	{
		if (IsOpen && !InputBlocked)
		{
			BeginClose();
		}
	}

	// ───────────────────────── 判定（IAbilityCardHost：规则只在这一处）─────────────────────────

	/// <summary>
	/// 元素请求执行一次操作 → 交给 <see cref="AbilityStore"/> → 立刻把整屏刷成新状态。
	/// 元素只拿结果决定"播哪个动画 + 哪个音"，界面不重复实现判定。
	/// </summary>
	public AbilityCardResult Request(AbilityCardTile tile, AbilityCardAction action)
	{
		if (tile.Card == null)
		{
			return AbilityCardResult.NoCard;
		}

		// 摆过"示例状态"又不在沙箱里时只演视觉、不动真存档（拍摄台自己会先开沙箱）。
		if (_debugSample && !AbilityStore.SandboxActive)
		{
			return AbilityCardResult.NoCard;
		}

		AbilityCardResult result = action switch
		{
			AbilityCardAction.Unlock => AbilityStore.TryUnlock(tile.Card),
			AbilityCardAction.ToggleActive => AbilityStore.TryToggleActive(tile.Card),
			AbilityCardAction.Upgrade => AbilityStore.TryUpgrade(tile.Card),
			AbilityCardAction.ResetUpgrades => AbilityStore.TryResetUpgrades(tile.Card),
			_ => AbilityCardResult.NoCard,
		};

		RefreshAll();
		return result;
	}

	/// <summary>扩增额度上限（需求："额度也是可以被局外资源升级扩增的"）。失败同样震动 + 失败音。</summary>
	public void BuyQuota()
	{
		if (_debugSample && !AbilityStore.SandboxActive)
		{
			return;
		}

		AbilityCardResult result = AbilityStore.TryBuyQuota();
		if (result == AbilityCardResult.Ok)
		{
			AudioManager.PlayUi(AbilityAudioIds.Upgrade);
			RefreshAll();
			return;
		}

		AudioManager.PlayUi(AbilityAudioIds.Fail);
		ShakeControl(_quotaUpgrade);
		RefreshAll();
	}

	// ───────────────────────── 矩阵搭建与刷新 ─────────────────────────

	/// <summary>视口变化时重算页容量，保持原先首张可见卡仍在当前页。</summary>
	private void OnMatrixResized()
	{
		if (_matrix == null)
		{
			return;
		}

		// 从视口推导预算，避免 GridContainer 的最小尺寸反过来顶大窗口。
		int firstVisible = _pageIndex * PageCapacity;
		Columns = ColumnsFor(AvailableMatrixWidth(), CardMinWidth, CardSeparation);
		Rows = Mathf.Clamp(Rows, 1, 3);
		float width = (AvailableMatrixWidth() - CardSeparation * (Columns - 1)) / Columns;
		var panel = (Control)_matrix.GetParent().GetParent();
		float height = _root.Size.Y + panel.OffsetBottom - panel.OffsetTop;
		_pageRowLimit = Mathf.Clamp(Mathf.FloorToInt((height + CardSeparation) / (CardMinWidth / CardAspect + CardSeparation)), 1, Rows);
		_pageIndex = firstVisible / PageCapacity;
		float heightWidth = (height - CardSeparation * (_pageRowLimit - 1)) / _pageRowLimit * CardAspect;
		_cardWidth = Mathf.Max(CardMinWidth, Mathf.Min(CardMaxWidth, Mathf.Min(width, heightWidth)));
		_matrix.AddThemeConstantOverride("h_separation", Mathf.RoundToInt(CardSeparation));
		_matrix.AddThemeConstantOverride("v_separation", Mathf.RoundToInt(CardSeparation));
		RebuildIfNeeded();
		foreach (Node child in _matrix.GetChildren())
		{
			((Control)child).CustomMinimumSize = new Vector2(_cardWidth, _cardWidth / CardAspect);
		}
		RefreshAll();
	}

	/// <summary>
	/// 本次布局里**矩阵区真正能用的宽度**（P6b 修的第二个自适应缺陷）。
	///
	/// <para>⚠️ <b>不能直接读 <c>_matrix.Size.X</c></b>：<see cref="GridContainer"/> 的尺寸被它自己的
	/// 最小尺寸顶住，而最小尺寸 = 当前列数 × 卡宽 → **列数只会涨、不会跌**。实测（2026-10-16）：
	/// 1920 宽 → 11 列；把窗口拉成 12:5（视口 2592）→ 18 列；再缩回 16:9，视口确实回到 1920，
	/// 但矩阵区还是报 2218（= 18 列的最小宽），于是仍然 18 列 —— 窗口变窄后列数**卡住不下来**，
	/// 矩阵还会溢出右栏。</para>
	///
	/// <para>改法：从**视口**算可用宽。`Root` 是锚满视口的普通 <see cref="Control"/>，
	/// 它的尺寸只由锚点决定、**不受子节点最小尺寸影响**；Panel 的左右偏移与 HBox 的间隔都从场景读，
	/// 右栏按它自己的最小尺寸扣掉。这样"宽 → 列多、窄 → 列少"两个方向都成立。</para>
	/// </summary>
	private float AvailableMatrixWidth()
	{
		if (_matrix.GetParent() is not BoxContainer box || box.GetParent() is not Control panel)
		{
			return _matrix.Size.X;   // 场景结构被改过：退回旧行为，不抛异常
		}

		// 不读 panel.Size.X（容器会被最小尺寸顶大），用它相对 Root 的锚点偏移还原"本应分配到的宽度"：
		// 锚满左右时 `宽 = 父宽 + offset_right − offset_left`。
		float panelWidth = _root.Size.X - panel.OffsetLeft + panel.OffsetRight;
		float others = 0f;
		foreach (Node child in box.GetChildren())
		{
			if (child != _matrix && child is Control control)
			{
				others += control.GetCombinedMinimumSize().X;
			}
		}

		float separation = box.GetThemeConstant("separation") * Mathf.Max(0, box.GetChildCount() - 1);
		return Mathf.Max(CardMinWidth, panelWidth - others - separation);
	}

	public static int ColumnsFor(float availWidth, float cardWidth, float separation)
	{
		float unit = Mathf.Max(1f, cardWidth + separation);
		return Mathf.Clamp(Mathf.FloorToInt((availWidth + separation) / unit), 1, 6);
	}

	public static int RowsFor(int cardCount, int columns, int maxRows)
		=> Mathf.Min(Mathf.Clamp(maxRows, 1, 3), Mathf.CeilToInt(cardCount / (float)Mathf.Max(1, columns)));

	public static int PagesFor(int cardCount, int capacity)
		=> Mathf.Max(1, Mathf.CeilToInt(cardCount / (float)Mathf.Max(1, capacity)));

	/// <summary>按钮与键盘共用同一翻页入口；首尾夹紧，卡牌状态由 AbilityStore 保留。</summary>
	public void ChangePage(int direction)
	{
		int page = Mathf.Clamp(_pageIndex + direction, 0, PageCount - 1);
		if (page == _pageIndex)
		{
			return;
		}
		_pageIndex = page;
		RebuildIfNeeded();
		RefreshAll();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsOpen || InputBlocked || @event is not InputEventKey { Pressed: true, Echo: false } key)
		{
			return;
		}
		if (key.Keycode is Key.Pageup or Key.Pagedown)
		{
			ChangePage(key.Keycode == Key.Pageup ? -1 : 1);
			GetViewport().SetInputAsHandled();
		}
	}

	/// <summary>
	/// 只有卡数、行列或页码变化时重建（平时只 <see cref="RefreshAll"/> 刷状态），
	/// 所以点卡片不会重建节点、不会打断缓动动画。
	///
	/// <para>只遍历当前页真实行；卡与卡保留间距，其余画布留白。</para>
	/// </summary>
	private void RebuildIfNeeded()
	{
		Columns = Mathf.Clamp(Columns, 1, 6);
		int cardCount = CardRowCount;
		_pageIndex = Mathf.Clamp(_pageIndex, 0, PageCount - 1);
		int start = _pageIndex * PageCapacity;
		int visible = Mathf.Min(PageCapacity, cardCount - start);
		int rows = RowsFor(visible, Columns, Rows);
		if (_builtCardCount == cardCount && _builtRows == rows && _builtPage == _pageIndex && _builtColumns == Columns)
		{
			return;
		}

		_builtCardCount = cardCount;
		_builtRows = rows;
		_builtPage = _pageIndex;
		_builtColumns = Columns;
		_rows = rows;
		_tiles.Clear();
		foreach (Node child in _matrix.GetChildren())
		{
			_matrix.RemoveChild(child);
			child.QueueFree();
		}

		_tileScene ??= GD.Load<PackedScene>("res://Scenes/UI/AbilityCardTile.tscn");
		_matrix.Columns = Columns;
		for (int i = 0; i < visible; i++)
		{
			if (_tileScene?.Instantiate() is not AbilityCardTile tile)
			{
				// 预制体读不到：这一格空着也不抛异常（与"表读不到就用自身默认值"同一条纪律）。
				break;
			}

			tile.Bind(CardRows[start + i], this);
			_tiles.Add(tile);

			// 卡牌感的关键：用 AspectRatioContainer 把每格锁成 2.5:4（用户要求 2.5:4 或 3:4），
			// 这样无论矩阵分到多大，卡面永远是竖长的（不会因为屏幕是 16:9 就变横）。
			var holder = new AspectRatioContainer
			{
				Ratio = CardAspect,
				CustomMinimumSize = new Vector2(_cardWidth, _cardWidth / CardAspect),
				SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
				SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			holder.AddChild(tile);
			_matrix.AddChild(holder);
		}
	}

	/// <summary>一整屏刷成当前状态：每格的三状态视觉 + 碎片 / 额度栏 + 底部说明。</summary>
	public void RefreshAll()
	{
		AbilityStore.Load();
		foreach (AbilityCardTile tile in _tiles)
		{
			tile.RefreshVisual();
		}

		int used = AbilityStore.QuotaUsed;
		int cap = AbilityStore.QuotaCap;
		_shards.Text = $"碎片：{MetaProgress.Shards}";
		_quota.Text = $"额度：{used} / {cap}　（扩增 Lv{AbilityStore.QuotaLevel}，剩余 {AbilityStore.QuotaLeft}）";
		_quotaUpgrade.Text = $"扩增额度 +1（费用 {AbilityStore.QuotaUpgradeCost}）";
		_quotaUpgrade.Disabled = AbilityStore.QuotaRow == null;

		// 左侧竖进度条：填的是"已占用 / 总额度"。
		// ⚠️ P6b：填充色**由下面的 <see cref="ApplyQuotaBarColor"/> 决定**（正常 / 快满 / 满三档）。
		if (_quotaBar != null)
		{
			_quotaBar.MaxValue = Mathf.Max(1, cap);
			_quotaBar.Value = used;
			ApplyQuotaBarColor(used, cap);
		}
		if (_quotaText != null)
		{
			_quotaText.Text = $"{used}/{cap}";
		}
		_previous.Disabled = _pageIndex == 0;
		_next.Disabled = _pageIndex >= PageCount - 1;
		_pageLabel.Text = $"{_pageIndex + 1:00} / {PageCount:00}";

		_footer.Text =
			$"解锁 {AbilityStore.UnlockedCount} · 激活 {AbilityStore.ActiveCount} · 共 {CardRowCount} 张　｜　"
			+ "激活的能力在下局开局生效";
	}

	/// <summary>
	/// 额度条填充色（P6b）：按"已占用 / 上限"分三档写进填充 <c>StyleBoxFlat.BgColor</c>
	/// —— 正常蓝 / ≥<see cref="QuotaWarnRatio"/> 黄 / 满红。
	///
	/// <para>为什么改 <c>BgColor</c> 而不是 <c>Modulate</c>：Modulate 会连**背景槽**一起染色，
	/// 暗底会被染成暗黄。改样式盒只动填充那一层。</para>
	/// <para>⚠️ 只在**确实是我们自己的覆盖**（场景里的 <c>theme_override_styles/fill</c>）时才改：
	/// 否则拿到的是主题默认的那一份共享 <c>StyleBoxFlat</c>，改它会波及其它所有 ProgressBar。</para>
	/// </summary>
	private void ApplyQuotaBarColor(int used, int cap)
	{
		if (_quotaBar == null || !IsInstanceValid(_quotaBar))
		{
			return;
		}

		float ratio = used / (float)Mathf.Max(1, cap);
		Color color = ratio >= 1f ? QuotaFullColor : ratio >= QuotaWarnRatio ? QuotaWarnColor : QuotaOkColor;
		_quotaBarFillColor = color;

		if (!_quotaBar.HasThemeStyleboxOverride("fill") || _quotaBar.GetThemeStylebox("fill") is not StyleBoxFlat fill)
		{
			return;   // 场景里没配样式盒（例如被人删了）：不碰共享资源，保持默认外观
		}

		fill.BgColor = color;
	}

	/// <summary>额度条当前填充色（自检 / 探针用）。</summary>
	public Color QuotaBarFillColor => _quotaBarFillColor;

	private Color _quotaBarFillColor = new Color(1f, 1f, 1f);

	/// <summary>等级色图例（"边框颜色表示等级：灰0 &gt; 白1 &gt; 绿2 &gt; 蓝3 &gt; 紫4 &gt; 红5"）。</summary>
	private void BuildLegend()
	{
		foreach (Node child in _legend.GetChildren())
		{
			_legend.RemoveChild(child);
			child.QueueFree();
		}

		var title = new Label
		{
			Text = "边框 / 光圈颜色 = 等级：",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			VerticalAlignment = VerticalAlignment.Center,
		};
		title.ThemeTypeVariation = "UiCaption";
		_legend.AddChild(title);

		for (int level = 0; level <= 5; level++)
		{
			var item = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			item.AddThemeConstantOverride("separation", 4);

			var swatch = new ColorRect
			{
				Color = AbilityCardTile.LevelColor(level),
				CustomMinimumSize = new Vector2(14, 14),
				MouseFilter = Control.MouseFilterEnum.Ignore,
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			};

			var text = new Label
			{
				Text = AbilityCardTile.LevelName(level),
				MouseFilter = Control.MouseFilterEnum.Ignore,
				VerticalAlignment = VerticalAlignment.Center,
			};
			text.ThemeTypeVariation = "UiCaption";

			item.AddChild(swatch);
			item.AddChild(text);
			_legend.AddChild(item);
		}
	}

	/// <summary>震动一个普通控件（额度按钮用；元素自己的震动在 <see cref="AbilityCardTile.PlayFailShake"/>）。</summary>
	private static void ShakeControl(Control control)
	{
		control.PivotOffset = control.Size * 0.5f;
		control.Rotation = 0f;
		Tween tween = control.CreateTween();
		for (int i = 0; i < 6; i++)
		{
			tween.TweenProperty(control, "rotation", (i % 2 == 0 ? 1f : -1f) * 0.05f, 0.045f);
		}

		tween.TweenProperty(control, "rotation", 0f, 0.05f);
	}

	// ───────────────────────── 自检 / 拍摄台入口 ─────────────────────────

	/// <summary>
	/// 拍摄台用：把整屏摆成"看得出三状态 + 六档等级色"的样子（**只在内存里改，不写存档**）。
	/// 前三张激活，第四张未解锁；以后追加的卡可展示更多等级色。
	/// </summary>
	public void DebugShowSampleStates()
	{
		_debugSample = true;
		RebuildIfNeeded();
		int used = 0;
		for (int i = 0; i < _tiles.Count; i++)
		{
			AbilityCardConfig card = _tiles[i].Card!;
			int level = i switch
			{
				0 => 1,
				1 => 2,
				2 => 3,
				3 => 0,
				4 => 5,
				5 => 1,
				6 => 0,
				_ => 2,
			};
			bool active = i < 3;
			if (active)
			{
				used += card.QuotaCost;
			}

			AbilityStore.DebugSetState(card.Id, level, active);
		}

		// 额度上限要够装下这 4 张激活卡，并留 4 点余量 —— 否则拍摄台里"再激活一张"必然额度不足，
		// 拍不到"激活后亮起光圈"的样子（额度不足本身在交互自检里单独验）。
		AbilityStore.DebugSetQuotaLevel(
			Mathf.Max(0, Mathf.CeilToInt((used - AbilityStore.QuotaBase) / Mathf.Max(0.01f, AbilityStore.QuotaPerLevel)) + 4));
		RefreshAll();
	}

	/// <summary>拍摄台用：把四种交互逐条跑一遍并返回结果文本（证据，不是"点了没报错"）。</summary>
	public string DebugRunInteractions()
	{
		_debugSample = false;
		if (_tiles.Count == 0)
		{
			return "能力矩阵：没有卡（表没读到）";
		}

		var lines = new List<string>();
		AbilityCardTile first = _tiles[0];
		int before = AbilityStore.LevelOf(first.Card!.Id);
		AbilityStore.DebugSetState(first.Card.Id, 1, false);

		AbilityCardResult unlockAgain = first.DebugRunAction(AbilityCardAction.Unlock);
		lines.Add($"重复长按左键解锁 → {unlockAgain}（期望 AlreadyUnlocked → 震动+失败音）");

		AbilityCardResult upgrade = first.DebugRunAction(AbilityCardAction.Upgrade);
		lines.Add($"右键升级 → {upgrade}（期望 Ok）Lv{AbilityStore.LevelOf(first.Card.Id)}");

		AbilityCardResult toggle = first.DebugRunAction(AbilityCardAction.ToggleActive);
		lines.Add($"左键激活 → {toggle}（期望 Ok）激活={AbilityStore.IsActive(first.Card.Id)} 额度 {AbilityStore.QuotaUsed}/{AbilityStore.QuotaCap}");

		AbilityCardResult reset = first.DebugRunAction(AbilityCardAction.ResetUpgrades);
		lines.Add($"长按右键重置 → {reset}（期望 Ok）Lv{AbilityStore.LevelOf(first.Card.Id)}");

		lines.Add($"元素计数：成功 {first.SuccessCount} 次 / 失败（震动）{first.FailCount} 次；"
			+ $"三状态 [{string.Join(" / ", _tiles.ConvertAll(t => $"{t.Card!.Id}:{t.StateName}"))}]");

		AbilityStore.DebugSetState(first.Card.Id, before, false);
		RefreshAll();
		return string.Join(" ｜ ", lines);
	}

	// ── 下面几个是给 GDScript 拍摄台/探针用的薄封装 ──
	// GDScript 调不到 C# 的**静态类**（`AbilityStore` 不在场景树上），所以每个要用的入口都在这里开一个实例方法。
	// 返回值一律是 Variant 兼容类型（string / int / bool / Node），见 architecture.md 第 6 条。

	/// <summary>探针用：开沙箱（真的走解锁/升级/激活链路，但不花真实碎片、不落盘）。</summary>
	public void DebugBeginSandbox() => AbilityStore.DebugBeginSandbox();

	/// <summary>探针用：开"花钱是内存钱包、但**允许落盘**"的沙箱 —— 端到端验证
	/// "激活的卡在下局开局生效"时用它（`user://ability_cards.cfg` 会被真的写出来，但碎片不动）。</summary>
	public void DebugBeginSandboxWithSave() => AbilityStore.DebugBeginSandbox(1000, allowSave: true);

	/// <summary>探针用：关沙箱。</summary>
	public void DebugEndSandbox()
	{
		_debugCardList = false;
		_debugCards.Clear();
		_debugSample = false;
		_builtCardCount = -1;
		AbilityStore.DebugEndSandbox();
		OnMatrixResized();
	}

	/// <summary>沙箱内生成分页样本，复制真实行但使用独立 Id；绝不改索引与玩家存档。</summary>
	public void DebugSetCardCount(int count)
	{
		if (!AbilityStore.SandboxActive || AbilityStore.Rows.Count == 0)
		{
			return;
		}
		_debugCards.Clear();
		_debugCardList = true;
		for (int i = 0; i < Mathf.Clamp(count, 0, 100); i++)
		{
			var card = (AbilityCardConfig)AbilityStore.Rows[i % AbilityStore.Rows.Count].Duplicate();
			card.Id = $"debug_page_card_{i:00}";
			card.DisplayName = $"{card.DisplayName} {i + 1:00}";
			_debugCards.Add(card);
		}
		_pageIndex = 0;
		_builtCardCount = -1;
		OnMatrixResized();
	}

	/// <summary>探针用：一行状态（碎片 / 额度 / 每张卡的等级与激活）。</summary>
	public string DebugSummary() => AbilityStore.Summary();

	/// <summary>探针用：本页卡牌状态、页容量与实际像素尺寸。</summary>
	public string DebugMatrixReport()
	{
		var lines = new List<string>();
		foreach (AbilityCardTile tile in _tiles)
		{
			lines.Add(tile.DebugState());
		}

		string size = _tiles.Count > 0
			? $"视口 {GetViewport().GetVisibleRect().Size.X:0}×{GetViewport().GetVisibleRect().Size.Y:0}"
				+ $" → 矩阵区 {_matrix.Size.X:0}×{_matrix.Size.Y:0} → 每格 {_tiles[0].Size.X:0}×{_tiles[0].Size.Y:0}px"
				+ "（宽高比固定，尺寸同时受可用宽高限制）"
			: $"视口 {GetViewport().GetVisibleRect().Size.X:0}×{GetViewport().GetVisibleRect().Size.Y:0}"
				+ $" → 矩阵区 {_matrix.Size.X:0}×{_matrix.Size.Y:0}（没有真卡）";

		return $"列 {Columns} × 行 {RowCount} → {SlotCount} 张真卡｜页 {_pageIndex + 1}/{PageCount} 容量 {PageCapacity}｜{size}"
			+ "｜" + string.Join(" ｜ ", lines);
	}

	/// <summary>
	/// 对边界卡数检查分页容量与六列三行上限，不改卡表或存档。
	/// </summary>
	public string DebugSlotPlan()
	{
		var lines = new List<string>();
		bool ok = true;
		foreach (int count in new[] { 0, 1, 8, 17, 18, 19, 24, 25, 37, 60 })
		{
			int pages = PagesFor(count, PageCapacity);
			int visible = Mathf.Min(count, PageCapacity);
			int rows = RowsFor(visible, Columns, Rows);
			bool fits = rows <= 3 && Columns <= 6 && pages * PageCapacity >= count;
			ok &= fits;
			lines.Add($"{count} 张 → {pages} 页 / 首屏 {visible} 张 / {rows} 行{(fits ? "✓" : "✗")}");
		}

		return "分页算式：" + string.Join(" ｜ ", lines) + $"　→ 全部满足「最多六列三行、空位留白」 = {ok}";
	}

	/// <summary>探针用：额度栏那一行的实际文案（证明界面上的数字来自 AbilityStore）。</summary>
	public string DebugHeaderText() => $"{_shards.Text} ／ {_quota.Text} ／ {_quotaUpgrade.Text}";

	// ── 额度条配色与"自适应列数"的取证入口（P6b）──

	/// <summary>
	/// 探针用：额度条的**实际**读数 —— 格数/阈值/填充样式盒的真颜色，以及当前矩阵区尺寸与列数。
	/// 判据不是"代码里写了这句话"，而是**样式盒上的 <c>BgColor</c> 真的被改掉了**。
	/// </summary>
	public string DebugQuotaBarReport()
	{
		if (_quotaBar == null || !IsInstanceValid(_quotaBar))
		{
			return "额度条：场景里没有 QuotaBar 节点";
		}

		bool overridden = _quotaBar.HasThemeStyleboxOverride("fill");
		var fill = overridden ? _quotaBar.GetThemeStylebox("fill") as StyleBoxFlat : null;
		string bg = _quotaBar.HasThemeStyleboxOverride("background")
			? (_quotaBar.GetThemeStylebox("background") as StyleBoxFlat)?.BgColor.ToHtml(false) ?? "非 StyleBoxFlat"
			: "（没配 background 样式盒）";
		return $"额度条：{_quotaBar.Value:0}/{_quotaBar.MaxValue:0} 三档色 蓝={QuotaOkColor.ToHtml(false)}/"
			+ $"黄={QuotaWarnColor.ToHtml(false)}/红={QuotaFullColor.ToHtml(false)}（快满阈值 {QuotaWarnRatio:0.##}）"
			+ $" ｜ 场景 override: fill={overridden} background={_quotaBar.HasThemeStyleboxOverride("background")}"
			+ $" ｜ fill.BgColor={(fill != null ? fill.BgColor.ToHtml(false) : "—")} background={bg}"
			+ $" ｜ 宽 {_quotaBar.Size.X:0}px 填充方向={_quotaBar.FillMode} 左贴卡区={_quotaBar.GlobalPosition.X < _matrix.GlobalPosition.X}"
			+ $" ｜ 列 {Columns} 行 {RowCount} 矩阵区 {_matrix.Size.X:0}×{_matrix.Size.Y:0}";
	}

	/// <summary>探针 / 拍摄台用：把额度占用摆成指定值（只改这一个控件的显示，不动存档）。</summary>
	public string DebugSetQuotaOccupancy(int used, int cap)
	{
		if (_quotaBar == null || !IsInstanceValid(_quotaBar))
		{
			return "额度条：场景里没有 QuotaBar 节点";
		}

		_quotaBar.MaxValue = Mathf.Max(1, cap);
		_quotaBar.Value = Mathf.Clamp(used, 0, Mathf.Max(1, cap));
		ApplyQuotaBarColor(used, cap);
		// 顶栏与条旁文本同步，避免拍摄时数字错位。
		if (_quotaText != null)
		{
			_quotaText.Text = $"{used}/{cap}";
		}

		if (_quota != null && IsInstanceValid(_quota))
		{
			_quota.Text = $"额度：{used} / {cap}　（扩增 Lv{AbilityStore.QuotaLevel}，剩余 {Mathf.Max(0, cap - used)}）";
		}

		return DebugQuotaBarReport();
	}

	/// <summary>
	/// 探针用：**自适应列数**的算术核对 —— 给定矩阵区宽度，列数应当是"铺得下几张就是几列"，
	/// 且每格的**宽高比恒定**（卡牌比例不变）。纯算式，不动真表、不动存档。
	/// </summary>
	public string DebugAdaptiveColumns()
	{
		var lines = new List<string>();
		bool ok = true;
		foreach (float width in new[] { 900f, 1200f, 1500f, 1900f, 2400f })
		{
			int cols = ColumnsFor(width, CardMinWidth, CardSeparation);
			// 每格分到的宽度 = (可用宽 - 列间间隔) / 列数；高度由 AspectRatioContainer 锁 0.625 定。
			float cell = (width - (CardSeparation * (cols - 1))) / cols;
			float height = cell / 0.625f;
			bool fits = cols >= 2 && cell >= CardMinWidth - 0.51f;
			ok &= fits;
			lines.Add($"{width:0}px → {cols} 列 · 每格 {cell:0}×{height:0}（比例 {(height > 0 ? cell / height : 0f):0.###}，"
				+ $"最小宽 {CardMinWidth:0}）{(fits ? "✓" : "✗")}");
		}

		return $"自适应列数（每格最小宽 {CardMinWidth:0} + 间隔 {CardSeparation:0}）：{string.Join(" ｜ ", lines)}"
			+ $"　→ 全部「铺得下 + 不小于最小宽」 = {ok}";
	}
}

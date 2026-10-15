using System.Collections.Generic;
using ActRoguelike.AbilityCards;
using ActRoguelike.Audio;
using ActRoguelike.Config;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// **能力矩阵界面**（需求 §二）：全屏铺开一整个矩阵，一格 = 一张能力卡。
///
/// <list type="bullet">
/// <item><b>为什么是全屏矩阵</b>：需求原话"像卡牌一样展开矩阵排序，比如 5×5 / 6×6 / 3×6 / 6×4 这样的全屏矩阵，
///   这样每个元素视觉上都比较大"。所以这里不写死行列：<see cref="Columns"/> / <see cref="Rows"/> 是导出属性，
///   默认 <b>6 × 3 = 18 格</b>。矩阵铺满可用区域，格子尺寸由 <c>GridContainer</c> 随屏幕/容器大小分配
///   （每一格都是 Expand，见 <c>AbilityCardTile.tscn</c>）—— 也就是"自适应大小"。</item>
/// <item><b>空位也有卡</b>（用户 A5 拍板："直接做满屏矩阵不翻页，空的位置也有卡牌占着，但是会因为没有设定解锁条件而处于一直无法解锁"）：
///   不足的格子放的是 <b>占位卡</b>（<see cref="AbilityCardTile.IsPlaceholder"/>，卡表里没有对应行）
///   —— 它**永远无法解锁 / 激活 / 升级**，但点它 / 长按它的**读条、震动、失败音效照旧**。
///   行数 = <c>max(Rows, ceil(卡数 / Columns))</c>，格数 = 列 × 行 → **永远是一个填满的矩形、不翻页**。</item>
/// <item><b>元素是基类组件</b>：每一格都是 <see cref="AbilityCardTile"/>（三状态 + 四种交互 + 读条 + 震动 + 音效），
///   本界面只做三件事：建格子、<b>判定规则</b>（<see cref="IAbilityCardHost"/>）、以及额度栏 / 图例 / 文案。
///   换一个界面（比如以后要塞进基地界面）只要复用那个 .tscn + 实现一个宿主，交互一行都不用重写。</item>
/// <item><b>数据从哪来</b>：`ConfigIndex.tres → AbilityCards` 数组（顺序就是矩阵顺序），
///   **加一张卡 = 加一行 .tres + 加进那个数组**，本文件一个字都不用改（占位卡会自动少一张）。</item>
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
	[Export(PropertyHint.Range, "2,10,1")] public int Columns { get; set; } = 6;

	/// <summary>矩阵**最少**行数（默认 3 → 一共 18 格）。卡数超过列 × 行时行数会自己长（"永远是填满的矩阵、不翻页"）。</summary>
	[Export(PropertyHint.Range, "1,8,1")] public int Rows { get; set; } = 3;

	// ── 右侧额度进度条的配色（P6b）──
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

	// 右侧额度区（用户要求：右半屏显示额度占用 + 竖起进度条 + 资源）
	private ProgressBar? _quotaBar;
	private Label? _quotaText;
	private Label? _shardsText;

	private readonly List<AbilityCardTile> _tiles = new();

	/// <summary>占位卡（真卡之后的所有格子）。与 <see cref="_tiles"/> 一起构成整屏矩阵，顺序 = 屏幕顺序。</summary>
	private readonly List<AbilityCardTile> _placeholders = new();

	private PackedScene? _tileScene;
	private bool _toggleHeld;
	private bool _quotaHeld;
	private int _builtCardCount = -1;
	private int _builtRows = -1;
	private int _rows = 3;
	private bool _debugSample;

	/// <summary>矩阵里一共几格（真卡 + 占位卡）。</summary>
	public int SlotCount => _matrix.GetChildCount();

	/// <summary>其中真正的卡有几格。</summary>
	public int TileCount => _tiles.Count;

	/// <summary>占位卡有几格（永不解锁，但交互反馈照旧）。</summary>
	public int PlaceholderCount => _placeholders.Count;

	/// <summary>矩阵现在实际几行（= <c>max(Rows, ceil(卡数 / Columns))</c>）。</summary>
	public int RowCount => _rows;

	/// <summary>按顺序取第 <paramref name="index"/> 个元素（自检 / 探针用）。</summary>
	public AbilityCardTile? TileAt(int index) => index >= 0 && index < _tiles.Count ? _tiles[index] : null;

	/// <summary>按顺序取第 <paramref name="index"/> 张占位卡（自检 / 探针用）。</summary>
	public AbilityCardTile? PlaceholderAt(int index) => index >= 0 && index < _placeholders.Count ? _placeholders[index] : null;

	/// <summary>本界面读到的卡表行数（探针用）。</summary>
	public int CardRowCount => AbilityStore.Rows.Count;

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
		_quotaBar = GetNodeOrNull<ProgressBar>("Root/Panel/HBox/RightPanel/QuotaBar");
		_quotaText = GetNodeOrNull<Label>("Root/Panel/HBox/RightPanel/QuotaText");
		_shardsText = GetNodeOrNull<Label>("Root/Panel/HBox/RightPanel/ShardsText");

		_matrix.Columns = Columns;
		// ⚠️ 只在这里订阅一次：`RebuildIfNeeded` 里原来又订阅了一遍 → 每次改尺寸回调会被调两次。
		// ⚠️ 订阅的是 **Root**（跟着视口走）而不是 Matrix：矩阵自己的尺寸被最小尺寸顶住，
		// 窗口变窄时它**根本不会发出 `Resized`** → 列数会卡在拉宽后的值（实测：拉宽到 19 列，
		// 缩回 16:9 之后仍然是 19 列、矩阵区还报着 2342）。Root 是锚满视口的普通 Control，
		// 视口一变它一定变，这个信号才是可靠的触发源。Matrix 那一路继续留着（右栏内容变化时也要重算）。
		_root.Resized += OnMatrixResized;
		_matrix.Resized += OnMatrixResized;
		_quotaUpgrade.Pressed += BuyQuota;
		BuildLegend();
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
			(Key.Escape, "关闭"));

		BeginOpen();
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

	/// <summary>
	/// 行数 = <c>max(最少行数, ceil(卡数 / 列数))</c> —— **"永远是填满的矩形、不翻页"** 的那条算式。
	/// 做成公开静态方法是为了让探针能对任意卡数算一遍（**不动真表**）：加卡时行数自己长，
	/// 格数 = 列 × 行 ≥ 卡数，所以最后一行不会缺角。
	/// </summary>
	/// <summary>
	/// 按**可用宽度**算列数：卡宽 + 间隔 一组，铺得下几张就是几列。
	/// 用户要求「不要求固定 n×n，但卡牌比例/大小不变 + 留间隔给光圈」，
	/// 所以列数跟着容器宽度走，挤不下的卡自然流到下一行。
	/// </summary>
	/// <summary>容器尺寸变了就重算列数（卡牌大小与比例不变，挤不下的流到下一行）。</summary>
	private void OnMatrixResized()
	{
		int cols = ColumnsFor(AvailableMatrixWidth(), CardMinWidth, CardSeparation);
		if (cols == _matrix.Columns)
		{
			return;
		}

		Columns = cols;
		_builtRows = -1; // 强制重建
		RebuildIfNeeded();
		// ⚠️ 重建之后必须再刷一次文案：底部那一行写的是「矩阵 列×行 = 格数」，
		// 它只在 `RefreshAll` 里生成 —— 不刷就会出现"矩阵真的变成 18 列、页脚还写着 10×3"。
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

	/// <summary>卡面最小宽 / 间隔（与 .tscn 的 custom_minimum_size 和 h_separation 对齐）。</summary>
	private const float CardMinWidth = 110f;
	private const float CardSeparation = 14f;
	public static int ColumnsFor(float availWidth, float cardWidth, float separation)
	{
		float unit = Mathf.Max(1f, cardWidth + separation);
		return Mathf.Max(2, Mathf.FloorToInt((availWidth + separation) / unit));
	}

	public static int RowsFor(int cardCount, int columns, int minRows)
		=> Mathf.Max(minRows, Mathf.CeilToInt(cardCount / (float)Mathf.Max(1, columns)));

	/// <summary>
	/// 建矩阵。**只有卡表行数 / 行数配置变了才重建**（平时只 <see cref="RefreshAll"/> 刷状态），
	/// 所以点卡片不会重建节点、不会打断缓动动画。
	///
	/// <para>格数 = <see cref="Columns"/> × 行数，行数 = <see cref="RowsFor"/>（见它的注释）——
	/// **永远是一个填满的矩形、不翻页**：不足的格子由**占位卡**占住（用户 A5），卡多了就把行数往下长。
	/// 每一格的尺寸不由这里决定：矩阵铺满可用区域、每格都是 Expand，
	/// 由 <c>GridContainer</c> 按屏幕/容器的实际大小等比分配 —— 这就是"自适应大小"。</para>
	/// </summary>
	private void RebuildIfNeeded()
	{
		int cardCount = AbilityStore.Rows.Count;
		int rows = RowsFor(cardCount, Columns, Rows);
		if (_builtCardCount == cardCount && _builtRows == rows && _matrix.GetChildCount() > 0)
		{
			return;
		}

		_builtCardCount = cardCount;
		_builtRows = rows;
		_rows = rows;
		_tiles.Clear();
		_placeholders.Clear();
		foreach (Node child in _matrix.GetChildren())
		{
			_matrix.RemoveChild(child);
			child.QueueFree();
		}

		_tileScene ??= GD.Load<PackedScene>("res://Scenes/UI/AbilityCardTile.tscn");
		_matrix.Columns = Columns;
		int slots = Columns * rows;
		for (int i = 0; i < slots; i++)
		{
			if (_tileScene?.Instantiate() is not AbilityCardTile tile)
			{
				// 预制体读不到：这一格空着也不抛异常（与"表读不到就用自身默认值"同一条纪律）。
				break;
			}

			if (i < cardCount)
			{
				tile.Bind(AbilityStore.Rows[i], this);
				_tiles.Add(tile);
			}
			else
			{
				// 空位 = **占位卡**：卡表里没有对应行 → 没有解锁条件 → 永远无法解锁 / 激活 / 升级。
				// 但它是真的元素（不是空框）：点它 / 长按它照常读条、震动、失败音效。
				tile.Bind(null, this);
				_placeholders.Add(tile);
			}

			// 卡牌感的关键：用 AspectRatioContainer 把每格锁成 2.5:4（用户要求 2.5:4 或 3:4），
			// 这样无论矩阵分到多大，卡面永远是竖长的（不会因为屏幕是 16:9 就变横）。
			var holder = new AspectRatioContainer
			{
				Ratio = 0.625f, // 宽/高 = 2.5/4
				CustomMinimumSize = new Vector2(CardMinWidth, CardMinWidth / 0.625f),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill,
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

		foreach (AbilityCardTile placeholder in _placeholders)
		{
			placeholder.RefreshVisual();
		}

		int used = AbilityStore.QuotaUsed;
		int cap = AbilityStore.QuotaCap;
		_shards.Text = $"碎片：{MetaProgress.Shards}";
		_quota.Text = $"额度：{used} / {cap}　（扩增 Lv{AbilityStore.QuotaLevel}，剩余 {AbilityStore.QuotaLeft}）";
		_quotaUpgrade.Text = $"扩增额度 +1（费用 {AbilityStore.QuotaUpgradeCost}）";
		_quotaUpgrade.Disabled = AbilityStore.QuotaRow == null;

		// 右侧竖进度条：填的是"已占用 / 总额度"，一眼看出还剩多少。
		// ⚠️ P6b：填充色**由下面的 <see cref="ApplyQuotaBarColor"/> 决定**（正常 / 快满 / 满三档）。
		if (_quotaBar != null)
		{
			_quotaBar.MaxValue = Mathf.Max(1, cap);
			_quotaBar.Value = used;
			ApplyQuotaBarColor(used, cap);
		}
		if (_quotaText != null)
		{
			_quotaText.Text = $"{used} / {cap}　（剩余 {AbilityStore.QuotaLeft}）";
		}
		if (_shardsText != null)
		{
			_shardsText.Text = $"碎片：{MetaProgress.Shards}";
		}

		_footer.Text =
			$"三状态：变暗 = 未解锁 ｜ 恢复颜色 = 解锁未激活 ｜ 边框外光圈 = 激活　·　"
			+ $"解锁 {AbilityStore.UnlockedCount} 张 / 激活 {AbilityStore.ActiveCount} 张 / 共 {_tiles.Count} 张　·　"
			+ $"矩阵 {Columns}×{_rows} = {SlotCount} 格（占位卡 {_placeholders.Count} 张：没有解锁条件，永远无法解锁）　·　"
			+ "激活的能力在下局开局生效（额度不够就装不下，必须取舍）";
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
	/// 前 4 张激活（亮光圈：白 1 / 绿 2 / 蓝 3 / 紫 4）、第 5 张红 5 未激活、第 6 张白 1 未激活、
	/// 第 7 张**未解锁**（变暗的灰 0）、第 8 张绿 2 未激活 —— 一张图里六个等级色 + 三种状态全齐。
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
				3 => 4,
				4 => 5,
				5 => 1,
				6 => 0,
				_ => 2,
			};
			bool active = i < 4;
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
	public void DebugEndSandbox() => AbilityStore.DebugEndSandbox();

	/// <summary>探针用：一行状态（碎片 / 额度 / 每张卡的等级与激活）。</summary>
	public string DebugSummary() => AbilityStore.Summary();

	/// <summary>探针用：整屏每个元素的逐格状态（真卡 + 占位卡），以及矩阵/格子的**实际像素尺寸**（自适应）。</summary>
	public string DebugMatrixReport()
	{
		var lines = new List<string>();
		foreach (AbilityCardTile tile in _tiles)
		{
			lines.Add(tile.DebugState());
		}

		string placeholder = _placeholders.Count == 0
			? "占位卡 0 张（矩阵被真卡填满）"
			: $"占位卡 {_placeholders.Count} 张（第 1 张：{_placeholders[0].DebugState()}）";

		string size = _tiles.Count > 0
			? $"视口 {GetViewport().GetVisibleRect().Size.X:0}×{GetViewport().GetVisibleRect().Size.Y:0}"
				+ $" → 矩阵区 {_matrix.Size.X:0}×{_matrix.Size.Y:0} → 每格 {_tiles[0].Size.X:0}×{_tiles[0].Size.Y:0}px"
				+ "（自适应：格子尺寸 = 矩阵区 ÷ 行列，跟着容器走，场景里不再写死尺寸）"
			: $"视口 {GetViewport().GetVisibleRect().Size.X:0}×{GetViewport().GetVisibleRect().Size.Y:0}"
				+ $" → 矩阵区 {_matrix.Size.X:0}×{_matrix.Size.Y:0}（没有真卡）";

		return $"列 {Columns} × 行 {RowCount} → {SlotCount} 格（真卡 {TileCount} + 占位卡 {PlaceholderCount}）｜{size}"
			+ $"｜{placeholder}｜" + string.Join(" ｜ ", lines);
	}

	/// <summary>
	/// 探针用：**"永远是一个填满的矩阵、不翻页"** 的算式自检 —— 对任意卡数算出行数 / 格数，
	/// 并断言"格数 ≥ 卡数"且"格数 % 列数 == 0"（没有缺角的最后一行，也就不需要翻页）。
	/// **不动真表**：这里只是纯算式，玩家存档与卡表一个字节都不碰。
	/// </summary>
	public string DebugSlotPlan()
	{
		var lines = new List<string>();
		bool ok = true;
		foreach (int count in new[] { 0, 1, 8, 17, 18, 19, 24, 25, 37, 60 })
		{
			int rows = RowsFor(count, Columns, Rows);
			int slots = Columns * rows;
			bool filled = slots >= count && slots % Columns == 0;
			ok &= filled;
			lines.Add($"{count} 张 → {Columns}×{rows} = {slots} 格（占位卡 {slots - count}）{(filled ? "✓" : "✗")}");
		}

		return $"满屏矩阵算式（列 {Columns}、最少行 {Rows}，行数 = max(最少行, ceil(卡数/列))）："
			+ string.Join(" ｜ ", lines) + $"　→ 全部满足「填满矩形、不翻页」 = {ok}";
	}

	/// <summary>
	/// 探针用：**占位卡的交互反馈**（用户 A5：「占位卡不可解锁、不可激活、不可升级 —— 但交互反馈仍然要有」）。
	/// 四种操作各跑一次，每一次都必须：① 判定 = <see cref="AbilityCardResult.NoCard"/>；
	/// ② 走失败分支（<c>FailCount</c> +1 = 震动 + 失败音效）；③ 状态一个字节都不变。
	/// 走的是**同一条** <see cref="AbilityCardTile.RunAction"/>，所以这里证明的就是玩家点它时发生的事。
	/// </summary>
	public string DebugRunPlaceholderFeedback()
	{
		if (_placeholders.Count == 0)
		{
			return "占位卡：0 张（矩阵被真卡填满，没有空位）—— 期望 ≥ 1（默认 6×3 = 18 格 / 8 张卡）";
		}

		AbilityCardTile tile = _placeholders[0];
		int failBefore = tile.FailCount;
		int successBefore = tile.SuccessCount;
		var lines = new List<string>();
		foreach (AbilityCardAction action in new[]
			{
				AbilityCardAction.Unlock, AbilityCardAction.ToggleActive,
				AbilityCardAction.Upgrade, AbilityCardAction.ResetUpgrades,
			})
		{
			lines.Add($"{action} → {tile.DebugRunAction(action)}（期望 NoCard）");
		}

		lines.Add($"反馈：失败（震动 + 失败音效）{tile.FailCount - failBefore} 次 / 成功 {tile.SuccessCount - successBefore} 次（期望 4 / 0）");
		lines.Add($"失败音效 last={AudioManager.Instance?.DebugLastId} 缺失 id=[{AudioManager.Instance?.DebugMissingIds}]（期望 ui_card_fail）");
		lines.Add($"状态：{tile.DebugState()}");
		return "占位卡交互：" + string.Join(" ｜ ", lines);
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
		// 顶栏与右栏两个文本一起改，否则截图里会出现"进度条 5/6、文字 0/6"的错位。
		if (_quotaText != null)
		{
			_quotaText.Text = $"{used} / {cap}　（剩余 {Mathf.Max(0, cap - used)}）";
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

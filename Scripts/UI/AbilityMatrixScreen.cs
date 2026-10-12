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
///   默认 <b>6 × 3 = 18 格</b>（1920×1080 下每格 ≈ 232×214，全是手指头点得到的大小）。
///   卡不满时会补上**空位**占位格 —— "位置先占住"，以后加卡直接填进空格。</item>
/// <item><b>元素是基类组件</b>：每一格都是 <see cref="AbilityCardTile"/>（三状态 + 四种交互 + 读条 + 震动 + 音效），
///   本界面只做三件事：建格子、<b>判定规则</b>（<see cref="IAbilityCardHost"/>）、以及额度栏 / 图例 / 文案。
///   换一个界面（比如以后要塞进基地界面）只要复用那个 .tscn + 实现一个宿主，交互一行都不用重写。</item>
/// <item><b>数据从哪来</b>：`Balance.tres → AbilityCards` 数组（顺序就是矩阵顺序），
///   **加一张卡 = 加一行 .tres + 加进那个数组**，本文件一个字都不用改。</item>
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

	/// <summary>矩阵列数（默认 6 —— 需求点名过的 3×6 的横向版本，1080p 下每格够大）。</summary>
	[Export(PropertyHint.Range, "2,10,1")] public int Columns { get; set; } = 6;

	/// <summary>矩阵行数（默认 3 → 一共 18 格）。</summary>
	[Export(PropertyHint.Range, "1,8,1")] public int Rows { get; set; } = 3;

	protected override Control Root => _root;

	private Control _root = null!;
	private Label _shards = null!;
	private Label _quota = null!;
	private Button _quotaUpgrade = null!;
	private GridContainer _matrix = null!;
	private HBoxContainer _legend = null!;
	private Label _footer = null!;

	private readonly List<AbilityCardTile> _tiles = new();
	private PackedScene? _tileScene;
	private bool _toggleHeld;
	private bool _quotaHeld;
	private int _builtCardCount = -1;
	private bool _debugSample;

	/// <summary>矩阵里一共几格（含空位）。</summary>
	public int SlotCount => _matrix.GetChildCount();

	/// <summary>其中真正的卡有几格。</summary>
	public int TileCount => _tiles.Count;

	/// <summary>按顺序取第 <paramref name="index"/> 个元素（自检 / 探针用）。</summary>
	public AbilityCardTile? TileAt(int index) => index >= 0 && index < _tiles.Count ? _tiles[index] : null;

	/// <summary>本界面读到的卡表行数（探针用）。</summary>
	public int CardRowCount => AbilityStore.Rows.Count;

	public override void _Ready()
	{
		_root = GetNode<Control>("Root");
		InitializeOverlay();
		_shards = GetNode<Label>("Root/Panel/VBox/Header/Shards");
		_quota = GetNode<Label>("Root/Panel/VBox/Header/Quota");
		_quotaUpgrade = GetNode<Button>("Root/Panel/VBox/Header/QuotaUpgrade");
		_matrix = GetNode<GridContainer>("Root/Panel/VBox/Matrix");
		_legend = GetNode<HBoxContainer>("Root/Panel/VBox/Legend");
		_footer = GetNode<Label>("Root/Panel/VBox/Footer");

		_matrix.Columns = Columns;
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
	/// 建矩阵。**只有卡表行数变了才重建**（平时只 <see cref="RefreshAll"/> 刷状态），
	/// 所以点卡片不会重建节点、不会打断缓动动画。
	/// </summary>
	private void RebuildIfNeeded()
	{
		int cardCount = AbilityStore.Rows.Count;
		if (_builtCardCount == cardCount && _matrix.GetChildCount() > 0)
		{
			return;
		}

		_builtCardCount = cardCount;
		_tiles.Clear();
		foreach (Node child in _matrix.GetChildren())
		{
			_matrix.RemoveChild(child);
			child.QueueFree();
		}

		_tileScene ??= GD.Load<PackedScene>("res://Scenes/UI/AbilityCardTile.tscn");

		int slots = Mathf.Max(Columns * Rows, cardCount);
		for (int i = 0; i < slots; i++)
		{
			if (i < cardCount && _tileScene != null && _tileScene.Instantiate() is AbilityCardTile tile)
			{
				tile.Bind(AbilityStore.Rows[i], this);
				_matrix.AddChild(tile);
				_tiles.Add(tile);
				continue;
			}

			// 空位：占住矩阵的位置（"位置先占住"），以后加卡直接填进来。
			_matrix.AddChild(BuildEmptySlot());
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

		_footer.Text =
			$"三状态：变暗 = 未解锁 ｜ 恢复颜色 = 解锁未激活 ｜ 边框外光圈 = 激活　·　"
			+ $"解锁 {AbilityStore.UnlockedCount} 张 / 激活 {AbilityStore.ActiveCount} 张 / 共 {_tiles.Count} 张　·　"
			+ "激活的能力在下局开局生效（额度不够就装不下，必须取舍）";
	}

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

	/// <summary>空格子：只占位置，明确写着"空位"（不是坏掉的卡）。</summary>
	private static Control BuildEmptySlot()
	{
		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(232, 214),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			TooltipText = "空位：以后加一张卡就填进这里（在 Config/Tables/AbilityCards/ 加一行 .tres 并挂进 Balance.tres 的 AbilityCards 数组）",
		};

		var box = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.06f, 0.08f, 0.55f),
			BorderColor = new Color(0.30f, 0.32f, 0.38f, 0.55f),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
		};
		box.SetBorderWidthAll(1);
		panel.AddThemeStyleboxOverride("panel", box);

		var label = new Label
		{
			Text = "空位",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = new Color(0.55f, 0.58f, 0.65f, 0.8f),
		};
		label.ThemeTypeVariation = "UiCaption";
		panel.AddChild(label);
		return panel;
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

	/// <summary>探针用：整屏每个元素的逐格状态。</summary>
	public string DebugMatrixReport()
	{
		var lines = new List<string>();
		foreach (AbilityCardTile tile in _tiles)
		{
			lines.Add(tile.DebugState());
		}

		return $"列 {Columns} × 行 {Rows} → {SlotCount} 格（卡 {TileCount} + 空位 {SlotCount - TileCount}）｜"
			+ string.Join(" ｜ ", lines);
	}

	/// <summary>探针用：额度栏那一行的实际文案（证明界面上的数字来自 AbilityStore）。</summary>
	public string DebugHeaderText() => $"{_shards.Text} ／ {_quota.Text} ／ {_quotaUpgrade.Text}";
}

using System.Collections.Generic;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// **「角色持有」面板**（用户需求第四节 4）：本局构筑（<see cref="RunBuild"/>）用**图标矩阵**铺开，
/// 鼠标悬停才出说明文字。
///
/// 为什么长这样：
/// <list type="bullet">
/// <item>「构筑元素都用图标」→ 一格 = 一个图标 + 叠层数（×N），不写名字；</item>
/// <item>「只有选取界面和鼠标悬停时显示说明」→ 名字 / 描述 / 类型只在 <c>TooltipText</c> 里
///   （面板是 <see cref="OverlayScreen.UiLayer.Panel"/>：打开即暂停 + 放开鼠标，悬停真的能悬停）；</item>
/// <item>「一个面板来显示角色持有」→ 全屏铺开，空格子直接铺底色，几件东西一眼看完。</item>
/// </list>
///
/// 与 HUD 的分工：HUD 只放**一行小图标**（战斗中扫一眼），要看说明按 <see cref="ToggleKey"/> 开这个面板。
/// </summary>
public partial class BuildScreen : OverlayScreen
{
	/// <summary>默认快捷键（HUD 的构筑图标条也读它，两处不写死同一个键）。</summary>
	public const Key DefaultToggleKey = Key.C;

	[Export] public Key ToggleKey { get; set; } = DefaultToggleKey;

	protected override Control Root => _root;

	private Control _root = null!;
	private Label _summary = null!;
	private GridContainer _grid = null!;
	private Label _empty = null!;
	private bool _keyHeld;

	/// <summary>拍图 / 探针塞了示例格（这时不再被真实构筑状态覆盖）。</summary>
	private bool _debugSample;

	/// <summary>面板里画出来的图标格数（验证用只读属性）。</summary>
	public int TileCount => _grid.GetChildCount();

	public override void _Ready()
	{
		_root = GetNode<Control>("Root");
		InitializeOverlay();
		_summary = GetNode<Label>("Root/Panel/VBox/Summary");
		_grid = GetNode<GridContainer>("Root/Panel/VBox/Grid");
		_empty = GetNode<Label>("Root/Panel/VBox/Empty");
	}

	public override void _Process(double delta)
	{
		bool held = Input.IsPhysicalKeyPressed(ToggleKey);
		if (held && !_keyHeld)
		{
			Toggle();
		}

		_keyHeld = held;

		if (IsOpen)
		{
			Refresh();
		}
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

	public void Open()
	{
		if (IsOpen)
		{
			return;
		}

		SetKeyHints(KeyHint.Of(ToggleKey, "关闭"), (Key.Escape, "关闭"), KeyHint.Text("悬停", "看说明"));
		BeginOpen();
	}

	public override void Close()
	{
		if (IsOpen && !InputBlocked)
		{
			BeginClose();
		}
	}

	// ───────────────────────── 内容：RunBuild → 图标格 ─────────────────────────

	private void Refresh()
	{
		if (_debugSample)
		{
			return;   // 拍图 / 探针塞的示例格，不要被真实状态覆盖
		}

		RunBuild? build = RunController.Instance?.Build;

		foreach (Node child in _grid.GetChildren())
		{
			_grid.RemoveChild(child);
			child.QueueFree();
		}

		if (build == null)
		{
			_summary.Text = "还没有开局 —— 进入关卡后，这里显示本局拿到的全部构筑。";
			_empty.Visible = false;
			return;
		}

		IReadOnlyList<RewardDefinition> acquired = build.Acquired;
		int total = 0;
		foreach (RewardDefinition definition in acquired)
		{
			total += build.StacksOf(definition.Id);
		}

		_summary.Text = acquired.Count == 0
			? "本局还没有拿到任何构筑：去地面上的节点按 F（强化池 / 进化池 / 异化池…）。"
			: $"本局持有 {acquired.Count} 种（共 {total} 层）　鼠标悬停看说明";
		_empty.Visible = acquired.Count == 0;

		foreach (RewardDefinition definition in acquired)
		{
			_grid.AddChild(BuildTile(definition, build.StacksOf(definition.Id)));
		}
	}

	/// <summary>一格：图标 + 叠层数；名字与说明只在悬停提示里。</summary>
	private static Control BuildTile(RewardDefinition definition, int stacks)
	{
		var tile = new PanelContainer
		{
			ThemeTypeVariation = "UiTile",
			CustomMinimumSize = new Vector2(112, 112),
			TooltipText = $"{definition.Name} ×{stacks}（{KindLabel(definition.Kind)}）\n{definition.Description}",
		};

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 4);
		column.MouseFilter = Control.MouseFilterEnum.Ignore;

		var icon = new TextureRect
		{
			Texture = UiIcons.ForReward(definition),
			CustomMinimumSize = new Vector2(56, 56),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		var count = new Label
		{
			Text = stacks > 1 ? $"×{stacks}" : string.Empty,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		column.AddChild(icon);
		column.AddChild(count);
		tile.AddChild(column);
		return tile;
	}

	private static string KindLabel(RewardKind kind) => kind switch
	{
		RewardKind.Evolution => "进化",
		RewardKind.Mutation => "异化",
		_ => "强化",
	};

	/// <summary>
	/// 验证 / 拍图用：用**真实的奖励目录**（<see cref="RewardCatalog.All"/>）填几格 ——
	/// 不依赖本局状态，所以拍摄台与探针都能看到"图标矩阵"真正的样子。
	/// </summary>
	public void DebugShowSample(int count = 12)
	{
		_debugSample = true;
		foreach (Node child in _grid.GetChildren())
		{
			_grid.RemoveChild(child);
			child.QueueFree();
		}

		RewardDefinition[] all = RewardCatalog.All;
		int shown = Mathf.Min(count, all.Length);
		for (int i = 0; i < shown; i++)
		{
			// 每 4 格叠 2 层：截图里能同时看到"×N"与不显示层数两种形态。
			_grid.AddChild(BuildTile(all[i], i % 4 == 0 ? 2 : 1));
		}

		_summary.Text = $"示例：奖励目录前 {shown} 条（真实数据，共 {all.Length} 条）　鼠标悬停看说明";
		_empty.Visible = false;
	}
}

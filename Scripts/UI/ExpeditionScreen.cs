using System;
using System.Linq;
using ActRoguelike.AbilityCards;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.UI;

/// <summary>基地出击前共读当前装备，确认后才加载关卡。</summary>
public partial class ExpeditionScreen : OverlayScreen
{
	private Control _root = null!;
	private Label _summary = null!;
	private Button _depart = null!;
	private Action? _onDepart;
	protected override Control Root => _root;
	public string SummaryText => _summary.Text;
	private bool IsTopOverlay => IsOpen && UiRouter.Instance?.IsTopScreen(this) == true;
	private bool CanReceiveInput => IsTopOverlay && !InputBlocked;

	public override void _Ready()
	{
		Layer = 12;
		_root = new Control { Name = "Root", Theme = RuntimeResources.Load<Theme>("res://Assets/UI/Themes/ui_theme.tres") };
		AddChild(_root); _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		var dim = new ColorRect { Color = new Color(0.01f, 0.02f, 0.04f, 0.92f) };
		_root.AddChild(dim); dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		var margin = new MarginContainer(); _root.AddChild(margin);
		margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		margin.OffsetLeft = 64; margin.OffsetRight = -64; margin.OffsetTop = 40; margin.OffsetBottom = -85;
		var panel = new PanelContainer(); margin.AddChild(panel);
		var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 20); panel.AddChild(body);
		body.AddChild(new Label { Text = "准备出击", ThemeTypeVariation = "UiTitle" });
		var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		body.AddChild(scroll);
		_summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		scroll.AddChild(_summary);
		_depart = new Button { Text = "确认配置 · 出击", CustomMinimumSize = new Vector2(0, 60), ThemeTypeVariation = "UiPrimaryButton" };
		_depart.Pressed += Depart; body.AddChild(_depart);
		var back = new Button { Text = "返回基地调整", CustomMinimumSize = new Vector2(0, 48) };
		back.Pressed += Close; body.AddChild(back);
		InitializeOverlay(); SetKeyHints((Key.Enter, "出击"), (Key.Escape, "返回调整"));
	}

	public static string DescribeCurrentLoadout()
		=> DescribeDestination(null);

	private static string DescribeDestination(ExpeditionDestination? destination)
	{
		CharacterConfig? character = RunController.CharacterRow(RunSetup.Character);
		WeaponConfig? weapon = RunController.WeaponRow(RunSetup.Weapon);
		string cards = string.Join("、", AbilityStore.Rows.Where(r => AbilityStore.IsActive(r.Id)).Select(r => $"{r.DisplayName} Lv{AbilityStore.LevelOf(r.Id)}"));
		string stage = destination?.ResolvedTitle ?? ConfigIndex.Settings.Stages.FirstOrDefault()?.DisplayName ?? "第一关";
		return $"角色：{character?.DisplayName ?? "未选择"}\n武器：{weapon?.DisplayName ?? "未选择"}\n" +
			$"携带符文：{RuneCarry.Row?.DisplayName ?? "不携带"}（出击后生效）\n" +
			$"激活能力卡：{(cards.Length == 0 ? "未激活" : cards)}\n\n" +
			$"目的地：{stage}\n\n" +
			(destination?.ResolvedInstructions ?? ("探索节点 → 先选择奖励 → 完成对应挑战 → 自行决定何时挑战首领。\n" +
			"节点旁会提示挑战波数和本次是否免费；不提前展示具体随机奖励。\n" +
			"普通挑战结束后仍可能有游离敌人。首领击败后通过离场节点继续，阵亡后返回基地。"));
	}

	public bool Open(Action onDepart)
		=> OpenDestination(onDepart, null);

	public bool OpenDestination(Action onDepart, ExpeditionDestination? destination)
	{
		if (IsOpen) return false;
		_onDepart = onDepart; _summary.Text = DescribeDestination(destination);
		BeginOpen(); _depart.GrabFocus(); return true;
	}
	public void Depart()
	{
		if (!CanReceiveInput) return;
		Action? next = _onDepart; _onDepart = null; BeginClose(); next?.Invoke();
	}
	public override void Close()
	{
		if (!CanReceiveInput) return;
		_onDepart = null; BeginClose();
	}
	public override void _UnhandledInput(InputEvent @event)
	{
		if (IsTopOverlay && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Enter })
		{ GetViewport().SetInputAsHandled(); Depart(); }
	}
}

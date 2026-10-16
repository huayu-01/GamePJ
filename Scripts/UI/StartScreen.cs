using System;
using System.Collections.Generic;
using ActRoguelike.Config;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 选武器界面（基地「装备配置台」的第二步）。选定后回调 <c>Open</c> 传进来的委托。
/// 复用 OverlayScreen 的防误触与 Esc 约定。
///
/// <para>⚠️ P6a：**列表改成直接遍历武器表**（`ConfigIndex.Settings.Weapons` 的资源数组），
/// 不再逐个 <c>CreateCard(WeaponPresetKind.Pistol …)</c> 写死三把旧枪。这样：
/// ① 清掉旧武器后界面自动只剩新武器；② 以后**加一把武器 = 加一行 `.tres`**，
/// 这个文件一行都不用改（用户 §1.5「选角色/选武器界面要能容纳任意数量」）。</para>
/// </summary>
public partial class StartScreen : OverlayScreen
{
	protected override Control Root => _root;

	private Control _root = null!;
	private Action<WeaponPresetKind>? _onChosen;
	private Button _template = null!;
	private VBoxContainer _cards = null!;
	private Label _hint = null!;

	/// <summary>这一次列出来的武器；顺序 = 卡片顺序 = 数字键 <c>1..N</c> 的下标 + 1。</summary>
	private readonly List<WeaponPresetKind> _options = new();
	public int OptionCount => _options.Count;
	public string OptionIds => string.Join(",", _options.ConvertAll(kind => RunController.WeaponRow(kind)?.Id ?? string.Empty));
	public int SelectedIndex => _options.IndexOf(RunSetup.Weapon);
	public bool SelectedCardDisabled => SelectedIndex >= 0 && _cards.GetChild<Button>(SelectedIndex + 1).Disabled;
	[Export] public Color SelectionColor { get; set; } = new(0.46f, 0.93f, 0.78f);

	public override void _Ready()
	{
		CloseOnCancel = false;
		_root = GetNode<Control>("Root");
		InitializeOverlay();
		_cards = GetNode<VBoxContainer>("Root/Panel/VBox/Scroll/Cards");
		_template = _cards.GetNode<Button>("CardTemplate");
		_hint = GetNode<Label>("Root/Panel/VBox/Hint");
		GetNode<Button>("Root/Panel/VBox/Return").Pressed += Close;
	}

	/// <summary>打开选武器界面；选完武器后回调。</summary>
	public void Open(Action<WeaponPresetKind> onChosen)
	{
		if (IsOpen)
		{
			return;
		}

		_onChosen = onChosen;
		GetNode<Label>("Root/Panel/VBox/Title").Text = $"{RunController.CharacterRow(RunSetup.Character)?.DisplayName} · 选择武器";
		ClearCards();
		_options.Clear();

		// 表是唯一真源：数量 / 顺序 / 文案全部来自武器表（`Config/Tables/Weapons/wpn_*.tres`）。
		foreach (WeaponConfig? row in ConfigIndex.Settings.Weapons)
		{
			if (!LoadoutSelection.Allows(RunController.CharacterRow(RunSetup.Character), row))
			{
				continue;
			}

			// 枚举值 = 行 `Slot - 1`（见 ConfigIndexData 的注释）；即使某个 Slot 还没有对应的
			// 枚举成员，这里也能正确换回来（C# 枚举只是 int，`RunController.WeaponRow` 按 Slot 查表）。
			var kind = (WeaponPresetKind)(row.Slot - 1);
			_options.Add(kind);
			CreateCard(_options.Count - 1, kind, WeaponCardText(row));
		}

		_hint.Text = _options.Count == 0
			? "武器表读不到（ConfigIndex.tres 的 Weapons 索引为空）—— 这一局用玩家场景自带的武器数值。"
			: $"按 1 ~ {_options.Count} 或点击切换 · ✓ 为当前装备 · 各角色分别记住武器";

		SetKeyHints(
			KeyHint.Text(_options.Count == 0 ? "—" : $"1 ~ {_options.Count}", "选择武器"),
			(Key.Enter, "第一把"),
			(Key.Escape, "返回"));

		BeginOpen();
	}

	/// <summary>
	/// 武器卡片文案。标题与数值**都来自武器表**（<c>DisplayName</c> + <c>DescriptionTemplate</c>，
	/// 后者支持 <c>{字段名}</c> 占位符）—— 代码里不写死任何武器名/数值。
	/// </summary>
	private static string WeaponCardText(WeaponConfig row)
	{
		string title = string.IsNullOrEmpty(row.DisplayName) ? row.Id : row.DisplayName;
		if (string.IsNullOrEmpty(row.DescriptionTemplate))
		{
			return title;
		}

		return $"{title}（{ConfigText.Format(row.DescriptionTemplate, row, row.Params)}）";
	}

	public override void Close()
	{
		if (IsOpen && !InputBlocked)
		{
			BeginClose();
		}
	}

	private void CreateCard(int index, WeaponPresetKind kind, string text)
	{
		var card = (Button)_template.Duplicate();
		card.Visible = true;
		card.Text = text;
		if (kind == RunSetup.Weapon)
		{
			card.Text = "✓ 当前装备　" + text;
			card.Disabled = true;
			card.AddThemeColorOverride("font_disabled_color", SelectionColor);
		}
		card.Alignment = HorizontalAlignment.Left;

		// 图标代替文字：武器用图标，具体数值留在文字里（选择界面允许有说明）。
		WeaponConfig? row = RunController.WeaponRow(kind);
		card.Icon = !string.IsNullOrEmpty(row?.IconPath) ? GD.Load<Texture2D>(row.IconPath) : UiIcons.ForWeapon(kind);
		card.ExpandIcon = true;
		card.AddThemeConstantOverride("icon_max_width", 42);

		card.Pressed += () => Choose(index);
		_cards.AddChild(card);
	}

	private void ClearCards()
	{
		foreach (Node child in _cards.GetChildren())
		{
			if (child != _template)
			{
				_cards.RemoveChild(child);
				child.QueueFree();
			}
		}
	}

	private void Choose(int index)
	{
		if (InputBlocked || index < 0 || index >= _options.Count || index == SelectedIndex)
		{
			return;
		}

		WeaponPresetKind weapon = _options[index];
		if (!LoadoutSelection.Allows(RunController.CharacterRow(RunSetup.Character), RunController.WeaponRow(weapon))) return;
		Action<WeaponPresetKind>? callback = _onChosen;
		_onChosen = null;
		Close();
		callback?.Invoke(weapon);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsOpen || @event is not InputEventKey { Pressed: true, Echo: false } key)
		{
			return;
		}

		if (key.Keycode is Key.Enter or Key.KpEnter or Key.Space)
		{
			Choose(0);
			return;
		}

		int digit = DigitOf(key.Keycode);
		if (digit > 0)
		{
			Choose(digit - 1);
		}
	}

	/// <summary>主键盘数字键与小键盘数字键 → 1..9（0 = 不是数字键）。</summary>
	private static int DigitOf(Key key) => key switch
	{
		Key.Key1 or Key.Kp1 => 1,
		Key.Key2 or Key.Kp2 => 2,
		Key.Key3 or Key.Kp3 => 3,
		Key.Key4 or Key.Kp4 => 4,
		Key.Key5 or Key.Kp5 => 5,
		Key.Key6 or Key.Kp6 => 6,
		Key.Key7 or Key.Kp7 => 7,
		Key.Key8 or Key.Kp8 => 8,
		Key.Key9 or Key.Kp9 => 9,
		_ => 0,
	};
}

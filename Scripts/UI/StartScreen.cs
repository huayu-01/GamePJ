using System;
using ActRoguelike.Config;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// 开局配置界面（主循环里 Base → Configure → Stage 的 Configure 步）。
/// 目前只选武器；选定后回调 <see cref="RunController.BeginRunWith"/> 进入第一关。
/// 复用 OverlayScreen 的防误触与 Esc 约定。
/// </summary>
public partial class StartScreen : OverlayScreen
{
	/// <summary>0 = 手枪，1 = 激光狙击枪。</summary>
	[Signal]
	public delegate void WeaponChosenEventHandler(int index);

	protected override Control Root => _root;

	private Control _root = null!;
	private Action<WeaponPresetKind>? _onChosen;
	private Button _template = null!;
	private VBoxContainer _cards = null!;

	public override void _Ready()
	{
		CloseOnCancel = false;
		_root = GetNode<Control>("Root");
		InitializeOverlay();
		_cards = GetNode<VBoxContainer>("Root/Panel/VBox/Cards");
		_template = _cards.GetNode<Button>("CardTemplate");
	}

	/// <summary>打开配置界面；选完武器后回调。</summary>
	public void Open(Action<WeaponPresetKind> onChosen)
	{
		if (IsOpen)
		{
			return;
		}

		_onChosen = onChosen;

		ClearCards();
		CreateCard(0, WeaponPresetKind.Pistol, WeaponCardText(WeaponPresetKind.Pistol, "[1] 手枪\n单发高伤、精准，适合中远距离点射"));
		CreateCard(1, WeaponPresetKind.Sniper, WeaponCardText(WeaponPresetKind.Sniper, "[2] 激光狙击枪\n高伤单发"));
		CreateCard(2, WeaponPresetKind.Rifle, WeaponCardText(WeaponPresetKind.Rifle, "[3] 步枪\n按住左键连发，续航最好"));

		SetKeyHints(("1 / 2 / 3", "选择武器"), (Key.Enter, "默认手枪"), ("悬停", "看武器数值"));

		BeginOpen();
	}

	/// <summary>
	/// 武器卡片文案。数值来自武器表（`Config/Tables/Weapons/wpn_*.tres` 的 <c>DescriptionTemplate</c>
	/// 支持 <c>{字段名}</c> 占位符）；整张表读不到时只留标题 —— 这时武器组件用的是自身默认值，
	/// 编不出具体数字（这比编错数字好）。
	/// </summary>
	private static string WeaponCardText(WeaponPresetKind kind, string title)
	{
		WeaponConfig? row = RunController.WeaponRow(kind);
		if (row == null)
		{
			return title;
		}

		string detail = ConfigText.Format(
			string.IsNullOrEmpty(row.DescriptionTemplate)
				? "{Damage:0} 伤害 / {MagazineSize} 发弹匣"
				: row.DescriptionTemplate,
			row,
			row.Params);
		return $"{title}（{detail}）";
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
		card.Alignment = HorizontalAlignment.Left;

		// 图标代替文字：武器用图标，具体数值留在文字里（选择界面允许有说明）。
		card.Icon = UiIcons.ForWeapon(kind);

		card.Pressed += () => Choose(index);
		_cards.AddChild(card);
	}

	private void ClearCards()
	{
		foreach (Node child in _cards.GetChildren())
		{
			if (child != _template)
			{
				child.QueueFree();
			}
		}
	}

	private void Choose(int index)
	{
		if (InputBlocked)
		{
			return;
		}

		Action<WeaponPresetKind>? callback = _onChosen;
		_onChosen = null;
		EmitSignal(SignalName.WeaponChosen, index);
		Close();
		callback?.Invoke(index switch
		{
			1 => WeaponPresetKind.Sniper,
			2 => WeaponPresetKind.Rifle,
			_ => WeaponPresetKind.Pistol,
		});
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsOpen || @event is not InputEventKey { Pressed: true, Echo: false } key)
		{
			return;
		}

		int index = key.Keycode switch
		{
			Key.Key1 or Key.Kp1 => 0,
			Key.Key2 or Key.Kp2 => 1,
			Key.Key3 or Key.Kp3 => 2,
			Key.Enter or Key.KpEnter or Key.Space => 0,
			_ => -1,
		};

		if (index >= 0)
		{
			Choose(index);
		}
	}
}

using System;
using ActRoguelike.Config;
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
		BalanceConfig config = Balance.Settings;

		ClearCards();
		CreateCard(0, $"[1] 手枪\n单发高伤、精准，适合中远距离点射（{config.PistolDamage:0} 伤害 / {config.PistolMagazineSize} 发弹匣）");
		CreateCard(1, $"[2] 激光狙击枪\n高伤单发、可穿透 {config.SniperPierce} 个目标（{config.SniperDamage:0} 伤害 / {config.SniperMagazineSize} 发弹匣）");
		CreateCard(2, $"[3] 步枪\n按住左键连发，续航最好（{config.RifleDamage:0} 伤害 / {config.RifleFireInterval:0.00} 秒间隔 / {config.RifleMagazineSize} 发弹匣）");

		BeginOpen();
	}

	public override void Close()
	{
		if (IsOpen && !InputBlocked)
		{
			BeginClose();
		}
	}

	private void CreateCard(int index, string text)
	{
		var card = (Button)_template.Duplicate();
		card.Visible = true;
		card.Text = text;
		card.Alignment = HorizontalAlignment.Left;
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

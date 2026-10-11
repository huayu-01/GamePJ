using System;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.UI;

/// <summary>
/// Base 界面（主循环起点）：显示碎片，消费碎片换永久强化，然后进入 Configure。
/// 复用 OverlayScreen 的防误触与 Esc 约定。
/// </summary>
public partial class BaseScreen : OverlayScreen
{
	protected override Control Root => _root;

	private Control _root = null!;
	private Label _shards = null!;
	private Button _health = null!;
	private Button _damage = null!;
	private Button _speed = null!;
	private Button _crit = null!;
	private Button _reload = null!;
	private Button _start = null!;
	private Action? _onStart;

	public override void _Ready()
	{
		CloseOnCancel = false;
		_root = GetNode<Control>("Root");
		InitializeOverlay();
		_shards = GetNode<Label>("Root/Panel/VBox/Shards");
		_health = GetNode<Button>("Root/Panel/VBox/Upgrades/Health");
		_damage = GetNode<Button>("Root/Panel/VBox/Upgrades/Damage");
		_speed = GetNode<Button>("Root/Panel/VBox/Upgrades/Speed");
		_crit = GetNode<Button>("Root/Panel/VBox/Upgrades/Crit");
		_reload = GetNode<Button>("Root/Panel/VBox/Upgrades/Reload");
		_start = GetNode<Button>("Root/Panel/VBox/Start");

		_health.Pressed += () => { MetaProgress.BuyHealth(); Refresh(); };
		_damage.Pressed += () => { MetaProgress.BuyDamage(); Refresh(); };
		_speed.Pressed += () => { MetaProgress.BuySpeed(); Refresh(); };
		_crit.Pressed += () => { MetaProgress.BuyCrit(); Refresh(); };
		_reload.Pressed += () => { MetaProgress.BuyReload(); Refresh(); };
		_start.Pressed += Begin;
	}

	public void Open(Action onStart)
	{
		if (IsOpen)
		{
			return;
		}

		_onStart = onStart;
		MetaProgress.Load();
		Refresh();
		BeginOpen();
	}

	public override void Close()
	{
		if (IsOpen && !InputBlocked)
		{
			BeginClose();
		}
	}

	private void Refresh()
	{
		_shards.Text = $"碎片：{MetaProgress.Shards}";
		_health.Text = $"最大生命 +10 ／ 等级 {MetaProgress.HealthLevel} 　（费用 {MetaProgress.HealthCost}）";
		_damage.Text = $"伤害 +5% ／ 等级 {MetaProgress.DamageLevel} 　（费用 {MetaProgress.DamageCost}）";
		_speed.Text = $"移速 +3% ／ 等级 {MetaProgress.SpeedLevel} 　（费用 {MetaProgress.SpeedCost}）";
		_crit.Text = $"暴击率 +3% ／ 等级 {MetaProgress.CritLevel} 　（费用 {MetaProgress.CritCost}）";
		_reload.Text = $"装弹速度 +6% ／ 等级 {MetaProgress.ReloadLevel} 　（费用 {MetaProgress.ReloadCost}）";
		_start.Text = "开始探索（进入配置）";
	}

	private void Begin()
	{
		if (InputBlocked)
		{
			return;
		}

		Action? callback = _onStart;
		_onStart = null;
		Close();
		callback?.Invoke();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsOpen || @event is not InputEventKey { Pressed: true, Echo: false } key)
		{
			return;
		}

		switch (key.Keycode)
		{
			case Key.Key1 or Key.Kp1:
				MetaProgress.BuyHealth();
				Refresh();
				break;
			case Key.Key2 or Key.Kp2:
				MetaProgress.BuyDamage();
				Refresh();
				break;
			case Key.Key4 or Key.Kp4:
				MetaProgress.BuyCrit();
				Refresh();
				break;
			case Key.Key5 or Key.Kp5:
				MetaProgress.BuyReload();
				Refresh();
				break;
			case Key.Key3 or Key.Kp3:
				MetaProgress.BuySpeed();
				Refresh();
				break;
			case Key.Enter or Key.KpEnter or Key.Space:
				Begin();
				break;
		}
	}
}

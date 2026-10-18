using System;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>气贯长虹：真实按键蓄力、落地同帧召回、往返伤害与配装还原。</summary>
public partial class SpearRecallProbe : Node3D
{
	private int _pass, _fail;
	private double _elapsed;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private readonly PlayerLoadout _loadout = new();
	private SpearThrowAbility Spear => _player.GetNode<SpearThrowAbility>("AbilityWeapon6Skill2");
	private MeleeComboAbility Melee => _player.GetNode<MeleeComboAbility>("AbilityWeapon6Skill1");
	public override void _Process(double delta) => _elapsed += delta;
	private void Press(string action, bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task<bool> Until(Func<bool> predicate, double seconds = 4)
	{
		double start = _elapsed;
		while (!predicate() && _elapsed - start < seconds) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		return predicate();
	}
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[SpearRecall] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Equip(bool grant = false)
	{
		Press("weapon_skill_1", false); Press("weapon_skill_2", false);
		_loadout.Apply(_player, this, PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Spear);
		_player.GlobalPosition = new Vector3(20, 0.05f, 20); _player.Velocity = Vector3.Zero;
		_camera.GlobalPosition = new Vector3(20, 2, 26); _camera.LookAt(new Vector3(20, 0, 0)); _camera.ResetPhysicsInterpolation();
		if (grant)
		{
			var row = ConfigRows.ById<RewardConfig>("evo_rainbow");
			var def = RewardCatalog.Find("evo_rainbow");
			Check(row?.WeaponRecall?.TargetAbility?.Id == "skl_b3_w1_2" && row.HasKnownHandler && def != null, "气贯长虹真实表行／强类型目标与处理器有效");
			var build = new RunBuild();
			if (def != null) build.Add(def, new RewardContext { Player = _player, Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!, Build = build });
			Check(build.Has("evo_rainbow") && !Spear.RecallOnSecondPress && Spear.AutoRecallOnSettle, "真实入构筑后禁止手动召回，启用落地召回");
			Check(Mathf.IsEqualApprox(Spear.ChargeBaseSeconds, 0.5f) && Mathf.IsEqualApprox(Spear.ChargeStepSeconds, 0.125f)
				&& Mathf.IsEqualApprox(Spear.ChargeMaxSeconds, 1f) && Spear.ChargeMaxStepsEffective == 4, "基础／每档／总上限全部减半，仍为四档");
		}
		await Wait(0.2);
	}
	private async Task Throw(double seconds, int steps)
	{
		Press("weapon_skill_2", true); await Wait(seconds); Press("weapon_skill_2", false);
		Check(await Until(() => Spear.InFlight), "真实松手后由释放动画ATK抛出");
		Check(Spear.ChargeSteps == steps && Mathf.IsEqualApprox(Spear.LastPowerScale, 1f + 0.2f * steps),
			$"实际蓄力{Spear.ChargeTime:0.###}秒：{Spear.ChargeSteps}档／倍率{Spear.LastPowerScale:0.###}，期望{steps}档");
		Check(Melee.ExternallyBlocked, "离手后武技1锁门仍生效");
	}
	private async Task ManualRecall()
	{
		Press("weapon_skill_2", true); await Wait(0.02); Press("weapon_skill_2", false);
		Check(await Until(() => !Spear.InFlight), "默认状态再次按键正常收回");
	}
	private async Task LandAndReturn()
	{
		Check(await Until(() => Spear.HasSettled), "实际弹道碰World落地");
		Check(Spear.IsReturning && Spear.InFlight && Melee.ExternallyBlocked, "首次观察落地时已在回程，真实到手前仍锁武技1");
		Check(Spear.WeaponPosition.DistanceTo(Spear.TargetPoint) < 0.25f, $"实际落地位置接近准星弹道落点：{Spear.WeaponPosition}");
		Check(await Until(() => !Spear.InFlight) && !Melee.ExternallyBlocked, "无需再次按键，实际回手后才解锁武技1");
	}
	private async Task Screenshot()
	{
		if (DisplayServer.GetName() == "headless") return;
		Transform3D before = _camera.GlobalTransform;
		var label = new Label3D { Text = "Rainbow: World landing starts recall immediately\nCharge 0.5 + 4 x 0.125 = 1s | max power 1.8", Position = Spear.WeaponPosition + Vector3.Up * 2.2f, FontSize = 44, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled };
		AddChild(label);
		_camera.GlobalPosition = new Vector3(29, 8, 23); _camera.LookAt(new Vector3(20, 0.8f, 12)); _camera.ResetPhysicsInterpolation();
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		using var screenshot = GetViewport().GetTexture().GetImage();
		Check(screenshot.SavePng("res://docs/screenshots/j16_rainbow.png") == Error.Ok, "截图 j16_rainbow");
		label.QueueFree(); _camera.GlobalTransform = before; _camera.ResetPhysicsInterpolation();
	}
	public override async void _Ready()
	{
		try
		{
			await Wait(0.2);
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.WeaponMaster; RunSetup.Weapon = WeaponPresetKind.Spear;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera);
			await Equip();
			Check(Spear.RecallOnSecondPress && !Spear.AutoRecallOnSettle && Mathf.IsEqualApprox(Spear.ChargeMaxSeconds, 2f), "默认仍手动召回／总上限2秒");
			await Throw(2.12, 4);
			Check(await Until(() => Spear.HasSettled), "默认投掷真实落地");
			await Wait(0.15);
			Check(Spear.InFlight && !Spear.IsReturning && Spear.HasSettled, "默认落地停留，不被气贯长虹污染");
			await ManualRecall();
			await Equip(true);
			Press("weapon_skill_2", true); await Wait(0.32); Press("weapon_skill_2", false); await Wait(0.5);
			Check(!Spear.InFlight && !Spear.IsActive && !Melee.ExternallyBlocked, "未满新基础0.5秒松手仍取消，零出手不锁门");
			foreach (var shot in new[] { (0.55, 0), (0.70, 1), (0.83, 2), (0.95, 3), (1.12, 4), (1.50, 4) })
			{
				await Equip(true); await Throw(shot.Item1, shot.Item2);
				Press("weapon_skill_2", true); await Wait(0.02); Press("weapon_skill_2", false);
				Check(Spear.InFlight && !Spear.IsReturning && !Spear.HasSettled, "真实飞行中再次按键不能提前召回");
				await LandAndReturn();
			}
			await DamageAndScreenshot();
			await Equip();
			Check(Spear.RecallOnSecondPress && !Spear.AutoRecallOnSettle && Mathf.IsEqualApprox(Spear.ChargeBaseSeconds, 1f)
				&& Mathf.IsEqualApprox(Spear.ChargeStepSeconds, 0.25f) && Mathf.IsEqualApprox(Spear.ChargeMaxSeconds, 2f), "重新配装还原召回策略与全部蓄力时间");
			int strikes = Melee.StrikeCount;
			Press("weapon_skill_1", true); await Wait(0.02); Press("weapon_skill_1", false);
			Check(await Until(() => Melee.StrikeCount > strikes), "回手／配装后武技1真实ATK正常");
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[SpearRecall] === 气贯长虹验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task DamageAndScreenshot()
	{
		await Equip(true);
		var target = new Unit { Name = "RoundTripTarget", Faction = Faction.Enemy, MaxHealth = 100000, KnockbackResistance = 1 };
		target.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.4f, Height = 4f } });
		target.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.4f, BottomRadius = 0.4f, Height = 4f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.Coral } });
		AddChild(target); target.GlobalPosition = new Vector3(20, 1.6f, 16);
		await Wait(0.1); await Throw(1.12, 4);
		Check(await Until(() => Spear.HasSettled), "带靶真实World落地");
		float outboundDamage = target.MaxHealth - target.Health;
		Check(outboundDamage > 0f && Spear.IsReturning, $"出程真实路径命中{outboundDamage:0.###}，落地已经自动回程");
		await Screenshot();
		Check(await Until(() => !Spear.InFlight), "带靶自动回手完成");
		Check(Mathf.IsEqualApprox(target.MaxHealth - target.Health, outboundDamage * 2f), $"往返各命中一次，出程{outboundDamage:0.###}／总计{target.MaxHealth - target.Health:0.###}");
		target.QueueFree(); await Wait(0.1);
	}
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实MoveAndSlide验证高度、打落生命周期、三维受力和现有动作控制的配合。</summary>
public partial class FlightPhysicsProbe : Node3D
{
	private int _pass, _fail;
	private readonly List<Unit> _units = new();
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[Flight] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
	private async Task Tick(int count = 1)
	{
		for (int i = 0; i < count; i++) { await Frame(); foreach (var unit in _units) unit.Movement!._PhysicsProcess(1d / 60d); }
	}
	private void Solid(string name, Vector3 position, Vector3 size)
	{
		var body = new StaticBody3D { Name = name, CollisionLayer = CombatLayers.World, CollisionMask = 0, Position = position };
		body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } }); AddChild(body);
	}
	private Unit Spawn(Vector3 position, UnitPhysicsProfile? profile = null)
	{
		var unit = new Unit { Faction = Faction.Enemy, MaxHealth = 100 };
		unit.AddChild(new CollisionShape3D { Position = Vector3.Up * 0.9f, Shape = new CapsuleShape3D { Radius = 0.45f, Height = 1.8f } });
		unit.AddChild(new CharacterMovement { Name = "Movement", PhysicsProfile = profile, FaceMovementDirection = false });
		AddChild(unit); unit.GlobalPosition = position; unit.SetPhysicsProcess(false); unit.Movement!.SetPhysicsProcess(false);
		_units.Add(unit); return unit;
	}
	private async Task Clear()
	{
		foreach (var unit in _units) unit.QueueFree(); _units.Clear(); await Frame(); await Frame();
	}
	private static UnitPhysicsProfile Flight() => new() { CanFly = true, HoverHeight = 3, GroundRecoverySeconds = 0.5f };
	public override async void _Ready()
	{
		try
		{
			Solid("Floor", new Vector3(0, -0.5f, 0), new Vector3(100, 1, 100));
			Solid("Wall", new Vector3(8, 4, 0), new Vector3(0.5f, 8, 12));
			Solid("Platform", new Vector3(-10, 1, 0), new Vector3(6, 2, 6));
			await Frame(); await Frame();
			await Forces(); await Clear();
			await Heights(); await Clear();
			await Knockdown(); await Clear();
			await Boundaries(); await Clear();
			await Contracts(); await Clear();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[Flight] === 飞行与受力验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task Forces()
	{
		var light = Spawn(new Vector3(-3, 0.05f, 10));
		var heavy = Spawn(new Vector3(3, 0.05f, 10), new UnitPhysicsProfile { Mass = 2 });
		await Tick(3);
		light.ReceiveKnockback(Vector3.Right * 6); heavy.ReceiveKnockback(Vector3.Right * 6);
		await Tick();
		Check(Mathf.Abs(light.Movement!.ExternalVelocity.X - heavy.Movement!.ExternalVelocity.X * 2) < 0.001f, "相同战斗冲量质量2只获得一半速度");
		heavy.KnockbackResistance = 0.5f; heavy.Movement.ConfigurePhysics(new UnitPhysicsProfile { Mass = 2 });
		heavy.ReceiveKnockback(Vector3.Right * 8); await Tick();
		Check(Mathf.Abs(heavy.Movement.ExternalVelocity.X - 1.8f) < 0.001f, "质量与击退抗性分别相乘");
		await Clear();
		var vertical = Spawn(new Vector3(0, 10, 10)); var movement = vertical.Movement!;
		movement.GravityScale = 0; movement.ImpulseDamping = 0;
		movement.AddImpulse(Vector3.Up * 6); await Tick(10);
		Check(Mathf.Abs(vertical.Velocity.Y - 6) < 0.001f, $"垂直冲量不逐帧重复累计，速度={vertical.Velocity.Y:0.###}");
		Check(Mathf.Abs(vertical.GlobalPosition.Y - 11) < 0.08f, "垂直实际位移遵守速度×时间");
		movement.ConfigurePhysics(null); vertical.Velocity = Vector3.Zero;
		object a = new(), b = new(); int damaged = 0; vertical.Damaged += (_, _) => damaged++;
		vertical.ReceiveForce(a, Vector3.Right * 12, 0.5f); vertical.ReceiveForce(b, Vector3.Right * 6, 1);
		await Tick(60);
		Check(Mathf.Abs(movement.ExternalVelocity.X - 12) < 0.005f, "不同来源持续力叠加并按各自时限结束");
		await Tick(10); Check(Mathf.Abs(movement.ExternalVelocity.X - 12) < 0.005f, "持续力到期不继续加速");
		Check(damaged == 0 && vertical.Health == 100, "冲量与持续力不制造伤害事件");
		movement.ConfigurePhysics(null); vertical.Velocity = Vector3.Zero;
		movement.SetForce(a, Vector3.Right * 60, 1); movement.SetForce(a, Vector3.Right * 6, 0.1f); await Tick(6);
		Check(Mathf.Abs(movement.ExternalVelocity.X - 0.6f) < 0.001f, "同来源刷新持续力而非叠加");
		movement.SetForce(a, Vector3.Right * 60, 1); movement.RemoveForce(a); await Tick();
		Check(Mathf.Abs(movement.ExternalVelocity.X - 0.6f) < 0.001f, "来源可提前移除持续力");
		movement.ConfigurePhysics(null); vertical.Velocity = Vector3.Zero;
		movement.SetForce(a, Vector3.Right * 60, 0.005f); await Tick();
		Check(Mathf.Abs(movement.ExternalVelocity.X - 0.3f) < 0.001f, "持续力末帧只积分实际剩余时长");
		await Clear();
		var wall = Spawn(new Vector3(6, 0.05f, 0)); wall.Movement!.ImpulseDamping = 0;
		wall.ReceiveKnockback(Vector3.Right * 20); await Tick(20);
		Check(wall.GlobalPosition.X < 7.4f && Mathf.Abs(wall.Movement.ExternalVelocity.X) < 0.001f, "实体墙消去朝墙内的残留冲量");
	}
	private async Task Heights()
	{
		var flyer = Spawn(new Vector3(0, 0.05f, 0), Flight()); var movement = flyer.Movement!;
		await Tick(120);
		Check(movement.FlightState == CharacterMovement.FlightPhase.Flying && Mathf.Abs(flyer.GlobalPosition.Y - 3) < 0.1f, $"从地面平滑起飞并悬停 y={flyer.GlobalPosition.Y:0.###}");
		Check(!movement.TryJump() && flyer.CollisionMask == CombatLayers.BodyMask, "飞行不消耗跳跃且保持共用身体层");
		object weak = new(), strong = new();
		flyer.ReceiveHeightSuppression(weak, 2, 5); flyer.ReceiveHeightSuppression(strong, 0.7f, 1.5f); await Tick(75);
		Check(flyer.GlobalPosition.Y < 1 && movement.EffectiveHoverHeight == 0.7f, "多个压低效果取最低高度而非相加");
		await Tick(75);
		Check(Mathf.Abs(flyer.GlobalPosition.Y - 2) < 0.15f && movement.EffectiveHoverHeight == 2, "较强压低到期后仍受另一个来源限制");
		movement.RemoveHeightSuppression(weak); await Tick(120);
		Check(Mathf.Abs(flyer.GlobalPosition.Y - 3) < 0.1f, "移除压低后平滑回升到原悬停高度");
		flyer.ReceiveKnockback(Vector3.Down * 8); await Tick(6);
		Check(flyer.GlobalPosition.Y < 2.9f, "普通向下冲量实际压低飞行身体");
		await Tick(120); Check(Mathf.Abs(flyer.GlobalPosition.Y - 3) < 0.1f, "普通冲量消退后飞行恢复悬停");
		var platform = Spawn(new Vector3(-10, 2.05f, 0), Flight()); await Tick(120);
		Check(Mathf.Abs(platform.GlobalPosition.Y - 5) < 0.12f, "悬停高度相对真实World平台而非绝对世界Y");
	}
	private async Task Knockdown()
	{
		var flyer = Spawn(new Vector3(0, 3, 0), Flight()); var movement = flyer.Movement!; await Tick(3);
		int landed = 0, jumps = 0; float speed = 0;
		movement.KnockdownLanded += value => { landed++; speed = value; }; movement.JumpStarted += _ => jumps++;
		var ability = new PhysicsControlProbeAbility { Name = "TestAction" }; flyer.AddChild(ability); ability.SetPhysicsProcess(false);
		Check(ability.TryActivate() && movement.HasVelocityOverride, "真实能力起手并持有速度覆盖");
		Check(flyer.ReceiveKnockDown(10) && !ability.IsActive && !movement.HasVelocityOverride, "打落经能力收尾释放原动作与速度所有权");
		Check(!ability.TryActivate() && !movement.TryJump(), "落下期间拒绝主动技能和跳跃");
		movement.SetIntent(Vector3.Right, 6); await Tick(6);
		Check(flyer.GlobalPosition.Y < 2.2f && Mathf.Abs(flyer.GlobalPosition.X) < 0.01f, "打落关闭升力及主动移动，真正向地面下降");
		for (int i = 0; i < 120 && landed == 0; i++) await Tick();
		Check(landed == 1 && speed >= 10 && movement.FlightState == CharacterMovement.FlightPhase.Recovering, "实际着地才发出一次带冲击速度的落地事件");
		await Tick(10); Check(movement.FlightState == CharacterMovement.FlightPhase.Recovering && flyer.GlobalPosition.Y < 0.1f, "着地恢复期内保持地面状态");
		movement.Stop(); await Tick(150);
		Check(landed == 1 && jumps == 0 && movement.FlightState == CharacterMovement.FlightPhase.Flying && Mathf.Abs(flyer.GlobalPosition.Y - 3) < 0.1f, "恢复期结束重新起飞，无伪造起跳或重复落地事件");
	}
	private async Task Contracts()
	{
		var flyer = Spawn(new Vector3(0, 3, 10), Flight()); var movement = flyer.Movement!;
		flyer.IsInvulnerable = true;
		Check(!flyer.ReceiveKnockDown(10) && !flyer.ReceiveHeightSuppression(this, 1, 1) && !flyer.ReceiveForce(this, Vector3.Down, 1), "无敌阻止战斗控制与持续战斗力");
		flyer.IsInvulnerable = false;
		movement.PhysicsProfile!.ControlImmune = true;
		Check(!flyer.ReceiveKnockDown(10) && !flyer.ReceiveKnockback(Vector3.Right) && !flyer.ReceiveHeightSuppression(this, 1, 1), "控制免疫与质量／抗性独立");
		movement.SetForce(this, Vector3.Right * 6, 0.1f); await Tick(6);
		Check(movement.ExternalVelocity.X > 0, "环境力可独立作用于战斗控制免疫单位");
		movement.ConfigurePhysics(Flight()); movement.PhysicsProfile!.CanBeKnockedDown = false;
		Check(!flyer.ReceiveKnockDown(10) && flyer.ReceiveHeightSuppression(this, 1, 1), "禁止打落可单独配置，仍可压低高度");
		movement.ConfigurePhysics(null); await Tick(120);
		Check(!movement.CanFly && movement.FlightState == CharacterMovement.FlightPhase.Ground && flyer.GlobalPosition.Y < 0.1f && movement.ExternalVelocity.IsZeroApprox(), "切回地面清理力、压低和飞行状态并自然落地");
		Check(!flyer.ReceiveKnockDown(10) && !flyer.ReceiveHeightSuppression(this, 1, 1), "地面单位拒绝飞行专用控制");
		var row = new EnemyConfig { PhysicsProfile = Flight() }; var fresh = new Unit(); fresh.AddChild(new CharacterMovement { Name = "Movement" }); row.ApplyPhysicsTo(fresh);
		Check(fresh.GetNode<CharacterMovement>("Movement").CanFly, "单位表可在入树前套用物理档案"); fresh.Free();
		Check(RuntimeResources.Load<UnitPhysicsProfile>("res://Config/Physics/flight_standard.tres").CanFly && RuntimeResources.Load<UnitPhysicsProfile>("res://Config/Physics/ground_heavy.tres").Mass == 3, "可复用飞行与重型地面资源可实际加载");
		await Clear();
		var player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>();
		player.GetNode<CharacterMovement>("Movement").PhysicsProfile = Flight(); AddChild(player); _units.Add(player);
		player.GlobalPosition = new Vector3(0, 3, 10);
		// 只暂停回调；禁用整个CollisionObject的ProcessMode会把身体移出物理空间。
		FreezeCallbacks(player);
		var loadout = new PlayerLoadout(); loadout.Apply(player, this, PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
		Check(player.Movement!.CanFly, "角色表未配物理档案时保留玩家场景飞行回退");
		player.ReceiveHeightSuppression(this, 0.5f, 10); player.ReceiveForce(this, Vector3.Right * 6, 10); await Tick(3);
		loadout.Apply(player, this, PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm); await Tick();
		Check(player.Movement.ExternalVelocity.IsZeroApprox() && player.Movement.EffectiveHoverHeight == 3, "真实换装清理临时持续力与压低来源");
	}
	private static void FreezeCallbacks(Node node)
	{
		node.SetPhysicsProcess(false); node.SetProcess(false);
		foreach (Node child in node.GetChildren()) FreezeCallbacks(child);
	}
	private async Task Boundaries()
	{
		Solid("Ceiling", new Vector3(20, 3, 0), new Vector3(6, 1, 6)); await Frame();
		var flyer = Spawn(new Vector3(20, 0.05f, 0), Flight()); await Tick(120);
		Check(flyer.GlobalPosition.Y < 0.8f && flyer.GlobalPosition.Y > 0.4f, "悬停升力被真实天花板阻挡，不穿模到目标高度");
		await Clear();
		flyer = Spawn(new Vector3(0, 3, 0), Flight()); Spawn(new Vector3(0, 0.05f, 0)); await Tick(120);
		Check(Mathf.Abs(flyer.GlobalPosition.Y - 3) < 0.1f, "飞行探地只查World，不把其他单位当地板逐级抬高");
		await Clear();
		flyer = Spawn(new Vector3(60, 5, 0), Flight()); await Tick(30);
		Check(Mathf.Abs(flyer.GlobalPosition.Y - 5) < 0.1f, "无探地结果时保持最后参考高度，初始回退到生成高度");
		int landed = 0; flyer.Movement!.KnockdownLanded += _ => landed++; flyer.ReceiveKnockDown(10); await Tick(120);
		Check(landed == 0 && flyer.Movement.FlightState == CharacterMovement.FlightPhase.Falling && flyer.GlobalPosition.Y < -10, "悬崖打落不凭空触地或按空中计时重新飞起");
		await Clear();
		var slow = Spawn(new Vector3(-5, 5, 10)); var fast = Spawn(new Vector3(5, 5, 10));
		slow.Movement!.ImpulseDamping = fast.Movement!.ImpulseDamping = 0;
		slow.Movement.SetForce(this, Vector3.Right * 6, 1); fast.Movement.SetForce(this, Vector3.Right * 6, 1);
		for (int i = 0; i < 60; i++) { await Frame(); fast.Movement._PhysicsProcess(1d / 60d); if (i % 2 == 0) slow.Movement._PhysicsProcess(1d / 30d); }
		Check(Mathf.Abs(slow.Movement.ExternalVelocity.X - fast.Movement.ExternalVelocity.X) < 0.001f && Mathf.Abs(fast.Movement.ExternalVelocity.X - 6) < 0.001f, "持续力积分在30／60步长下相同");
		Check(!fast.ReceiveKnockback(new Vector3(float.NaN, 0, 0)) && !fast.ReceiveForce(this, Vector3.Up, float.NaN), "非法受力输入不污染物理速度");
	}
}

public partial class PhysicsControlProbeAbility : ActRoguelike.Abilities.Ability
{
	protected override void OnActivate(Unit? target) => OwnerUnit!.Movement!.BeginVelocityOverride(this, Vector3.Forward);
	protected override void OnFinishOrCancel() => OwnerUnit!.Movement!.EndVelocityOverride(this);
}

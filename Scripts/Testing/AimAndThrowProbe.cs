using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>当前槽位真实输入：立体瞄准、预览显隐、半途召回路径伤害及悬停伤害快照。</summary>
public partial class AimAndThrowProbe : Node3D
{
	private int _pass, _fail;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private readonly List<Unit> _targets = new();
	private void Check(bool ok, string label) { if (ok) _pass++; else _fail++; GD.Print($"[AimThrow] {(ok ? "PASS" : "FAIL")} {label}"); }
	private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	private async Task Wait(float seconds)
	{
		float elapsed = 0;
		while (elapsed < seconds) { await Frame(); elapsed += (float)GetProcessDeltaTime(); }
	}
	private async Task Until(Func<bool> condition, float seconds = 4f)
	{
		float elapsed = 0;
		while (!condition() && elapsed < seconds) { await Frame(); elapsed += (float)GetProcessDeltaTime(); }
	}
	private void Press(bool held) => Input.ParseInputEvent(new InputEventAction { Action = "weapon_skill_2", Pressed = held });
	private async Task Equip(WeaponPresetKind weapon)
	{
		Press(false); await Frame(); await Frame();
		foreach (var target in _targets) if (IsInstanceValid(target)) target.QueueFree();
		_targets.Clear();
		new PlayerLoadout().Apply(_player, this, weapon >= WeaponPresetKind.SpiritSword ? PlayerCharacterKind.WeaponMaster : PlayerCharacterKind.Hunter, weapon);
		_player.GlobalPosition = new Vector3(0, .05f, 0); _player.Velocity = Vector3.Zero; _player.Rotation = Vector3.Zero;
		_player.Stats.SetBase("CritChanceAdd", -100); _player.Stats.SetBase("DamageMultiplier", 1.5f);
		_player.ResetPhysicsInterpolation(); Aim(new Vector3(0, 5, -8)); await Wait(.1f);
	}
	private void Aim(Vector3 point)
	{
		_camera.GlobalPosition = new Vector3(0, 2, 6); _camera.LookAt(point); _camera.ResetPhysicsInterpolation(); _camera.MakeCurrent();
	}
	private Unit Target(Vector3 point)
	{
		// 入树前设置位置，避免靶子先在玩家脚下注册，再瞬移时产生移动平台带人效果。
		var target = new Unit { Position = point, Faction = Faction.Enemy, MaxHealth = 10000, KnockbackResistance = 1 };
		target.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = .45f } });
		target.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = .45f, Height = .9f }, MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.Coral } });
		AddChild(target); _targets.Add(target); return target;
	}
	private Vector3[] Points(SpearThrowAbility spear)
	{
		var path = spear.GetNode<DashPathIndicator>("Path");
		return path.GetNode<MeshInstance3D>("Path").Mesh!.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array()
			.Select(p => p - Vector3.Up * path.GroundOffset).ToArray();
	}
	private async Task Capture(string name, string text)
	{
		if (DisplayServer.GetName() == "headless") return;
		var layer = new CanvasLayer(); layer.AddChild(new Label { Text = text, Position = new Vector2(24, 24) }); AddChild(layer);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		using var picture = GetViewport().GetTexture().GetImage();
		Check(picture.SavePng($"res://docs/screenshots/{name}.png") == Error.Ok, $"截图 {name}");
		layer.QueueFree();
	}
	public override async void _Ready()
	{
		try
		{
			var floor = new StaticBody3D { Position = Vector3.Down * .5f, CollisionLayer = CombatLayers.World, CollisionMask = 0 };
			floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(100, 1, 100) } });
			floor.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(100, 1, 100) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(.12f, .16f, .21f) } }); AddChild(floor);
			AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-60, -25, 0) });
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera); await Wait(.1f);
			await SpearAim(); await CrossbowAim(); await ScytheDamage();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		finally { Press(false); }
		GD.Print($"[AimThrow] === 立体瞄准与投掷验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task SpearAim()
	{
		await Equip(WeaponPresetKind.Spear);
		var spear = (SpearThrowAbility)_player.AbilitySlots.Resolve("weapon_skill_2")!;
		var path = spear.GetNode<DashPathIndicator>("Path");
		Aim(new Vector3(0, 14, -18)); Press(true); await Until(() => spear.ChargeTime > .15f);
		Check(path.IsShowing && !spear.ChargeBaseComplete, "右键按住即显示预览，不等待蓄力完成");
		var first = Points(spear);
		Check(first[^1].Y > first[0].Y + 3, "抬头看天空时，抛物线目标随准星升高");
		Aim(new Vector3(5, 8, -12)); await Wait(.08f);
		var moved = Points(spear);
		Check(moved[^1].X > 1 && moved[^1].DistanceTo(first[^1]) > 1, "保持右键转镜头，预览实时跟随三维准星");
		await Until(() => spear.ChargeTime >= 1.04f);
		var preview = Points(spear);
		await Capture("j40_spear_aim", "长虹贯日：抬头蓄力，空间路径随准星更新；松手立即隐藏");
		Press(false); await Frame(); await Frame();
		Check(!path.IsShowing && spear.IsActive, "松手立即隐藏路径，技能仍可处于释放或飞行阶段");
		await Until(() => spear.InFlight);
		Check(spear.InFlight && spear.TargetPoint.Y > 3 && spear.WeaponVelocity.Y > 0, "真实ATK抛出的长枪保留向上速度和空中目标");
		Check(spear.TargetPoint.DistanceTo(preview[^1]) < .03f, "预览末点与释放目标一致，不再投到地面");
		Check(spear.LastAimDistance <= spear.LastMaxDistance + .001f, "天空远端按三维射程截断，垂直瞄准也不产生无限目标");
		float height = spear.WeaponPosition.Y; await Wait(.15f);
		Check(spear.WeaponPosition.Y > height + .5f && !path.IsShowing, "真实飞行向上，离手期间路径不残留");
		Press(true); await Until(() => spear.IsReturning);
		Check(spear.IsReturning && !path.IsShowing, "再次右键召回不会重显抛掷预览");
		Press(false); await Until(() => !spear.InFlight);
		Check(!spear.InFlight && !path.IsShowing, "真实回手后无路径残留");

		await Equip(WeaponPresetKind.Spear);
		var air = Target(new Vector3(0, 5, -6)); Aim(air.GlobalPosition); await Wait(.1f);
		Press(true); await Until(() => spear.ChargeTime >= 1.04f); Press(false); await Until(() => spear.InFlight);
		Check(spear.TargetPoint.Y > 4, "准星射线识别敌方身体，瞄准空中靶而非背后地面");
		await Until(() => air.Health < air.MaxHealth);
		Check(air.Health < air.MaxHealth, "长枪实际弹道与空中靶发生伤害碰撞");
		await Equip(WeaponPresetKind.Spear);
		_player.Rotation = new Vector3(0, .7f, 0);
		Aim(new Vector3(0, 50, 5.9f)); Press(true); await Until(() => spear.ChargeTime >= 1.04f);
		var verticalPreview = Points(spear); Press(false); await Until(() => spear.InFlight);
		Check(spear.InFlight && spear.WeaponVelocity.IsFinite() && spear.WeaponVelocity.Length() < 100
			&& spear.TargetPoint.Y > 10 && spear.LastAimDistance <= spear.LastMaxDistance + .001f, "接近垂直瞄准仍有有界目标和有限速度");
		Check(spear.TargetPoint.DistanceTo(verticalPreview[^1]) < .03f, "身体偏转且接近垂直时，预览和真实发射使用同一退化方向与出手位置");
		await Equip(WeaponPresetKind.Spear); Press(true); await Wait(.2f); Press(false); await Wait(.15f);
		Check(spear.LastReleaseEarly && !spear.LastReleaseAborted && !path.IsShowing, "提前松手接受低伤投掷并立即清理预览");
		await Equip(WeaponPresetKind.Spear);
		Check(spear.TryActivate(), "程序接口仍可启动独立蓄力"); spear.OnInputHeld(.2f); await Wait(.1f);
		Check(spear.IsCharging && !path.IsShowing, "程序驱动蓄力没有玩家按住输入，不自行打开玩家指示器");
		await Equip(WeaponPresetKind.Spear); Press(true); await Wait(.2f);
		Check(path.IsShowing, "换装清理检查前预览真实打开");
		new PlayerLoadout().Apply(_player, this, PlayerCharacterKind.Hunter, WeaponPresetKind.Crossbow); await Wait(.1f);
		Check(!path.IsShowing && !_player.AbilitySlots.Resolve("weapon_skill_2")!.IsActive, "按住中换装清理旧预览，输入不穿越到新右键槽"); Press(false);
	}
	private async Task CrossbowAim()
	{
		await Equip(WeaponPresetKind.Crossbow);
		var volley = (ArrowVolleyAbility)_player.AbilitySlots.Resolve("weapon_skill_2")!;
		var air = Target(new Vector3(0, 6, -8)); Aim(air.GlobalPosition); await Wait(.1f);
		var shots = new List<Projectile>();
		void Added(Node node) { if (node is Projectile shot) { shot.SetPhysicsProcess(false); shots.Add(shot); } }
		GetTree().NodeAdded += Added;
		try
		{
			Press(true); await Until(() => volley.ChargeTime >= .43f); Press(false); await Until(() => shots.Count == 3);
			Check(volley.Aim == Ability.AimSource.Crosshair && volley.LastShotDirection.Y > .2f, $"飞刀右键真实配装为准星瞄准，保留俯仰：{volley.LastShotDirection} player={_player.GlobalPosition} target={air.GlobalPosition}");
			Check(shots.Count == 3 && volley.LastShotCount == 3, "基础蓄力依旧由真实ATK发出三支散射箭");
			Check(shots.Count == 3 && shots[1].VelocityDirection.Dot(volley.LastShotDirection) > .9999f && shots.All(p => p.VelocityDirection.Y > .1f), "实际中央箭沿准星飞行，整排箭保留向上分量");
			if (DisplayServer.GetName() != "headless")
			{
				// 从真实弹体的初速画参考线，侧面观察整排散射与高处靶；不改弹体位置或速度。
				var lines = new List<DashPathIndicator>();
				foreach (var shot in shots)
				{
					var line = new DashPathIndicator(); AddChild(line); line.Configure(Colors.Gold, .06f, 0);
					line.Show(DashPathIndicator.SampleLine(shot.GlobalPosition, shot.VelocityDirection, 8, 2)); lines.Add(line);
				}
				Transform3D before = _camera.GlobalTransform;
				_camera.GlobalPosition = new Vector3(10, 7, 5); _camera.LookAt(new Vector3(0, 3, -4)); _camera.ResetPhysicsInterpolation();
				await Capture("j40_crossbow_aim", "飞刀右键：高处敌方靶；金线为三支真实飞刀的初速方向（8米参考线）");
				_camera.GlobalTransform = before; _camera.ResetPhysicsInterpolation(); foreach (var line in lines) line.QueueFree();
			}
			foreach (var shot in shots) if (IsInstanceValid(shot)) shot.SetPhysicsProcess(true);
			await Until(() => air.Health < air.MaxHealth);
			Check(air.Health < air.MaxHealth, "中央飞刀实际命中高处敌方靶");
		}
		finally { GetTree().NodeAdded -= Added; foreach (var shot in shots) if (IsInstanceValid(shot)) shot.QueueFree(); }
		await Equip(WeaponPresetKind.Crossbow); Aim(new Vector3(0, 0, -4));
		int downShots = 0; void DownAdded(Node node) { if (node is Projectile) downShots++; }
		GetTree().NodeAdded += DownAdded;
		try { Press(true); await Until(() => volley.ChargeTime >= .43f); Press(false); await Until(() => downShots == 3); }
		finally { GetTree().NodeAdded -= DownAdded; }
		Check(volley.LastShotDirection.Y < -.05f, "向下瞄准时飞刀右键也保留向下分量");
	}
	private async Task ScytheDamage()
	{
		await Equip(WeaponPresetKind.Scythe);
		var scythe = (ScytheThrowAbility)_player.AbilitySlots.Resolve("weapon_skill_2")!;
		var near = Target(new Vector3(0, .95f, -3.2f)); var far = Target(new Vector3(0, .95f, -7.8f));
		var hits = new List<HitInstance>(); near.HitReceived += hit => hits.Add(hit); await Wait(.1f);
		Press(true); await Until(() => scythe.InFlight); Press(false);
		Check(scythe.TravelPathDamage && Mathf.IsEqualApprox(scythe.TravelDamageScale, .8f), "真实镰刀表启用移动路径命中及80%伤害");
		await Until(() => hits.Count > 0, .3f);
		Check(hits.Count == 1 && !scythe.HasSettled, "未到首个0.5秒周期且仍在飞出时，沿途目标已经真实受伤");
		float expected = scythe.GetNode<AttackHitbox>("Attack").Damage * 1.5f * .8f;
		Check(hits.Count == 1 && Mathf.IsEqualApprox(hits[0].Damage, expected), $"飞出路径伤害降低且攻击倍率只乘一次：{(hits.Count > 0 ? hits[0].Damage : 0):0.###}/{expected:0.###}");
		await Wait(.06f); Press(true); await Until(() => scythe.IsReturning); Press(false);
		Check(scythe.IsReturning && !scythe.HasSettled, "真实右键可在尚未飞满射程时开始回手");
		await Until(() => !scythe.InFlight);
		Check(hits.Count == 2 && Mathf.Abs(near.MaxHealth - near.Health - expected * 2) < .01f, $"半途收回完整扫过回程，往返各一次低伤，不按帧重复扣血：hits={hits.Count} damage={near.MaxHealth - near.Health} player={_player.GlobalPosition}");
		Check(far.Health == far.MaxHealth, "提前收回不伤到武器尚未到达的远端目标");
		await Equip(WeaponPresetKind.Scythe);
		var stay = Target(new Vector3(0, .95f, -8.6f)); var pulseHits = new List<HitInstance>(); var times = new List<ulong>();
		stay.HitReceived += hit => { pulseHits.Add(hit); times.Add(Time.GetTicksMsec()); }; await Wait(.1f);
		Press(true); await Until(() => scythe.InFlight); Press(false);
		float rawDamage = scythe.GetNode<AttackHitbox>("Attack").Damage * 1.5f;
		_player.Stats.SetBase("DamageMultiplier", 3f);
		await Until(() => scythe.HasSettled); await Until(() => pulseHits.Count >= 3, 2f);
		Check(pulseHits.Count >= 3 && Mathf.IsEqualApprox(pulseHits[0].Damage, rawDamage * .8f)
			&& pulseHits.Skip(1).All(h => Mathf.IsEqualApprox(h.Damage, rawDamage)), $"先低伤路径命中，悬停周期恢复原伤害；飞出后增益不篡改冻结快照：hits={string.Join(',', pulseHits.Select(h => h.Damage))} pos={scythe.WeaponPosition} player={_player.GlobalPosition}");
		Check(times.Count >= 3 && times[2] - times[1] >= 450, "悬停周期仍为0.5秒，不叠加逐帧路径伤害");
		await Equip(WeaponPresetKind.Spear);
		Check(!scythe.InFlight && !scythe.GetNode<Node3D>("WeaponVisual").Visible, "换装清理镰刀飞行和低伤判定状态");
	}
}

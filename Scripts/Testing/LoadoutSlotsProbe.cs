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

/// <summary>真实玩家反复换装、技能槽输入隔离、长枪回收、世界路径与接触点可见性。</summary>
public partial class LoadoutSlotsProbe : Node3D
{
	private int _pass, _fail;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private readonly List<SlotCounterAbility> _foreign = new();
	private void Check(bool ok, string label) { if (ok) _pass++; else _fail++; GD.Print($"[Slots] {(ok ? "PASS" : "FAIL")} {label}"); }
	private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
	private async Task Wait(int frames) { for (int i = 0; i < frames; i++) await Frame(); }
	private async Task Until(Func<bool> condition, int frames = 180) { for (int i = 0; i < frames && !condition(); i++) await Frame(); }
	private async Task InputFrame() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
	private void Press(string action, bool down) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = down });
	private async Task Equip(WeaponPresetKind weapon)
	{
		Press("weapon_skill_1", false); Press("weapon_skill_2", false); Press("skill_1", false); Press("skill_2", false);
		// 故意每次新建配装器，覆盖用户发现的真实失败路径，不能只靠复用辅助对象避开问题。
		new PlayerLoadout().Apply(_player, this, weapon >= WeaponPresetKind.SpiritSword ? PlayerCharacterKind.WeaponMaster : PlayerCharacterKind.Hunter, weapon);
		_player.GlobalPosition = new Vector3(12, .05f, 5); _player.Velocity = Vector3.Zero; _player.ResetPhysicsInterpolation();
		_camera.GlobalPosition = new Vector3(12, 5, 16); _camera.LookAt(new Vector3(12, 0, -8)); _camera.ResetPhysicsInterpolation(); _camera.MakeCurrent(); await Wait(4);
	}
	public override async void _Ready()
	{
		try
		{
			await Wait(3);
			var floor = new StaticBody3D { Position = Vector3.Down * .5f, CollisionLayer = CombatLayers.World, CollisionMask = 0 };
			floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(100, 1, 100) } });
			floor.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(100, 1, 100), Material = new StandardMaterial3D { AlbedoColor = new Color(.12f, .16f, .21f) } } }); AddChild(floor);
			AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-60, -25, 0) });
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera);
			await Switching(); await SlotIsolation(); await Throws(); await Visibility();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		finally { foreach (string action in new[] { "weapon_skill_1", "weapon_skill_2", "skill_1", "skill_2", "fire" }) Press(action, false); }
		GD.Print($"[Slots] === 换装与技能槽验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task Switching()
	{
		var sequence = new[] { WeaponPresetKind.SpiritSword, WeaponPresetKind.Scythe, WeaponPresetKind.Spear, WeaponPresetKind.Bow, WeaponPresetKind.EnergyPalm, WeaponPresetKind.Crossbow };
		for (int round = 0; round < 2; round++) foreach (var weapon in round == 0 ? sequence : sequence.Reverse())
		{
			await Equip(weapon); var slots = _player.AbilitySlots;
			Check(slots.Inputs.Count == (weapon >= WeaponPresetKind.SpiritSword ? 5 : 4) && slots.Resolve("weapon_skill_1") != null && slots.Resolve("weapon_skill_2") != null, $"{round}/{weapon}当前主动槽唯一且就绪（武器大师另有下落槽）");
			var all = new List<Ability>(); NodeSearch.CollectInSubtree(_player, all);
			Check(all.All(a => !a.Enabled || slots.Owns(a)), $"{round}/{weapon}仓库技能没有残留启用");
			Check(!all.OfType<WeaponThrowAbility>().Any(a => a.InFlight), $"{round}/{weapon}换装后没有旧投掷状态");
		}
		await Equip(WeaponPresetKind.EnergyPalm);
		var left = (EnergyWaveAbility)_player.AbilitySlots.Resolve("weapon_skill_1")!;
		int shots = 0; void Added(Node node) { if (node is Projectile) shots++; } GetTree().NodeAdded += Added;
		Press("fire", true); Press("weapon_skill_1", true); await Wait(25); Press("fire", false); Press("weapon_skill_1", false); await Wait(3);
		GetTree().NodeAdded -= Added;
		Check(shots == 1 && left.LastShotDamage > 0, "fire与左键技能别名同帧，只发一个真实能量掌弹体");
	}
	private async Task SlotIsolation()
	{
		await Equip(WeaponPresetKind.EnergyPalm);
		for (int i = 0; i < 100; i++) { var fake = new SlotCounterAbility { Name = "Stored" + i, AbilityId = "stored_" + i, InputAction = "skill_1", Enabled = true }; _player.AddChild(fake); _foreign.Add(fake); }
		Press("skill_1", true); await Until(() => _player.GetNode<BackdashAbility>("AbilitySkill1").IsActive, 30); Press("skill_1", false);
		Check(_player.GetNode<BackdashAbility>("AbilitySkill1").IsActive && _foreign.Sum(a => a.PressCount) == 0, "100个启用同键仓库技能存在时，Q只执行当前后撤槽"); await Wait(40);
		void Bind(int index)
		{
			var row = new AbilityConfig { Id = "stored_" + index, NodeName = "Stored" + index, InputAction = "skill_2" };
			row.ApplyTo(_foreign[index]); _player.AbilitySlots.Clear(); _player.AbilitySlots.AssignToSlot(_player, "skill_1", row);
			_player.GetNode<PlayerAbilityInput>("AbilityInput").ResetForLoadout();
		}
		Bind(0); _foreign[0].InputAction = "skill_2";
		Press("skill_1", true); await InputFrame(); Press("skill_1", false); await InputFrame();
		Check(_foreign[0].PressCount == 1 && _foreign.Skip(1).Sum(a => a.PressCount) == 0, "槽位索引决定执行对象，不受技能仓库自身InputAction改写影响");
		var input = _player.GetNode<PlayerAbilityInput>("AbilityInput"); input.SetProcess(false);
		Press("skill_1", true); await InputFrame(); Check(input.PendingPressCount > 0, "真实按下已进入输入队列，尚未消费");
		Bind(1); input.SetProcess(true); await InputFrame();
		Check(_foreign[1].PressCount == 0 && !_foreign[1].IsActive, "换槽前排队的按下／按住不穿越到新技能");
		Press("skill_1", false); await InputFrame(); Press("skill_1", true); await InputFrame(); Press("skill_1", false);
		Check(_foreign[1].PressCount == 1 && _foreign[0].PressCount == 1, "松开再按后，仅执行新槽位技能");
		await Equip(WeaponPresetKind.Spear); Check(_foreign.All(a => !a.Enabled), "重新配装后100个仓库技能全部退出活动集合");
	}
	private async Task Throws()
	{
		await Equip(WeaponPresetKind.Spear); var manual = (SpearThrowAbility)_player.AbilitySlots.Resolve("weapon_skill_2")!;
		manual.TryActivate(); manual.OnInputHeld(.1f); await Wait(5);
		Check(manual.IsActive && manual.IsCharging, "程序接口启动的蓄力不被输入层误认为物理松手");
		await Equip(WeaponPresetKind.Scythe); var scythe = (ScytheThrowAbility)_player.AbilitySlots.Resolve("weapon_skill_2")!;
		Press("weapon_skill_2", true); await Until(() => scythe.InFlight); Press("weapon_skill_2", false);
		Check(scythe.InFlight, "镰刀先真实抛出，覆盖在飞时切武器");
		await Equip(WeaponPresetKind.Spear); var spear = (SpearThrowAbility)_player.AbilitySlots.Resolve("weapon_skill_2")!;
		Check(!scythe.Enabled && !scythe.InFlight && spear.SpinDegreesPerSecond == 0, "切长枪时镰刀飞行清理，长枪保留不自转配置");
		scythe.Enabled = true; scythe.InputAction = "weapon_skill_2";
		Press("weapon_skill_2", true); await Until(() => spear.ChargeTime >= 1.25f);
		var path = spear.GetNode<DashPathIndicator>("Path"); var mesh = path.GetNode<MeshInstance3D>("Path");
		Check(path.IsShowing && mesh.TopLevel && mesh.GlobalTransform.IsEqualApprox(Transform3D.Identity), "长枪右键蓄力路径在世界坐标绘制，角色偏移与旋转不二次叠加");
		var vertices = mesh.Mesh!.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
		Vector3 flat = AimUtil.GetCrosshairDirection(GetViewport(), Vector3.Forward); flat.Y = 0; flat = flat.Normalized();
		Vector3 expected = _player.GlobalPosition + Vector3.Up * (.9f + path.GroundOffset) + flat * .6f;
		Check(vertices.Length > 2 && vertices[0].DistanceTo(expected) < .1f, "平移到12米外的真实玩家，预览起点仍贴实际出手位置");
		if (DisplayServer.GetName() != "headless")
		{
			await InputFrame(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			Check(GetViewport().GetTexture().GetImage().SavePng("res://docs/screenshots/j39_spear_preview.png") == Error.Ok, "长枪真实蓄力路径截图保存");
		}
		var hitTarget = Box(vertices[Mathf.Min(5, vertices.Length - 1)] - Vector3.Up); hitTarget.ApplyBaseStats(1000, 0, 0, 0);
		await Wait(3);
		int pauseCount = _player.ImpactPause!.AcceptedCount;
		Press("weapon_skill_2", false); await Until(() => spear.InFlight);
		Check(spear.InFlight && !scythe.InFlight && !scythe.IsActive, "释放蓄力只抛长枪，旧镰刀即使同键启用也不执行");
		await Until(() => hitTarget.Health < 1000, 60);
		Check(hitTarget.Health < 1000 && _player.ImpactPause!.AcceptedCount == pauseCount, "真实脱手长枪命中仍扣血，不让玩家进入近战顿帧"); hitTarget.QueueFree();
		await Wait(8); int recalls = spear.RecallCount; Press("weapon_skill_2", true); await InputFrame(); Press("weapon_skill_2", false);
		await Until(() => spear.IsReturning || spear.RecallCount == recalls + 1, 30);
		Check((spear.IsReturning || spear.RecallCount == recalls + 1) && !scythe.InFlight && !scythe.IsActive, "再次右键只召回长枪，不启动旋转镰刀逻辑");
		await Until(() => !spear.InFlight); Check(!spear.InFlight && spear.RecallCount == recalls + 1 && !path.IsShowing, "长枪真实回手并清理路径");
		await Equip(WeaponPresetKind.Spear); Press("weapon_skill_2", true); await Wait(10);
		new PlayerLoadout().Apply(_player, this, PlayerCharacterKind.Hunter, WeaponPresetKind.Crossbow); await Wait(4);
		Check(!_player.AbilitySlots.Resolve("weapon_skill_2")!.IsActive, "蓄力按住中换角色，新右键不继承旧蓄力");
		Press("weapon_skill_2", false); await Wait(4);
		Check(!_player.AbilitySlots.Resolve("weapon_skill_2")!.IsActive && !spear.InFlight, "旧右键松开不释放新角色技能或残留长枪");
	}
	private Unit Box(Vector3 point)
	{
		var unit = new Unit { Faction = Faction.Enemy }; unit.AddChild(new CollisionShape3D { Position = Vector3.Up, Shape = new BoxShape3D { Size = Vector3.One * 2 } });
		unit.AddChild(new MeshInstance3D { Position = Vector3.Up, Mesh = new BoxMesh { Size = Vector3.One * 2, Material = new StandardMaterial3D { AlbedoColor = new Color(.4f, .48f, .57f) } } }); AddChild(unit); unit.GlobalPosition = point; return unit;
	}
	private async Task Visibility()
	{
		_player.QueueFree(); await Wait(5); _camera.Position = new Vector3(0, 3, 10); _camera.Fov = 45; _camera.LookAt(Vector3.Up); _camera.ResetPhysicsInterpolation(); await Wait(120);
		var target = Box(Vector3.Zero); Vector3 point = new Vector3(.6f, 1.3f, -1.01f); await Wait(3);
		var core = ImpactVisibility.Spawn(target, point, ImpactKind.Flesh);
		Check(core != null && core.GlobalPosition == point, "目标模型背面命中点保留原坐标，独立亮核可显示");
		Check(core?.GetChild<MeshInstance3D>(0).Mesh?.SurfaceGetMaterial(0) is StandardMaterial3D { NoDepthTest: true }, "仅短时接触亮核越过目标深度，外围粒子保持正常深度");
		var wall = new StaticBody3D { Position = new Vector3(0, 1.5f, 4), CollisionLayer = CombatLayers.World, CollisionMask = 0 };
		wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(5, 4, .3f) } }); AddChild(wall); await Wait(3);
		Check(ImpactVisibility.Spawn(target, point, ImpactKind.Flesh) == null, "World墙体挡住视线时不创建穿墙亮核");
		wall.QueueFree(); target.QueueFree(); await Wait(4);
		if (DisplayServer.GetName() == "headless") return;
		var layer = new CanvasLayer(); AddChild(layer); layer.AddChild(new Label { Position = new Vector2(24, 22), Text = "接触点可见性对照：命中点均在模型背面\n左：原正常深度火花；右：原火花＋短时接触亮核（墙体遮挡仍有效）" });
		float duration = CombatFeedback.Profile.ContactCoreSeconds; CombatFeedback.Profile.ContactCoreSeconds = .4f;
		try
		{
			for (int i = 0; i < 2; i++) { var box = Box(new Vector3((i == 0 ? -1 : 1) * 1.8f, 0, 0)); Vector3 hit = box.GlobalPosition + point;
				CombatImpactFx.Spawn(this, hit, Vector3.Forward, ImpactKind.Block, 1); if (i == 1) ImpactVisibility.Spawn(box, hit, ImpactKind.Block); }
			await InputFrame(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			Check(GetViewport().GetTexture().GetImage().SavePng("res://docs/screenshots/j39_contact_visibility.png") == Error.Ok, "接触点遮挡对照截图保存");
		}
		finally { CombatFeedback.Profile.ContactCoreSeconds = duration; }
	}
}

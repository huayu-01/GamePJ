using System;
using System.Collections.Generic;
using System.Linq;
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

/// <summary>从真实配装、输入与动画ATK采样弹体初速，验证固定散射角及进化边界。</summary>
public partial class KnifeVolleyProbe : Node3D
{
	private int _pass, _fail, _atks;
	private double _elapsed;
	private Unit _player = null!;
	private Camera3D _camera = null!;
	private readonly List<Projectile> _shots = new();
	private readonly List<Unit> _targets = new();
	private ArrowVolleyAbility Volley => (ArrowVolleyAbility)_player.AbilitySlots.Resolve("weapon_skill_2")!;
	public override void _Process(double delta) => _elapsed += delta;
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[KnifeVolley] {(ok ? "PASS" : "FAIL")} {text}"); }
	private void Added(Node node)
	{
		// 仅暂停已生成弹体的后续运动，保留Launch真实写入的速度；追踪不会污染初速测量。
		if (node is Projectile shot) { shot.SetPhysicsProcess(false); _shots.Add(shot); }
	}
	private void Event(string name, ulong id) { if (name == "ATK") _atks++; }
	private void Press(string action, bool held) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = held });
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	private async Task<bool> Until(Func<bool> predicate, double seconds = 4)
	{
		double start = _elapsed;
		while (!predicate() && _elapsed - start < seconds) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		return predicate();
	}
	private async Task Equip(WeaponPresetKind weapon = WeaponPresetKind.Crossbow)
	{
		Press("weapon_skill_1", false); Press("weapon_skill_2", false);
		foreach (var shot in _shots) if (IsInstanceValid(shot)) shot.QueueFree();
		foreach (var target in _targets) if (IsInstanceValid(target)) target.QueueFree();
		await Wait(.05); _shots.Clear(); _targets.Clear(); _atks = 0;
		new PlayerLoadout().Apply(_player, this, PlayerCharacterKind.Hunter, weapon);
		_player.GlobalPosition = new Vector3(20, .05f, 20); _player.Velocity = Vector3.Zero;
		_player.Rotation = Vector3.Zero; _player.ResetPhysicsInterpolation();
		Aim(Vector3.Forward); await Wait(.1);
	}
	private void Aim(Vector3 direction)
	{
		direction = direction.Normalized();
		Vector3 origin = _player.GlobalPosition + Vector3.Up * .9f;
		// 相机放在胸口测量准星，避免仰视夹具把相机退到地板下而命中地板背面。
		_camera.GlobalPosition = origin;
		_camera.LookAt(origin + direction * 100f, Mathf.Abs(direction.Y) > .99f ? Vector3.Forward : Vector3.Up);
		_camera.ResetPhysicsInterpolation();
	}
	private async Task Fire(float hold, int expected)
	{
		Press("weapon_skill_2", true);
		Check(await Until(() => Volley.ChargeTime >= hold), $"真实右键蓄力达到{hold:0.##}秒");
		Check(_shots.Count == 0, "松手前不生成飞刀");
		int atks = _atks; Press("weapon_skill_2", false);
		Check(await Until(() => _shots.Count > 0) && _shots.Count == expected && _atks > atks, $"真实释放ATK生成{expected}把飞刀，实际{_shots.Count}");
		Check(Volley.LastShotCount == expected, "技能记录与真实弹体数量一致");
	}
	private void CheckFan(float span, string name)
	{
		var dirs = _shots.Where(IsInstanceValid).Select(s => s.VelocityDirection).ToArray();
		Vector3 center = Volley.LastShotDirection;
		float actual = dirs.Length > 1 ? Mathf.RadToDeg(dirs[0].AngleTo(dirs[^1])) : 0;
		float expected = dirs.Length > 1 ? span : 0;
		Check(dirs.Length > 0 && dirs.All(d => d.IsFinite() && Mathf.Abs(d.Length() - 1) < .001f), name + "：初速方向有效且归一化");
		Check(dirs.All(d => Mathf.RadToDeg(d.AngleTo(center)) <= span * .5f + .03f), name + "：每把飞刀都在中心两侧半角内");
		Check(Mathf.Abs(actual - expected) < .03f, $"{name}：真实外沿总夹角{actual:0.###}°，期望{expected:0.###}°");
		float step = dirs.Length > 1 ? span / (dirs.Length - 1) : 0;
		Check(Enumerable.Range(1, Math.Max(0, dirs.Length - 1)).All(i => Mathf.Abs(Mathf.RadToDeg(dirs[i - 1].AngleTo(dirs[i])) - step) < .03f), name + "：相邻飞刀等角度均分");
		Vector3 sum = dirs.Aggregate(Vector3.Zero, (total, d) => total + d);
		Check(sum.LengthSquared() > 0 && sum.Normalized().Dot(center) > .9999f, name + "：整体对称于三维准星中心");
		Check(Mathf.Abs(Volley.LastSpreadAngleDegrees - expected) < .001f, name + "：诊断总角度与实际弹道一致");
		if (dirs.Length % 2 == 1) Check(dirs[dirs.Length / 2].Dot(center) > .9999f, name + "：中央飞刀准确沿准星出手");
	}
	private void Grant(string id)
	{
		var build = new RunBuild(); var reward = RewardCatalog.Find(id)!;
		build.Add(reward, new RewardContext { Player = _player, Weapon = NodeSearch.FindInSubtree<Weapon>(_player)!, Build = build });
		Check(build.Has(id), "真实进化生效：" + reward.Name);
	}
	public override async void _Ready()
	{
		try
		{
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.Hunter; RunSetup.Weapon = WeaponPresetKind.Crossbow;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_camera = new Camera3D { Current = true }; AddChild(_camera);
			GetTree().NodeAdded += Added; _player.GetNode<UnitAnimator>("Animator").SkillEvent += Event;
			await Equip();
			var row = ConfigRows.ById<WeaponConfig>("wpn_crossbow")!;
			Check(row.DisplayName == "飞刀" && row.Slot == 5 && row.Pool?.DisplayName == "飞刀升级池", "旧武器ID与槽位保持可读，玩家可见名称和升级池均为飞刀");
			Check(ConfigRows.ById<AbilityConfig>("skl_a2_w1_1")?.DisplayName == "飞刀散掷" && ConfigRows.ById<AbilityConfig>("skl_a2_w1_2")?.DisplayName == "千刃齐发", "两个武技使用新的飞刀名称");
			Check(Volley.SpreadAngleDegrees == 60f && Volley.Aim == Ability.AimSource.Crosshair, "生产表默认总夹角60°并使用准星瞄准");
			foreach (var entry in new[] { (.43f, 3), (.63f, 5), (.83f, 7), (1.03f, 9) })
			{
				await Equip(); await Fire(entry.Item1, entry.Item2); CheckFan(60, $"蓄力{entry.Item2}把");
			}
			foreach (int count in new[] { 1, 2, 32, 101, 256 })
			{
				await Equip(); Volley.Count = count; await Fire(.43f, count); CheckFan(60, $"扩展{count}把");
			}
			foreach (Vector3 direction in new[] { new Vector3(0, .7f, -1), new Vector3(0, -.7f, -1), Vector3.Up, Vector3.Down })
			{
				await Equip(); Aim(direction); await Fire(1.03f, 9); CheckFan(60, "俯仰" + direction);
				Check(Volley.LastShotDirection.Dot(direction.Normalized()) > .999f, "实际准星中心保留俯仰方向");
			}
			foreach (float angle in new[] { 0f, 40f, 90f })
			{
				await Equip(); Volley.SpreadAngleDegrees = angle; await Fire(.43f, 3); CheckFan(angle, $"调参{angle}°");
			}
			await Equip(); Grant("evo_focus"); await Fire(1.03f, 1); CheckFan(60, "专一单刀");
			Check(Mathf.IsEqualApprox(Volley.LastShotDamage, 88.8f), "专一保留少八把的80%逐把伤害补偿");
			await Equip(); Grant("evo_philander");
			var target = new Unit { Name = "SideTarget", Faction = Faction.Enemy, MaxHealth = 10000 };
			target.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = .5f } });
			AddChild(target); target.GlobalPosition = new Vector3(30, 1, 20); _targets.Add(target); await Wait(.05);
			await Fire(.73f, 9); CheckFan(60, "花心侧方追踪");
			Check(_shots.All(s => s.SeekAnyHostile && s.AssignedHomingTarget == target), "侧方目标可被追踪分配，但所有初速仍在60°内");
			await Equip(); Grant("evo_flow"); Press("weapon_skill_2", true);
			Check(await Until(() => _shots.Count > 0) && _shots.Count == 1 && _atks > 0, "心流真实蓄满自动释放一把飞刀");
			CheckFan(60, "心流单刀"); Press("weapon_skill_2", false);
			await Equip(); Press("weapon_skill_2", true); await Until(() => Volley.IsCharging); await Wait(.03); Press("weapon_skill_2", false); await Wait(.4);
			Check(_shots.Count == 0 && Volley.LastReleaseAborted, "未满基础蓄力仍取消，改名不改变释放门槛");
			await Equip(); var primary = (ArrowVolleyAbility)_player.AbilitySlots.Resolve("weapon_skill_1")!;
			Press("weapon_skill_1", true); await Wait(.02); Press("weapon_skill_1", false);
			Check(await Until(() => _shots.Count == 3) && primary.SpreadAngleDegrees < 0 && primary.LastSpreadDegrees == 20, "左键仍为相邻20°的三刀散掷");
			await Equip(WeaponPresetKind.Bow);
			Check(((ArrowVolleyAbility)_player.AbilitySlots.Resolve("weapon_skill_1")!).SpreadAngleDegrees < 0, "切换到弓后不带入飞刀的固定散射参数");
			await Equip();
			Check(Volley.SpreadAngleDegrees == 60 && !Volley.AutoReleaseOnFullCharge, "重新装备飞刀恢复生产参数并清除上次进化状态");
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		finally
		{
			Press("weapon_skill_1", false); Press("weapon_skill_2", false); GetTree().NodeAdded -= Added;
			if (IsInstanceValid(_player)) _player.GetNode<UnitAnimator>("Animator").SkillEvent -= Event;
		}
		GD.Print($"[KnifeVolley] === 飞刀散射验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
}

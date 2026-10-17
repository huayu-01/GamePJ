using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>E 批独立验收场景：真实输入链路、状态生命周期、渲染截图；不挂入正式场景。</summary>
public partial class BatchEProbe : Node
{
	private int _failed;
	private int _passed;
	private Node _main = null!;
	private Unit _player = null!;
	public override async void _Ready()
	{
		try
		{
			await Run();
		}
		catch (Exception ex)
		{
			Check(false, ex.ToString());
		}
		GetTree().Paused = false;
		Input.ActionRelease("weapon_skill_1");
		Input.ActionRelease("weapon_skill_2");
		GD.Print($"[BatchE] === E 批验收结束：通过 {_passed}，失败 {_failed} ===");
		ActRoguelike.Core.GameExit.Request(GetTree(), _failed == 0 ? 0 : 1);
	}

	private void Check(bool ok, string message)
	{
		if (ok) _passed++; else _failed++;
		GD.Print($"[BatchE] {(ok ? "PASS" : "FAIL")} {message}");
	}
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds, true), SceneTreeTimer.SignalName.Timeout);
	private void Button(string action, bool pressed) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed });
	private async Task Shot(string name)
	{
		if (DisplayServer.GetName() == "headless") return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Error result = GetViewport().GetTexture().GetImage().SavePng($"res://docs/screenshots/{name}.png");
		Check(result == Error.Ok, $"视口截图 {name}.png");
	}

	private Unit Target(string name, Vector3 position)
	{
		Unit unit = GD.Load<PackedScene>("res://Scenes/Enemies/Chaser.tscn").Instantiate<Unit>();
		unit.Name = name;
		_main.AddChild(unit);
		unit.GlobalPosition = position;
		unit.GetNode("Brain").ProcessMode = ProcessModeEnum.Disabled;
		unit.GetNode("Movement").ProcessMode = ProcessModeEnum.Disabled;
		unit.Velocity = Vector3.Zero;
		return unit;
	}
	private static void Apply(Unit unit, string id, float duration, int count = 1)
	{
		StatusEffect status = StatusPresets.Create(id, duration) ?? throw new InvalidOperationException($"缺少状态 {id}");
		for (int i = 0; i < count; i++) unit.ApplyStatus(status);
	}
	private string Groups() => $"{GetTree().GetNodesInGroup("player_units").Count}/{GetTree().GetNodesInGroup("enemy_units").Count}/{GetTree().GetNodesInGroup("summon_units").Count}";
	private static bool SafeTree(Node node)
	{
		if (node is CollisionObject3D || node is Unit || node.GetGroups().Count != 0) return false;
		foreach (Node child in node.GetChildren()) if (!SafeTree(child)) return false;
		return true;
	}
	private static string Materials(Node unit)
	{
		var ids = new List<string>();
		foreach (Node node in unit.GetNode("Model").FindChildren("*", "MeshInstance3D", true, false))
		{
			var mesh = (MeshInstance3D)node;
			ids.Add($"{mesh.GetInstanceId()}:{mesh.MaterialOverride?.GetInstanceId()}:{mesh.MaterialOverlay?.GetInstanceId()}");
		}
		return string.Join(";", ids);
	}

	private async Task Run()
	{
		await Wait(1);
		_main = GetParent();
		_main.GetNode("RunController").Call("BeginRunWithCharacter", 3, 5);
		await Wait(0.3);
		_player = _main.GetNode<Unit>("Player");
		var hawk = _player.GetNode<HawkEyeAbility>("AbilityWeapon3Skill2");
		var arrow = _player.GetNode<ArrowVolleyAbility>("AbilityWeapon3Skill1");
		var animator = _player.GetNode<UnitAnimator>("Animator");
		Camera3D camera = GetViewport().GetCamera3D();
		float baseFov = camera.Fov;
		await Wait(1); // 等第三人称相机平滑跟随完成，再把靶子放到真实准星点附近。
		Vector3 point = AimUtil.GetCrosshairPoint(GetViewport(), _player, 200, CombatLayers.World | CombatLayers.UnitMask);
		Unit markTarget = Target("EyeTarget", point + camera.GlobalBasis.Z * 0.8f);
		Check(hawk.IsBackground && hawk.HoldToMaintain && !hawk.HoldToCharge && hawk.AnimName == "", "表配置：后台维持=True，蓄力=False，动画为空");
		Button("weapon_skill_2", true);
		await Wait(0.12);
		Check(hawk.IsMaintaining && hawk.StartupProtectionActive && !hawk.IsBusy && !hawk.IsCharging, $"启动窗口：held={hawk.HeldTime:0.###}，占动作={hawk.IsBusy}，蓄力={hawk.IsCharging}");
		Check(!hawk.TryActivate(), "后台维持期间不能重复激活");
		await Wait(0.6);
		// 开镜会按屏幕构图调整相机高度；待其收敛后重新对准靶子，测实际准星邻域。
		point = AimUtil.GetCrosshairPoint(GetViewport(), _player, 200, CombatLayers.World | CombatLayers.UnitMask);
		markTarget.GlobalPosition = point + camera.GlobalBasis.Z * 0.8f;
		await Wait(0.6);
		Check(hawk.IsAiming && hawk.MarkedCount >= 1 && camera.Fov < baseFov - 10, $"按住瞄准：标记={hawk.MarkedCount}，FOV={camera.Fov:0.##}（原{baseFov}）");
		Check(markTarget.GetStatusStacks("Mark") >= 1, "标记写入真实敌人状态");
		Button("weapon_skill_1", true);
		await Wait(0.3);
		Check(arrow.IsCharging && arrow.IsBusy && hawk.IsAiming, $"左键蓄力与右键并用：左键={arrow.ChargeTime:0.##}s，动画={animator.CurrentAnimation}");
		await Shot("e_eye_with_arrow");
		Button("weapon_skill_2", false);
		await Wait(0.15);
		Check(arrow.IsCharging && animator.CurrentAnimation == "skills/ATK_A3_Draw", "松开右键当帧没有停止左键技能动画");
		await Wait(0.95);
		Check(!hawk.IsActive && Mathf.IsEqualApprox(camera.Fov, baseFov), $"松开右键：active={hawk.IsActive}，FOV恢复={camera.Fov}");
		Check(arrow.IsCharging && arrow.IsBusy, $"后台收尾后左键仍在蓄力；当前动画={animator.CurrentAnimation}（非循环动画可自然播完）");
		Check(hawk.CooldownRemaining > 0, $"松开后进入冷却：{hawk.CooldownRemaining:0.##}s");
		Button("weapon_skill_1", false);
		await Wait(0.7);
		Check(arrow.LastShotCount > 0 && arrow.LastShotDamage > 0, $"松开左键真实发箭：{arrow.LastShotCount}支，伤害={arrow.LastShotDamage:0.##}");
		Check(!IsInstanceValid(markTarget) || markTarget.IsDead, "左键箭命中标记靶子并造成击杀");
		hawk.RefreshCooldown();
		Button("weapon_skill_2", true);
		await Wait(0.1);
		Button("weapon_skill_2", false);
		await Wait(0.2);
		Check(!hawk.IsActive && hawk.MarkedCount == 0 && !hawk.IsCharging && Mathf.IsEqualApprox(camera.Fov, baseFov), "短按不标记、不蓄力，相机恢复");

		hawk.RefreshCooldown();
		Button("weapon_skill_2", true);
		await Wait(0.1);
		float health = _player.Health;
		var dodge = _player.GetNode<BackdashAbility>("AbilitySkill1");
		int charges = dodge.ChargesRemaining;
		DamageResult hit = _player.ReceiveHit(new HitInstance { Target = _player, Context = EventContext.Create(null, _player), Damage = 40, Direction = Vector3.Forward });
		Check(hit.Invulnerable && _player.Health == health && hawk.ForcedDodgeCount == 1 && dodge.ChargesRemaining == charges, $"软启动防御：掉血={hit.HealthLost}，免费后撤={hawk.ForcedDodgeCount}，层数={charges}→{dodge.ChargesRemaining}");
		Button("weapon_skill_2", false);
		await Wait(1.2);
		Check(!_player.IsInvulnerable, "防御结束后不残留无敌");

		hawk.RefreshCooldown();
		Button("weapon_skill_2", true);
		await Wait(0.8);
		GetTree().Paused = true;
		Button("weapon_skill_2", false);
		await Wait(0.2);
		GetTree().Paused = false;
		await Wait(1.1);
		Check(!hawk.IsActive && Mathf.IsEqualApprox(camera.Fov, baseFov), "暂停期间松手，恢复后结束维持并还原视角");
		hawk.RefreshCooldown();
		Button("weapon_skill_2", true);
		await Wait(0.8);
		hawk.Enabled = false;
		await Wait(0.1);
		Check(!hawk.IsActive && Mathf.IsEqualApprox(camera.Fov, baseFov), "禁用后台技能立即还原相机并结束");
		Button("weapon_skill_2", false);
		hawk.Enabled = true;

		// 后台淡出即将结束时再起左键，确保 Finish 这一帧也不会停止另一个新动作。
		camera.Fov = 90;
		hawk.RefreshCooldown();
		Button("weapon_skill_2", true);
		await Wait(0.8);
		Button("weapon_skill_2", false);
		await Wait(0.85);
		arrow.RefreshCooldown();
		Button("weapon_skill_1", true);
		await Wait(0.3);
		Check(!hawk.IsActive && arrow.IsCharging && animator.CurrentAnimation == "skills/ATK_A3_Draw", "后台Finish时另一武技动画继续播放");
		Check(Mathf.IsEqualApprox(camera.Fov, 90), "再次开镜捕获新的FOV基准：恢复90度");
		Button("weapon_skill_1", false);
		await Wait(0.15);
		camera.Fov = baseFov;
		var owner = new Unit { Name = "BackgroundDeathOwner" };
		var effect = new HawkEyeAbility { IsBackground = true, HoldToMaintain = true, MaintainStartupSeconds = 0.3f };
		owner.AddChild(effect);
		_main.AddChild(owner);
		effect.TryActivate();
		effect.OnInputHeld(0.4f);
		effect._PhysicsProcess(0.2);
		owner.ReceiveHit(new HitInstance { Target = owner, Context = EventContext.Create(null, owner), Damage = 9999 });
		effect._PhysicsProcess(0.05);
		Check(owner.IsDead && !effect.IsActive && Mathf.IsEqualApprox(camera.Fov, baseFov), "拥有者死亡立即取消后台效果并还原相机");
		owner.QueueFree();
		if (IsInstanceValid(markTarget)) markTarget.QueueFree();
		await Wait(0.1);

		// 固定镜头只用于近距离验收，单位状态与正式表现组件均使用生产实现。
		_player.GlobalPosition = new Vector3(0, 0.5f, 8);
		camera.GetParent().ProcessMode = ProcessModeEnum.Disabled;
		camera.TopLevel = true;
		camera.GlobalPosition = new Vector3(0, 4.2f, 10);
		camera.LookAt(new Vector3(0, 1.1f, 0));
		Unit marked = Target("Marked", new Vector3(-3, 0, 0));
		Unit frozen = Target("Frozen", Vector3.Zero);
		Unit burning = Target("Burning", new Vector3(3, 0, 0));
		string groups = Groups();
		string material = Materials(marked);
		Apply(marked, "Mark", 8, 3);
		Apply(frozen, "Freeze", 2);
		Apply(burning, "Burn", 8);
		await Wait(0.3);
		var markFx = marked.GetNode<StatusEffectFx>("StatusEffects");
		var frostFx = frozen.GetNode<StatusEffectFx>("StatusEffects");
		var burnFx = burning.GetNode<StatusEffectFx>("StatusEffects");
		Check(markFx.DisplayedStacks("Mark") == 3 && markFx.VisualCount == 1, markFx.DebugState());
		Check(frostFx.VisualCount == 1 && burnFx.VisualCount == 1, "冻结与燃烧真实状态各生成一份表现");
		Check(SafeTree(markFx) && SafeTree(frostFx) && SafeTree(burnFx), "完整表现子树：碰撞体=0，单位=0，分组=0");
		Check(Groups() == groups && Materials(marked) == material, $"表现生成后单位组计数={Groups()}，模型材质引用不变");
		await Shot("e_status_three");
		Apply(marked, "Mark", 8, 10);
		Apply(marked, "Slow", 1.0f);
		await Wait(0.2);
		Check(marked.GetStatusStacks("Mark") == 5 && markFx.DisplayedStacks("Mark") == 5 && markFx.VisualCount == 2, "Mark封顶5层与Slow并存，图标层数同步");
		await Shot("e_status_stacks_five");
		await Wait(1.1);
		Check(!marked.HasStatus("Slow") && markFx.VisualCount == 1, "Slow到期仅清理自身表现，Mark保留");
		await Wait(0.6);
		Check(!frozen.HasStatus("Freeze") && frostFx.VisualCount == 0 && frostFx.GetChildCount() == 0, "冻结到期：状态和表现全部清理");
		Apply(frozen, "MissileBeacon", 0.2f);
		await Wait(0.1);
		Check(frozen.HasStatus("MissileBeacon") && frostFx.VisualCount == 0, "索敌信标维持原隐形语义");
		burning.ReceiveHit(new HitInstance { Target = burning, Attacker = _player, Context = EventContext.Create(_player, burning), Damage = 9999 });
		await Wait(0.1);
		Check(burning.IsDead && burnFx.VisualCount == 0 && burnFx.GetChildCount() == 0, "死亡同帧路径清理状态表现，不等尸体退树");
		await Shot("e_status_cleanup");
		foreach (string id in new[] { "FreezePlus", "BurnPlus", "Stun", "Empowered", "Weaken" })
		{
			Apply(frozen, id, 0.35f);
			await Wait(0.12);
			Check(frostFx.DisplayedStacks(id) == 1 && SafeTree(frostFx), $"扩展状态 {id}：图标与形态已生成，安全树通过");
			await Wait(0.4);
			Check(!frozen.HasStatus(id) && frostFx.VisualCount == 0, $"扩展状态 {id}：到期清理");
		}
	}
}

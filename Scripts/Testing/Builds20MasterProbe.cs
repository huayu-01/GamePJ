using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.Visual;
using Godot;

namespace ActRoguelike.Testing;

public partial class Builds20MasterProbe : Builds20ProbeSupport
{
	protected override string ProbeTag => "Builds20Master";
	protected override PlayerCharacterKind TestCharacter => PlayerCharacterKind.WeaponMaster;
	protected override WeaponPresetKind TestWeapon => WeaponPresetKind.Spear;
	protected override async Task RunChecks() { Prepare(); await Spear(); await Arcane(); await Reap(); }
	private async Task Spear()
	{
		Check(Run.TryAcquireReward("evo_rainbow") && Run.TryAcquireReward("1349"), "069气贯长虹先取，斜阳仍通过正式正常候选授予");
		var skill = Skill<SlantSunAbility>();
		Check(Mathf.Abs(skill.Cooldown - .6f) < .001f && skill.LanceUnlimitedTargets, "069用户确认斜阳自带穿透，兼容既有0.3倍冷却为0.6秒");
		RewardEffectHost.Ensure(Player).ReleaseReward(Run.Build, "evo_rainbow");
		Check(skill.Cooldown == 2 && skill.LanceUnlimitedTargets, "069先取得的气贯撤销时只恢复冷却，本体穿透保留");
		await Equip(WeaponPresetKind.Spear); Check(Give("1349") && Give("evo_rainbow"), "069斜阳先取、气贯后取同样合法");
		skill = Skill<SlantSunAbility>(); Check(Mathf.Abs(skill.Cooldown - .6f) < .001f, "069两种获取顺序冷却一致，不暗中重复叠乘");
		Release("evo_rainbow"); Check(skill.Cooldown == 2, "069后取得的气贯撤销也精确恢复2秒");
		Check(Give("mut_cleave") && Give("1341"), "069斜阳可叠大刀阔斧与巨化"); await Wait(.08);
		Vector3 origin = Player.GlobalPosition;
		Camera.MakeCurrent(); Camera.GlobalPosition = origin + new Vector3(0, 5, 6); Camera.LookAt(origin + Vector3.Forward * 8);
		Camera.ResetPhysicsInterpolation(); await Wait(.04);
		Check(skill.TryGetAimSurface(out var surface, out _), $"069斜阳瞄准实际World表面，当前相机一致={GetViewport().GetCamera3D() == Camera}、已初始化={skill.OwnerUnit != null}、命中{surface}");
		await Tap("weapon_skill_2"); Check(await Until(() => skill.Pin != null), "069真实右键松开沿瞄准表面刺落长枪");
		var pin = skill.Pin!; var victim = MakeEnemy(pin.Destination);
		var attacks = new List<AttackInstance>(); victim.Damaged += (_, hit) => { if (hit.Attack is { } attack) attacks.Add(attack); };
		Check(Mathf.Abs(pin.Radius - 2.6f * 1.3f * 1.2f) < .01f && Mathf.Abs(pin.SizeScale - 1.3f * 1.2f) < .01f,
			"069大刀与巨化只各乘一次，落点范围和离手长枪体型同步");
		Check(await Until(() => pin.Landed) && attacks.Count(a => a == pin.FallingAttack) == 1 && attacks.Count(a => a == pin.ImpactAttack) == 1,
			"069下刺路径和落地AOE各实际命中一次，未重复开窗");
		Check(Player.AbilitySlots.Resolve("weapon_skill_1") == null && Player.AbilitySlots.Resolve("weapon_skill_2") == skill,
			"069留枪期间只锁武技1，武技2仍保有当前槽实例");
		skill.UpdateInputPreview(true); Check(!skill.PreviewShowing, "069冷却中不显示技能指示器");
		Node abilityInput = Player.GetNode("AbilityInput"); abilityInput.SetProcess(false);
		skill.RefreshCooldown(); skill.UpdateInputPreview(true); await Wait(.05); Check(skill.PreviewShowing, "069冷却结束才允许显示指示器");
		skill.UpdateInputPreview(false); Check(!skill.PreviewShowing, "069输入松开立即隐藏新指示器"); abilityInput.SetProcess(true);
		await Shot("build20-slant-sun-ground");
		Check(Mathf.IsEqualApprox(skill.ReturnSpeed, 20f), "069召回速度为20米/秒");
		skill.RefreshCooldown(); await Tap("weapon_skill_2");
		Check(await Until(() => pin.Returning), "069长枪落地后再次使用改为召回，不读取新落点");
		Check(skill.Pin == pin, "069召回期间不生成第二根枪");
		Check(await Until(() => skill.Pin == null, 4) && Player.AbilitySlots.Resolve("weapon_skill_1")?.Enabled == true,
			"069长枪以20米/秒飞回，收回后武技1解锁");
		var wall = new StaticBody3D { CollisionLayer = CombatLayers.World, CollisionMask = 0 };
		wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(8, 6, .3f) } });
		wall.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(8, 6, .3f) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(.25f, .3f, .4f) } });
		Mode.AddChild(wall); wall.GlobalPosition = origin + Vector3.Forward * 8 + Vector3.Up * 2;
		await Wait(.05); Aim(wall.GlobalPosition); skill.RefreshCooldown(); await Tap("weapon_skill_2");
		Check(await Until(() => skill.Pin != null && skill.Pin != pin), "069再次施放先停旧枪，再生成唯一新枪");
		var wallPin = skill.Pin!; Check(Mathf.Abs(wallPin.SurfaceNormal.Y) < .1f && wallPin.SurfaceNormal.LengthSquared() > .9f,
			"069墙面命中沿墙面法线出现，不误当地面有效点");
		await Until(() => wallPin.Landed); await Wait(.04); Check(!IsInstanceValid(pin) && skill.GetChildren().OfType<SurfaceSpearPin>().Count() == 1, "069场景同时只有一根长枪");
		await Shot("build20-slant-sun-wall");
		Player.GlobalPosition = wallPin.Destination + wallPin.SurfaceNormal; Player.ResetPhysicsInterpolation();
		Check(await Until(() => skill.Pin == null) && Player.AbilitySlots.Resolve("weapon_skill_1")?.Enabled == true, "069实际靠近收回长枪并解除武技1锁门");
		wall.QueueFree(); victim.QueueFree(); Player.GlobalPosition = origin; Player.ResetPhysicsInterpolation();
	}
	private async Task Arcane()
	{
		await Equip(WeaponPresetKind.SpiritSword); Check(Give("1347"), "069秘法切刃在灵剑正常池获取"); await Wait(.08);
		using var pool = new RewardPool(); pool.Initialize(Context.Weapon, "chr_weapon_master", Director.ModeId);
		Check(!pool.Available(Build).Any(r => r.Id == "evo_great_draw"), "069替换灵剑召唤后，失去作用的灵能汲取被互斥排除");
		var targets = new[] { new Vector3(0,0,-4), new Vector3(3,0,-5), new Vector3(-3,0,-6), new Vector3(0,0,-8), new Vector3(4,0,-9), new Vector3(-4,0,-10) }
			.Select(offset => MakeEnemy(Player.GlobalPosition + offset)).ToArray();
		float[] health = targets.Select(t => t.Health).ToArray(); Player.Movement!.SetPhysicsProcess(true);
		var model = Player.GetNode<CharacterDisplay>("Appearance").ActiveModel!;
		Check(model.Visible, "069切刃操作实际展示模型，换模后的隐藏缓存不参与");
		var skill = Skill<ArcaneSliceAbility>(); Press("weapon_skill_2", true);
		Check(await Until(() => skill.IsActive && Player.IsInvulnerable), "069真实输入进入无敌粒子突进"); Press("weapon_skill_2", false);
		Check(!model.Visible, "069飞行时隐藏当前角色模型，使用独立小粒子，不绑定骨骼");
		float before = Player.Health; Player.ApplyDirectDamage(100, targets[0]); Check(Player.Health == before, "069突进过程实际免疫伤害");
		// 独立验收相机不跟随角色；重新取景，避免把上一项墙面测试的树内视角当成效果截图。
		Camera.MakeCurrent(); Camera.GlobalPosition = Player.GlobalPosition + new Vector3(0, 8, 16);
		Camera.LookAt(Player.GlobalPosition + Vector3.Up); Camera.ResetPhysicsInterpolation();
		await Shot("build20-arcane-flight");
		Check(await Until(() => !skill.IsActive, 3) && skill.ContactCount >= 2 && skill.ContactCount <= 5, "069至多初始目标加4次弹射，最后合法落点并结束前滑");
		Check(!Player.IsInvulnerable && model.Visible && !Player.Movement.HasVelocityOverride,
			$"069收尾恢复模型、操控与原无敌状态（无敌={Player.IsInvulnerable}、模型={model.Visible}、位移覆盖={Player.Movement.HasVelocityOverride}）");
		float damage = 36 * AttackOffense.Capture(Player).DamageMultiplier;
		Check(targets.Select((t,i) => health[i] - t.Health).All(d => d == 0 || Mathf.Abs(d - damage) < .1f)
			&& targets.Count(t => t.Health < t.MaxHealth) == skill.ContactCount, "069序列结束每个接触目标只结算一份冻结伤害");
		foreach (var target in targets) target.QueueFree(); Player.Movement.SetPhysicsProcess(false);
	}
	private async Task Reap()
	{
		await Equip(WeaponPresetKind.Scythe); Check(Give("1348"), "069死亡收割在镰刀正常池获取"); await Wait(.08);
		Build.SetRuneUnlocks(new[] { "rune_lei_seal" }); using var pool = new RewardPool(); pool.Initialize(Context.Weapon, "chr_weapon_master", Director.ModeId);
		Check(!pool.Available(Build).Any(r => r.Id is "evo_affection" or "evo_thunder_recall"), "069死亡收割与喜爱、落雷术按原要求双向互斥");
		var skill = Skill<ReapingSpinAbility>(); var target = MakeEnemy(Player.GlobalPosition + Vector3.Forward);
		float health = target.Health, damage = 32 * AttackOffense.Capture(Player).DamageMultiplier;
		await Tap("weapon_skill_2"); Check(await Until(() => !skill.IsActive), "069死亡收割点按一圈后收招");
		Check(skill.LastDashDistance == 0 && skill.PulseCount == 1 && Mathf.Abs(health - target.Health - damage) < .1f,
			"069未满0.5秒走原地一圈，实际仅一次32基础伤害"); target.QueueFree();
		skill.RefreshCooldown(); Player.Movement!.SetPhysicsProcess(true); Press("weapon_skill_2", true);
		Check(await Until(() => skill.ChargeTime >= 2.02f), "069真实长按达到2秒满蓄"); Press("weapon_skill_2", false);
		Check(await Until(() => skill.LastDashDistance == 12 && skill.LastSpinSeconds == 4), "069满蓄按可调首版前冲12米、旋转4秒");
		Check(await Until(() => skill.PulseCount >= 2), "069前冲后进入持续旋转伤害阶段");
		Check(skill.IsSpinning && !Player.Movement.IsActionMovementLocked, "069旋转落地阶段允许主动移动");
		Vector3 spinStart = Player.GlobalPosition;
		Input.ActionPress("move_right");
		await Wait(.25);
		Input.ActionRelease("move_right");
		float spinMoved = Player.GlobalPosition.DistanceTo(spinStart);
		Check(spinMoved > .1f && spinMoved < Player.MoveSpeed * .2f, $"069旋转移动按浮空半速（{spinMoved:0.##}m / 0.25s）");
		await Shot("build20-reaping-spin"); await Tap("skill_1");
		Check(await Until(() => !skill.IsActive) && skill.CooldownRemaining > 0, "069其它主动技能可打断旋转，但已产生效果的本次仍消耗冷却");
		int pulses = skill.PulseCount; await Wait(.8); Check(skill.PulseCount == pulses, "069打断后停止剩余旋转伤害，没有后台漏伤");
		Player.Movement.SetPhysicsProcess(false);
	}
}

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Testing;

public partial class Builds20HunterProbe : Builds20ProbeSupport
{
	protected override string ProbeTag => "Builds20Hunter";
	protected override async Task RunChecks() { Prepare(); await Dome(); await Knife(); await Eye(); }
	private async Task Dome()
	{
		Player.GlobalPosition += Vector3.Up * 30; Player.ResetPhysicsInterpolation(); Aim(Player.GlobalPosition + Vector3.Forward * 10);
		Ability old = Player.AbilitySlots.Resolve("weapon_skill_2")!;
		Check(Run.TryAcquireReward("1350") && Skill<EnergyDomeAbility>() is { }, "069能量罩经正式入口替换武技2槽，沿用稳定技能身份");
		var skill = Skill<EnergyDomeAbility>(); Check(!old.Enabled && Ability.FindByAbilityId(Player, old.AbilityId) == skill, "069原能量波武技禁用，增强寻址指向当前替换实例");
		var row = Player.AbilitySlots.Equipped.Single(e => e.Ability == skill).Row;
		string chinese = row.LocalizedDescription, locale = ContentText.Locale;
		try
		{
			ContentText.SetLocale("en");
			Check(row.LocalizedDescription != chinese && row.LocalizedDescription.Contains("24") && !row.LocalizedDescription.Contains('{') && row.Id == old.AbilityId,
				"069已装入槽位的新技能说明随语言切换，继续读取场景数值且身份不变");
		}
		finally { ContentText.SetLocale(locale); }
		Check(row.LocalizedDescription == chinese, "069切回中文无需重新抽取奖励或换装");
		Check(!Run.AvailableRewards().Any(r => r.Id == "evo_perfect_focus"), "069能量罩与只适用原蓄力释放的专注互斥");
		await Wait(.08); await Tap("weapon_skill_2"); Check(await Until(() => skill.DomeActive), "069真实右键开启独立能量罩");
		var dome = Player.GetChildren().OfType<EnergyDomeField>().Single();
		var enemy = MakeEnemy(Player.GlobalPosition + Vector3.Forward * 15);
		Projectile MakeShot(Unit source, bool destroyable)
		{
			var p = new Projectile { Speed = 1600, CanBeDestroyed = destroyable }; Mode.AddChild(p); p.SetPhysicsProcess(false);
			p.GlobalPosition = Targeting.AimPoint(Player) + Vector3.Left * 10;
			p.Launch(new AttackInstance { Attacker = source, BaseDamage = 1, Lifetime = 5 }, Vector3.Right); return p;
		}
		var hostile = MakeShot(enemy, true); hostile._PhysicsProcess(.5);
		Check(hostile.IsQueuedForDeletion() && dome.DestroyedCount == 1, "069敌方高速弹完整扫过罩体时被销毁，不漏穿");
		var friendly = MakeShot(Player, true); friendly._PhysicsProcess(.5);
		Check(dome.DestroyedCount == 1 && !friendly.IsQueuedForDeletion(), "069能量罩不清理己方投掷物"); friendly.QueueFree();
		float health = Player.Health; var immune = MakeShot(enemy, false); immune._PhysicsProcess(.5);
		Check(dome.DestroyedCount == 1 && Player.Health < health && !Player.IsInvulnerable, "069不可摧毁弹可以命中，能量罩不冒充角色无敌");
		await Until(() => !skill.IsActive); Check(skill.DomeActive && skill.CooldownRemaining > 0, "069短起手结束恢复操控，罩仍独立存在且冷却已开始");
		await Shot("build20-energy-dome");
		RewardEffectHost.Ensure(Player).ReleaseReward(Run.Build, "1350"); await Wait(.05);
		Check(Player.AbilitySlots.Resolve("weapon_skill_2") == old && old.Enabled && !IsInstanceValid(dome), "069撤销替换恢复原槽，独立罩体与拦截注册回收"); enemy.QueueFree();
	}
	private async Task Knife()
	{
		await Equip(WeaponPresetKind.Crossbow); Player.GlobalPosition = new Vector3(0, 25, 30); Player.ResetPhysicsInterpolation();
		Aim(Player.GlobalPosition + Vector3.Forward * 10 + Vector3.Up * .9f);
		Check(Give("1351"), "069刀舞在飞刀正常池可获取"); await Wait(.08);
		var skill = Skill<KnifeDanceAbility>("weapon_skill_1"); var target = MakeEnemy(Player.GlobalPosition + Vector3.Forward * 10);
		var attacks = new List<AttackInstance>(); target.Damaged += (_, hit) => { if (hit.Attack is { } attack && attack.Attacker == Player) attacks.Add(attack); };
		var shots = new List<Projectile>(); void Added(Node node) { if (node is Projectile p) shots.Add(p); }
		GetTree().NodeAdded += Added;
		try
		{
			await Tap("weapon_skill_1"); Check(await Until(() => shots.Count >= 4), "069刀舞真实ATK首刀命中后生成三枚有序追击");
			Check(shots.Count == 4 && target.GetStatusStacks("Mark") == 1, "069一枚基础飞刀加三枚组成段，首刀实际施加一层标记");
			Check(shots.Skip(1).All(s => !IsInstanceValid(s) || s.AssignedTargetOnly && s.AssignedHomingTarget == target && s.HomingStartDistance > 4), "069追击先展开约0.15秒，再只追本轮目标");
			Check(await Until(() => attacks.Count >= 4, 3) && attacks.Count == 4 && attacks.Count(a => a.IsWeaponSkillComponent) == 3, "069三枚追击均产生实际命中且不再刀生刀");
			float multiplier = AttackOffense.Capture(Player).DamageMultiplier;
			Check(Mathf.Abs(attacks.Sum(a => a.BaseDamage) - 36 * multiplier) < .05f && attacks.Select(a => a.CastId).Distinct().Count() == 1,
				"069基础12加追击8×3，沿同一根施法归属，无三倍主刀误伤");
			await Shot("build20-knife-dance");
		}
		finally { GetTree().NodeAdded -= Added; foreach (var shot in shots) if (IsInstanceValid(shot)) shot.QueueFree(); }
		target.QueueFree();
		Check(Give("evo_focus") && Skill<ArrowVolleyAbility>().LastShotCount >= 0, "069刀舞仅替换武技1，与武技2专一正常共存");
	}
	private async Task Eye()
	{
		await Equip(WeaponPresetKind.Bow); Player.GlobalPosition = new Vector3(0, 25, 30); Player.ResetPhysicsInterpolation();
		Check(Give("1352"), "069灵魂之眼在弓正常池可获取"); await Wait(.08);
		var skill = Skill<SoulEyeAbility>(); var first = MakeEnemy(Player.GlobalPosition + new Vector3(-2, 0, -6));
		var second = MakeEnemy(Player.GlobalPosition + new Vector3(2, 0, -7));
		float healthBefore = first.Health + second.Health;
		await Tap("weapon_skill_2"); Check(await Until(() => skill.Eye?.MarkCount >= 2), "069真实右键释放灵魂眼并依次标记可见敌人");
		Check(first.GetStatusStacks("Mark") == 1 && second.GetStatusStacks("Mark") == 1 && !skill.IsActive && skill.Eye != null,
			"069眼独立留场，短起手后恢复手动战斗");
		int marks = skill.Eye!.MarkCount; await Wait(.5);
		Check(skill.Eye.MarkCount == marks && first.GetStatusStacks("Mark") == 1, "069已标记目标不会反复叠层");
		// 084（2026-11-01 口径）：标记间隔 0.5 秒；每次标记顺带一支真实灵魂箭（伤害 1，可触发增幅）。
		Check(Mathf.IsEqualApprox(skill.Eye.Interval, .5f), $"084灵魂眼标记间隔为 0.5 秒（实测 {skill.Eye.Interval:0.##}）");
		Check(skill.Eye.ArrowCount >= 2, $"084每次标记都射出灵魂箭（{skill.Eye.ArrowCount} 支）");
		Check(await Until(() => first.Health + second.Health < healthBefore), "084灵魂箭经真实弹道命中目标并结算伤害");
		await Shot("build20-soul-eye"); var eye = skill.Eye;
		Release("1352"); await Wait(.05); Check(!IsInstanceValid(eye), "069撤销眼回收独立状态，不残留自动标记");
		first.QueueFree(); second.QueueFree();
	}
}

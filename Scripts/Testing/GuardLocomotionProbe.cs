using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Testing;

public partial class GuardLocomotionProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "GuardLocomotion";
	protected override PlayerCharacterKind TestCharacter => PlayerCharacterKind.WeaponMaster;
	protected override WeaponPresetKind TestWeapon => WeaponPresetKind.Scythe;
	private void Press(string action, bool value) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = value });
	protected override async Task RunChecks()
	{
		// 共享精英夹具默认把玩家移速设为0；本专项必须恢复真实移动能力。
		Player.ApplyBaseStats(Player.MaxHealth, 0, 0, 5);
		var guard = (GuardStanceAbility)Player.AbilitySlots.Resolve("skill_2")!;
		var movement = Player.Movement!; movement.SetPhysicsProcess(true);
		await Wait(.2);
		Check(guard.TryPress(), "真实招架起手");
		Press("move_right", true); await Wait(.04); Press("move_right", false);
		Check(!guard.IsActive && !guard.StanceActive && !movement.IsActionMovementLocked, "移动立刻取消招架前摇，不等待GuardOn");
		await Wait(.15); guard.RefreshCooldown();
		Check(await OpenGuard(guard), "真实GuardOn已开启防御窗口");
		Vector3 before = Player.GlobalPosition;
		Press("move_right", true); await Wait(.06); Press("move_right", false);
		Check(!guard.IsActive && !guard.StanceActive && Player.GlobalPosition.DistanceTo(before) > .01f, "移动取消已开启架势并立即开始移动");
		var enemy = MakeEnemy(Player.GlobalPosition + Vector3.Forward * 3);
		float health = Player.Health; int parries = guard.ParryCount;
		var hit = new AttackInstance { Attacker = enemy, BaseDamage = 10 }.RollHit(Player, Vector3.Back, Player.GlobalPosition);
		Player.ReceiveHit(hit);
		Check(!hit.Negated && Player.Health < health && guard.ParryCount == parries, "取消后真实伤害不被残留防御订阅规避");
		await Wait(.35); guard.RefreshCooldown();
		Check(await OpenGuard(guard), "再次开启架势测试跳跃取消");
		Press("jump", true); await Wait(.05); Press("jump", false);
		Check(!guard.IsActive && !guard.StanceActive && Player.Velocity.Y > 0 && !movement.IsFacingLocked,
			"跳跃立即取消架势并起跳，无残留动作／朝向锁");
		for (int i = 0; i < 120 && !movement.IsGrounded; i++) await Wait(.02);
		await Wait(.1); guard.RefreshCooldown();
		Check(await OpenGuard(guard), "取消行为未破坏正常招架");
		hit = new AttackInstance { Attacker = enemy, BaseDamage = 10 }.RollHit(Player, Vector3.Back, Player.GlobalPosition);
		Player.ReceiveHit(hit);
		Press("move_right", true); await Wait(.02); Press("move_right", false);
		Check(hit.Parried && guard.ParryCount == parries + 1 && movement.IsControlDisabled, "成功格挡者自身硬直保留，移动不移除该反应");
	}
	private async Task<bool> OpenGuard(GuardStanceAbility guard)
	{
		if (!guard.TryPress()) return false;
		for (int i = 0; i < 100 && !guard.StanceActive; i++) await Wait(.01);
		return guard.StanceActive;
	}
}

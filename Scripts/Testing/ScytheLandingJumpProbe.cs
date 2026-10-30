using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Player;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Testing;

public partial class ScytheLandingJumpProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "ScytheLandingJump";
	protected override PlayerCharacterKind TestCharacter => PlayerCharacterKind.WeaponMaster;
	protected override WeaponPresetKind TestWeapon => WeaponPresetKind.Scythe;
	protected override async Task RunChecks()
	{
		Player.IsInvulnerable = true;
		var movement = Player.Movement!;
		movement.SetPhysicsProcess(true);
		await Wait(.2);
		var scythe = (ScytheThrowAbility)PlayerAbilityInput.FindByInputAction(Player, "weapon_skill_2")!;
		Check(scythe.TryPress(), "真实B角色镰刀起手");
		for (int i = 0; i < 100 && !scythe.InFlight; i++) await Wait(.02);
		Check(scythe.InFlight && !movement.IsActionMovementLocked, "镰刀离手不持续占据地面移动／跳跃动作");
		var run = ActRoguelike.Rewards.RunController.Instance!;
		run.Build.SetRuneUnlocks(new[] { "rune_lei_seal" });
		foreach (string id in new[] { "evo_thunder_recall", "evo_ground_burst", "evo_affection", "mut_cleave" })
			Check(run.TryAcquireReward(id), "安装真实联动构筑：" + id);
		Check(scythe.TryPress(), "落雷术实际瞬移接落地攻击并收回镰刀");
		await Wait(.3);
		Check(!movement.HasVelocityOverride, "落雷落地脉冲没有残留速度覆盖");
		// 瞬移到武器后回到同一平整地面，重复测试短按时机。
		Player.GlobalPosition = new Vector3(0, .2f, 38); Player.Velocity = Vector3.Down;
		await Wait(.2);
		Vector3 ground = Player.GlobalPosition;
		int jumps = 0; movement.JumpStarted += _ => jumps++;
		for (int attempt = 0; attempt < 3; attempt++)
		{
			Player.GlobalPosition = ground + Vector3.Up * .18f;
			Player.Velocity = Vector3.Down * 2;
			Player.ResetPhysicsInterpolation();
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			Check(!movement.IsGrounded, "短按发生在落地前的空中，不伪造已落地状态");
			int before = jumps;
			Input.ParseInputEvent(new InputEventAction { Action = "jump", Pressed = true });
			await Wait(.018);
			Input.ParseInputEvent(new InputEventAction { Action = "jump", Pressed = false });
			await Wait(.17);
			Check(jumps == before + 1 && Player.GlobalPosition.Y > ground.Y + .3f, "落地前短按保留一次并立即起跳，不黏地");
			await Wait(.7);
		}
		Player.GlobalPosition = ground + Vector3.Up * 8; Player.Velocity = Vector3.Zero;
		await Wait(.05); int old = jumps;
		Input.ParseInputEvent(new InputEventAction { Action = "jump", Pressed = true }); await Wait(.02);
		Input.ParseInputEvent(new InputEventAction { Action = "jump", Pressed = false }); await Wait(1.2);
		Check(jumps == old, "高空过早输入过期，不在很久以后落地自动跳跃");
	}
}

using System;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Testing;

public partial class PauseDiveProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "PauseDive";
	protected override PlayerCharacterKind TestCharacter => PlayerCharacterKind.WeaponMaster;
	protected override WeaponPresetKind TestWeapon => WeaponPresetKind.Spear;
	private async Task<bool> Until(Func<bool> ready, double timeout = 3)
	{
		ulong end = Time.GetTicksMsec() + (ulong)(timeout * 1000);
		while (!ready() && Time.GetTicksMsec() < end) await Wait(.01);
		return ready();
	}
	protected override async Task RunChecks()
	{
		Player.IsInvulnerable = true;
		var fall = Player.GetNode<FallAttackAbility>("AbilityFallAttack"); var movement = Player.Movement!;
		var screen = Mode.GetNode<RewardScreen>("RunController/RewardScreen");
		var rig = Player.GetNode<Node3D>("CameraRig"); rig.SetPhysicsProcess(false); rig.SetProcessInput(false);
		var camera = new Camera3D { Current = true }; Mode.AddChild(camera);
		Vector3 ground = Player.GlobalPosition;
		foreach (var sample in new[] { (Weapon: WeaponPresetKind.Spear, Action: false, Terminal: false), (Weapon: WeaponPresetKind.Spear, Action: true, Terminal: false), (Weapon: WeaponPresetKind.Scythe, Action: true, Terminal: false), (Weapon: WeaponPresetKind.Spear, Action: true, Terminal: true) })
		{
			new PlayerLoadout().Apply(Player, this, TestCharacter, sample.Weapon); await Wait(.04);
			movement.SetPhysicsProcess(true); Player.GlobalPosition = ground + Vector3.Up * 10; Player.Velocity = Vector3.Zero; Player.ResetPhysicsInterpolation();
			camera.GlobalPosition = ground + new Vector3(0, 14, 9); camera.LookAt(ground + Vector3.Right * 2); camera.ResetPhysicsInterpolation();
			await Wait(.03);
			Check(fall.TryPress(), $"051真实{sample.Weapon}下落起手");
			if (sample.Action) Check(await Until(() => fall.IsDiving, .8), "051真实Action进入下冲");
			if (sample.Terminal)
			{
				// 控制反例：有限下冲已到预定终点，但离实际碰地仍有小间隙；旧实现永远覆写零速度。
				Player.GlobalPosition = ground + Vector3.Up * .12f; Player.ResetPhysicsInterpolation();
				movement.BeginVelocityOverride(fall, Vector3.Down * fall.EffectiveDiveSpeed, 0);
			}
			Director.GrantExperience(Director.NextLevelExperience); Director._PhysicsProcess(0);
			Check(screen.IsOpen && GetTree().Paused, "051下落中获得足够经验，正式升级奖励暂停游戏");
			Vector3 frozen = Player.GlobalPosition; double time = fall.ActionTimelineTime;
			await Wait(1.2);
			Check(Player.GlobalPosition.DistanceTo(frozen) < .001f && Math.Abs(fall.ActionTimelineTime - time) < .001f, "051阅读奖励时位置与动作计时完整冻结");
			Input.ParseInputEvent(new InputEventKey { Keycode = Key.Key1, PhysicalKeycode = Key.Key1, Pressed = true });
			await Wait(.02); Input.ParseInputEvent(new InputEventKey { Keycode = Key.Key1, PhysicalKeycode = Key.Key1, Pressed = false });
			Check(!screen.IsOpen && !GetTree().Paused, "051实际数字键选择后关闭奖励并恢复游戏");
			bool landed = await Until(() => fall.HasLanded, 1.3);
			Check(landed && movement.IsGrounded && !movement.HasVelocityOverride, $"051无需其它技能即可继续下落并真实着地：landed={landed} ground={movement.IsGrounded} override={movement.HasVelocityOverride} p={Player.GlobalPosition} v={Player.Velocity}");
			await Wait(.2); fall.ResetForLoadout(); movement.SetPhysicsProcess(false);
		}
	}
}

using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Testing;

public partial class ThunderHeartCooldownProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "ThunderHeartCooldown";
	protected override async Task RunChecks()
	{
		var runtime = SpecializationRuntime.Ensure(Player);
		var victim = MakeEnemy(Player.GlobalPosition + Vector3.Forward * 3);
		var definition = new ThunderHeartBehaviorDefinition();
		using (var behavior = definition.Create(new SpecializationContext(Player, runtime)))
		{
			var fact = new GameplayEvent { Kind = GameplayEventKind.Hit, Source = Player, Target = victim, Position = victim.GlobalPosition };
			int count = runtime.ChainCount;
			behavior.OnPrimaryHit(fact); behavior.OnPrimaryHit(fact);
			Check(runtime.ChainCount == count + 1, "同帧多目标命中仅触发一次真实闪电链");
			behavior.Tick(.999f); behavior.OnPrimaryHit(fact);
			Check(runtime.ChainCount == count + 1, "0.999游戏秒尚未冷却");
			behavior.Tick(.001f); behavior.OnPrimaryHit(fact);
			Check(runtime.ChainCount == count + 2, "满1游戏秒恢复触发");
		}
		runtime.Apply(ConfigRows.ById<RewardConfig>("evo_thunder_heart")!);
		void Hit() => victim.ReceiveHit(new AttackInstance { Attacker = Player, Ability = "CooldownProbe", BaseDamage = 1 }.RollHit(victim, Vector3.Forward, victim.GlobalPosition));
		int before = runtime.ChainCount; Hit(); await Wait(.06);
		Check(runtime.ChainCount == before + 1, "真实命中总线接入冷却行为");
		GetTree().Paused = true; await Wait(1.1); GetTree().Paused = false;
		Hit(); await Wait(.06);
		Check(runtime.ChainCount == before + 1, "暂停1.1秒不推进游戏冷却");
		await Wait(1.02); Hit(); await Wait(.06);
		Check(runtime.ChainCount == before + 2, "恢复游戏后实际冷却到期可再次触发");
	}
}

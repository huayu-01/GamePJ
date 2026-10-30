using ActRoguelike.Combat;
using K = ActRoguelike.Config.SpecializationKind;
namespace ActRoguelike.Rewards;
public partial class SpecializationRuntime
{
	private DeathDuelBehavior? DeathDuel => Behavior<DeathDuelBehavior>(K.DeathDuel);
	public bool DuelActive => DeathDuel?.IsActive == true;
	public int DuelKills => DeathDuel?.Kills ?? 0;
	public float DuelCooldown => DeathDuel?.Cooldown ?? 0;
	public bool TryAvoidDeath(Unit unit) { foreach (var behavior in _behaviorOrder) if (!behavior.IsDisposed && behavior.TryAvoidDeath(unit)) return true; return false; }
}

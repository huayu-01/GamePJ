using ActRoguelike.Combat;
using K = ActRoguelike.Config.SpecializationKind;
namespace ActRoguelike.Rewards;
public partial class SpecializationRuntime
{
	public int MarkCount => Behavior<ThunderMarkBehavior>(K.ThunderMark)?.Count ?? 0;
	public void Thunder(Unit target, float damage) => Services.Thunder(target, damage);
}

using ActRoguelike.Config;
using K = ActRoguelike.Config.SpecializationKind;
namespace ActRoguelike.Rewards;
public partial class SpecializationRuntime
{
	public int WindShotCount => Services.WindShotCount;
	public float WindSizeScale => Definition<WildWindBehaviorDefinition>(K.WildWind)?.SizeScale ?? 1;
}

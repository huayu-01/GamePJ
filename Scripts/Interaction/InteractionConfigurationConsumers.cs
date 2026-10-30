using ActRoguelike.Rewards;

namespace ActRoguelike.Interaction;

public partial class RewardNode : IInteractionConfigurationConsumer
{
	public void ConfigureInteraction(InteractionComponent component)
	{
		SpawnsEncounter = Kind != RewardNodeKind.Portal && SpawnsEncounter && component.SpawnsEncounter;
		Prompt = Kind == RewardNodeKind.Portal ? "前往下一关" : RewardService.TitleFor(Kind);
	}
}

public partial class BaseTerminal : IInteractionConfigurationConsumer
{
	public void ConfigureInteraction(InteractionComponent component) { if (Kind == TerminalKind.Gate) { TargetScene = component.TargetScene; Destination = component.Destination; } }
}

public partial class CharacterStand : IInteractionConfigurationConsumer
{
	public void ConfigureInteraction(InteractionComponent component) { if (component.Character != null) Character = component.Character; }
}

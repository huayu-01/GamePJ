using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Interaction;

/// <summary>
/// Boss 之门。策划文档 §6.2：可以随时打开，玩家能放弃剩余普通节点直接挑战 Boss。
/// </summary>
public partial class BossDoor : Interactable
{
	protected override bool OnInteract() => StageController.Instance?.OpenBossDoor() ?? false;
}

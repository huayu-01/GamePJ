namespace ActRoguelike.Interaction;

/// <summary>可视化连线的交互后端；成功信号由挂载组件统一转发。</summary>
public partial class SignalInteraction : Interactable
{
	protected override bool OnInteract() => true;
}

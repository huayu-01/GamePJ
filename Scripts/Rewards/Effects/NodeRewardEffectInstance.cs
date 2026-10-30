using Godot;

namespace ActRoguelike.Rewards;

/// <summary>专用事件规则在节点尚未释放时也能同步停止订阅。</summary>
public interface IStoppableRewardRule { void Stop(); }

/// <summary>非战斗总线规则的托管适配器；脱离场景树时同步解除其专用订阅。</summary>
internal sealed class NodeRewardEffectInstance : RewardEffectInstance
{
	private readonly Node _node;
	public NodeRewardEffectInstance(RewardContext context, string id, Node node) : base(context, id) => _node = node;
	protected override void ApplyCore() => Context.Player.AddChild(_node);
	protected override void RemoveCore()
	{
		if (!GodotObject.IsInstanceValid(_node)) return;
		if (_node is IStoppableRewardRule rule) rule.Stop();
		_node.ProcessMode = Node.ProcessModeEnum.Disabled;
		if (_node is Node3D visual) visual.Hide();
		if (!IsTreeExitCleanup && _node.IsInsideTree() && GodotObject.IsInstanceValid(Context.Player) && Context.Player.IsInsideTree()) _node.GetParent()?.RemoveChild(_node);
		_node.QueueFree();
	}
}

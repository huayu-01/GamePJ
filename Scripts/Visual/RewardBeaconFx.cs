using ActRoguelike.Interaction;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Visual;

/// <summary>高价值奖励的可替换信标；仅消费交互物的只读状态。</summary>
public partial class RewardBeaconFx : Node
{
	[Export] public PackedScene? EffectScene { get; set; }
	[Export] public int MinimumLayer { get; set; } = 2;
	private RewardNode _owner = null!;
	private Node3D? _effect;
	private Color _color;
	private bool _emitting;
	public Node3D? Effect => _effect;
	public override void _Ready() => _owner = GetParent<RewardNode>();
	public override void _Process(double delta)
	{
		bool high = NodePlacement.LayerOf(_owner.Kind) >= MinimumLayer;
		bool active = high && _owner.Enabled && !_owner.Used && !_owner.Vanished && _owner.IsVisibleInTree();
		if (active && _effect == null && EffectScene != null)
		{
			_effect = EffectScene.Instantiate<Node3D>();
			_owner.AddChild(_effect);
			_effect.Scale = new Vector3(0.65f, 0.85f, 0.65f);
			_color = _owner.PresentationColor;
			FxPalette.Apply(_effect, _color, 1.1f);
		}
		if (_effect != null)
		{
			_effect.Visible = active;
			if (_color != _owner.PresentationColor)
			{
				_color = _owner.PresentationColor;
				FxPalette.Apply(_effect, _color, 1.1f);
			}
			if (_emitting != active) SetParticles(_effect, active);
			_emitting = active;
		}
		if (_owner.GetNodeOrNull<Node3D>("Aura") is { } aura) aura.Visible = !high || _effect == null;
	}
	private static void SetParticles(Node node, bool emitting)
	{
		if (node is GpuParticles3D particles) particles.Emitting = emitting;
		foreach (Node child in node.GetChildren()) SetParticles(child, emitting);
	}
	public override void _ExitTree()
	{
		if (IsInstanceValid(_effect)) _effect!.QueueFree();
		if (IsInstanceValid(_owner) && _owner.GetNodeOrNull<Node3D>("Aura") is { } aura) aura.Visible = true;
	}
}

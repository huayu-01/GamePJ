using ActRoguelike.Abilities;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Visual;

/// <summary>只观察真实近战判定次数，一次判定播放一次独立挥砍特效。</summary>
public partial class MeleeSwingFx : Node
{
	[Export] public PackedScene? EffectScene { get; set; }
	private MeleeComboAbility _ability = null!;
	private int _lastCount;
	public int SpawnCount { get; private set; }
	public Node3D? LastEffect { get; private set; }
	public override void _Ready()
	{
		_ability = GetParent<MeleeComboAbility>();
		_lastCount = _ability.StrikeCount;
	}
	public override void _Process(double delta)
	{
		int count = _ability.StrikeCount;
		int added = count - _lastCount;
		_lastCount = count;
		if (!_ability.Enabled || EffectScene == null) return;
		for (int i = 0; i < added; i++)
		{
			Node3D effect = EffectScene.Instantiate<Node3D>();
			// 必须早于进树播放：AnimationPlayer会缓存材质属性轨道的目标。
			Color color = ConfigRows.ById<AbilityConfig>(_ability.AbilityId)?.HudColor ?? Colors.LightCyan;
			FxPalette.Apply(effect, color, 1.25f);
			(GetTree().CurrentScene ?? GetTree().Root).AddChild(effect);
			effect.GlobalTransform = _ability.GetNode<Node3D>("Attack").GlobalTransform;
			Vector3 size = _ability.CurrentStepBoxSize;
			float width = Mathf.Clamp(size.X / 3f, 0.45f, 1.7f);
			float depth = Mathf.Clamp((size.Z > 0.01f ? size.Z : size.X) / 3f, 0.6f, 1.7f);
			effect.Scale = new Vector3(width, 1, depth);
			LastEffect = effect;
			SpawnCount++;
		}
	}
}

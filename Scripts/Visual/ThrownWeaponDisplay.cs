using ActRoguelike.Abilities;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Visual;

/// <summary>只读投掷运行事实；使用当前武器同源模型，不创建碰撞或战斗节点。</summary>
public partial class ThrownWeaponDisplay : Node3D
{
	[Export] public bool AlignToFlight { get; set; }
	[Export] public Vector3 ModelRotationDegrees { get; set; } = new(90, 0, 0);
	[Export] public Vector3 ModelOffset { get; set; }
	private string _path = string.Empty;
	private Node3D? _model;
	private float _angle;
	public override void _PhysicsProcess(double delta)
	{
		var ability = GetParent<WeaponThrowAbility>();
		Visible = ability.Enabled && ability.InFlight && (ability.OwnerUnit?.AbilitySlots is not { IsConfigured: true } slots || slots.Owns(ability));
		if (!Visible) { _angle = 0; return; }
		string path = ability.OwnerUnit?.GetNodeOrNull<CharacterDisplay>("Appearance")?.CurrentWeaponPath ?? string.Empty;
		if (_path != path)
		{
			_model?.QueueFree();
			_model = null;
			_path = path;
			if (path.Length > 0)
			{
				_model = RuntimeResources.Load<PackedScene>(path).Instantiate<Node3D>();
				AddChild(_model);
				_model.RotationDegrees = ModelRotationDegrees;
				_model.Position = ModelOffset;
			}
		}
		_angle += Mathf.DegToRad(ability.SpinDegreesPerSecond) * (float)delta;
		Vector3 direction = ability.LastFlightDirection;
		Basis basis = AlignToFlight
			? Basis.LookingAt(direction, Mathf.Abs(direction.Dot(Vector3.Up)) > 0.99f ? Vector3.Right : Vector3.Up)
			: new Basis(Vector3.Up, _angle);
		GlobalTransform = new Transform3D(basis, ability.WeaponPosition);
	}
}

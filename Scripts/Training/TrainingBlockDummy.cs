using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Training;

/// <summary>训练木桩：常驻正面格挡，并持续面向玩家；不抢输入、不挂玩家防御状态机。</summary>
public partial class TrainingBlockDummy : Node
{
	[Export] public float BlockDamageMultiplier { get; set; } = .2f;
	[Export] public float FrontDotThreshold { get; set; } = -.5f;

	private Unit? _unit;
	private BlockComponent? _block;

	public override void _Ready()
	{
		_unit = GetParent<Unit>();
		_block = _unit.GetNodeOrNull<BlockComponent>("Block");
		if (_block == null)
		{
			_block = new BlockComponent { Name = "Block" };
			_unit.AddChild(_block);
		}
		_block.Blocking = true;
		_block.BlockDamageMultiplier = BlockDamageMultiplier;
		_block.FrontDotThreshold = FrontDotThreshold;
		_block.BlockStunSeconds = 0f;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_unit == null || !IsInstanceValid(_unit) || _unit.IsDead) return;
		Unit? player = PlayerLocator.Player;
		if (player == null || !IsInstanceValid(player)) return;
		Vector3 to = player.GlobalPosition - _unit.GlobalPosition;
		to.Y = 0;
		if (to.LengthSquared() < .0001f) return;
		_unit.Rotation = new Vector3(0, Mathf.Atan2(-to.X, -to.Z), 0);
	}
}

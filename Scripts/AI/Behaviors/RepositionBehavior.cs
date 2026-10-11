using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.AI.Behaviors;

/// <summary>重新站位：在目标周围挑一个点走过去，到达后进入冷却。用于保持战场分布。</summary>
public partial class RepositionBehavior : Behavior
{
	[Export] public float MinDistance { get; set; } = 6f;
	[Export] public float MaxDistance { get; set; } = 12f;
	[Export] public float ArriveTolerance { get; set; } = 1.5f;
	[Export] public float RestartDelay { get; set; } = 4f;
	[Export] public float SpeedScale { get; set; } = 1f;

	private Vector3 _destination;
	private bool _hasDestination;
	private float _cooldownRemaining;

	public override bool CanRun()
	{
		_cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - (float)GetPhysicsProcessDeltaTime());
		return base.CanRun() && _cooldownRemaining <= 0f;
	}

	public override void OnEnter()
	{
		if (Target == null)
		{
			return;
		}

		float angle = (float)GD.RandRange(0d, Mathf.Tau);
		float radius = (float)GD.RandRange(MinDistance, MaxDistance);
		Vector3 offset = new(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
		_destination = Target.GlobalPosition + offset;
		_hasDestination = true;
	}

	public override void Tick(float delta)
	{
		if (Movement == null || !_hasDestination)
		{
			return;
		}

		Vector3 offset = _destination - OwnerUnit.GlobalPosition;
		offset.Y = 0f;

		if (offset.Length() <= ArriveTolerance)
		{
			Movement.SetIntent(Vector3.Zero, 0f);
			_hasDestination = false;
			_cooldownRemaining = RestartDelay;
			return;
		}

		Vector3 direction = offset.Normalized();
		Movement.FaceDirection(direction, delta);
		Movement.SetIntent(direction, OwnerUnit.MoveSpeed * SpeedScale);
	}

	public override void OnExit() => Movement?.Stop();
}

using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.AI;

/// <summary>
/// Controller（执行文档 §4.3）：决定单位想做什么 —— 选目标、按优先级挑行为。
/// 移动物理在 CharacterMovement，攻击执行在 Ability，这里只做决策。
/// </summary>
public partial class EnemyBrain : Node
{
	[Export] public float TargetRefreshInterval { get; set; } = 0.4f;
	[Export] public float SearchRange { get; set; } = 40f;

	public Unit OwnerUnit { get; private set; } = null!;
	public Unit? Target { get; private set; }

	private readonly List<Behavior> _behaviors = new();
	private readonly List<Ability> _abilities = new();
	private Behavior? _current;
	private float _refreshTimer;

	public override void _Ready()
	{
		OwnerUnit = GetParent<Unit>();
		foreach (Node child in GetChildren())
		{
			if (child is Behavior behavior)
			{
				behavior.Initialize(this, OwnerUnit);
				_behaviors.Add(behavior);
			}
		}

		_behaviors.Sort((a, b) => b.Priority.CompareTo(a.Priority));
		NodeSearch.CollectInSubtree(OwnerUnit, _abilities);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (OwnerUnit.IsDead)
		{
			return;
		}

		_refreshTimer -= dt;
		if (_refreshTimer <= 0f || Target == null || !Target.IsValidTarget)
		{
			_refreshTimer = TargetRefreshInterval;
			Target = Targeting.FindNearestHostile(OwnerUnit, OwnerUnit, SearchRange);
		}

		// 能力执行期间不做新决策：攻击/技能过程保持站桩与朝向。
		foreach (Ability ability in _abilities)
		{
			if (ability.IsBusy)
			{
				return;
			}
		}

		Behavior? next = null;
		foreach (Behavior behavior in _behaviors)
		{
			if (behavior.CanRun())
			{
				next = behavior;
				break;
			}
		}

		if (!ReferenceEquals(next, _current))
		{
			_current?.OnExit();
			_current = next;
			_current?.OnEnter();
		}

		if (_current != null)
		{
			_current.Tick(dt);
		}
		else
		{
			OwnerUnit.Movement?.Stop();
		}
	}
}

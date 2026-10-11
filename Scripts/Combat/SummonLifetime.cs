using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 召唤物存活时间（策划文档 §13 的召唤物规则之一）：到点自己消失，避免场上越堆越多。
/// 挂在召唤物预制体上，普通敌人不需要。
/// </summary>
public partial class SummonLifetime : Node
{
	[Export] public float Lifetime { get; set; } = 25f;

	/// <summary>剩余存活时间（HUD/调试可用）。</summary>
	public float Remaining { get; private set; }

	private Unit _unit = null!;

	public override void _Ready()
	{
		_unit = GetParent<Unit>();
		Remaining = Lifetime;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_unit.IsDead)
		{
			return;
		}

		Remaining -= (float)delta;
		if (Remaining <= 0f)
		{
			_unit.ForceKill();
		}
	}
}

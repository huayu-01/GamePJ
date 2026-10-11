using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// 增益光环（技能精英用）：给半径内的友军挂一个状态效果。
/// 效果本身走 StatusPresets，不在这里写具体数值分支。
/// </summary>
public partial class SummonAbility : Ability
{
	[Export] public PackedScene? SummonScene { get; set; }
	[Export] public int Count { get; set; } = 3;
	[Export] public float Radius { get; set; } = 6f;

	/// <summary>场上同时存在的召唤物上限（0 = 不限；Boss 的召唤增援走不限）。</summary>
	[Export] public int MaxAlive { get; set; }

	/// <summary>累计召唤数量（验证与调试用；也能给 HUD 显示）。</summary>
	public int Summoned { get; private set; }

	protected override bool CanActivate(Unit? target)
	{
		if (SummonScene == null || OwnerUnit == null)
		{
			return false;
		}

		if (MaxAlive <= 0)
		{
			return true;
		}

		int alive = 0;
		foreach (Node node in GetTree().GetNodesInGroup("summon_units"))
		{
			if (node is Unit unit && !unit.IsDead)
			{
				alive++;
			}
		}

		return alive < MaxAlive;
	}

	protected override void OnActivate(Unit? target)
	{
		if (SummonScene == null || OwnerUnit == null)
		{
			Finish();
			return;
		}

		Node parent = GetTree().CurrentScene ?? GetTree().Root;
		for (int i = 0; i < Count; i++)
		{
			var summoned = SummonScene.Instantiate<Unit>();
			parent.AddChild(summoned);
			float angle = Mathf.Tau * i / Mathf.Max(1, Count);
			summoned.GlobalPosition = OwnerUnit.GlobalPosition + new Vector3(Mathf.Cos(angle) * Radius, 0.6f, Mathf.Sin(angle) * Radius);
			Summoned++;
		}

		Finish();
	}
}
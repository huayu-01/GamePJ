using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Encounter;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// 在施法者附近召唤单位；生成前验证导航与身体净空，实际成功才计数。
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
	private readonly List<Unit> _summonedUnits = new();
	private static bool IsLiving(Unit unit) => IsInstanceValid(unit) && !unit.IsDead && !unit.IsQueuedForDeletion();

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

		return CountAlive() < MaxAlive;
	}

	private int CountAlive()
	{
		_summonedUnits.RemoveAll(unit => !IsLiving(unit));
		// 保留全场召唤物的旧计数，同时补计本技能生成但没有 Summon 标签的敌方增援。
		var alive = new HashSet<Unit>(_summonedUnits);
		foreach (Node node in GetTree().GetNodesInGroup("summon_units"))
		{
			if (node is Unit unit && IsLiving(unit))
			{
				alive.Add(unit);
			}
		}

		return alive.Count;
	}

	protected override void OnActivate(Unit? target)
	{
		if (SummonScene == null || OwnerUnit == null)
		{
			Finish();
			return;
		}

		Node parent = GetTree().CurrentScene ?? GetTree().Root;
		_summonedUnits.RemoveAll(unit => !IsLiving(unit));
		var reserved = new List<Vector3>();
		for (int i = 0; i < Count; i++)
		{
			if (MaxAlive > 0 && CountAlive() >= MaxAlive) break;
			var summoned = SummonScene.Instantiate<Unit>();

			// 召唤物表（`Config/Tables/Summons/summon_*.tres`）：生命/移速/攻击/持有技能/存活时间。
			// 与敌人一样，必须在 `Unit._EnterTree` 之前套用（AddChild 触发 _EnterTree 捕获基础值）。
			EnemyConfig? enemyRow = ConfigRows.ByScenePath<EnemyConfig>(SummonScene.ResourcePath);
			if (ConfigRows.ByScenePath<SummonConfig>(SummonScene.ResourcePath) is { } summonRow) summonRow.ApplyTo(summoned);
			else enemyRow?.ApplyTo(summoned);

			float radius = Mathf.Max(0f, Radius);
			if (!SpawnPlanner.TryFindSpawnPoint(OwnerUnit.GlobalPosition, radius * 0.5f, radius, out Vector3 point, summoned, reserved))
			{
				summoned.Free();
				continue;
			}

			parent.AddChild(summoned);
			summoned.GlobalPosition = point;
			if (enemyRow != null && summoned.Faction == Faction.Enemy) ConfigIndex.ScaleEnemy(summoned, this, false);
			reserved.Add(point);
			_summonedUnits.Add(summoned);
			Summoned++;
		}

		Finish();
	}
}

using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Encounter;
using Godot;

namespace ActRoguelike.Boss;

/// <summary>
/// Boss 压力（策划文档 §13）：战斗拖得越久，Boss 越强，避免无限拖。
/// 每 <see cref="Interval"/> 秒升一级：加伤害/攻速/移速，达到 <see cref="AddSpawnLevel"/> 后每次升级额外召唤增援。
/// 具体曲线是原型值，属待策划确认项，全部可在 Inspector 调。
/// </summary>
public partial class BossPressure : Node
{
	[Export] public float Interval { get; set; } = 12f;
	[Export] public int MaxLevel { get; set; } = 5;
	[Export] public float DamagePerLevel { get; set; } = 0.08f;
	[Export] public float AttackSpeedPerLevel { get; set; } = 0.06f;
	[Export] public float MoveSpeedPerLevel { get; set; } = 0.05f;

	[Export] public int AddSpawnLevel { get; set; } = 3;
	[Export] public PackedScene? AddScene { get; set; }
	[Export] public int AddsPerLevel { get; set; } = 1;
	[Export] public float AddSpawnRadius { get; set; } = 9f;

	public int Level { get; private set; }
	public float Elapsed { get; private set; }
	public int PendingAdds { get; private set; }
	private float _spawnRetry;

	/// <summary>
	/// 战斗开始时的初始压力等级（由 Stage 的免费挑战额度超出部分决定，策划文档 §6.3）。
	/// 赋值即立刻套用一次修正，所以可以在 Boss 实例化之后再设置。
	/// </summary>
	public int InitialLevel
	{
		get => _initialLevel;
		set
		{
			_initialLevel = Mathf.Max(0, value);
			Level = _initialLevel;
			if (_unit != null)
			{
				ApplyModifiers();
			}
		}
	}

	private int _initialLevel;
	private Unit _unit = null!;

	public override void _Ready()
	{
		_unit = GetParent<Unit>();
		Level = _initialLevel;
		ApplyModifiers();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_unit.IsDead)
		{
			PendingAdds = 0;
			return;
		}

		Elapsed += (float)delta;
		int level = Mathf.Min(MaxLevel, _initialLevel + (int)(Elapsed / Mathf.Max(1f, Interval)));
		if (level != Level)
		{
			bool leveled = level > Level;
			Level = level;
			ApplyModifiers();
			if (leveled && AddSpawnLevel > 0 && level >= AddSpawnLevel && AddScene != null)
				PendingAdds += Mathf.Max(0, AddsPerLevel);
		}

		_spawnRetry -= (float)delta;
		if (PendingAdds > 0 && _spawnRetry <= 0f)
		{
			SpawnAdds();
			_spawnRetry = 0.3f;
		}
	}

	private void ApplyModifiers()
	{
		_unit.Stats.RemoveBySource(this);
		_unit.Stats.AddModifier(new StatModifier { Stat = "DamageMultiplier", Operation = ModifierOperation.Multiply, Value = 1f + (DamagePerLevel * Level), Source = this });
		_unit.Stats.AddModifier(new StatModifier { Stat = "AttackSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = 1f + (AttackSpeedPerLevel * Level), Source = this });
		_unit.Stats.AddModifier(new StatModifier { Stat = "MoveSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = 1f + (MoveSpeedPerLevel * Level), Source = this });
		_unit.RefreshStats();
	}

	private void SpawnAdds()
	{
		if (AddScene == null)
		{
			return;
		}

		Node parent = GetTree().CurrentScene ?? GetTree().Root;
		var reserved = new List<Vector3>();
		while (PendingAdds > 0)
		{
			var add = AddScene.Instantiate<Unit>();

			// 单位表先套用，避免进树捕获错误基础值；失败不消耗本次增援。
			ConfigRows.ByScenePath<EnemyConfig>(AddScene.ResourcePath)?.ApplyTo(add);
			float radius = Mathf.Max(0f, AddSpawnRadius);
			if (!SpawnPlanner.TryFindSpawnPoint(_unit.GlobalPosition, radius * 0.5f, radius, out Vector3 point, add, reserved))
			{
				add.Free();
				return;
			}

			parent.AddChild(add);
			add.GlobalPosition = point;

			// 全局难度倍率：与 `EncounterController` 一样在**进树之后**套用（会回满生命）。
			// ⚠️ 反过来（进树前套）会让 `_EnterTree` 把乘过倍率的生命捕获成基础值，`_Ready` 再乘一次。
			ConfigIndex.ScaleEnemy(add, this, false);
			reserved.Add(point);
			PendingAdds--;
		}
	}
}

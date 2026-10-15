using ActRoguelike.Combat;
using ActRoguelike.Config;
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
			return;
		}

		Elapsed += (float)delta;
		int level = Mathf.Min(MaxLevel, _initialLevel + (int)(Elapsed / Mathf.Max(1f, Interval)));
		if (level == Level)
		{
			return;
		}

		bool leveled = level > Level;
		Level = level;
		ApplyModifiers();

		if (leveled && AddSpawnLevel > 0 && level >= AddSpawnLevel)
		{
			SpawnAdds();
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
		for (int i = 0; i < AddsPerLevel; i++)
		{
			var add = AddScene.Instantiate<Unit>();

			// ⚠️ 与其它 4 处生成路径**同一条**：实例化 → **套单位表** → `AddChild`
			// （见 `EncounterController.cs:571/383/856/895` 与 `SummonAbility.cs:61`）。
			// 顺序不能反：`Unit._EnterTree` 在 `AddChild` 时捕获基础值。
			//
			// 改前这里**只 `AddChild`**，于是首领召唤的小怪用的是场景值（Chaser **60 血**），
			// 既漏掉全局倍率（`enemy_global` 的 ×0.75 → **45 血**），也漏掉敌人行上的 AI 档案。
			// → 现在与波次刷怪出来的同类小怪**完全一致**（45 血 + `ai_chaser`）。
			ConfigRows.ByScenePath<EnemyConfig>(AddScene.ResourcePath)?.ApplyTo(add);

			parent.AddChild(add);
			float angle = (float)GD.RandRange(0d, Mathf.Tau);
			add.GlobalPosition = _unit.GlobalPosition + new Vector3(Mathf.Cos(angle) * AddSpawnRadius, 0.6f, Mathf.Sin(angle) * AddSpawnRadius);

			// 全局难度倍率：与 `EncounterController` 一样在**进树之后**套用（会回满生命）。
			// ⚠️ 反过来（进树前套）会让 `_EnterTree` 把乘过倍率的生命捕获成基础值，`_Ready` 再乘一次。
			ConfigIndex.ScaleEnemy(add, this, false);
		}
	}
}

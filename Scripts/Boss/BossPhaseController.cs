using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Boss;

/// <summary>
/// Boss 阶段控制（策划文档 §12）：按剩余生命比例切换阶段，
/// 每个阶段解锁一批能力并叠加一组 Modifier。能力与数值都在预制体里配，代码不认具体 Boss。
/// </summary>
public partial class BossPhaseController : Node
{
	/// <summary>进入各阶段的生命比例阈值（从高到低）。</summary>
	[Export] public float[] PhaseFractions { get; set; } = { 1.0f, 0.65f, 0.33f };

	[Export] public string[] Phase1Abilities { get; set; } = System.Array.Empty<string>();
	[Export] public string[] Phase2Abilities { get; set; } = System.Array.Empty<string>();
	[Export] public string[] Phase3Abilities { get; set; } = System.Array.Empty<string>();

	[Export] public float[] PhaseDamageMultipliers { get; set; } = { 1f, 1.2f, 1.5f };
	[Export] public float[] PhaseAttackSpeedMultipliers { get; set; } = { 1f, 1f, 1.2f };

	[Signal]
	public delegate void PhaseChangedEventHandler(int phase);

	public int Phase { get; private set; } = -1;
	public int PhaseCount => PhaseFractions.Length;

	private Unit _unit = null!;

	public override void _Ready()
	{
		_unit = GetParent<Unit>();
		_unit.AddToGroup("boss_units");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_unit.IsDead || PhaseFractions.Length == 0)
		{
			return;
		}

		float fraction = _unit.MaxHealth > 0f ? _unit.Health / _unit.MaxHealth : 0f;
		int target = 0;
		for (int i = 0; i < PhaseFractions.Length; i++)
		{
			if (fraction <= PhaseFractions[i])
			{
				target = i;
			}
		}

		if (target != Phase)
		{
			EnterPhase(target);
		}
	}

	private void EnterPhase(int phase)
	{
		Phase = phase;

		_unit.Stats.RemoveBySource(this);
		_unit.Stats.AddModifier(new StatModifier
		{
			Stat = "DamageMultiplier",
			Operation = ModifierOperation.Multiply,
			Value = ValueAt(PhaseDamageMultipliers, phase),
			Source = this,
		});
		_unit.Stats.AddModifier(new StatModifier
		{
			Stat = "AttackSpeedMultiplier",
			Operation = ModifierOperation.Multiply,
			Value = ValueAt(PhaseAttackSpeedMultipliers, phase),
			Source = this,
		});
		_unit.RefreshStats();

		Unlock(Phase1Abilities);
		if (phase >= 1)
		{
			Unlock(Phase2Abilities);
		}

		if (phase >= 2)
		{
			Unlock(Phase3Abilities);
		}

		EmitSignal(SignalName.PhaseChanged, phase);
	}

	private void Unlock(string[] abilityNames)
	{
		foreach (string name in abilityNames)
		{
			if (_unit.GetNodeOrNull<Abilities.Ability>(name) is { } ability)
			{
				ability.Enabled = true;
			}
		}
	}

	private static float ValueAt(float[] values, int index)
		=> index >= 0 && index < values.Length ? values[index] : 1f;
}

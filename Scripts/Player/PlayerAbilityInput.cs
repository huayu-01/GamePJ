using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Player;

/// <summary>
/// 玩家技能输入：把按键映射到角色/武器技能上。
/// 技能本身不知道输入从哪来，敌人用同一套能力由 Behavior 触发。
/// </summary>
public partial class PlayerAbilityInput : Node
{
	private Unit _unit = null!;
	private DashAbility? _dash;
	private BurstFireAbility? _burst;
	private SummonAbility? _summon;
	private AreaAttackAbility? _control;

	public override void _Ready()
	{
		_unit = GetParent<Unit>();
		foreach (Node child in _unit.GetChildren())
		{
			switch (child)
			{
				case DashAbility dash:
					_dash = dash;
					break;
				case BurstFireAbility burst:
					_burst = burst;
					break;
				case SummonAbility summon:
					_summon = summon;
					break;
				case AreaAttackAbility control:
					_control = control;
					break;
			}
		}
	}

	public override void _Process(double delta)
	{
		if (_unit.IsDead)
		{
			return;
		}

		if (_dash != null && Input.IsActionJustPressed("dash"))
		{
			_dash.TryActivate();
		}

		if (_burst != null && Input.IsActionJustPressed("burst"))
		{
			_burst.TryActivate();
		}

		// 角色 3：主动召唤（能力默认禁用，只有召唤师角色会启用）。
		if (_summon is { Enabled: true } && Input.IsActionJustPressed("summon"))
		{
			_summon.TryActivate();
		}

		// 角色 3：范围定身冲击（策划文档 §7.3 的控制部分）。
		if (_control is { Enabled: true } && Input.IsActionJustPressed("control"))
		{
			Unit? victim = Targeting.FindNearestHostile(_unit, _unit, _control.TriggerRadius);
			if (victim != null)
			{
				_control.TryActivate(victim);
			}
		}
	}
}

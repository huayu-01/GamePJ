using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Player;

/// <summary>
/// 玩家技能输入：把**按键动作**映射到角色/武器技能上。
///
/// <b>本轮的关键改动（用户硬要求：加角色/武器/技能不改代码）</b>：按键**不再按能力的 C# 类名硬编码**，
/// 而是读技能行的 <see cref="AbilityConfig.InputAction"/> 列 —— 想让一个新技能按某个键，
/// 就是**填一列表**，不是改这个文件。
///
/// 原来的写法是 <c>switch (child) { case BackdashAbility a: ... Input.IsActionJustPressed("skill_1") }</c>，
/// 于是"加一个技能"必然要动 C#，且 <c>AbilityConfig</c> 里连一列按键都没有（那条注释就是本轮推翻的决定）。
///
/// 支持点按、按住蓄力与按住维持（由技能行的强类型列决定）：
/// <list type="bullet">
/// <item>点按：<c>IsActionJustPressed</c> → <c>TryActivate()</c>。</item>
/// <item>按住蓄力：按下 → <c>TryActivate()</c>；按住期间每帧 <c>OnInputHeld(dt)</c>；松开 → <c>OnInputReleased()</c>。</item>
/// <item>按住维持：同样转发输入，由 <c>HoldToMaintain</c> 驱动独立效果，不进入蓄力状态。</item>
/// </list>
/// </summary>
public partial class PlayerAbilityInput : Node
{
	private Unit _unit = null!;
	private readonly List<Ability> _abilities = new();

	/// <summary>已经为哪些能力写过"这个按键动作在 project.godot 里不存在"的警告（只写一次，不刷屏）。</summary>
	private static readonly HashSet<string> WarnedMissingActions = new();

	public override void _Ready()
	{
		_unit = GetParent<Unit>();
		Collect(_unit);
	}

	/// <summary>把单位子树里的全部能力收进来（**不看类型**，只按表里的 <c>InputAction</c> 驱动）。</summary>
	private void Collect(Node root)
	{
		foreach (Node child in root.GetChildren())
		{
			if (child is Ability ability)
			{
				_abilities.Add(ability);
			}

			Collect(child);
		}
	}

	public override void _Process(double delta)
	{
		if (_unit.IsDead)
		{
			return;
		}

		float dt = (float)delta;
		foreach (Ability ability in _abilities)
		{
			if (!IsInstanceValid(ability))
			{
				continue;
			}

			string action = ability.InputAction;
			if (string.IsNullOrEmpty(action))
			{
				continue;
			}

			if (!InputMap.HasAction(action))
			{
				if (WarnedMissingActions.Add($"{action}"))
				{
					GD.PushWarning($"[PlayerAbilityInput] 技能 {ability.AbilityId} 要的按键动作「{action}」在 project.godot 的 [input] 里不存在，这个技能按不出来。");
				}

				continue;
			}

			if (!ability.HoldToCharge && !ability.HoldToMaintain)
			{
				if (Input.IsActionJustPressed(action))
				{
					Activate(ability);
				}

				continue;
			}

			// 按住式：按下开始，按住期间累加，松开结束。
			if (Input.IsActionJustPressed(action))
			{
				Activate(ability);
			}

			if (ability.IsActive && Input.IsActionPressed(action))
			{
				ability.OnInputHeld(dt);
			}

			// 维持类也检测当前按键状态，避免暂停期间松手后恢复时漏掉释放沿。
			// 同一渲染帧内松开又按下时，两个边沿均可能为真；最终仍按住不能被旧释放沿取消。
			if (ability.IsActive && !Input.IsActionPressed(action)
				&& (Input.IsActionJustReleased(action) || ability.IsMaintaining))
			{
				ability.OnInputReleased();
			}
		}
	}

	/// <summary>
	/// 在单位子树里找"绑在某个按键动作上"的技能（按技能行的 <see cref="AbilityConfig.InputAction"/> 匹配）。
	///
	/// 存在的理由：左键有两个消费者 —— 武器组件（<c>PlayerWeaponInput</c>）与"绑在左键上的武技"
	/// （角色 A 的能量掌）。两个消费者都只读**表**，所以"哪个技能在左键上"是配置，不是代码分支。
	/// </summary>
	public static Ability? FindByInputAction(Unit unit, string action)
	{
		if (string.IsNullOrEmpty(action))
		{
			return null;
		}

		foreach (Node node in unit.FindChildren("*", string.Empty, true, false))
		{
			if (node is Ability ability && ability.Enabled && ability.InputAction == action)
			{
				return ability;
			}
		}

		return null;
	}

	/// <summary>
	/// 触发一次"按下"。走 <see cref="Ability.TryPress"/> 而不是直接 <c>TryActivate</c> ——
	/// **多段连招**（角色 B 的三把近战武器武技1）的语义是"同一个键按第二下 = 推进到第 2 段"，
	/// 而那属于那个技能自己的事（基类默认实现就是 <c>TryActivate</c>，所以既有技能行为逐位不变）。
	///
	/// <para>需要"范围内有目标"的技能（近战/控制）自己校验距离，够不着就不出招。
	/// 这里只额外处理"需要一个目标"的通用约定：把最近敌对单位作为参数传进去（没有就传 null）。</para>
	/// </summary>
	private void Activate(Ability ability)
	{
		Unit? victim = Targeting.FindNearestHostile(_unit, _unit, 200f);
		ability.TryPress(victim);
	}
}

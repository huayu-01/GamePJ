using System.Collections.Generic;
using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 技能精英的技能随机化（策划文档 §13）：候选技能都挂在同一个预制体上，
/// 生成时只随机启用一个，于是同一波护卫里会同时出现不同种类的技能精英。
/// 候选技能名在预制体里配置，代码不认具体技能。
/// </summary>
public partial class EliteSkillRandomizer : Node
{
	[Export] public string[] Skills { get; set; } = System.Array.Empty<string>();

	/// <summary>本次随机到的技能名（调试/验证用）。</summary>
	public string Picked { get; private set; } = string.Empty;

	public override void _Ready()
	{
		var unit = GetParent<Unit>();
		if (Skills.Length == 0)
		{
			return;
		}

		var candidates = new List<Ability>();
		foreach (string name in Skills)
		{
			if (unit.GetNodeOrNull<Ability>(name) is { } ability)
			{
				ability.Enabled = false;
				candidates.Add(ability);
			}
		}

		if (candidates.Count == 0)
		{
			return;
		}

		Ability chosen = candidates[(int)(GD.Randi() % (uint)candidates.Count)];
		chosen.Enabled = true;
		Picked = chosen.Name;
	}
}

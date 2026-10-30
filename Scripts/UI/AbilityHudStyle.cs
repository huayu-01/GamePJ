using Godot;

namespace ActRoguelike.UI;

/// <summary>技能栏自身的显示策略；不参与输入、计时、冷却或伤害计算。</summary>
[Tool, GlobalClass]
public partial class AbilityHudStyle : Resource
{
	[Export] public Color Accent { get; set; } = new(.55f, .85f, .9f);
	[Export] public bool ShowChargeMeter { get; set; } = true;
	[Export] public bool ShowHoldStatus { get; set; } = true;
	[Export] public bool ShowEffectTimer { get; set; } = true;
	[Export] public bool ShowCharges { get; set; } = true;
	[Export] public string[] InputOrder { get; set; } = { "skill_1", "skill_2", "weapon_skill_1", "weapon_skill_2", "fall_attack", "" };
	public int Order(string input) { int index = System.Array.IndexOf(InputOrder, input); return index < 0 ? InputOrder.Length : index; }
}

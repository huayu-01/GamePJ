using System.Collections.Generic;
using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Config;

/// <summary>编辑器可筛选的技能增强基类；目标引用共用，行为和专用校验由各参数类型实现。</summary>
[Tool, GlobalClass]
public abstract partial class AbilityRewardTuning : Resource, IAbilityRewardTuning, IContentValidatable
{
	[Export] public AbilityConfig? TargetAbility { get; set; }
	public bool Supports(Ability ability) => Supports(ability.GetType());
	/// <summary>制作期只检查类型，不为判断兼容性构造临时技能节点。</summary>
	public abstract bool Supports(System.Type abilityType);
	public abstract void Apply(Ability ability);
	public abstract IEnumerable<string> Validate();
}

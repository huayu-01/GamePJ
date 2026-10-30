using System.Collections.Generic;
using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Config;

/// <summary>动作定义只存参数；时序、分支、动画事件与取消仍由 EnemyPlanAbility 协调。</summary>
[Tool, GlobalClass]
public abstract partial class EnemyActionDefinition : Resource, IContentValidatable
{
	public virtual bool RequiresAttackEvent => false;
	public virtual bool BlocksDuringStep => false;
	public virtual EnemyActionStep.ActionKind? SemanticKind => null;
	public virtual EnemyActionState Resolve(EnemyActionState state) => state with { Kind = SemanticKind ?? state.Kind };
	/// <summary>这些旧字段由动作资源负责，不在动作段重复编辑或保存。</summary>
	public virtual string[] OwnedStepProperties => SemanticKind.HasValue ? new[] { "Kind" } : System.Array.Empty<string>();
	public abstract EnemyActionExecution Create(EnemyActionContext context);
	public virtual IEnumerable<string> Validate() => System.Array.Empty<string>();
}

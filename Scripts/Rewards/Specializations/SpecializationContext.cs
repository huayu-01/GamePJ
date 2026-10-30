using System;
using System.Linq;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>特化只访问所属单位、只读同伴定义与公用战斗服务。</summary>
public sealed class SpecializationContext
{
	public Unit Owner { get; }
	public SpecializationRuntime? Runtime { get; }
	public string RewardId { get; }
	public SpecializationServices Services => Runtime?.Services ?? throw new InvalidOperationException("此特化需要运行协调器。");
	public SpecializationContext(Unit owner, SpecializationRuntime? runtime = null, string rewardId = "") { Owner = owner; Runtime = runtime; RewardId = rewardId; }
	public T? Definition<T>(SpecializationKind kind) where T : SpecializationBehaviorDefinition => Runtime?.Definition<T>(kind);
	public T? Behavior<T>(SpecializationKind kind) where T : SpecializationBehavior => Runtime?.Behavior<T>(kind);
	public bool Sprinting => Owner.GetNodeOrNull<SprintController>("Sprint")?.IsSprinting == true;
	public T? Ability<T>(AbilityConfig[] targets) where T : Abilities.Ability => targets.Select(t => Abilities.Ability.FindByAbilityId(Owner, t.Id)).OfType<T>().FirstOrDefault();
}

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

/// <summary>特化自己的参数与创建入口；跨特化事件仍由协调器分发。</summary>
[Tool, GlobalClass]
public abstract partial class SpecializationBehaviorDefinition : Resource, IContentValidatable
{
	public abstract SpecializationKind Kind { get; }
	[Export] public AbilityConfig[] Targets { get; set; } = System.Array.Empty<AbilityConfig>();
	[Export] public PackedScene? ProjectileScene { get; set; }
	[Export] public RewardConfig[] ConvertedRewards { get; set; } = System.Array.Empty<RewardConfig>();
	public SpecializationBehavior Create(Unit owner) => Create(new SpecializationContext(owner));
	public SpecializationBehavior Create(SpecializationContext context)
	{
		var copy = (SpecializationBehaviorDefinition)Duplicate();
		var behavior = copy.CreateCore(context); behavior.Bind(context, copy); return behavior;
	}
	protected abstract SpecializationBehavior CreateCore(SpecializationContext context);
	public virtual IEnumerable<string> Validate()
	{
		foreach (var p in GetType().GetProperties().Where(p => p.IsDefined(typeof(ExportAttribute))))
			if (p.GetValue(this) is float value && (!float.IsFinite(value) || value < 0)) yield return $"{p.Name}：数值必须非负且有限。";
		if (ProjectileScene != null)
		{
			Node root = ProjectileScene.Instantiate(); bool compatible = root is Projectile; root.Free();
			if (!compatible) yield return "ProjectileScene：弹体根节点必须使用 Projectile。";
		}
	}
	internal float Parameter(string key, float fallback)
	{
		var value = GetType().GetProperty(key)?.GetValue(this);
		return value is float f ? f : value is int i ? i : fallback;
	}
}

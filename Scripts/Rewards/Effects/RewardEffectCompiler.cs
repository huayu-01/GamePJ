using System;
using System.Linq;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>把效果资源编译为一次应用入口；实例分配只发生在获取时，不在每帧解析配置。</summary>
public static class RewardEffectCompiler
{
	public static Action<RewardContext>? Compile(RewardConfig row, RewardEffectDefinition[]? effects = null)
	{
		var definitions = effects ?? row.Effects.ToArray();
		for (int i = 0; i < definitions.Length; i++)
		{
			if (definitions[i] == null) { GD.PushWarning($"[Reward] {row.Id}.Effects[{i}]为空，已拒绝编译。"); return null; }
			var errors = definitions[i].Validate().ToArray();
			if (errors.Length > 0) { GD.PushWarning($"[Reward] {row.Id}.Effects[{i}]：{string.Join("；", errors)}"); return null; }
		}
		return context =>
		{
			var instances = new System.Collections.Generic.List<RewardEffectInstance>();
			try
			{
				foreach (var definition in definitions) { var instance = definition.Create(context, row.Id); instances.Add(instance); instance.Activate(); }
				foreach (var instance in instances) instance.Commit();
				var host = RewardEffectHost.Ensure(context.Player);
				foreach (var instance in instances) host.Own(context.Build, instance, row is RuneConfig);
			}
			catch { foreach (var instance in Enumerable.Reverse(instances)) instance.Rollback(); throw; }
		};
	}
}

using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Interaction;

/// <summary>仅将旧Kind映射到内置资源；自定义组件直接引用Definition。</summary>
internal static class InteractionDefinitionCatalog
{
	public static InteractionDefinition? For(InteractionKind kind)
	{
		if (kind == InteractionKind.None) return null;
		string path = $"res://Config/Interactions/{kind.ToString().ToLowerInvariant()}.tres";
		return ResourceLoader.Exists(path) ? RuntimeResources.Load<InteractionDefinition>(path) : null;
	}
}

/// <summary>后端自行消费实例覆盖项，挂载组件无需识别具体交互类型。</summary>
public interface IInteractionConfigurationConsumer
{
	void ConfigureInteraction(InteractionComponent component);
}

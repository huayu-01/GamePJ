using ActRoguelike.Config;

namespace ActRoguelike.Abilities;

/// <summary>具体能力自己消费专用参数；通用配置不再认识每一个能力子类。</summary>
public interface IAbilityConfigConsumer
{
	// 保留原先专用默认值与通用 Params 的覆盖顺序。
	void ConfigureBeforeParameters(AbilityConfig config) { }
	void ConfigureAfterParameters(AbilityConfig config) { }
}

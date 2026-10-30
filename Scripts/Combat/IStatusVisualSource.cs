using System.Collections.Generic;
using ActRoguelike.Config;

namespace ActRoguelike.Combat;

/// <summary>规则自行计时的临时效果只读表现；不伪造状态、不参与伤害或属性结算。</summary>
public interface IStatusVisualSource
{
	IEnumerable<EffectConfig> ActiveStatusVisuals();
}

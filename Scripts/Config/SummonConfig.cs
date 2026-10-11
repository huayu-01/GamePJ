using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 召唤物表的一行 —— 一个 `.tres` 文件就是一行（`Config/Tables/Summons/summon_*.tres`）。
/// 索引用 <see cref="BalanceConfig.Summons"/>。
///
/// 召唤物也是"单位"：生命 / 护甲 / 移速 / 攻击组 / 持有技能 / 类标签都来自
/// <see cref="UnitConfig"/>（Drone 的 `Tags = ["Summon"]` 决定它进 summon_units 组、
/// 不被敌人当成目标）。这里只加召唤物私有的一列：存活时间。
/// </summary>
[GlobalClass]
public partial class SummonConfig : UnitConfig
{
	/// <summary>召唤物预制体路径（与各技能行的 <c>SummonScene</c> 按路径配对）。</summary>
	[Export(PropertyHint.File, "*.tscn")] public string ScenePath { get; set; } = "res://Scenes/Summons/Drone.tscn";

	/// <summary>存活时间（秒；0 = 永不消失）。写进召唤物的 <c>Lifetime</c> 节点。</summary>
	[Export] public float LifetimeSeconds { get; set; } = 25f;

	/// <summary>套用通用单位参数后再处理召唤物私有项（存活时间）。</summary>
	public override void ApplyTo(Node target)
	{
		base.ApplyTo(target);

		if (target is not Unit unit || LifetimeSeconds <= 0f)
		{
			return;
		}

		if (NodeSearch.FindInSubtree<SummonLifetime>(unit) is { } lifetime)
		{
			lifetime.Lifetime = LifetimeSeconds;
		}
	}
}

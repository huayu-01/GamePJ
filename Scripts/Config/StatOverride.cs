using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 可序列化的一条属性修正（配置表版 <see cref="StatModifier"/>）。
/// 为什么要它：<see cref="StatModifier"/> 是普通 <c>sealed class</c>（`Scripts/Combat/StatSheet.cs`），
/// **不是 Resource，存不进 `.tres`**。所以配置行里用这个 Resource 表达同一条数据，
/// 运行期再由 <see cref="ToRuntime"/> 转成引擎侧使用的 <see cref="StatModifier"/>。
/// </summary>
[Tool]
[GlobalClass]
public partial class StatOverride : Resource
{
	/// <summary>属性键，与 <c>StatSheet</c> 的键名一致（<c>MoveSpeedMultiplier</c> / <c>DamagePerSecondAdd</c> …）。</summary>
	[Export] public string Stat { get; set; } = string.Empty;

	[Export] public ModifierOperation Operation { get; set; } = ModifierOperation.Multiply;

	[Export] public float Value { get; set; } = 1f;

	public StatModifier ToRuntime()
		=> new() { Stat = Stat, Operation = Operation, Value = Value };
}

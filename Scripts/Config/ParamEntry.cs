using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 通用「键值对」条目（设计文档 config-schema.md §4.3）：一个键 + 一个值 + 一句说明。
/// 用在增幅项的额外参数、技能/敌人的可选参数覆盖、关卡的额外配置。
/// 为什么用 <see cref="Variant"/> 而不是 float：键值对要能同时表达数字（1.15）、
/// 字符串（"Burn" / "res://Scenes/Summons/Drone.tscn"）与布尔，用 float 就得为字符串另开一张表。
/// </summary>
[GlobalClass]
public partial class ParamEntry : Resource
{
	/// <summary>参数名。与组件 [Export] 属性名一致时可以被 Set() 直接套用。</summary>
	[Export] public string Key { get; set; } = string.Empty;

	/// <summary>值。用 Variant 才能既放数字又放字符串/资源路径。</summary>
	[Export] public Variant Value { get; set; } = 0f;

	/// <summary>这一条是干什么的（给策划看，也可用于 {Key} 文本模板）。</summary>
	[Export] public string Note { get; set; } = string.Empty;

	public float AsFloat(float fallback = 0f)
		=> Value.VariantType is Variant.Type.Float or Variant.Type.Int ? Value.AsSingle() : fallback;

	public int AsInt(int fallback = 0)
		=> Value.VariantType is Variant.Type.Float or Variant.Type.Int ? Value.AsInt32() : fallback;

	public bool AsBool(bool fallback = false)
		=> Value.VariantType == Variant.Type.Bool ? Value.AsBool() : fallback;

	public string AsText(string fallback = "")
		=> Value.VariantType is Variant.Type.String or Variant.Type.StringName ? Value.AsString() : fallback;
}

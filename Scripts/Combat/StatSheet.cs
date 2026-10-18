using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Combat;

public enum ModifierOperation
{
	Add,
	Multiply,
	Override,
	Cap,
}

/// <summary>一条属性修正。Source 用于成组移除（例如某个增益失效时）。</summary>
public sealed class StatModifier
{
	public required string Stat { get; init; }
	public ModifierOperation Operation { get; init; } = ModifierOperation.Add;
	public float Value { get; init; }
	public object? Source { get; init; }
	/// <summary>产生修正器的配置行，供指定奖励转换；不替代成组撤销的Source。</summary>
	public string OriginId { get; init; } = string.Empty;
}

/// <summary>将指定来源行的加算修正映射到另一属性，获取顺序无关。</summary>
public sealed record StatConversion(string From, string To, float Ratio, HashSet<string> OriginIds, object Source);

/// <summary>
/// 单位属性表：基础值 + 修正器列表（执行文档 §3.5 Modifier）。
/// 结算顺序固定为 Override → Add → Multiply → Cap，避免不同效果各自隐藏叠加（战斗规则说明 §16）。
/// 属性名是字符串键，新增内容不需要改这里的代码。
/// 继承 RefCounted 是为了能从编辑器脚本/GDScript 侧直接读数值做验证。
/// </summary>
public sealed partial class StatSheet : RefCounted
{
	private readonly Dictionary<string, float> _baseValues = new();
	private readonly List<StatModifier> _modifiers = new();
	private readonly List<StatConversion> _conversions = new();
	public void AddConversion(StatConversion conversion) => _conversions.Add(conversion);

	public void SetBase(string stat, float value) => _baseValues[stat] = value;

	public float GetBase(string stat) => _baseValues.TryGetValue(stat, out float value) ? value : 0f;

	public void AddModifier(StatModifier modifier) => _modifiers.Add(modifier);

	public void RemoveBySource(object source)
	{
		_modifiers.RemoveAll(m => ReferenceEquals(m.Source, source));
		_conversions.RemoveAll(c => ReferenceEquals(c.Source, source));
	}

	public float Get(string stat)
	{
		float? overrideValue = null;
		float add = 0f;
		float multiply = 1f;
		float? cap = null;

		foreach (StatModifier modifier in _modifiers)
		{
			string effectiveStat = modifier.Stat;
			float effectiveValue = modifier.Value;
			foreach (StatConversion conversion in _conversions)
			{
				if (modifier.Operation == ModifierOperation.Add && modifier.Stat == conversion.From && conversion.OriginIds.Contains(modifier.OriginId))
				{
					effectiveStat = conversion.To;
					effectiveValue *= conversion.Ratio;
					break;
				}
			}
			if (effectiveStat != stat)
			{
				continue;
			}

			switch (modifier.Operation)
			{
				case ModifierOperation.Override:
					overrideValue = modifier.Value;
					break;
				case ModifierOperation.Add:
					add += effectiveValue;
					break;
				case ModifierOperation.Multiply:
					multiply *= modifier.Value;
					break;
				case ModifierOperation.Cap:
					cap = cap.HasValue ? Mathf.Min(cap.Value, modifier.Value) : modifier.Value;
					break;
			}
		}

		float value = (overrideValue ?? GetBase(stat)) + add;
		value *= multiply;

		if (cap.HasValue)
		{
			value = Mathf.Min(value, cap.Value);
		}

		return value;
	}
}

using System.Globalization;
using System.Text;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 配置表里的文本模板（设计文档 config-schema.md §4.4）：把 "伤害 +{Value}%" 里的 {Key} 换成行里的实际值。
/// 规则：
///   {字段名}        优先取行自身的 [Export] 属性（走 GodotObject.Get）
///   {Params 键}     再取 Params 里同名 Key 的值
///   {Modifiers.属性键:格式} 奖励行直接读取该属性修饰器的Value，说明与效果共用同一个表值。
///   {字段名:格式}   **可选的格式说明符**（.NET 数字格式串，如 {MaxHealth:0} / {Value:0.0}）：
///                   只对数字字段生效（套 <c>double.ToString(格式)</c>），非数字字段与无效格式串一律**忽略冒号后的部分**；
///                   格式串不合法（<c>FormatException</c>）时退回默认写法，**不会抛异常、也不会多打一条警告**。
///   都找不到 → 原样保留 {Key} 并打一条警告（不抛异常、不显示空白）
/// 刻意**不做表达式求值**（没有 eval、没有内嵌脚本）—— 配置表只放数据，不放逻辑（§6.1）。
/// </summary>
public static class ConfigText
{
	public static string Format(string template, GodotObject source, System.Collections.Generic.IReadOnlyList<ParamEntry>? extra = null)
	{
		if (string.IsNullOrEmpty(template))
		{
			return string.Empty;
		}

		var builder = new StringBuilder(template.Length + 16);
		int i = 0;
		while (i < template.Length)
		{
			int open = template.IndexOf('{', i);
			if (open < 0)
			{
				builder.Append(template, i, template.Length - i);
				break;
			}

			int close = template.IndexOf('}', open + 1);
			if (close < 0)
			{
				builder.Append(template, i, template.Length - i);
				break;
			}

			builder.Append(template, i, open - i);
			string token = template.Substring(open + 1, close - open - 1);

			// {字段:格式}：冒号后面是 .NET 数字格式串（{MaxHealth:0} / {Value:0.0}）。
			// 这里**只切开**、不解释：解释在 TryResolve 里按值的类型做（非数字直接忽略格式串）。
			string key = token;
			string? spec = null;
			int colon = token.IndexOf(':');
			if (colon >= 0)
			{
				key = token.Substring(0, colon);
				spec = token.Substring(colon + 1);
			}

			if (TryResolve(source, extra, key, spec, out string text))
			{
				builder.Append(text);
			}
			else
			{
				GD.PushWarning($"[ConfigText] 模板里的 {{{token}}} 在配置行里找不到，原样保留。");
				builder.Append('{').Append(token).Append('}');
			}

			i = close + 1;
		}

		return builder.ToString();
	}

	private static bool TryResolve(GodotObject source, System.Collections.Generic.IReadOnlyList<ParamEntry>? extra, string key, string? spec, out string text)
	{
		// 1) 先找 Params（键值对组里的 Note 也能当文本用）
		if (extra != null)
		{
			foreach (ParamEntry entry in extra)
			{
				if (entry.Key == key)
				{
					// 文本值原样返回；数字值在没有格式串时与以前**逐字节一致**（同一句 ToString）。
					text = entry.AsText(NumberText(entry.AsFloat(), entry.AsFloat().ToString(CultureInfo.InvariantCulture), spec));
					return true;
				}
			}
		}

		// 奖励说明直接读取强类型修饰器；不复制一份说明参数，也不做表达式求值。
		const string modifierPrefix = "Modifiers.";
		if (source is RewardConfig reward && key.StartsWith(modifierPrefix, System.StringComparison.Ordinal))
		{
			string stat = key.Substring(modifierPrefix.Length);
			foreach (StatOverride modifier in reward.Modifiers)
			{
				if (modifier.Stat != stat) continue;
				text = NumberText(modifier.Value, modifier.Value.ToString("0.##", CultureInfo.InvariantCulture), spec);
				return true;
			}
			text = string.Empty;
			return false;
		}

		// 再找行自身的 [Export] 属性（引擎自带 Get，不需要维护映射）。
		Variant value = source.Get(key);
		if (value.VariantType != Variant.Type.Nil)
		{
			text = value.VariantType switch
			{
				Variant.Type.Float => NumberText(value.AsDouble(), value.AsSingle().ToString("0.##", CultureInfo.InvariantCulture), spec),
				Variant.Type.Int => NumberText(value.AsDouble(), value.AsInt32().ToString(CultureInfo.InvariantCulture), spec),
				Variant.Type.Bool => value.AsBool() ? "是" : "否",
				Variant.Type.String or Variant.Type.StringName => value.AsString(),
				_ => value.ToString(),
			};
			return true;
		}

		text = string.Empty;
		return false;
	}

	/// <summary>
	/// 按 {字段:格式} 的格式串写一个数字；没有格式串就返回 <paramref name="fallback"/>（调用方原来的写法）。
	/// 格式串写错（<c>FormatException</c>）时同样退回 <paramref name="fallback"/> ——
	/// 模板是给人写的，写错格式**只影响这一处显示**，绝不能让它变成异常或刷屏警告。
	/// </summary>
	private static string NumberText(double number, string fallback, string? spec)
	{
		if (string.IsNullOrEmpty(spec))
		{
			return fallback;
		}

		try
		{
			return number.ToString(spec, CultureInfo.InvariantCulture);
		}
		catch (System.FormatException)
		{
			return fallback;
		}
	}
}

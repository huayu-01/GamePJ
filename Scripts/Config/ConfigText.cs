using System.Globalization;
using System.Text;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 配置表里的文本模板（设计文档 config-schema.md §4.4）：把 "伤害 +{Value}%" 里的 {Key} 换成行里的实际值。
/// 规则：
///   {字段名}      优先取行自身的 [Export] 属性（走 GodotObject.Get）
///   {Params 键}   再取 Params 里同名 Key 的值
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
			string key = template.Substring(open + 1, close - open - 1);

			if (TryResolve(source, extra, key, out string text))
			{
				builder.Append(text);
			}
			else
			{
				GD.PushWarning($"[ConfigText] 模板里的 {{{key}}} 在配置行里找不到，原样保留。");
				builder.Append('{').Append(key).Append('}');
			}

			i = close + 1;
		}

		return builder.ToString();
	}

	private static bool TryResolve(GodotObject source, System.Collections.Generic.IReadOnlyList<ParamEntry>? extra, string key, out string text)
	{
		// 1) 先找 Params（键值对组里的 Note 也能当文本用）
		if (extra != null)
		{
			foreach (ParamEntry entry in extra)
			{
				if (entry.Key == key)
				{
					text = entry.AsText(entry.AsFloat().ToString(CultureInfo.InvariantCulture));
					return true;
				}
			}
		}

		// 2) 再找行自身的 [Export] 属性（引擎自带 Get，不需要维护映射）
		Variant value = source.Get(key);
		if (value.VariantType != Variant.Type.Nil)
		{
			text = value.VariantType switch
			{
				Variant.Type.Float => value.AsSingle().ToString("0.##", CultureInfo.InvariantCulture),
				Variant.Type.Int => value.AsInt32().ToString(CultureInfo.InvariantCulture),
				Variant.Type.Bool => value.AsBool() ? "是" : "否",
				Variant.Type.String or Variant.Type.StringName => value.AsString(),
				_ => value.ToString(),
			};
			return true;
		}

		text = string.Empty;
		return false;
	}
}

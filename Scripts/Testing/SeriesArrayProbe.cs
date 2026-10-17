using ActRoguelike.Core;
using System.Collections.Generic;
using System.Globalization;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>
/// **一次性探针（P8 第 0 步，2026-10-16）**：实测"数组型表参数"在**当前**（已重启的）编辑器 /
/// 运行期下**能不能导出 + 读回来** —— 用来判定 P4 那三条"实测限制"里哪几条是真的。
///
/// <para><b>P4 写在 <see cref="AbilitySeries"/> 类注释里的三条</b>（这次逐条实测）：</para>
/// <list type="number">
/// <item><c>[Export] float[]</c> / <c>string[]</c> 编译不过（<c>GD0102</c>）；</item>
/// <item><c>PackedFloat32Array</c> / <c>PackedStringArray</c> 在 C# 侧没有公开类型；</item>
/// <item><c>Godot.Collections.Array&lt;T&gt;</c> / <c>T[]</c> / 多个 <c>[Export] Resource</c>
/// 在运行期**读不回来**（<c>Count</c> 读得到、每一项都是 <c>null</c>）。</item>
/// </list>
///
/// <para>探针只读 + 只往 <c>user://</c> 写一个临时 .tres，**不碰任何正式数据**；
/// 触发点在 <c>SelfTest.ReportTables</c>（自测的"配置表比对"那一段）。</para>
/// </summary>
public static class SeriesArrayProbe
{
	/// <summary>探针产物（写在 user://，不进仓库；重复运行会覆盖）。</summary>
	public const string ProbePath = "user://p8_series_probe.tres";

	private static readonly string[] Watched =
	{
		"ComboAnimations", "StepDamageScales", "StepBoxSizes", "StepBoxOffsets",
		"StepActiveTimes", "CutSceneNames", "CutExcludeSceneNames",
	};

	/// <summary>跑一遍探针，返回要写进日志的每一行（原样）。</summary>
	public static List<string> Run()
	{
		var lines = new List<string>();
		var probeRow = new AbilitySeries();
		Script? probeScript = probeRow.GetScript().AsGodotObject() as Script;
		lines.Add("P8探针① 属性表（AbilitySeries 的数组列在 Godot 那边注册成了什么）：");
		lines.Add($"    GodotObject.GetPropertyList() 共 {probeRow.GetPropertyList().Count} 项；Script.GetScriptPropertyList() 共 {probeScript?.GetScriptPropertyList().Count ?? -1} 项（后者就是编辑器 Inspector 的那一份）");
		if (probeScript?.GetScriptPropertyList() is { } scriptProps)
		{
			foreach (Godot.Collections.Dictionary prop in scriptProps)
			{
				string name = prop["name"].AsString();
				bool watched = false;
				foreach (string w in Watched)
				{
					watched |= name == w;
				}

				if (!watched)
				{
					continue;
				}

				lines.Add(
					$"    {name}: Variant.Type={prop["type"].AsInt32()} hint={(PropertyHint)prop["hint"].AsInt32()} hintString=\"{prop["hint_string"].AsString()}\"");
			}
		}

		// ② 现有铁证：技能行里的 `Params` 就是 ParamEntry[]（托管对象数组），P1 起一直在用。
		const string skillRowPath = "res://Config/Tables/Skills/skl_b1_w1_1.tres";
		AbilityConfig? skillRow = ResourceLoader.Exists(skillRowPath) ? RuntimeResources.Load<AbilityConfig>(skillRowPath) : null;
		if (skillRow == null)
		{
			lines.Add($"P8探针② 🔴 {skillRowPath} 读不到（下面的判断失去参照）。");
		}
		else
		{
			var parts = new List<string>();
			foreach (ParamEntry? entry in skillRow.Params)
			{
				parts.Add(entry == null ? "null" : $"{entry.Key}={entry.Value}");
			}

			lines.Add($"P8探针② 现有数据里的 ParamEntry[]（{skillRowPath} 的 Params）：Length={skillRow.Params.Length} 每项[{string.Join(", ", parts)}]");
		}

		// ③ 写盘 → 读回：走的就是 .tres 的序列化/反序列化那一整条路。
		var written = new AbilitySeries
		{
			Id = "p8_probe",
			DisplayName = "P8 探针",
			ComboNextEvent = "ATK",
			ComboWindowSeconds = 0.45f,
			ComboLoop = true,
		};
		written.ComboAnimations.Add("A1");
		written.ComboAnimations.Add("A2");
		written.StepDamageScales.Add(1f);
		written.StepDamageScales.Add(1.15f);
		written.StepBoxSizes.Add(new Vector3(2.2f, 1.6f, 2.8f));
		written.StepBoxSizes.Add(new Vector3(7.2f, 1.8f, 0f));
		written.StepBoxOffsets.Add(1.4f);
		written.StepActiveTimes.Add(0.1f);
		written.CutSceneNames.Add("Bullet");
		written.CutExcludeSceneNames.Add("EnergyWave");
		written.Params = new[]
		{
			new ParamEntry { Key = "P1", Value = 1f },
			new ParamEntry { Key = "P2", Value = "text" },
		};

		Error saveError = ResourceSaver.Save(written, ProbePath);
		lines.Add($"P8探针③ 写盘 ResourceSaver.Save(\"{ProbePath}\") → {saveError}（Ok=0）");
		lines.Add("P8探针③ 盘上原文（这就是「正规数组」在 .tres 里的样子）：");
		foreach (string line in FileAccess.GetFileAsString(ProbePath).Split('\n'))
		{
			string trimmed = line.TrimEnd('\r').TrimEnd();
			if (trimmed.Length > 0)
			{
				lines.Add($"    {trimmed}");
			}
		}

		AbilitySeries? back = RuntimeResources.Load<AbilitySeries>(ProbePath);
		if (back == null)
		{
			lines.Add("P8探针③ 🔴 写下去的 .tres 读不回来。");
			return lines;
		}

		lines.Add(
			$"P8探针③ 读回：Variant.Type(ComboAnimations)={back.Get("ComboAnimations").VariantType}"
			+ $"｜段动画 {back.ComboAnimations.Count} 项 [{string.Join(",", back.ComboAnimations)}]"
			+ $"｜伤害倍率 {back.StepDamageScales.Count} 项 [{Join(back.StepDamageScales)}]"
			+ $"｜盒子尺寸 {back.StepBoxSizes.Count} 项 [{Join(back.StepBoxSizes)}]"
			+ $"｜前伸 {back.StepBoxOffsets.Count} 项 [{Join(back.StepBoxOffsets)}]"
			+ $"｜判定时长 {back.StepActiveTimes.Count} 项 [{Join(back.StepActiveTimes)}]"
			+ $"｜斩断名单 [{string.Join(",", back.CutSceneNames)}] 排除 [{string.Join(",", back.CutExcludeSceneNames)}]"
			+ $"｜窗口 {back.ComboWindowSeconds.ToString("0.###", CultureInfo.InvariantCulture)} 循环 {back.ComboLoop} 事件帧 {back.ComboNextEvent}");

		var entryParts = new List<string>();
		foreach (ParamEntry? entry in back.Params)
		{
			entryParts.Add(entry == null ? "null" : $"{entry.Key}={entry.Value}");
		}

		lines.Add($"P8探针③ 读回 Params（ParamEntry[]）：Length={back.Params.Length} 每项[{string.Join(", ", entryParts)}]");

		// 最强判据（P4 说"Count 读得到、每一项都是 null"——所以两项都要查）：
		bool countsOk = back.ComboAnimations.Count == 2 && back.StepDamageScales.Count == 2
			&& back.StepBoxSizes.Count == 2 && back.StepBoxOffsets.Count == 1 && back.StepActiveTimes.Count == 1
			&& back.CutSceneNames.Count == 1 && back.CutExcludeSceneNames.Count == 1;
		bool itemsOk = back.Params.Length == 2 && back.Params[0] != null && back.Params[1] != null
			&& back.Params[0].Key == "P1" && back.Params[1].Key == "P2";
		bool valuesOk = back.ComboAnimations[0] == "A1" && back.ComboAnimations[1] == "A2"
			&& back.StepDamageScales[1] == 1.15f && back.StepBoxSizes[1] == new Vector3(7.2f, 1.8f, 0f)
			&& back.CutSceneNames[0] == "Bullet" && back.CutExcludeSceneNames[0] == "EnergyWave"
			&& back.ComboLoop && back.ComboWindowSeconds == 0.45f;
		lines.Add(
			$"{(countsOk && itemsOk && valuesOk ? "✅" : "🔴")} P8探针·结论：项数对={countsOk} 每一项非空且键对={itemsOk} 值逐位对={valuesOk}"
			+ "（P4 的第 ③ 条说「Count 读得到、每一项都是 null」 —— 这条**不成立**）");

		return lines;
	}

	private static string Join(Godot.Collections.Array<float> values)
	{
		var parts = new List<string>();
		foreach (float value in values)
		{
			parts.Add(value.ToString("0.###", CultureInfo.InvariantCulture));
		}

		return string.Join(",", parts);
	}

	private static string Join(Godot.Collections.Array<Vector3> values)
	{
		var parts = new List<string>();
		foreach (Vector3 value in values)
		{
			parts.Add($"({value.X.ToString("0.###", CultureInfo.InvariantCulture)},{value.Y.ToString("0.###", CultureInfo.InvariantCulture)},{value.Z.ToString("0.###", CultureInfo.InvariantCulture)})");
		}

		return string.Join(",", parts);
	}
}

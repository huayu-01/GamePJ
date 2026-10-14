using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Editor;

/// <summary>
/// **技能动画生成器**（`docs/refactor-survey.md` §1.4 的落地实现）：
/// 按 <see cref="SkillAnimationConfig"/> 表逐行「复制现有素材动画 → 改名 → 挂事件帧 → 存 `.tres`」。
///
/// <b>为什么是生成而不是运行期现造</b>：`.glb` 里的 32 段 <c>Animation</c> 是**导入的子资源**，
/// 13 个模型文件各自的实例共享它们；运行期 `Duplicate + AddTrack` 一旦漏掉一步就会串味。
/// 生成期落成独立 `.tres` 之后，运行期只是 `ResourceLoader.Load` —— 没有任何共享写。
///
/// <b>怎么用（加一段新技能动画只要 3 步，都不改代码）</b>：
/// <list type="number">
/// <item>在 `Config/Tables/SkillAnimations/` 加一行 `.tres`（源模型 / 源动画名 / 目标名 /
///   事件帧名与时刻 / 目标时长）。</item>
/// <item>在 <c>Config/ConfigIndex.tres</c> 的 <c>SkillAnimations</c> 数组里加上这一行
///   （**同时**加 `[ext_resource]` 并改 `load_steps`，三者缺一不可）。</item>
/// <item>编辑器里跑「项目 → 工具 → 生成技能动画」，或命令行
///   <c>Godot --headless --path &lt;项目&gt; --script res://Scripts/Editor/GenerateSkillAnimations.cs</c>。</item>
/// </list>
/// 之后运行期由 <c>UnitAnimator</c> 按单位表的 <c>AnimLibraryDir</c> 自动加载，
/// 技能行里的 <c>AnimName</c> 按「库名/动画名」查（**必须全路径**，见 <see cref="SkillAnimationLibrary"/>）。
/// </summary>
public static class SkillAnimationGenerator
{
	/// <summary>一行生成的结果（日志与自测用）。</summary>
	public sealed class Result
	{
		public required string Id { get; init; }

		public required bool Ok { get; init; }

		public string OutputPath { get; init; } = string.Empty;

		public string QualifiedName { get; init; } = string.Empty;

		public int TrackCount { get; init; }

		public float Length { get; init; }

		public int EventCount { get; init; }

		/// <summary>失败 / 跳过的原因（Ok=false 时非空）。</summary>
		public string Note { get; init; } = string.Empty;
	}

	/// <summary>把整张表跑一遍，逐行生成。返回每一行的结果（顺序与入参一致）。</summary>
	public static List<Result> Generate(IEnumerable<SkillAnimationConfig> rows)
	{
		var results = new List<Result>();
		var modelCache = new Dictionary<string, PackedScene>();

		foreach (SkillAnimationConfig row in rows)
		{
			if (row == null)
			{
				continue;
			}

			results.Add(GenerateOne(row, modelCache));
		}

		return results;
	}

	private static Result GenerateOne(SkillAnimationConfig row, Dictionary<string, PackedScene> cache)
	{
		if (string.IsNullOrEmpty(row.Id) || string.IsNullOrEmpty(row.SourceModelPath) || string.IsNullOrEmpty(row.TargetName))
		{
			return Fail(row, "Id / SourceModelPath / TargetName 有空值");
		}

		if (!ResourceLoader.Exists(row.SourceModelPath))
		{
			return Fail(row, $"源模型读不到：{row.SourceModelPath}");
		}

		if (!cache.TryGetValue(row.SourceModelPath, out PackedScene? model))
		{
			model = GD.Load<PackedScene>(row.SourceModelPath);
			cache[row.SourceModelPath] = model;
		}

		if (model == null)
		{
			return Fail(row, $"源模型不是 PackedScene：{row.SourceModelPath}");
		}

		// 源片段必须在**临时实例**上取：`.glb` 的 AnimationPlayer 自带 32 段，直接拿引用来 Duplicate。
		Node root = model.Instantiate();
		Animation? source;
		AnimationPlayer? sourcePlayer;
		try
		{
			sourcePlayer = root.GetNodeOrNull<AnimationPlayer>(row.SourcePlayerPath) ?? FindInSubtree<AnimationPlayer>(root);
			if (sourcePlayer == null)
			{
				return Fail(row, $"{row.SourceModelPath} 的 {row.SourcePlayerPath} 下没有 AnimationPlayer");
			}

			source = sourcePlayer.GetAnimation(row.SourceAnimation);
			if (source == null)
			{
				return Fail(row, $"{row.SourceModelPath} 里没有动画「{row.SourceAnimation}」（现有 {sourcePlayer.GetAnimationList().Length} 段）");
			}

			Animation target = Build(row, source, root);
			string path = string.IsNullOrEmpty(row.OutputDir)
				? SkillAnimationLibrary.OutputPath(row.Library, row.TargetName)
				: $"{row.OutputDir.TrimEnd('/')}/{row.TargetName}.tres";

			// 首次生成时目录还不存在 —— ResourceSaver 不会自己建目录（会返回 CantOpen）。
			EnsureDir(path);

			Error err = ResourceSaver.Save(target, path);
			if (err != Error.Ok)
			{
				return Fail(row, $"保存 {path} 失败：{err}");
			}
			// 回读一次：证明"存下去的真的能被读回来、轨道与事件帧都在"（不做这一步的话，
			// 写坏一个 `.tres` 只会在运行期表现为"技能静默不执行"）。
			var reread = ResourceLoader.Load<Animation>(path);
			if (reread == null)
			{
				return Fail(row, $"{path} 存下去了但回读失败");
			}

			return new Result
			{
				Id = row.Id,
				Ok = true,
				OutputPath = path,
				QualifiedName = SkillAnimationLibrary.Qualified(row.Library, reread.ResourceName),
				TrackCount = reread.GetTrackCount(),
				Length = (float)reread.Length,
				EventCount = CountMethodKeys(reread),
			};
		}
		finally
		{
			// 临时实例用完必须释放：它是整个 `.glb` 的实例（含骨骼与网格）。
			root.Free();
		}
	}

	/// <summary>
	/// 「复制 → 改名 → 加事件帧」的核心（与 `refactor-survey.md` §1.4 实测过的 API 逐条对应）。
	///
	/// ⚠️ <c>Duplicate(true)</c> 必须**深**复制：`.glb` 的 Animation 是共享子资源，
	/// 浅复制会让 <c>AddTrack</c> 改到原始动画上（= 串味到同类单位）。
	/// </summary>
	/// <param name="modelRoot">源模型实例的根（= <c>AnimationMixer.RootNode</c> 解析到的那一级）。</param>
	private static Animation Build(SkillAnimationConfig row, Animation source, Node modelRoot)
	{
		var target = (Animation)source.Duplicate(true);
		target.ResourceName = row.TargetName;
		target.LoopMode = row.Loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;

		float sourceLength = (float)source.Length;
		float targetLength = row.ResolveLength(sourceLength);
		// 比例：拉长/缩短后事件帧仍落在相对同一个位置（"0.08s 那一帧"在拉长后的动画里还在 0.08/L 处）。
		float scale = sourceLength > 0.0001f && targetLength > 0.0001f ? targetLength / sourceLength : 1f;

		if (row.EventNames.Length > 0)
		{
			int track = target.AddTrack(Animation.TrackType.Method);
			// ⚠️ 方法轨道的 path 是**相对 AnimationMixer.RootNode** 的（root_node 默认 `".."`，
			// 解析到 `.glb` 实例根那一级），所以给的是"从模型根回到 Unit 的相对路径"。
			// 这个路径由 SkillAnimationLibrary.ResolveEventTargetPath 统一算（生成器会先用一个真单位标定）。
			target.TrackSetPath(track, new NodePath(SkillAnimationLibrary.ResolveEventTargetPath(modelRoot)));

			if (row.EventTimes.Length != row.EventNames.Length)
			{
				GD.PushWarning(
					$"[SkillAnimation] {row.Id} 的事件帧名 {row.EventNames.Length} 个、时刻 {row.EventTimes.Length} 个，"
					+ "只生成两者都有的那部分。");
			}

			int count = Mathf.Min(row.EventNames.Length, row.EventTimes.Length);
			for (int i = 0; i < count; i++)
			{
				if (string.IsNullOrEmpty(row.EventNames[i]))
				{
					continue;
				}

				float time = Mathf.Clamp(row.EventTimes[i] * scale, 0f, targetLength);
				var key = new Godot.Collections.Dictionary
				{
					{ "method", row.EventMethod },
					{ "args", new Godot.Collections.Array { row.EventNames[i] } },
				};
				target.TrackInsertKey(track, time, key);
			}
		}

		if (targetLength > 0f && !Mathf.IsEqualApprox(targetLength, sourceLength))
		{
			target.Length = targetLength;
		}

		return target;
	}

	/// <summary>数一遍所有方法轨道上的关键帧（生成结果的"事件帧真的写进去了"证据）。</summary>
	private static int CountMethodKeys(Animation animation)
	{
		int total = 0;
		for (int i = 0; i < animation.GetTrackCount(); i++)
		{
			if (animation.TrackGetType(i) == Animation.TrackType.Method)
			{
				total += animation.TrackGetKeyCount(i);
			}
		}

		return total;
	}

	/// <summary>确保目标文件所在目录存在（`res://` 路径要用 `DirAccess` 的全局路径形态）。</summary>
	private static void EnsureDir(string resPath)
	{
		string globalDir = ProjectSettings.GlobalizePath(resPath.GetBaseDir());
		if (!DirAccess.DirExistsAbsolute(globalDir))
		{
			DirAccess.MakeDirRecursiveAbsolute(globalDir);
		}
	}

	private static Result Fail(SkillAnimationConfig row, string note)	{
		GD.PushWarning($"[SkillAnimation] {row.Id} 生成失败：{note}");
		return new Result { Id = row.Id, Ok = false, Note = note };
	}

	private static T? FindInSubtree<T>(Node root) where T : Node
	{
		foreach (Node child in root.GetChildren())
		{
			if (child is T found)
			{
				return found;
			}

			if (FindInSubtree<T>(child) is { } deep)
			{
				return deep;
			}
		}

		return null;
	}
}

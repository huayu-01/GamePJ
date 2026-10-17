using ActRoguelike.Core;
using System.Collections.Generic;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 技能动画的**路径约定 + 运行期加载**（一个地方说了算：生成器写在哪、单位去哪读、技能怎么查）。
///
/// 三件事必须一致，所以只有这一处定义：
/// <list type="number">
/// <item><b>放哪</b>：<c>Assets/Animations/&lt;库名&gt;/&lt;动画名&gt;.tres</c>（见 <see cref="OutputPath"/>）。</item>
/// <item><b>怎么查</b>：技能动画在**命名库**里，所以完整名是 <c>&lt;库名&gt;/&lt;动画名&gt;</c>
///   （见 <see cref="Qualified"/>）。⚠️ <c>AnimationPlayer.HasAnimation</c> **不搜嵌套库**，
///   裸名永远返回 false —— 实测确认（`refactor-survey.md` §1.4 坑 1）。</item>
/// <item><b>怎么加载</b>：把一个目录下的全部 <c>.tres</c> 收进<b>本单位的</b> <c>AnimationPlayer</c>
///   的命名库（见 <see cref="LoadInto"/>）。每个单位实例各自一份，互不影响。</item>
/// </list>
/// </summary>
public static class SkillAnimationLibrary
{
	/// <summary>技能动画目录的根（生成器与单位表 <c>AnimLibraryDir</c> 都从它下面取）。</summary>
	public const string RootDir = "res://Assets/Animations/";

	/// <summary>生成出来的动画文件路径：<c>res://Assets/Animations/&lt;库名&gt;/&lt;动画名&gt;.tres</c>。</summary>
	public static string OutputPath(string library, string animationName)
		=> $"{RootDir}{library}/{animationName}.tres";

	/// <summary>
	/// 生成期注入的"事件帧该落在哪个节点的哪个路径上"解析器（见
	/// <see cref="ResolveEventTargetPath"/>）。桌面端的生成器（编辑器菜单）先实例化一个真单位、
	/// 从它的 <c>UnitAnimator</c> 记下"混合器根节点 → 事件接收节点"的相对路径，生成时就照抄，
	/// 于是**动作路径与单位场景的实际层级永远一致**。
	/// </summary>
	public static System.Func<Node, string>? EventTargetResolver { get; set; }

	/// <summary>
	/// 算出"方法轨道里该写的 path"：从 <paramref name="modelRoot"/>（`.glb` 实例根，也就是
	/// <c>AnimationMixer.RootNode</c> 解析到的那一级）出发，回到事件接收节点（<c>Unit</c>）的相对路径。
	///
	/// 有解析器就用解析器（权威）；没有（纯命令行生成）就退回约定：`.glb` 实例根的直接父节点
	/// 就是 <c>Unit</c>，所以写 <c>".."</c>。
	/// </summary>
	public static string ResolveEventTargetPath(Node modelRoot)
	{
		if (EventTargetResolver?.Invoke(modelRoot) is { Length: > 0 } resolved)
		{
			return resolved;
		}

		return "..";
	}

	/// <summary>命名库里的**完整**动画名（<c>skills/Skill_A1_ATK</c>）—— 查动画必须用这个。</summary>
	public static string Qualified(string library, string animationName)
		=> $"{library}/{animationName}";

	/// <summary>
	/// 把 <paramref name="dir"/> 下的全部 <c>.tres</c> 动画收进 <paramref name="player"/> 的
	/// <paramref name="library"/> 命名库。
	///
	/// <b>幂等</b>：同一个库已经加载过就直接返回（单位重生 / 能力重复初始化都走这条路）。
	/// 目录为空 / 读不到 → 返回 0 并**不报错**（"没有技能动画"是合法状态，只是技能不会执行）。
	/// </summary>
	/// <returns>实际加载进库的动画段数。</returns>
	public static int LoadInto(AnimationPlayer player, string library, string dir)
	{
		if (string.IsNullOrEmpty(library) || string.IsNullOrEmpty(dir))
		{
			return 0;
		}

		// 已经加载过同一个库：不再重复加（AddAnimationLibrary 覆盖同名库会让正在播的动画断掉）。
		if (player.HasAnimationLibrary(library))
		{
			return 0;
		}

		string[] files = ListAnimationFiles(dir);
		if (files.Length == 0)
		{
			return 0;
		}

		var lib = new AnimationLibrary();
		foreach (string path in files)
		{
			var animation = RuntimeResources.Load<Animation>(path);
			if (animation == null)
			{
				GD.PushWarning($"[SkillAnimation] {path} 不是 Animation，已跳过。");
				continue;
			}

			lib.AddAnimation(animation.ResourceName, PrepareForRuntime(animation, path, player));
		}

		if (lib.GetAnimationList().Count == 0)
		{
			return 0;
		}

		Error err = player.AddAnimationLibrary(library, lib);
		if (err != Error.Ok)
		{
			GD.PushWarning($"[SkillAnimation] 把库「{library}」加到 {player.Name} 失败：{err}。");
			return 0;
		}

		// ⚠️ 这一行是整个动画管线的**证据**：方法轨道回调必须是**当帧**生效的（Immediate），
		// 默认的 Deferred 会晚一帧、且排在物理之后 —— 战斗要的是"事件帧当帧生效"。
		player.CallbackModeMethod = AnimationMixer.AnimationCallbackModeMethod.Immediate;
		return lib.GetAnimationList().Count;
	}

	/// <summary>
	/// 把磁盘上的技能动画资源改造成"能安全播"的版本。
	///
	/// ⚠️ **只禁用方法轨道，关键帧一个都不动**。理由（实测，务必保留注释）：
	/// 在 Godot 4.7.2 + 本项目这套层级下，技能动画的**方法轨道**会在混合器内部直接把进程搞崩
	/// （`exit -1073741819` = 访问违规），而且一次都不会落到目标节点上；
	/// 同一份 `.tres` 在自测里那个独立探针节点上却能正常触发。
	/// 所以运行期不让引擎执行这条轨道，改由 <c>UnitAnimator</c> 自己读它的关键帧、
	/// 在播放位置跨过时触发（见 <c>UnitAnimator.DriveSkillEvents</c>）—— 时机与引擎回调一致，
	/// 授权格式（方法轨道 + `anim_event` + args）也完全不变。
	///
	/// 复制一份再禁用（不动 `ResourceLoader` 缓存里的那份），所以自测里那套"靠引擎回调触发"的
	/// 验证路径照样成立 —— 两条路都被覆盖到。
	/// </summary>
	private static Animation PrepareForRuntime(Animation source, string path, AnimationPlayer player)
	{
		var copy = (Animation)source.Duplicate(true);
		Node root = player.GetNodeOrNull(player.RootNode) ?? player.GetParent();
		Skeleton3D? skeleton = NodeSearch.FindInSubtree<Skeleton3D>(root);
		for (int track = 0; track < copy.GetTrackCount(); track++)
		{
			// 角色模型的导入根名不同；仅在私有副本中重定向同名骨骼，关键帧和方法事件不变。
			if (skeleton != null && copy.TrackGetType(track) is Animation.TrackType.Position3D or Animation.TrackType.Rotation3D or Animation.TrackType.Scale3D)
			{
				NodePath target = copy.TrackGetPath(track);
				if (target.GetSubNameCount() == 1 && skeleton.FindBone(target.GetSubName(0)) >= 0)
					copy.TrackSetPath(track, new NodePath($"{root.GetPathTo(skeleton)}:{target.GetSubName(0)}"));
			}
			if (copy.TrackGetType(track) == Animation.TrackType.Method && copy.TrackIsEnabled(track))
			{
				copy.TrackSetEnabled(track, false);
			}
		}

		if (string.IsNullOrEmpty(copy.ResourceName))
		{
			copy.ResourceName = path.GetFile().GetBaseName();
		}

		return copy;
	}

	/// <summary>列出一个目录下的动画文件（`*.tres`），**按名字排序**保证结果稳定可复现。</summary>
	public static string[] ListAnimationFiles(string dir)
	{
		var found = new List<string>();
		if (!DirAccess.DirExistsAbsolute(dir))
		{
			return System.Array.Empty<string>();
		}

		using DirAccess? access = DirAccess.Open(dir);
		if (access == null)
		{
			return System.Array.Empty<string>();
		}

		foreach (string file in access.GetFiles())
		{
			// 导出后资源名可能带 `.remap` 后缀，统一剥掉再判断。
			string name = file.EndsWith(".remap") ? file.Substring(0, file.Length - ".remap".Length) : file;
			if (name.EndsWith(".tres"))
			{
				found.Add($"{dir.TrimEnd('/')}/{name}");
			}
		}

		found.Sort(System.StringComparer.Ordinal);
		return found.ToArray();
	}

	/// <summary>
	/// 动画名解析：优先**完整路径**精确匹配，其次忽略大小写地在完整路径里找，
	/// 都命中不了返回空串（调用方据此判定"没有这段动画"）。
	///
	/// 与 <c>UnitAnimator.Resolve</c> 的区别：那个是给"移动/死亡"这类**全局库的裸名**用的回退链，
	/// 这个是给"技能动画必须存在"的**严格**判定用的 —— 找不到就是找不到，不回退。
	/// </summary>
	public static string ResolveExact(AnimationPlayer player, string library, string animationName)
	{
		if (player == null || string.IsNullOrEmpty(animationName))
		{
			return string.Empty;
		}

		// 表里可能已经写了完整路径（`skills/Skill_A1_ATK`）：原样用。
		if (animationName.Contains('/'))
		{
			return player.HasAnimation(animationName) ? animationName : string.Empty;
		}

		string full = Qualified(library, animationName);
		if (player.HasAnimation(full))
		{
			return full;
		}

		foreach (string name in player.GetAnimationList())
		{
			if (string.Equals(name, full, System.StringComparison.OrdinalIgnoreCase))
			{
				return name;
			}
		}

		return string.Empty;
	}
}

using System.Text;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Editor;

/// <summary>
/// 编辑器菜单入口：**项目 → 工具 → 生成技能动画**。
///
/// 它只做三件事：读 <c>Config/Tables/SkillAnimations/</c> 的行 → 交给
/// <see cref="SkillAnimationGenerator"/> 生成 → 把每一行的结果写进输出面板与 `GameLog`。
///
/// 命令行等价物（CI / 无编辑器时）：
/// <code>
/// Godot_v4.7.2-stable_mono_win64_console.exe --headless --path &lt;项目&gt; \
///   --script res://Scripts/Editor/GenerateSkillAnimations.cs
/// </code>
/// </summary>
[Tool]
public partial class GenerateSkillAnimations : EditorScript
{
	private const string TableDir = "res://Config/Tables/SkillAnimations";

	public override void _Run()
	{
		CalibrateEventTargetPath();

		var rows = new System.Collections.Generic.List<SkillAnimationConfig>();
		foreach (string path in SkillAnimationLibrary.ListAnimationFiles(TableDir))
		{
			if (ResourceLoader.Load<SkillAnimationConfig>(path) is { } row)
			{
				rows.Add(row);
			}
		}

		if (rows.Count == 0)
		{
			GD.PushWarning($"[SkillAnimation] {TableDir} 下没有可读的 SkillAnimationConfig 行 —— 什么都没生成。");
			return;
		}

		var report = new StringBuilder();
		report.Append($"[SkillAnimation] 共 {rows.Count} 行：\n");
		int ok = 0;
		foreach (SkillAnimationGenerator.Result result in SkillAnimationGenerator.Generate(rows))
		{
			if (!result.Ok)
			{
				report.Append($"  ✗ {result.Id} —— {result.Note}\n");
				continue;
			}

			ok++;
			report.Append($"  ✓ {result.Id} → {result.OutputPath}（{result.QualifiedName}，{result.TrackCount} 轨 / {result.Length:0.###}s / {result.EventCount} 个事件帧）\n");
		}

		report.Append($"[SkillAnimation] 成功 {ok}/{rows.Count}。");
		GD.Print(report.ToString());
		ActRoguelike.Core.GameLog.Write("Animation", report.ToString().Replace("\n", " | "));
		SkillAnimationLibrary.EventTargetResolver = null;
	}

	/// <summary>
	/// 用一个**真单位场景**标定"方法轨道该写什么 path"。
	///
	/// 为什么必须标定而不是写死 <c>".."</c>：轨道 path 是相对 <c>AnimationMixer.RootNode</c> 的，
	/// 而"从 <c>.glb</c> 实例根往上走到 <c>Unit</c> 要走几级"取决于单位场景怎么摆模型。
	/// 写死一个值，换个场景就静默失效（实测：玩家那边一度**一次都没触发**，而且引擎直接崩）。
	///
	/// ⚠️ 标定必须在 <c>Free()</c> **之前**把结论（"往上几级"）算成一个整数存下来：
	/// 直接把 <c>Animator</c> 节点封进闭包里，`Free()` 之后它会变成已释放对象，
	/// 之后每次解析都抛 <c>ObjectDisposedException</c>（实测踩到），生成器就静默退回默认值了。
	/// </summary>
	private static void CalibrateEventTargetPath()
	{
		int upLevels = 1;
		if (GD.Load<PackedScene>("res://Scenes/Player/Player.tscn")?.Instantiate() is not Node player)
		{
			GD.PushWarning("[SkillAnimation] 标定失败：读不到 Player.tscn，事件帧路径退回默认 \"..\"。");
			return;
		}

		try
		{
			Node? model = player.GetNodeOrNull<Node>("Model");
			Node? animator = player.GetNodeOrNull<Node>(UnitAnimator.NodeName);
			if (model == null || animator == null)
			{
				GD.PushWarning("[SkillAnimation] 标定失败：玩家场景里没有 Model/Animator，事件帧路径退回默认 \"..\"。");
				return;
			}

			// 方法轨道的 path 是**相对 AnimationMixer.RootNode** 的，而 RootNode 解析到的是
			// **模型实例的父节点**（项目里 RootNode='..'，从 AnimationPlayer 看是 Model、
			// 但解析基准本身是"模型的上一级"）。所以"路径里写几个 `..`" = 从**这个基准节点**
			// 沿祖先链走到事件接收节点要几级。
			Node? basis = model.GetParent();
			upLevels = 0;
			Node? cursor = animator;
			while (cursor != null && cursor != basis && upLevels < 8)
			{
				upLevels++;
				cursor = cursor.GetParent();
			}

			if (cursor != basis)
			{
				GD.PushWarning(
					$"[SkillAnimation] 标定异常：从 {animator.Name} 往上走不到 {basis?.Name ?? "（模型没有父节点）"}，退回 \"..\"。"
					+ $"（模型={model.Name} 路径={model.GetPath()}，接收节点={animator.Name} 路径={animator.GetPath()}）");
				upLevels = 1;
			}
		}
		finally
		{
			player.Free();
		}

		SkillAnimationLibrary.EventTargetResolver = _ => string.Join("/", System.Linq.Enumerable.Repeat("..", upLevels));
		GD.Print($"[SkillAnimation] 事件帧路径标定：Model 往上 {upLevels} 级到事件接收节点 → 轨道 path = '{string.Join("/", System.Linq.Enumerable.Repeat("..", upLevels))}'");
	}
}

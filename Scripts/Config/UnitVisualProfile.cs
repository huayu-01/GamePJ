using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Config;

/// <summary>模型、骨架路径、握持挂点与动画映射的单一制作入口；不配置身体或命中体。</summary>
[Tool, GlobalClass]
public partial class UnitVisualProfile : Resource
{
	[Export] public PackedScene? ModelScene { get; set; }
	[Export] public Transform3D ModelTransform { get; set; } = Transform3D.Identity;
	[Export] public NodePath SkeletonPath { get; set; } = "";
	[Export] public NodePath AnimationPlayerPath { get; set; } = "";
	[Export] public PackedScene? GripScene { get; set; }
	[Export] public string GripBone { get; set; } = string.Empty;
	[Export] public NodePath GripSocketPath { get; set; } = "Socket";
	[Export] public Vector3 DiveGripOffset { get; set; } = new(.25f, .9f, -.2f);
	[Export] public string AnimIdle { get; set; } = "idle";
	[Export] public string AnimWalk { get; set; } = "walk";
	[Export] public string AnimRun { get; set; } = "sprint";
	[Export] public string AnimDeath { get; set; } = "die";
	/// <summary>可选跳跃姿态；空值保留原有模型行为，升降仍由Movement控制。</summary>
	[Export] public string AnimJump { get; set; } = string.Empty;
	[Export] public string AnimHit { get; set; } = string.Empty;
	[Export] public float JumpAirPoseTime { get; set; } = .38f;
	[Export] public float JumpLandingTime { get; set; } = .61f;
	[Export(PropertyHint.Dir)] public string SkillLibraryDir { get; set; } = string.Empty;
	[Export] public string SkillLibraryName { get; set; } = "skills";
	[Export] public MotionFxPreset[] MotionEffects { get; set; } = System.Array.Empty<MotionFxPreset>();
	public Skeleton3D? ResolveSkeleton(Node model) => SkeletonPath.IsEmpty ? NodeSearch.FindInSubtree<Skeleton3D>(model) : model.GetNodeOrNull<Skeleton3D>(SkeletonPath);
	public AnimationPlayer? ResolveAnimationPlayer(Node model) => AnimationPlayerPath.IsEmpty ? NodeSearch.FindInSubtree<AnimationPlayer>(model) : model.GetNodeOrNull<AnimationPlayer>(AnimationPlayerPath);
	public IEnumerable<string> Validate()
	{
		if (ModelScene == null || !ModelScene.CanInstantiate()) { yield return "ModelScene：必须指定可实例化的模型场景。"; yield break; }
		Node? model = null, gripRoot = null; BoneAttachment3D? grip = null;
		try
		{
			model = ModelScene.Instantiate();
			if (model is not Node3D) yield return "ModelScene：模型场景根节点必须为 Node3D。";
			Skeleton3D? skeleton = ResolveSkeleton(model);
			if (skeleton == null) yield return $"SkeletonPath：模型找不到骨架 {SkeletonPath}。";
			var ids = new HashSet<string>(System.StringComparer.Ordinal);
			var events = new HashSet<string>(System.StringComparer.Ordinal);
			foreach (var preset in MotionEffects)
			{
				if (preset == null) { yield return "MotionEffects：预设引用不能为空。"; continue; }
				if (!ids.Add(preset.Id)) yield return $"MotionEffects：预设名重复 {preset.Id}。";
				foreach (string name in new[] { preset.EventName, preset.StopEventName })
					if (!string.IsNullOrEmpty(name) && !events.Add(name)) yield return $"MotionEffects：事件名重复 {name}。";
				foreach (string error in preset.Validate(skeleton)) yield return $"MotionEffects/{preset.Id}：{error}";
			}
			AnimationPlayer? player = ResolveAnimationPlayer(model);
			if (player == null) yield return $"AnimationPlayerPath：模型找不到动画播放器 {AnimationPlayerPath}。";
			else if (!string.IsNullOrEmpty(SkillLibraryDir))
				foreach (string path in SkillAnimationLibrary.ListAnimationFiles(SkillLibraryDir))
				{
					if (RuntimeResources.Load<Animation>(path) is not { } animation) { yield return $"SkillLibraryDir：不是动画资源 {path}。"; continue; }
					foreach (string error in SkillAnimationLibrary.ValidateBindings(animation, player, skeleton)) yield return $"SkillLibraryDir：{path} → {error}";
				}
			if (GripScene != null)
			{
				gripRoot = GripScene.Instantiate(); grip = gripRoot as BoneAttachment3D;
				if (grip == null) yield return "GripScene：挂点场景根节点必须为 BoneAttachment3D。";
				else
				{
					string bone = string.IsNullOrEmpty(GripBone) ? grip.BoneName : GripBone;
					if (skeleton == null || skeleton.FindBone(bone) < 0) yield return $"GripBone：骨架没有骨骼 {bone}。";
					if (grip.GetNodeOrNull<Node3D>(GripSocketPath) == null) yield return $"GripSocketPath：挂点缺少 {GripSocketPath}。";
				}
			}
		}
		finally { gripRoot?.Free(); model?.Free(); }
	}
}

using System;
using System.Linq;
using ActRoguelike.Config;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>制作期校验的真实配置及反例；不写入玩家进度，不运行预制体。</summary>
public partial class ContentValidationProbe : Node
{
	private int _pass, _fail;
	private void Check(bool ok, string message) { if (ok) _pass++; else _fail++; GD.Print($"[ContentValidation] {(ok ? "PASS" : "FAIL")} {message}"); }
	public override async void _Ready()
	{
		try
		{
			await ToSignal(GetTree().CreateTimer(.15), SceneTreeTimer.SignalName.Timeout);
			var real = ContentValidator.Validate(ConfigIndex.Settings, scanUnindexed: true);
			foreach (var issue in real) GD.Print($"[ContentValidation] ISSUE {issue}");
			Check(real.Count == 0, $"全部生产索引、引用、节点、真实动画事件、压力与制作目录，问题数 {real.Count}");
			var inventory = ContentInventorySnapshot.Current;
			Check(inventory.Check(ConfigIndex.Settings).Count == 0, "全部内容表及特化总数符合唯一显式基线");
			var wrongInventory = (ContentInventorySnapshot)inventory.Duplicate(true); wrongInventory.ExpectedCounts["Weapons"]--;
			Check(wrongInventory.Check(ConfigIndex.Settings).Any(i => i.Field.EndsWith("Weapons")) && inventory.Check(ConfigIndex.Settings).Count == 0, "意外丢内容会失败，检查不会自动改写基线");
			wrongInventory.ExpectedCounts.Remove("Characters"); wrongInventory.ExpectedCounts["UnknownTable"] = 1;
			Check(wrongInventory.Check(ConfigIndex.Settings).Any(i => i.Field.EndsWith("Characters")) && wrongInventory.Check(ConfigIndex.Settings).Any(i => i.Field.EndsWith("UnknownTable")), "漏声明内容表与写错表名均报告");
			var badBehavior = new ConfigIndexData { Upgrades = new[] { new RewardConfig { Id = "bad_behavior", ApplyHandler = "Specialization", Specialization = SpecializationKind.Bloodthirst, SpecializationBehavior = new DeathDuelBehaviorDefinition { Duration = float.NaN } } } };
			var behaviorIssues = ContentValidator.Validate(badBehavior);
			Check(behaviorIssues.Any(i => i.Field == "SpecializationBehavior" && i.Reason.Contains("不匹配")) && behaviorIssues.Any(i => i.Reason.Contains("Duration")), "新特化定义通过公共接口校验参数，同时拒绝身份类型不匹配");
			var missing = new AbilityConfig { Id = "probe_skill", NodeName = "MissingAbility", ClassHint = "DashAbility", AnimName = "MissingAnimation", AnimEvent = "ATK" };
			var bad = new ConfigIndexData
			{
				Upgrades = new[] { new RewardConfig { Id = "duplicate", ApplyHandler = "Unknown", Requires = new[] { "unknown" } }, new RewardConfig { Id = "duplicate", Effects = new RewardEffectDefinition[] { new StatusOnHitRewardEffect() } } },
				Enemies = new[] { new EnemyConfig { Id = "bad_enemy", ScenePath = "res://Scenes/Player/Player.tscn", Skills = new[] { missing }, PressureBehaviors = new[] { "unknown_behavior" } } },
			};
			var problems = ContentValidator.Validate(bad);
			Check(problems.Any(i => i.Field == "Id"), "拒绝重复 Id");
			Check(problems.Any(i => i.Field.Contains("Effects /")), "拒绝未知效果处理器");
			Check(problems.Any(i => i.Field.Contains("Effects[0]")), "效果定义参数进入统一报告");
			Check(problems.Any(i => i.Field.Contains("Requires /")), "拒绝不存在的前置条件");
			Check(problems.Any(i => i.Field == "PressureBehaviors"), "拒绝未知行为压力");
			Check(problems.Any(i => i.Reason.Contains("引用的配置行")), "引用存在但漏索引仍失败");
			Check(problems.Any(i => i.Field.EndsWith("NodeName")), "预制体缺能力节点在制作期失败");
			Check(problems.Any(i => i.Field.EndsWith("AnimName")), "缺真实动画不能由普通播放回退掩盖");
			Check(problems.All(i => !string.IsNullOrEmpty(i.Resource) && !string.IsNullOrEmpty(i.Consumer) && !string.IsNullOrEmpty(i.Fix)), "每项报告包含资源、字段、消费者、原因和修复方式");
			var animation = new Animation(); int track = animation.AddTrack(Animation.TrackType.Method);
			animation.TrackInsertKey(track, .1, new Godot.Collections.Dictionary { ["method"] = "anim_event", ["args"] = new Godot.Collections.Array { "ATK" } });
			Check(ContentValidator.HasEvent(animation, "ATK") && !ContentValidator.HasEvent(animation, "Release"), "实际方法轨道匹配指定事件");
			animation.TrackSetEnabled(track, false);
			Check(!ContentValidator.HasEvent(animation, "ATK"), "禁用事件轨道不能误判成功");
			var model = new Node3D(); var rig = new Skeleton3D { Name = "ReplacementRig" }; rig.AddBone("grip_joint"); model.AddChild(rig);
			var player = new AnimationPlayer(); model.AddChild(player);
			var motion = new Animation(); int boneTrack = motion.AddTrack(Animation.TrackType.Rotation3D); motion.TrackSetPath(boneTrack, new NodePath("OldRoot/OldRig:grip_joint"));
			motion.TrackInsertKey(boneTrack, 0, Quaternion.Identity);
			Check(SkillAnimationLibrary.ValidateBindings(motion, player, rig).Count == 0, "层级更换可按档案骨架重定位同名骨骼，不固定旧模型根名");
			Check(motion.TrackGetPath(boneTrack).ToString() == "OldRoot/OldRig:grip_joint", "制作校验不修改源动画");
			motion.TrackSetPath(boneTrack, new NodePath("ReplacementRig:missing_joint"));
			Check(SkillAnimationLibrary.ValidateBindings(motion, player, rig).Any(error => error.Contains("骨骼不存在")), "骨名改变且动画未重定向时提前报错");
			motion.TrackSetEnabled(boneTrack, false); Check(SkillAnimationLibrary.ValidateBindings(motion, player, rig).Count == 0, "停用的骨骼轨道不参与运行绑定检查"); model.Free();
			var build = new ActRoguelike.Rewards.RunBuild(); build.SetRuneUnlocks(new[] { "雷之印" });
			Check(build.SatisfiesPrerequisite("rune_lei_seal") && build.SatisfiesPrerequisite("雷之印") && !build.SatisfiesPrerequisite("rune_war_seal"), "旧中文条件双向兼容稳定 Id，派系不混用");
			var agility = new AirJumpPassiveAbility();
			new AbilityConfig { GrantAttackMovement = true, Params = new[] { new ParamEntry { Key = "GrantAttackMovement", Value = false } } }.ApplyTo(agility);
			Check(!agility.GrantAttackMovement, "能力自行消费被动参数，保持通用 Params 的覆盖顺序"); agility.Free();
			var series = new AbilitySeries(); var combo = new MeleeComboAbility();
			new AbilityConfig { ComboSeries = series }.ApplyTo(combo);
			Check(ReferenceEquals(combo.Series, series), "能力自行绑定连招资源，核心配置不识别具体连招类型"); combo.Free();
			var fall = new FallAttackAbility();
			new AbilityConfig { FallAttack = new FallAttackTuning { DiveSpeed = 38 }, Params = new[] { new ParamEntry { Key = "DiveSpeed", Value = 9f } } }.ApplyTo(fall);
			Check(fall.DiveSpeed == 38, "下落专用资源仍在通用 Params 后覆盖，数值权威不变"); fall.Free();
		}
		catch (Exception error) { Check(false, error.ToString()); }
		GD.Print($"[ContentValidation] SUMMARY pass={_pass} fail={_fail}"); GetTree().Quit(_fail == 0 ? 0 : 1);
	}
}

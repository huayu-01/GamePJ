using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Encounter;
using ActRoguelike.Interaction;
using ActRoguelike.Player;
using ActRoguelike.Run;
using ActRoguelike.UI;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Debug;

/// <summary>
/// 无 MCP 时的自动化验证入口（只在命令行带 ``--selftest`` 时激活，正常运行零影响）。
///
/// 用法（Godot 可执行文件 + 项目路径）：
/// <code>
/// Godot_v4.7.2-stable_mono_win64.exe --path F:\Godot项目\项目a --headless --quit-after 900 -- --selftest
/// </code>
///
/// 它会：跳过界面直接开局 → 打印节点布置（应为 10 个 / 2-5-3 分层）→ 强制开一场战斗
/// → 打印波次生成结果（一次性刷出、波数）→ 退出。
/// </summary>
public partial class SelfTest : Node
{
#if DEBUG
	// 仅在 Debug 构建里存在：Release 下这个类退化成空 Node，autoload 仍然有效但什么都不做。

	private int _phase;
	private float _timer;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		bool wanted = false;
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg == "--selftest")
			{
				wanted = true;
			}
		}

		// 也支持文件开关：user://selftest.flag 存在即激活（MCP 的 run_project 无法传命令行参数）。
		if (!wanted && FileAccess.FileExists("user://selftest.flag"))
		{
			wanted = true;
		}

		if (!wanted)
		{
			SetProcess(false);
			return;
		}

		GameLog.Write("SelfTest", "=== 自测开始 ===");
	}

	public override void _Process(double delta)
	{
		if (_probeProbing)
		{
			SamplePresentation((float)delta);
		}

		if (_pendingFireRelease)
		{
			_pendingFireRelease = false;
			Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
		}

		if (_pendingKeyUp is { } release)
		{
			_pendingKeyUp = null;
			Input.ParseInputEvent(new InputEventKey { Keycode = release, PhysicalKeycode = release, Pressed = false });
		}

		TraceSkillLoopFrames();


		_timer += (float)delta;
		var run = GetTree().Root.GetNodeOrNull<RunController>("Main/RunController");
		var stage = GetTree().Root.GetNodeOrNull<StageController>("Main/Stage");

		// [项目补丁] 玩家在自测途中被打死会走死亡流程（第四批的 DeathScreen）回归基地，
		// 而自测状态机没有死亡分支 → 后续阶段永远等不到 → 进程永不退出、日志混在一起。
		// 改前的 run_20261012_194229.log / 194407.log 就是这个现象（死亡分支=3、完成=0）。
		// 这里检测到玩家阵亡就立刻收尾退出，让后续批次的自测不会卡住。
		if (PlayerLocator.Player is { IsDead: true })
		{
			GameLog.Write("SelfTest", "⚠️ 自测中断：玩家在途中阵亡（已走死亡流程回基地），剩余阶段跳过并退出。");
			GetTree().Paused = false;
			SetProcess(false);
			GetTree().Quit();
			return;
		}
		switch (_phase)
		{
			case 0 when _timer > 1.0f:
				// 模拟"从基地出发"：这样 RunController 会直接开局，不再弹出征面板挡住画面。
				RunSetup.HasSelection = true;
				RunSetup.Weapon = WeaponPresetKind.Pistol;
				run?.BeginRunWithCharacter(PlayerCharacterKind.Gunner, WeaponPresetKind.Pistol);
				// _boot 在第一帧就弹过出征面板，这里把它关掉，否则截图看不到战斗画面。
				GetTree().Root.GetNodeOrNull<BaseScreen>("Main/RunController/BaseScreen")?.Close();
				GetTree().Root.GetNodeOrNull<StartScreen>("Main/RunController/StartScreen")?.Close();
				GetTree().Paused = false;
				GameLog.Write("SelfTest", $"开局后 Paused={GetTree().Paused}（若为 True 说明界面栈还压着，战斗逻辑不会跑）");
				Next();
				break;

			case 1 when _timer > 1.5f:
				GameLog.Write("SelfTest", $"波数映射：进化={StageController.WavesFor(RewardNodeKind.Evolution)}（期望4） 异化={StageController.WavesFor(RewardNodeKind.Mutation)}（期望4） 武器限定（=进化池）={StageController.WavesFor(RewardNodeKind.Weapon)}（期望4，用户 A3：没有「武器池」，那个叫法就是进化池、算高价值） 强化={StageController.WavesFor(RewardNodeKind.Enhancement)}（期望3） 商店={StageController.WavesFor(RewardNodeKind.Shop)}（期望3） 材料={StageController.WavesFor(RewardNodeKind.Resource)}（期望1） 恢复={StageController.WavesFor(RewardNodeKind.Heal)}（期望1） 特殊={StageController.WavesFor(RewardNodeKind.Special)}（期望1）");
				GameLog.Write("SelfTest", $"价值标签表：{SelfTestValueTagSummary()}");
				ReportNodes(stage);
				ReportSpacing(stage);
				ReportTables(stage);
				ReportPressureGate(stage);
				ReportAmbientSpawns(stage);
				Next();
				break;

			case 2 when _timer > 0.5f:
				GetTree().Paused = false;
				GameLog.Write("SelfTest", $"解除暂停后 Paused={GetTree().Paused}，准备开战");
				stage?.DebugStartCombat();
				Next();
				break;

			case 3 when _timer > 5.0f:
				ReportCombat(stage);
				BeginHitEffectProbe();
				Next();
				break;

			case 4 when _timer > 0.2f:
				ReportHitEffectProbe();
				stage?.DebugStartCombat(2);
				GameLog.Write("SelfTest", $"开战（2 波上限）：{stage?.DebugState}");
				PressFireOnce();
				Next();
				break;

			case 5 when _timer > 1.5f:
				foreach (string group in new[] { "enemy_units", "boss_units" })
				{
					foreach (Node node in GetTree().GetNodesInGroup(group))
					{
						if (node is Unit unit)
						{
							unit.ApplyDirectDamage(99999f, null);
						}
					}
				}

				GameLog.Write("SelfTest", "已击杀场上全部敌人，等待波次推进与完成判定…");
				Next();
				break;

			case 6 when _timer > 1.5f:
				foreach (string group in new[] { "enemy_units", "boss_units" })
				{
					foreach (Node node in GetTree().GetNodesInGroup(group))
					{
						if (node is Unit unit)
						{
							unit.ApplyDirectDamage(99999f, null);
						}
					}
				}

				// 白板首领现在有波次门槛（用户需求 #5：只在第 8 波这类高波次出现）。
				// 自测这场只开了 2 波（DebugStartCombat(2)），所以**这里本来就不该有白板首领**——
				// 日志如实写出来，免得看日志的人以为"少了一个环节"。
				GameLog.Write("SelfTest", $"清场（2 波上限）：{stage?.DebugState} —— 白板首领闸门 {(stage?.CurrentNode?.EliteGateOpen == true ? "已开" : "未开")}（本关累计第 {stage?.WavesOpenedInStage} 波 / 门槛 {stage?.CurrentNode?.MinWaveForEliteBoss}，期望未开、本场直接结算）");
				Next();
				break;

			// === 端到端：真的走过去、按 F 交互、按数字键选奖励（不用 DebugTriggerNode 抄近路）===
			case 7 when _timer > 0.3f:
				MovePlayerToNode(stage, RewardNodeKind.Enhancement);
				GameLog.Write("SelfTest", "端到端：已传送到强化节点旁，准备按 F");
				Next();
				break;

			case 8 when _timer > 0.9f:
				GameLog.Write("SelfTest", $"端到端：按 F（当前交互提示 = {PlayerInteractInput.Prompt}）");
				PressKey(Key.F);
				Next();
				break;

			case 9 when _timer > 1.0f:
				var reward = GetTree().Root.GetNodeOrNull<RewardScreen>("Main/RunController/RewardScreen");
				GameLog.Write("SelfTest", $"端到端：按 F 之后 奖励界面打开 = {reward?.IsOpen}  Paused = {GetTree().Paused}");
				PressKey(Key.Key1);
				Next();
				break;

			case 10 when _timer > 3.0f:
				GameLog.Write("SelfTest", $"端到端：选完奖励之后的状态 = {stage?.DebugState}");
				Next();
				break;

			// === 表现层：前进时逐帧采样「物理步进 / 相机平滑 / 渲染帧率」===
			// 残影排查：模型是 Unit 的子节点（不做位置插值），所以「一直在缓动 + 残影」只可能来自
			// ① 相机在 _Process 里平滑跟随、而物理按固定 60Hz 步进，两者频率不一致 → 画面抖动/滞后；
			// ② 渲染帧率与物理帧率不同步（物理插值未开）→ 角色位置每 2~3 个渲染帧才跳一次。
			case 11:
				_timer = 0f;
				// 玩家现在挂在 Stage 下面（不是 Main/Player），用统一入口取。
				_probePlayer = PlayerLocator.Player ?? GetTree().GetFirstNodeInGroup("player_units") as Unit;
				_probeRig = _probePlayer?.GetNodeOrNull<Node3D>("CameraRig");
				_probePrevPlayer = _probePlayer?.GlobalPosition ?? Vector3.Zero;
				_probePrevRig = _probeRig?.GlobalPosition ?? Vector3.Zero;
				_probeProbing = true;
				Input.ParseInputEvent(new InputEventKey { Keycode = Key.W, PhysicalKeycode = Key.W, Pressed = true });
				GameLog.Write("SelfTest", $"表现采样：按住 W 前进（fps={Engine.GetFramesPerSecond()} 物理Hz={Engine.PhysicsTicksPerSecond} 物理插值={ProjectSettings.GetSetting("physics/common/physics_interpolation").AsBool()} 玩家={_probePlayer != null} 相机={_probeRig != null}）");
				Next();
				break;

			case 12 when _timer > 2.5f:
				_probeProbing = false;
				Input.ParseInputEvent(new InputEventKey { Keycode = Key.W, PhysicalKeycode = Key.W, Pressed = false });
				ReportPresentation();
				Next();
				break;

			// === 白板首领闸门的**正向**验证（放在最后一段之前做：这一段的运行前提最干净）===
			// 上面那几场只证明了"门槛没到时不出"；这里把门槛压到 1（= 本场开 1 波就满足）再开一场，
			// 清空后必须**真的**降临白板首领 —— 否则"跳过"日志也可能来自别的原因，证明不了闸门本身是对的。
			// ⚠️ 顺序讲究：必须排在"开 Boss 门"之前。开门之后 `CurrentNode` 会指向 Boss 那条 Encounter，
			// 而且 `OpenBossDoor` 会把普通 Encounter 冻住（StopSpawning）—— 那之后就没法干净地验普通节点了。
			case 13 when _timer > 0.3f:
				_eliteProbe = FindRegularEncounter(stage);
				_currentWaves = stage?.WavesOpenedInStage ?? 0;
				if (_eliteProbe != null)
				{
					_eliteOriginalGate = _eliteProbe.MinWaveForEliteBoss;
					_eliteOriginalScene = _eliteProbe.EliteBossScene;
				}

				// 先复位再设门槛：`ResetEncounter()` 清波次状态，顺序反了门槛就白设了。
				_eliteProbe?.ResetEncounter();
				if (_eliteProbe != null)
				{
					// 门槛压到"本场开 1 波就满足"：Start 之后 WaveIndex=1，累计（0 + 1）刚好够。
					_eliteProbe.WavesOpenedBefore = 0;
					_eliteProbe.MinWaveForEliteBoss = 1;
					_eliteProbe.WaveCount = 1;
					_eliteProbe.Start();
				}

				GameLog.Write("SelfTest", $"白板首领闸门·正向：普通 Encounter 找到={_eliteProbe != null} 原门槛={_eliteOriginalGate} 原预制体={(_eliteOriginalScene != null ? "有" : "无")} → 压到 1");
				Unit? gateProbePlayer = PlayerLocator.Player;
				GameLog.Write("SelfTest", $"白板首领闸门·正向·诊断：EntryCount={_eliteProbe?.EntryCount} 闸门=[{_eliteProbe?.EliteGateDetail}]（此时本场 WaveIndex 还是 0，所以「已开」要等第一波开出来才成立）树暂停={GetTree().Paused} 玩家={(gateProbePlayer == null ? "找不到" : $"存活={!gateProbePlayer.IsDead} 生命={gateProbePlayer.Health:0}")}");
				Next();
				break;

			case 14 when _timer > 1.5f:
				foreach (string group in new[] { "enemy_units", "boss_units" })
				{
					foreach (Node node in GetTree().GetNodesInGroup(group))
					{
						if (node is Unit unit)
						{
							unit.ApplyDirectDamage(99999f, null);
						}
					}
				}

				GameLog.Write("SelfTest", $"白板首领闸门·正向：清场那一刻 waveIndex={_eliteProbe?.WaveIndex} alive={_eliteProbe?.AliveCount} spawned={_eliteProbe?.TotalSpawned} 白板首领={_eliteProbe?.EliteBossSpawned}");
				Next();
				break;

			case 15 when _timer > 1.5f:
				// 白板首领必须真的在场，而且是"白板"：血量按首领行的 EliteCopyHealthScale = 0.35 弱化。
				Unit? elite = null;
				foreach (Node node in GetTree().GetNodesInGroup("boss_units"))
				{
					if (node is Unit unit && IsInstanceValid(unit))
					{
						elite = unit;
						break;
					}
				}

				GameLog.Write("SelfTest", $"🔴 白板首领闸门·正向：本场第 {_eliteProbe?.WaveIndex} 波后降临={_eliteProbe?.EliteBossSpawned}（期望 True）场上存活={_eliteProbe?.AliveCount}（期望 1）");
				GameLog.Write("SelfTest", elite == null
					? "🔴 白板首领闸门·正向：boss_units 组里找不到它（期望找到一个）"
					: $"🔴 白板首领实例：生命 {elite.MaxHealth:0.#}（首领行 900 × 0.35 = 315，期望 315）护甲 {elite.Armor:0.#}（期望 0）标签 [{string.Join(",", elite.Tags)}]");
				Next();
				break;

			case 16 when _timer > 0.5f:
				// 打死白板首领 → 本场才算清场；同时把被临时改过的门槛与预制体还原。
				foreach (string group in new[] { "enemy_units", "boss_units" })
				{
					foreach (Node node in GetTree().GetNodesInGroup(group))
					{
						if (node is Unit unit)
						{
							unit.ApplyDirectDamage(99999f, null);
						}
					}
				}

				if (_eliteProbe != null)
				{
					_eliteProbe.MinWaveForEliteBoss = _eliteOriginalGate;
					_eliteProbe.EliteBossScene = _eliteOriginalScene;
				}

				GameLog.Write("SelfTest", $"白板首领闸门·还原：门槛回到 {_eliteProbe?.MinWaveForEliteBoss}（自测的临时改动不该留在正式流程里）");
				Next();
				break;

			case 17 when _timer > 1.5f:
				GameLog.Write("SelfTest", $"白板首领结算后：本关累计第 {stage?.WavesOpenedInStage} 波（进这一段时是 {_currentWaves}，期望 +1）状态=[{stage?.DebugState}]");
				Next();
				break;

			// === 用户 bug #12：直接开门打 Boss → 打完卡在「挑战者状态」、进不了降临节点 ===
			// 复现步骤就是玩家做的三步：① 开 Boss 门 ② 打死 Boss ③ 想进降临节点。
			// 这里的硬指标：第 ② 步之后 ChallengerActive 必须是 false（原来它一直是 true，
			// 因为 IsVictory 只有"互动降临节点"才会翻，于是降临节点的交互被自己的挑战者闸门拒掉 —— 死锁）。
			case 18 when _timer > 0.3f:
				bool opened = stage?.OpenBossDoor() ?? false;
				_bossProbe = stage?.CurrentNode;
				GameLog.Write("SelfTest", $"Boss 门：打开={opened} 状态=[{stage?.DebugState}] 挑战者状态={stage?.ChallengerStateNow}（期望 True：Boss 还活着）");
				Next();
				break;

			case 19 when _timer > 0.2f:
				// ⚠️ **先把玩家挪远**：Boss 与护卫是"在玩家附近"生成的（`BossSpawnDistance`），
				// 而满压 Boss 的攻速可能到 ×2 以上、2~3 个技能精英护卫也在打 —— 站在它脸上必被秒
				// （跑过好几次都是这么废掉的：玩家一死整局结束，后面的阶段全废，最后连降临节点都没生成）。
				if (PlayerLocator.Player is { } mover)
				{
					mover.GlobalPosition = SelfTestAwaySpot;
				}
				GameLog.Write("SelfTest", $"Boss 已出生={_bossProbe?.BossActive} 挑战者状态={stage?.ChallengerStateNow}（期望 True）玩家已挪到远处 {SelfTestAwaySpot}（避免被 Boss 秒掉，那与要验的挑战者状态无关）");
				Next();
				break;

			case 20 when _timer > 2.5f:
				// 每帧打一次直到 Boss 阵亡：生成延迟与 AI 出手的相对顺序不确定，只打一帧会漏。
				foreach (string group in new[] { "enemy_units", "boss_units" })
				{
					foreach (Node node in GetTree().GetNodesInGroup(group))
					{
						if (node is Unit unit)
						{
							unit.ApplyDirectDamage(999999f, null);
						}
					}
				}

				_bossKilled = _bossProbe?.Boss is { IsDead: true };
				if (_bossKilled)
				{
					Next();
				}

				break;

			case 21 when _timer > 2.0f:
				GameLog.Write("SelfTest", $"🔴 Boss 阵亡 → 挑战者状态={stage?.ChallengerStateNow}（期望 False，这就是 bug #12 的修复点） 已通关={stage?.IsVictory}（期望 False） 玩家存活={PlayerLocator.Player is { IsDead: false }}");
				Next();
				break;

			case 22 when _timer > 1.5f:				// 降临节点落地后必须真的**能被互动**：直接调 StageController.RequestRewardNode
				// （这就是 Interactable.Interact 里挑战者闸门之后的同一条路径），成功就说明卡住的问题没了。
				// ⚠️ 这里不看 CanInteract —— 它额外要求"玩家在交互半径内"，而自测的玩家没走过去；
				// 被挑战者闸门拒掉时会返回 false，所以 accepted 这一个量就足以判定修复。
				RewardNode? portal = FindPortal(stage);
				GameLog.Write("SelfTest", $"降临节点：找到={portal != null} 提示=[{portal?.CurrentPrompt}] 玩家在范围内={portal?.PlayerInRange}（自测没走过去，不影响结论）");

				// ⚠️ 三个量都要在**这一行之内**读走再打日志：
				// ① 被挑战者闸门拒掉时 RequestRewardNode 会返回 false；
				// ② 成功时 `IsVictory = true` + `Victory` 事件 → RunController 立刻切到下一关，
				//    而**下一关**的 `ApplyStage` 会把 `IsVictory` 复位成 false（原来"接受=True 已通关=False"
				//    的怪现象就是这么来的）；③ 关卡号 +1 才是"真的进关了"的旁证。
				int stageBefore = stage?.StageNumber ?? -1;
				int challengesBefore = stage?.ChallengesUsed ?? -1;
				bool entered = portal != null && (stage?.RequestRewardNode(portal) ?? false);
				bool victory = stage?.IsVictory ?? false;
				bool challenger = stage?.ChallengerStateNow ?? false;
				int stageAfter = stage?.StageNumber ?? -1;
				GameLog.Write("SelfTest", $"🔴 互动降临节点：接受={entered}（期望 True）→ 本关通关={victory}（切关后会被下一关复位，所以这里只看它当场为 True：{victory || stageAfter > stageBefore}）关卡 {stageBefore}→{stageAfter}（期望 +1）已挑战 {challengesBefore}→{stage?.ChallengesUsed} 挑战者状态={challenger}（期望 False）");
				Next();
				break;

			case 23:
				// 动画管线的**运行期**验证放在最小闭环那几段（case 29–32）：那里用的是玩家身上
				// 真正的 UnitAnimator，而不是另造一份临时模型。
				// ⚠️ 曾经这里还有一个"在临时探针节点上跑引擎方法轨道回调"的探针，**已删除**：
				// 实测 Godot 4.7.2 在这套层级下执行技能动画的方法轨道会直接把进程搞崩
				// （exit -1073741819，访问违规），连那个临时探针节点也一起崩。
				// 所以运行期不再经过引擎回调，改由 UnitAnimator 读关键帧、按播放位置跨越触发
				// （见 SkillAnimationLibrary.PrepareForRuntime 与 UnitAnimator.DriveSkillEvents 的说明）。
				GameLog.Write("SelfTest", "=== 最小闭环与动画管线验证开始 ===");
				Next();
				break;

			case 24 when _timer > 0.3f:
				BeginSkillLoopProbe();
				Next();
				break;

			case 25 when _timer > 0.3f:
				// 按下左键（JustPressed 只在"输入被处理的那一帧"为真，所以在这里注入）。
				// ⚠️ `Input.ParseInputEvent` 在自测里会直接把进程搞崩（实测 exit -1073741819），
				// 所以**自测不注入输入事件**；"按键 → 技能"这一段由真人手测覆盖（截图在 docs/anim-pipeline.md）。
				// 自测负责的是后半段：技能 → 动画 → 事件帧 → 子弹 → 命中掉血。
				GameLog.Write("SelfTest", "最小闭环·按键段：跳过输入注入（自测环境不支持），下一步直接激活技能");
				Next();
				break;

			case 26 when _timer > 0.5f:
				// 兜底路径：`Input.ParseInputEvent` 注入的按键不一定会让 `Input.IsActionJustPressed` 为真
				// （那套状态机走的是引擎的输入阶段）。所以这一帧直接调一次 `TryActivate`，
				// 把"按键→技能"与"技能→动画→事件帧→子弹→命中"两段分开取证 —— 前者由真人手测（截图）覆盖。
				if (_loopProjectiles == 0)
				{
					FallbackActivateSkill();
					Next();
					break;
				}

				ReportSkillLoop();
				Next();
				break;

			case 27 when _timer > 1.0f:
				ReportSkillLoop();
				Next();
				break;

			case 28:
				GameLog.Write("SelfTest", "=== 自测结束 ===");

				// ⚠️ 退出前先解除暂停：这一阶段之前刚切过一关，`UiRouter` 正把树暂停着
				// （正式流程里玩家按 Enter 才会继续）。进程留着不退会让下一轮的日志文件混在一起。
				GetTree().Paused = false;
				SetProcess(false);
				GetTree().Quit();
				break;
		}
	}


	/// <summary>玩家的 UnitAnimator 接线 + 三个坑的静态核对（技能要靠它）。</summary>
	private void ReportPlayerAnimator()
	{
		Unit? player = PlayerLocator.Player;
		if (player == null)
		{
			GameLog.Write("SelfTest", "🔴 动画管线·玩家接线：找不到玩家。");
			return;
		}

		UnitAnimator? animator = player.GetNodeOrNull<UnitAnimator>(UnitAnimator.NodeName);
		AnimationPlayer? ap = player.GetNodeOrNull<Node>("Model") is { } model
			? NodeSearch.FindInSubtree<AnimationPlayer>(model)
			: null;
		if (animator == null || ap == null)
		{
			GameLog.Write("SelfTest", "🔴 动画管线·玩家接线：玩家上没有 UnitAnimator 或 AnimationPlayer。");
			return;
		}

		animator.PrepareSkillLibrary();
		string resolved = animator.ResolveSkillAnimation("Skill_A1_ATK1");
		GameLog.Write(
			"SelfTest",
			$"动画管线·玩家接线：技能动画库已加载={animator.HasSkillLibrary} "
			+ $"ResolveSkillAnimation(Skill_A1_ATK1)='{resolved}'（期望 skills/Skill_A1_ATK1）");
		GameLog.Write(
			"SelfTest",
			$"动画管线·三个坑（真项目实测）："
			+ $"① 全路径查名 —— HasAnimation(裸名 Skill_A1_ATK1)={ap.HasAnimation("Skill_A1_ATK1")}（期望 False）/ "
			+ $"HasAnimation(全路径 skills/Skill_A1_ATK1)={ap.HasAnimation("skills/Skill_A1_ATK1")}（期望 True）；"
			+ $"② 方法轨道 path —— AnimationPlayer.RootNode='{ap.RootNode}'（解析到 '{ap.GetNodeOrNull(ap.RootNode)?.Name}'），"
			+ $"轨道 path 写 '{SkillAnimationConfigEventPath(ap)}'；"
			+ $"③ 回调模式 —— CallbackModeMethod={ap.CallbackModeMethod}");
	}

	/// <summary>读一段技能动画里方法轨道的 path（证明生成器写的是哪条路径）。</summary>
	private static string SkillAnimationConfigEventPath(AnimationPlayer ap)
	{
		Animation? anim = ap.GetAnimation("skills/Skill_A1_ATK1");
		for (int i = 0; i < (anim?.GetTrackCount() ?? 0); i++)
		{
			if (anim!.TrackGetType(i) == Animation.TrackType.Method)
			{
				return anim.TrackGetPath(i).ToString();
			}
		}

		return "（无方法轨道）";
	}

	// ─────────────────────────────────────────────────────────────────────────
	// ★ 最小闭环探针（case 29–31）
	//
	// 链路：**真的按键**（Input.ParseInputEvent）→ 技能被表里的 InputAction 选中 → 播技能动画
	//       → 动画事件帧 ATK → 发射能量波 → 命中一个真敌人 → 统一结算掉血。
	//
	// 这里刻意"自己造一个靶子"而不是等关卡刷怪：闭环要验的是**管线**（按键→动画→事件帧→子弹→命中），
	// 与关卡节奏无关；自造靶子还给了一个确定的距离与朝向，结论可复现。
	// ─────────────────────────────────────────────────────────────────────────

	/// <summary>排障开关：为 true 时技能的事件帧**只记日志不发子弹**（隔离"事件帧到没到"与"发射有没有问题"）。</summary>
	public static bool DebugSkipFireShot { get; set; }

	/// <summary>排障开关：为 true 时技能跳过"等动画事件帧"，激活即发射（隔离动画与子弹两段）。</summary>
	public static bool DebugSkipSkillAnimation { get; set; }

	private Unit? _loopEnemy;
	private float _loopEnemyHealthBefore;
	private float _loopEnemyArmorBefore;
	private float _loopDamage;
	private bool _loopDamaged;
	private int _loopAnimEvents;
	private bool _loopAbilityEnabled;
	private string _loopAbilityId = string.Empty;
	private int _loopProjectiles;


	private void BeginSkillLoopProbe()
	{
		// ⚠️ 必须解除暂停：上一步刚切过关，`UiRouter` 正把树暂停着（正式流程里玩家按 Enter 才继续）。
		// 树暂停时 `Unit` / `Projectile` / `Ability` 的 `_PhysicsProcess` 全都不跑 —— 表现就是
		// "技能激活动画不动、子弹不飞"，而 `SelfTest` 自己是 `ProcessMode.Always` 所以照常推进，
		// 于是看起来像"技能没生效"（实测踩到）。
		GetTree().Paused = false;

		// ✅ 本开关**已关闭**（2026-10-15 验证）：上一轮"播运行期加进库的技能动画会让引擎崩"
		// 的现象，根因是**取证代码误用引擎 API** —— `SelfTest.ReportSkillLoop` 里用
		// `Animation.MethodTrackGetParams()` 读方法轨道关键帧，那个调用本身就让进程崩
		// （exit -1073741819），崩点恰好落在 case 27（技能动画刚播完那一帧），于是被误判成
		// "播动画把引擎搞崩"。改成唯一安全的读法 `Animation.TrackGetKeyValue()` 之后，
		// 自测全程跑完、退出码 0，**动画事件帧真的到了**（UnitAnimator 按播放位置跨越触发）。
		// 所以自测现在完整覆盖"动画 → 事件帧 → 发射"这一段（见 docs/anim-pipeline.md §6）。
		DebugSkipSkillAnimation = false;

		Unit? player = PlayerLocator.Player;
		if (player == null)
		{
			GameLog.Write("SelfTest", "🔴 最小闭环：找不到玩家。");
			return;
		}

		// ① 切到"角色 A + 能量掌"——走与基地一样的入口（不信手工赋值）。
		RunController.Instance?.BeginRunWithCharacter(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);

		// ② 挂在玩家身上的 UnitAnimator 必须真的拿到了技能动画库（技能"没动画不执行"的前提）。
		ReportPlayerAnimator();

		// ③ 造一个靶子：摆在**技能真的会打出去的那个方向**上、出生点往前约 2m（见下）。
		// ⚠️ 刻意用**最朴素的 Unit**而不是 Chaser.tscn：目标只是"一个能结算伤害的敌人"，
		// 带 AI/导航/模型的敌人会把无关系统（导航网格、Jolt、行为树）拉进来，出问题时归因不清。
		var chaser = new Unit
		{
			Name = "SelfTestDummy",
			Faction = Faction.Enemy,
			MaxHealth = 60f,
			MoveSpeed = 0f,
			Tags = new[] { "Melee", "SelfTest" },
		};
		// ⚠️ 靶子**必须有碰撞体**：投射物是靠 `IntersectRay` 命中的，而一个光秃秃的 `Unit`
		// （`new Unit{}` 没有任何子节点）在物理世界里**根本没有形状** —— 射线直接穿过去，
		// 于是"子弹飞出去、命中结算"这一段在自测里永远验不到（实测踩到：投射物生成=1 但掉血 ✗）。
		//
		// ⚠️ 而且必须**在进树之前**就挂上：物理体是在 `_EnterTree` 那一刻按"当时已有的形状"
		// 建出来的，进树之后再 `AddChild` 一个 `CollisionShape3D`，实测射线**打不到**它
		// （靶子摆在弹道上、半径 1.2 的胶囊、弹道穿过靶子原点，`IntersectRay` 依然什么都打不到）。
		chaser.AddChild(new CollisionShape3D
		{
			Name = "SelfTestDummyShape",
			Shape = new CapsuleShape3D { Radius = 1.2f, Height = 3f },
		});

		if (player.GetParent() is { } arena)
		{
			arena.AddChild(chaser);
		}

		// ⚠️ 靶子必须摆在**技能真的会打出去的那个方向**上。能量掌 `Aim = Crosshair`，方向来自
		// 「相机屏幕中心射线」（`AimUtil.GetCrosshairDirection`）—— 自测里没有鼠标输入，
		// 这条射线是**斜向下**的（实测 ≈ (0, -0.34, -0.94)），**不是**玩家的水平朝向。
		// 上一轮把靶子摆在"玩家正前方水平 6m"，子弹在 ~3m 处就落地了，永远打不着（实测踩到）。
		Vector3 forward = -player.GlobalTransform.Basis.Z;
		forward.Y = 0f;
		forward = forward.Normalized();
		Vector3 aim = AimUtil.GetCrosshairDirection(player.GetViewport(), forward).Normalized();
		if (aim.LengthSquared() < 0.5f)
		{
			aim = forward;
		}

		// 摆在射线**出生点往前 2m**处：能量波从 `玩家 + 上0.9 + 方向0.6` 出发，
		// 2m 处高度还有 ~0.15m（不会先落地），且 0.45m 半径的胶囊能容下几度的角度误差。
		chaser.GlobalPosition = player.GlobalPosition + (Vector3.Up * 0.9f) + (aim * 2.1f);
		GameLog.Write(
			"SelfTest",
			$"最小闭环·靶子落点：准星方向={aim}（水平朝向={forward}）→ 靶子放在 {chaser.GlobalPosition}"
			+ $"（距玩家 {player.GlobalPosition.DistanceTo(chaser.GlobalPosition):0.##}m，带 0.45/1.8 胶囊碰撞体）");
		_loopEnemy = chaser;
		_loopEnemyHealthBefore = chaser.Health;
		_loopEnemyArmorBefore = chaser.Armor;
		chaser.Damaged += OnLoopDamage;
		GameLog.Write("SelfTest", "最小闭环·造靶子：完成（朴素 Unit 靶子）");

		// ④ 盯住动画事件帧与投射物。
		UnitAnimator.AnimationEvent += OnLoopAnimationEvent;
		GetTree().NodeAdded += OnLoopNodeAdded;

		EnergyWaveAbility? ability = PlayerAbilityInput.FindByInputAction(player, "weapon_skill_1") as EnergyWaveAbility;
		_loopAbilityId = ability?.AbilityId ?? "（找不到）";
		_loopAbilityEnabled = ability?.Enabled ?? false;

		GameLog.Write(
			"SelfTest",
			$"最小闭环·准备：角色 chr_hunter / 武器 wpn_energy_palm；左键技能={_loopAbilityId} 启用={_loopAbilityEnabled}"
			+ $"（期望 skl_a1_w1_1 / True）｜动画={ability?.AnimName} 事件帧={ability?.AnimEvent}；"
			+ $"靶子={_loopEnemy?.Name} 生命 {_loopEnemyHealthBefore:0.#} 护甲 {_loopEnemyArmorBefore:0.#} "
			+ $"距玩家 {(player.GlobalPosition.DistanceTo(_loopEnemy?.GlobalPosition ?? player.GlobalPosition)):0.##}m");
	}

	private void OnLoopAnimationEvent(string name) => _loopAnimEvents++;

	private void OnLoopNodeAdded(Node node)
	{
		if (node is not Projectile shot)
		{
			return;
		}

		_loopProjectiles++;

		// ⚠️ 靶子必须在**投射物出生的这一帧**才挪到弹道上 —— 这是唯一"一定对得上"的时刻。
		// 提前摆（哪怕只提前 0.08s、摆在"扣扳机那一刻"的正确位置上）也不行：自测走到这一段时
		// 关卡装配还在挪玩家（实测两次位置都不一样），弹道是从**发射那一帧**的玩家位置出发的，
		// 先摆好的靶子会被甩开。这里直接问投射物自己的出生点。
		Unit? dummy = _loopEnemy;
		Unit? player = PlayerLocator.Player;
		if (dummy == null || player == null)
		{
			return;
		}

		Vector3 face = -player.GlobalTransform.Basis.Z;
		face.Y = 0f;
		Vector3 aim = AimUtil.GetCrosshairDirection(player.GetViewport(), face.Normalized()).Normalized();
		if (aim.LengthSquared() < 0.5f)
		{
			aim = face.Normalized();
		}

		// 摆在出生点沿弹道 0.8m 处：投射物下一物理帧从出生点走 2.5m，必然穿过它。
		//
		// ⚠️ 不能用 `shot.GlobalPosition`：`NodeAdded` 是 `AddChild` 里触发的，而 `FireShot`
		// 是**先 AddChild、后设位置**，所以这一刻投射物还在世界原点 (0,0,0)（实测踩到）。
		// 这里按 `FireShot` 的同一个公式自己算出生点 —— 同一个物理帧、同一个玩家位置、同一个方向。
		Vector3 origin = player.GlobalPosition + (Vector3.Up * 0.9f) + (aim * 0.6f);
		dummy.GlobalPosition = origin + (aim * 0.8f);
		GameLog.Write(
			"SelfTest",
			$"最小闭环·靶子上弹道：算出出生点={origin}（投射物自带位置此刻还是 {shot.GlobalPosition}）"
			+ $" 方向={aim} → 靶子={dummy.GlobalPosition}");
	}

	private void OnLoopDamage(DamageResult result, HitInstance hit)
	{
		_loopDamaged = true;
		_loopDamage = result.HealthLost;
		GameLog.Write(
			"SelfTest",
			$"最小闭环·命中：伤害输入 {hit.Damage:0.##} → 生命扣 {result.HealthLost:0.##} 护甲扣 {result.ArmorLost:0.##} "
			+ $"被格挡={result.Blocked} 无视格挡标记={hit.BypassBlock} 攻击技能={hit.Attack?.AbilityKind}");
	}

	/// <summary>注入一次按键（`InputAction` 名，例如 <c>weapon_skill_1</c>）。</summary>
	private static void PressAbilityAction(string action)
	{
		if (!InputMap.HasAction(action))
		{
			GameLog.Write("SelfTest", $"🔴 最小闭环：project.godot 里没有按键动作「{action}」。");
			return;
		}

		foreach (InputEvent source in InputMap.ActionGetEvents(action))
		{
			// ⚠️ 必须**复制**一份再注入：`ActionGetEvents` 返回的是输入映射自己持有的那个实例，
			// 直接 `ParseInputEvent` 会让引擎接管它的生命周期 —— 实测直接把进程搞崩（exit -1073741819）。
			Input.ParseInputEvent((InputEvent)source.Duplicate());
			return;
		}
	}

	/// <summary>
	/// 排障用：把玩家那个 `AnimationPlayer` 的状态直接打出来 —— 技能动画到底有没有在播、
	/// 方法轨道在不在、`CallbackModeMethod` 是不是 Immediate。
	/// </summary>
	private void TracePlayerAnimationState()
	{
		Unit? player = PlayerLocator.Player;
		UnitAnimator? animator = player?.GetNodeOrNull<UnitAnimator>(UnitAnimator.NodeName);
		AnimationPlayer? ap = player?.GetNode("Model") is { } model ? NodeSearch.FindInSubtree<AnimationPlayer>(model) : null;
		if (ap == null)
		{
			GameLog.Write("SelfTest", "🔴 最小闭环·动画状态：拿不到玩家的 AnimationPlayer。");
			return;
		}

		var info = new System.Text.StringBuilder();
		foreach (string name in ap.GetAnimationList())
		{
			if (!name.StartsWith("skills/"))
			{
				continue;
			}

			Animation? a = ap.GetAnimation(name);
			int methods = 0;
			float t0 = -1f;
			string path = "?";
			for (int i = 0; i < (a?.GetTrackCount() ?? 0); i++)
			{
				if (a!.TrackGetType(i) == Animation.TrackType.Method)
				{
					methods++;
					path = a.TrackGetPath(i).ToString();
					t0 = a.TrackGetKeyCount(i) > 0 ? (float)a.TrackGetKeyTime(i, 0) : -1f;
				}
			}

			info.Append($"[{name} 长度={a?.Length:0.###} 方法轨道={methods} path='{path}' 首帧@{t0:0.###}] ");
		}

		GameLog.Write(
			"SelfTest",
			$"最小闭环·动画状态：库已加载={animator?.HasSkillLibrary} 当前={ap.CurrentAnimation} "
			+ $"播放中={ap.IsPlaying()} pos={ap.CurrentAnimationPosition:0.###} speed_scale={ap.SpeedScale} "
			+ $"回调模式={ap.CallbackModeMethod}｜技能动画：{info}");

		// ⚠️ 排障关键：把玩家的动画层级与"方法轨道到底会落到哪个节点"打出来。
		// AnimationPlayer.RootNode 默认是 ".."（= Model），方法轨道的 path 再相对它解析，
		// 所以轨道写 ".." 应该落在 Model 的父节点（Player）上。
		var chain = new System.Text.StringBuilder();
		Node? cursor = ap;
		while (cursor != null)
		{
			chain.Append(cursor.Name).Append(" ← ");
			cursor = cursor.GetParent();
		}

		Node? resolved = ap.GetNodeOrNull(ap.RootNode)?.GetNodeOrNull("..");
		GameLog.Write(
			"SelfTest",
			$"最小闭环·层级取证：AnimationPlayer='{ap.GetPath()}' RootNode='{ap.RootNode}' "
			+ $"GetNode(RootNode)='{ap.GetNodeOrNull(ap.RootNode)?.Name}' 再往上='{resolved?.Name}'（期望 Player）"
			+ $"｜MethodCallMode={ap.MethodCallMode}"
			+ $"｜Player.HasMethod('anim_event')={PlayerLocator.Player?.HasMethod("anim_event")} "
			+ $"｜链：{chain}");

		// 直接在玩家的播放器上播一遍（不经过 UnitAnimator）：用来把"方法轨道能不能点中 Player"、
		// "Stop()+Play() 有没有让事件帧失效"两件事分开。
		// ⚠️ 这是**排障用的取证动作**，默认关掉 —— 它会抢走播放器、把后续的正式激活搅乱。
		if (_loopTraceLeft > 0)
		{
			ap.SpeedScale = 1f;
			ap.Play("skills/Skill_A1_ATK1");
			GameLog.Write("SelfTest", "最小闭环·直放取证：已在玩家播放器上直接 Play(skills/Skill_A1_ATK1)，等待事件帧…");
		}

		GameLog.Write("SelfTest", "最小闭环·动画状态取证完成（这一帧不再做别的动画操作）");
	}

	/// <summary>兜底：直接激活左键技能（`Input` 注入不可用时的取证路径，见 case 31 的注释）。</summary>
	private void FallbackActivateSkill()
	{
		Unit? player = PlayerLocator.Player;
		if (player == null)
		{
			return;
		}

		if (PlayerAbilityInput.FindByInputAction(player, "weapon_skill_1") is { } ability)
		{
			// ⚠️ 激活**之前**把靶子重新摆一次：玩家在 `BeginSkillLoopProbe` 之后还在往下落
			// （实测 y 从 0.8 掉到地面），而"准星方向"是**从相机出发的射线** —— 玩家的高度一变，
			// 射线就整体平移，先前按旧高度摆好的靶子会落到弹道下方 0.8m，直接漏掉。
			// 所以靶子的位置必须以"扣扳机这一刻"的射线为准（两行，纯自测代码）。
			Unit? dummy = _loopEnemy;
			if (dummy != null)
			{
				Vector3 face = -player.GlobalTransform.Basis.Z;
				face.Y = 0f;
				face = face.Normalized();
				Vector3 aim = AimUtil.GetCrosshairDirection(player.GetViewport(), face).Normalized();
				if (aim.LengthSquared() < 0.5f)
				{
					aim = face;
				}

				dummy.GlobalPosition = player.GlobalPosition + (Vector3.Up * 0.9f) + (aim * 2.1f);
				GameLog.Write(
					"SelfTest",
					$"最小闭环·靶子重摆（扣扳机前）：玩家={player.GlobalPosition} 方向={aim} → 靶子={dummy.GlobalPosition} "
					+ $"（距玩家 {player.GlobalPosition.DistanceTo(dummy.GlobalPosition):0.##}m）");
			}

			GameLog.Write("SelfTest", $"最小闭环·兜底前：技能={ability.AbilityId} IsReady={ability.IsReady} 即将 TryActivate");
			bool ok = ability.TryActivate();
			GameLog.Write(
				"SelfTest",
				$"最小闭环·兜底直接激活：TryActivate={ok}｜诊断：{ability.Diagnose()}"
				+ $"（按键注入那条路没生效，机制本身照测）");
		}
	}

	/// <summary>
	/// 最小闭环那几帧的**每帧**取证（排障时才打开：`_loopTraceLeft &gt; 0`）。
	/// 平时是 0，也就是零开销、不刷日志。
	/// </summary>
	private void TraceSkillLoopFrames()
	{
		if (_loopTraceLeft <= 0)
		{
			return;
		}

		_loopTraceLeft--;
		AnimationPlayer? ap = PlayerLocator.Player?.GetNode("Model") is { } model ? NodeSearch.FindInSubtree<AnimationPlayer>(model) : null;
		GameLog.Write(
			"Combat",
			$"逐帧·技能动画：当前={ap?.CurrentAnimation} pos={ap?.CurrentAnimationPosition:0.0000} 播放中={ap?.IsPlaying()} "
			+ $"speed_scale={ap?.SpeedScale:0.##} 事件累计={_loopAnimEvents}");
	}

	private int _loopTraceLeft;

	private void ReportSkillLoop()
	{
		UnitAnimator? animator = PlayerLocator.Player?.GetNodeOrNull<UnitAnimator>(UnitAnimator.NodeName);
		AnimationPlayer? ap = PlayerLocator.Player?.GetNode("Model") is { } model ? NodeSearch.FindInSubtree<AnimationPlayer>(model) : null;
		if (ap != null)
		{
			Animation? a = ap.GetAnimation("skills/Skill_A1_ATK1");
			string keyDump = DescribeMethodKey(a);

			GameLog.Write(
				"SelfTest",
				$"最小闭环·关键帧取证：动画长度={a?.Length:0.###} 当前={ap.CurrentAnimation} pos={ap.CurrentAnimationPosition:0.###} "
				+ $"播放中={ap.IsPlaying()} speed_scale={ap.SpeedScale}｜{keyDump}");
		}

		float healthAfter = _loopEnemy?.Health ?? -1f;
		string damageText = _loopDamaged
			? $"生命 {_loopEnemyHealthBefore:0.#} → {healthAfter:0.#}（扣 {_loopEnemyHealthBefore - healthAfter:0.##}）"
			: $"🔴 没有收到伤害（靶子生命 {_loopEnemyHealthBefore:0.#} → {healthAfter:0.#} 未变）" +
				"；自测探针的靶子只有 `Unit` 自己 + 手工加的胶囊，实测投射物的射线打不到它 —— " +
				"所以『动画→事件帧→发射』自测覆盖，『命中掉血』由真人手测取证（docs/anim-pipeline.md §6.4）";

		GameLog.Write(
			"SelfTest",
			$"🔴 最小闭环·结果：动画事件帧收到={_loopAnimEvents} 次 投射物生成={_loopProjectiles} 个｜"
			+ $"{damageText} ｜技能={_loopAbilityId} 动画={animator?.CurrentAnimation}");
		GameLog.Write(
			"SelfTest",
			"🔴 最小闭环·链路：" + (string.Join(" → ", new[]
			{
				"按键 weapon_skill_1",
				$"技能 {_loopAbilityId}",
				$"动画 {_loopAnimEvents} 个事件帧",
				$"投射物 {_loopProjectiles}",
				_loopDamaged ? "命中掉血 ✓" : "命中掉血 ✗",
			})));
	}

	/// <summary>
	/// 读一段动画上方法轨道的**第一个关键帧**，拼成日志文本。
	///
	/// ⚠️ **这里绝对不能改用 `MethodTrackGetName / MethodTrackGetParams`**：
	/// 实测 Godot 4.7.2（这套节点层级 + 运行期加进命名库的动画）上这两个 API
	/// 会直接让进程崩掉（`exit -1073741819` = 访问违规，调用栈落在
	/// `Godot.Animation.MethodTrackGetParams`）。**唯一安全的读法是 `TrackGetKeyValue`**
	/// （取回那个 `Dictionary{"method","args"}`）—— 运行期的 `UnitAnimator.ReadSkillEvents`
	/// 走的也是这一条，两处口径必须一致。
	/// </summary>
	private static string DescribeMethodKey(Animation? a)
	{
		string dump = "（没有方法轨道）";
		for (int i = 0; i < (a?.GetTrackCount() ?? 0); i++)
		{
			if (a!.TrackGetType(i) != Animation.TrackType.Method)
			{
				continue;
			}

			if (a.TrackGetKeyCount(i) == 0)
			{
				dump = $"轨道{i} 无关键帧 启用={a.TrackIsEnabled(i)}";
				continue;
			}

			Variant raw = a.TrackGetKeyValue(i, 0);
			if (raw.VariantType != Variant.Type.Dictionary)
			{
				dump = $"轨道{i} key0 不是 Dictionary（{raw.VariantType}）";
				continue;
			}

			var dict = raw.AsGodotDictionary();
			string method = dict.TryGetValue("method", out Variant m) ? m.AsString() : "?";
			string argsText = dict.TryGetValue("args", out Variant av) && av.VariantType == Variant.Type.Array
				? string.Join(",", av.AsGodotArray())
				: "?";
			dump = $"method={method} args=[{argsText}] 启用={a.TrackIsEnabled(i)} "
				+ $"时间={a.TrackGetKeyTime(i, 0):0.###} path='{a.TrackGetPath(i)}'";
		}

		return dump;
	}

	/// <summary>白板首领闸门正向验证（阶段 13–17）盯着的普通 Encounter、它被临时改过的东西，以及进这一段时的累计波数。</summary>
	private EncounterController? _eliteProbe;
	private int _eliteOriginalGate;
	private PackedScene? _eliteOriginalScene;
	private int _currentWaves;

	/// <summary>Boss 门那一段（阶段 18–22）盯着的就是 Boss 战那条 Encounter。</summary>
	private EncounterController? _bossProbe;

	/// <summary>诊断用：Boss 是否已经被自测打死（阶段 20 每帧检查，打死了才推进到下一阶段）。</summary>
	private bool _bossKilled;

	/// <summary>Boss 战那一段把玩家挪到这里的远处（Boss 与护卫是"在玩家附近"生成的，站脸上会被秒）。</summary>
	private static readonly Vector3 SelfTestAwaySpot = new(18f, 0.8f, 18f);

	/// <summary>
	/// 找**普通节点**那条 Encounter（`Main.tscn` 的 <c>Stage/Combat</c>）——
	/// 也就是挂了白板首领预制体的那一条。Boss 战那条（<c>Stage/BossFight</c>）不算：
	/// 它没有 <c>EliteBossScene</c>，闸门永远判不开（自测第一次跑就踩到这个）。
	/// </summary>
	private static EncounterController? FindRegularEncounter(StageController? stage)
	{
		if (stage == null)
		{
			return null;
		}

		foreach (Node child in stage.GetChildren())
		{
			if (child is EncounterController { IsBossNode: false, EliteBossScene: not null } regular)
			{
				return regular;
			}
		}

		return null;
	}

	/// <summary>找 Boss 死后降临的那个节点（`Kind = Portal`，由 StageController 动态实例化）。</summary>
	private static RewardNode? FindPortal(StageController? stage)
	{
		if (stage == null)
		{
			return null;
		}

		foreach (Node child in stage.GetChildren())
		{
			if (child is RewardNode { Kind: RewardNodeKind.Portal } portal)
			{
				return portal;
			}
		}

		return null;
	}

	/// <summary>把玩家传送到指定类型节点旁边（用于端到端交互测试）。</summary>
	private void MovePlayerToNode(StageController? stage, RewardNodeKind kind)
	{
		// 玩家现在挂在 Stage 下面（不是 Main/Player），用统一入口取。
		var player = PlayerLocator.Player ?? GetTree().GetFirstNodeInGroup("player_units") as Unit;
		if (stage == null || player == null)
		{
			return;
		}

		foreach (Node child in stage.GetChildren())
		{
			if (child is RewardNode node && node.Kind == kind)
			{
				node.Enabled = true;
				node.Visible = true;
				Vector3 target = node.GlobalPosition + new Vector3(0f, 0.8f, 1.6f);
				player.GlobalPosition = target;
				return;
			}
		}
	}

	/// <summary>
	/// 注入一次按键：按下后**下一帧**才松开。
	/// 必须跨帧 —— IsActionJustPressed 只在"输入被处理的那一帧"为真，
	/// 同一帧里注入又立即松开，游戏侧可能完全看不到这次按下。
	/// </summary>
	private static void PressKey(Key key)
	{
		Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
		_pendingKeyUp = key;
	}

	private static Key? _pendingKeyUp;

	/// <summary>
	/// 开一枪。手枪 24 伤害 < 敌人 26 血，所以敌人会残血但不死 —— 用来观察血条位置。
	/// 跨帧松开：单击武器用 IsActionJustPressed，同帧按下又松开会被漏掉。
	/// </summary>
	private static void PressFireOnce()
	{
		Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
		_pendingFireRelease = true;
	}

	private static bool _pendingFireRelease;

	private void ReportInteraction(StageController? stage, int round)
	{
		bool accepted = stage?.DebugTriggerNode(RewardNodeKind.Evolution) ?? false;
		int depth = UiRouter.Instance?.StackDepth ?? -1;
		GameLog.Write("SelfTest", $"交互 #{round}：接受={accepted} Paused={GetTree().Paused} 界面栈深={depth} 状态=[{stage?.DebugState}]");
	}

	/// <summary>
	/// 价值标签表的只读汇总：每一行标签的波数 + "节点类型 → 实际波数"（用户需求 #7 的硬指标留痕）。
	///
	/// 这张表是**节点开几波的驱动者**（取代原来按 <see cref="RewardNodeKind"/> 枚举硬编码的 3/2/1）：
	/// 所以这里必须同时打出"标签定了几波"与"节点实际算出几波"，只打一个都证明不了链路是通的。
	/// </summary>
	private static string SelfTestValueTagSummary()
	{
		var tags = new List<string>();
		foreach (string id in ConfigRows.Ids<ValueTagConfig>())
		{
			if (ConfigRows.ById<ValueTagConfig>(id) is { } row)
			{
				tags.Add($"{row.Id}={row.WaveCount}波");
			}
		}

		tags.Sort(System.StringComparer.Ordinal);
		var nodes = new List<string>();
		foreach (RewardNodeKind kind in System.Enum.GetValues<RewardNodeKind>())
		{
			nodes.Add($"{kind}={StageController.WavesFor(kind)}");
		}

		return $"标签 [{string.Join(" ", tags)}]（期望 tag_high=4 tag_low=1 tag_mid=3）→ 节点 [{string.Join(" ", nodes)}]（期望 Evolution=4 Mutation=4 Weapon=4（用户 A3：武器限定池 = 进化池 = 高价值）Enhancement=3 Shop=3 Resource=1 Heal=1 Special=1 Portal=1）";
	}

	/// <summary>当前阶段号（崩溃面包屑用：写进 <c>user://logs/selftest_phase.txt</c>）。</summary>
	private static void WritePhaseBreadcrumb(int phase)
	{
		using FileAccess? file = FileAccess.Open("user://logs/selftest_phase.txt", FileAccess.ModeFlags.Write);
		file?.StoreLine($"{phase} {Time.GetDatetimeStringFromSystem()}");
	}


	private void Next()
	{
		_phase++;
		_timer = 0f;
		WritePhaseBreadcrumb(_phase);
	}


	// === 表现层采样（残影排查用）===
	// 逐帧记录：物理步进时角色位移、相机位移、两者相位差、以及渲染帧间隔抖动。
	private Unit? _probePlayer;
	private Node3D? _probeRig;
	private bool _probeProbing;
	private Vector3 _probePrevPlayer;
	private Vector3 _probePrevRig;
	private readonly List<float> _probePlayerStep = new();
	private readonly List<float> _probeRigStep = new();
	private readonly List<float> _probeRigOffset = new();
	private readonly List<float> _probeFrameMs = new();
	private readonly List<float> _probePlayerStepNonZero = new();
	private readonly List<float> _probeInterpStep = new();
	private readonly List<float> _probeInterpOffset = new();
	private readonly List<float> _probeRigInterpStep = new();
	private readonly List<float> _probeRigInterpOffset = new();

	private void SamplePresentation(float delta)
	{
		if (_probePlayer == null || _probeRig == null)
		{
			return;
		}

		Vector3 player = _probePlayer.GlobalPosition;
		Vector3 rig = _probeRig.GlobalPosition;
		_probePlayerStep.Add(player.DistanceTo(_probePrevPlayer));
		_probeRigStep.Add(rig.DistanceTo(_probePrevRig));
		_probeRigOffset.Add(_probePrevPlayer.DistanceTo(rig));
		_probeFrameMs.Add(delta * 1000f);

		// 渲染器真正用的变换：开了物理插值后它会落在两个物理帧之间（≠ GlobalPosition）。
		Vector3 interp = _probePlayer.GetGlobalTransformInterpolated().Origin;
		if (_probePrevInterp != null)
		{
			_probeInterpStep.Add(interp.DistanceTo(_probePrevInterp.Value));
		}

		Vector3 rigInterp = _probeRig.GetGlobalTransformInterpolated().Origin;
		if (_probePrevRigInterp != null)
		{
			_probeRigInterpStep.Add(rigInterp.DistanceTo(_probePrevRigInterp.Value));
		}

		_probeRigInterpOffset.Add(interp.DistanceTo(rigInterp));
		_probePrevRigInterp = rigInterp;
		_probeInterpOffset.Add(player.DistanceTo(interp));
		_probePrevInterp = interp;
		_probePrevPlayer = player;
		_probePrevRig = rig;
	}

	private Vector3? _probePrevInterp;
	private Vector3? _probePrevRigInterp;

	private void ReportPresentation()
	{
		if (_probePlayerStep.Count < 10)
		{
			GameLog.Write("SelfTest", $"表现采样：样本不足（{_probePlayerStep.Count}），跳过");
			return;
		}

		foreach (float step in _probePlayerStep)
		{
			if (step > 0.0001f)
			{
				_probePlayerStepNonZero.Add(step);
			}
		}

		int frozenFrames = _probePlayerStep.Count - _probePlayerStepNonZero.Count;
		int frozenInterp = 0;
		foreach (float step in _probeInterpStep)
		{
			if (step <= 0.0001f)
			{
				frozenInterp++;
			}
		}

		GameLog.Write("SelfTest",
			$"表现采样：帧数={_probePlayerStep.Count} 物理位置位移为 0 的帧={frozenFrames}（{(frozenFrames * 100f / _probePlayerStep.Count):0.#}%）" +
			$" 插值后位移为 0 的帧={frozenInterp}（{(frozenInterp * 100f / Mathf.Max(1, _probeInterpStep.Count)):0.#}%）" +
			$" 物理Hz={Engine.PhysicsTicksPerSecond} 渲染fps均值={(_probePlayerStep.Count / Mathf.Max(0.0001f, Sum(_probeFrameMs) / 1000f)):0.#}");
		GameLog.Write("SelfTest",
			$"表现采样：物理位置单帧位移 均值={Mean(_probePlayerStepNonZero):0.0000} 最小={Min(_probePlayerStepNonZero):0.0000} 最大={Max(_probePlayerStepNonZero):0.0000} 标准差={StdDev(_probePlayerStep, Mean(_probePlayerStep)):0.0000}");
		GameLog.Write("SelfTest",
			$"表现采样：插值后单帧位移 均值={Mean(_probeInterpStep):0.0000} 最小={Min(_probeInterpStep):0.0000} 最大={Max(_probeInterpStep):0.0000} 标准差={StdDev(_probeInterpStep, Mean(_probeInterpStep)):0.0000}（越接近 0 越平滑）");
		GameLog.Write("SelfTest",
			$"表现采样：物理位置↔插值位置 偏差 均值={Mean(_probeInterpOffset):0.0000} 最大={Max(_probeInterpOffset):0.0000}（>0 说明插值真的生效了）");
		GameLog.Write("SelfTest",
			$"表现采样：相机插值后单帧位移 均值={Mean(_probeRigInterpStep):0.0000} 最小={Min(_probeRigInterpStep):0.0000} 最大={Max(_probeRigInterpStep):0.0000} 标准差={StdDev(_probeRigInterpStep, Mean(_probeRigInterpStep)):0.0000}");
		GameLog.Write("SelfTest",
			$"表现采样：角色↔相机（都用插值后位置）相位差 均值={Mean(_probeRigInterpOffset):0.0000} 最小={Min(_probeRigInterpOffset):0.0000} 最大={Max(_probeRigInterpOffset):0.0000} 标准差={StdDev(_probeRigInterpOffset, Mean(_probeRigInterpOffset)):0.0000}（这才是真正决定画面抖动的量）");
		GameLog.Write("SelfTest",
			$"表现采样：相机单帧位移 均值={Mean(_probeRigStep):0.0000} 最小={Min(_probeRigStep):0.0000} 最大={Max(_probeRigStep):0.0000} 标准差={StdDev(_probeRigStep, Mean(_probeRigStep)):0.0000}");
		GameLog.Write("SelfTest",
			$"表现采样：角色↔相机 相位差 均值={Mean(_probeRigOffset):0.000} 最小={Min(_probeRigOffset):0.000} 最大={Max(_probeRigOffset):0.000}（残影=这个值在帧间反复变化）");
		GameLog.Write("SelfTest",
			$"表现采样：渲染帧间隔 均值={Mean(_probeFrameMs):0.00}ms 最小={Min(_probeFrameMs):0.00} 最大={Max(_probeFrameMs):0.00} 标准差={StdDev(_probeFrameMs, Mean(_probeFrameMs)):0.00}");
	}

	private static float Sum(List<float> values)
	{
		float total = 0f;
		foreach (float value in values)
		{
			total += value;
		}

		return total;
	}

	private static float Mean(List<float> values) => values.Count == 0 ? 0f : Sum(values) / values.Count;

	private static float Min(List<float> values)
	{
		float min = float.MaxValue;
		foreach (float value in values)
		{
			min = Mathf.Min(min, value);
		}

		return values.Count == 0 ? 0f : min;
	}

	private static float Max(List<float> values)
	{
		float max = float.MinValue;
		foreach (float value in values)
		{
			max = Mathf.Max(max, value);
		}

		return values.Count == 0 ? 0f : max;
	}

	private static float StdDev(List<float> values, float mean)
	{
		if (values.Count == 0)
		{
			return 0f;
		}

		float total = 0f;
		foreach (float value in values)
		{
			float diff = value - mean;
			total += diff * diff;
		}

		return Mathf.Sqrt(total / values.Count);
	}

	private static void ReportNodes(StageController? stage)
	{
		if (stage == null)
		{
			GameLog.Write("SelfTest", "节点检查失败：找不到 Stage");
			return;
		}

		int high = 0;
		int mid = 0;
		int low = 0;
		int total = 0;
		var names = new List<string>();
		foreach (Node child in stage.GetChildren())
		{
			if (child is not RewardNode node || !node.Enabled)
			{
				continue;
			}

			total++;
			names.Add($"{node.Name}(Kind={node.Kind})");

			// ⚠️ 高/中/低按**价值层**数（`NodePlacement.LayerOf` = 节点表的 `Layer`，也就是价值标签那一层），
			// **不再按 `RewardNodeKind` 枚举 switch** —— 用户 A3 拍板"没有武器池、那个叫法就是进化池（高价值）"
			// 之后，按枚举数会把 `Weapon` 这类节点数进"低"，与它挂的价值标签自相矛盾。
			// 当前场景里没有 `Kind=Weapon` 的实例，所以这条改动**不改变数字**（仍然 2/5/3），
			// 但规则从此与价值标签一致：以后把哪种奖励挂到高价值标签上，它就数进"高"。
			switch (NodePlacement.LayerOf(node.Kind))
			{
				case 2:
					high++;
					break;
				case 1:
					mid++;
					break;
				default:
					low++;
					break;
			}
		}

		GameLog.Write("SelfTest", $"节点布置：启用 {total} 个（高 {high} / 中 {mid} / 低 {low}）→ 期望 10（2/5/3）（按**价值层**数，见 NodePlacement.LayerOf）");
		GameLog.Write("SelfTest", $"节点清单：{string.Join(", ", names)}");
	}

	private static void ReportSpacing(StageController? stage)
	{
		if (stage == null)
		{
			return;
		}

		var list = new List<RewardNode>();
		foreach (Node child in stage.GetChildren())
		{
			if (child is RewardNode { Enabled: true } node)
			{
				list.Add(node);
			}
		}

		float min = float.MaxValue;
		string pair = "-";
		for (int i = 0; i < list.Count; i++)
		{
			for (int j = i + 1; j < list.Count; j++)
			{
				float distance = list[i].GlobalPosition.DistanceTo(list[j].GlobalPosition);
				if (distance < min)
				{
					min = distance;
					pair = $"{list[i].Name}↔{list[j].Name}";
				}
			}
		}

		GameLog.Write("SelfTest", $"节点间距：最小 {min:0.##}（{pair}），阈值 {ActRoguelike.Config.ConfigIndex.Settings.MinNodeSpacing:0.#}");
	}

	/// <summary>
	/// 配置表阶段 2 的**逐项比对**：把分表后的实际数值打出来，
	/// 与分表前 `ConfigIndex.tres` 的记录逐条对照（见 docs/config.md 的实测表）。
	/// 玩家/武器数值来自 `Config/Tables/Characters` 与 `Config/Tables/Weapons`。
	/// </summary>
	private void ReportTables(StageController? stage)
	{
		var player = PlayerLocator.Player;
		ActRoguelike.Weapons.Weapon? weapon = player == null ? null : NodeSearch.FindInSubtree<ActRoguelike.Weapons.Weapon>(player);

		GameLog.Write("SelfTest",
			player == null
				? "配置表比对：找不到玩家"
				: $"配置表比对·玩家：生命 {player.MaxHealth:0}（期望 250 + 元进度加成 {MetaProgress.BonusMaxHealth:0}） 护甲 {player.StartingArmor:0}（期望 40） 移速 {player.MoveSpeed:0.##}（期望 7） 减伤 {player.DamageReduction:0.##}（期望 0）");

		GameLog.Write("SelfTest",
			weapon == null
				? "配置表比对：找不到武器"
				: $"配置表比对·武器：伤害 {weapon.Damage:0}（期望 24） 间隔 {weapon.FireInterval:0.##}（期望 0.26） 弹匣 {weapon.EffectiveMagazineSize}（期望 12） 装填 {weapon.ReloadTime:0.##}（期望 0.7） 散布 {weapon.SpreadDegrees:0.##}（期望 0.5） 击退 {weapon.Knockback:0.##}（期望 2.5） 暴击 {weapon.CritChance:0.##}（期望 0.15） 弹速 {weapon.ProjectileSpeed:0}（期望 90） 预瞄FOV {weapon.AimFov:0}（期望 55） 穿透 {weapon.BasePierce}（期望 0） 连发 {weapon.Automatic}（期望 False）");

		GameLog.Write("SelfTest",
			$"配置表比对·索引：武器 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.WeaponConfig>()}/角色 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.CharacterConfig>()}/敌人 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.EnemyConfig>()}/首领 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.BossConfig>()}/召唤物 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.SummonConfig>()}/技能 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.AbilityConfig>()}/Pressure {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.PressureModConfig>()}/效果 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.EffectConfig>()}/节点 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.NodeConfig>()}/局外 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.MetaUpgradeConfig>()}/升级池 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.UpgradePoolConfig>()}/AI 档案 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.AiProfileConfig>()}/能力卡 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.AbilityCardConfig>()}（期望 3/3/10/1/1/25/7/9/8/6/3/11/8）");

		// 能力卡（局外装备层，`docs/ability-cards.md`）：卡表读数 + 状态层全流程
		// （解锁 → 重复解锁 → 升级 → 上限 → 重置归还 → 激活额度记账 → 未解锁/额度不足/碎片不足 → 额度扩增）。
		// ⚠️ 全程用**内存钱包 + 不落盘**（AbilityStore.DebugSelfTestLines 内部自己换），
		// 所以这一段不会改动玩家的 user://meta.cfg 与 user://ability_cards.cfg。
		foreach (string line in ActRoguelike.AbilityCards.AbilityStore.DebugSelfTestLines())
		{
			GameLog.Write("SelfTest", line);
		}

		// 激活的能力**真的影响局内**：临时激活一张数值卡 + 一张授予技能卡，量出实际改变，再逐项还原。
		if (player != null)
		{
			GameLog.Write("SelfTest", ActRoguelike.AbilityCards.AbilityEffects.DebugPreviewLine(player));
		}

		GameLog.Write("SelfTest",
			$"配置表比对·节点表：进化层 {NodePlacement.LayerOf(RewardNodeKind.Evolution)}/波 {NodePlacement.WavesFor(RewardNodeKind.Evolution)}（期望 2/4） 强化层 {NodePlacement.LayerOf(RewardNodeKind.Enhancement)}/波 {NodePlacement.WavesFor(RewardNodeKind.Enhancement)}（期望 1/3） 恢复层 {NodePlacement.LayerOf(RewardNodeKind.Heal)}/波 {NodePlacement.WavesFor(RewardNodeKind.Heal)}（期望 0/1） 商店价目 {RewardService.ShopCost(0)}/{RewardService.ShopCost(1)}/{RewardService.ShopCost(2)}（期望 5/12/18） 标题 {RewardService.TitleFor(RewardNodeKind.Mutation)}（期望 异化池）");

		// 节点波数的**来源**必须能一眼看出来（"高 4 / 中 3 / 低 1 是标签算出来的、不是代码写死的"）。
		GameLog.Write("SelfTest", $"配置表比对·价值标签：{SelfTestValueTagSummary()}");

		// 跳跃（需求 A2）：角色行给的是**固定值**，直接套到组件上。
		// ⚠️ 期望的**落点**是 13 m/s（角色行 `JumpVelocity`，场景回退值 6.5）与重力缩放 3.6
		// （角色行 `JumpGravityScale`，场景回退值 1.8）—— 只在"表读到了"时成立；表读不到时组件保留
		// 6.5 / 1.8，这一行会明显不同（回退路径也必须看得出来）。
		// 空中加速 / 最大下落**不再乘任何倍率**（上一批的 `JumpSpeedMultiplier` 已删除），所以期望回到场景值 15 / 60。
		if (player?.Movement is { } movement)
		{
			GameLog.Write("SelfTest",
				$"配置表比对·跳跃：起跳速度 {movement.JumpVelocity:0.##}（期望 13 = 角色行固定值，场景回退 6.5）重力缩放 {movement.GravityScale:0.##}（期望 3.6 = 角色行固定值，场景回退 1.8）空中加速 {movement.AirAcceleration:0.#}（期望 15 = 场景值，不再乘倍率）最大下落 {movement.MaxFallSpeed:0.#}（期望 60 = 场景值）");
		}
		else
		{
			GameLog.Write("SelfTest", "配置表比对·跳跃：拿不到玩家的 CharacterMovement（期望能拿到）");
		}

		ActRoguelike.Combat.StatusEffect? burn = ActRoguelike.Combat.StatusPresets.Create("Burn", 3f);
		ActRoguelike.Combat.StatusEffect? slow = ActRoguelike.Combat.StatusPresets.Create("Slow", 3f);
		GameLog.Write("SelfTest",
			$"配置表比对·效果表：燃烧 {burn?.DamagePerSecond:0} dps（期望 6） 减速修正 [{string.Join(",", System.Array.ConvertAll(slow?.Modifiers ?? System.Array.Empty<ActRoguelike.Combat.StatModifier>(), m => $"{m.Stat}={m.Value}"))}]（期望 MoveSpeedMultiplier=0.55）");

		// 增幅项目录来源 + 池子规模：`docs/rewards.md` 记录的硬指标是"枪手 18 / 召唤 19"。
		int enhancement = 0;
		int evolution = 0;
		int mutation = 0;
		foreach (RewardDefinition definition in RewardCatalog.All)
		{
			switch (definition.Kind)
			{
				case RewardKind.Enhancement:
					enhancement++;
					break;
				case RewardKind.Evolution:
					evolution++;
					break;
				default:
					mutation++;
					break;
			}
		}

		string poolIds = GetTree().Root.GetNodeOrNull<RunController>("Main/RunController")?.PoolPreview ?? string.Empty;
		string poolSize = poolIds.Length == 0 ? "空" : $"{poolIds.Split(',').Length} 条";
		GameLog.Write("SelfTest",
			$"配置表比对·增幅项：共 {RewardCatalog.All.Length} 条（强化 {enhancement} / 进化 {evolution} / 异化 {mutation}，期望 19 = 10/4/5） 用了表={(RewardCatalog.All.Length == 19 && RewardCatalog.All[0].Id == "upg_dmg_up")} 当前池子={poolSize}（枪手期望 18 条）");

		// 武器 → 升级池 → 选项（配置表阶段 4）：三级回退各层的条数 + 武器实际带上的池 Id。
		// 这是设计文档 §4.5.5 要求的"顺藤摸瓜"只读校验。回退路径（武器行没填 Pool / 池表读不到）时
		// PoolId 会是空、专属层命中 0 条，而**池子总数必须与表在时逐位一致**（上面那一行的 18 条）。
		int exactLayer = 0;
		int exactHit = 0;
		int tagLayer = 0;
		int tagHit = 0;
		int genericLayer = 0;
		foreach (RewardDefinition definition in RewardCatalog.All)
		{
			if (definition.ApplicablePoolIds.Length > 0)
			{
				exactLayer++;
				if (weapon != null && definition.AllowsWeapon(weapon))
				{
					exactHit++;
				}
			}
			else if (definition.RequiredWeaponTags.Length > 0)
			{
				tagLayer++;
				if (weapon != null && definition.AllowsWeapon(weapon))
				{
					tagHit++;
				}
			}
			else
			{
				genericLayer++;
			}
		}

		int pooledOptions = 0;
		foreach (ActRoguelike.Config.RewardConfig row in ActRoguelike.Config.ConfigIndex.Settings.Upgrades)
		{
			if (row?.Pool != null)
			{
				pooledOptions++;
			}
		}

		string poolIdText = weapon == null
			? "无武器"
			: string.IsNullOrEmpty(weapon.PoolId) ? "（留空 → 走「标签匹配 → 通用」两层）" : weapon.PoolId;

		// docs/rewards.md 的硬指标是"枪手 18 / **召唤 19**"：上面 PoolPreview 那行只覆盖枪手，
		// 这里用**同一个武器**另建一个池把召唤控制角色那一侧也量出来（不改 RunController 的状态 ——
		// 只换 RewardPool.SummonerAllowed 这一个开关，差异应当恰好是 mut_summon 那 1 条）。
		string summonerCount = "无武器";
		if (weapon != null)
		{
			var summonerPool = new RewardPool { SummonerAllowed = true };
			summonerPool.Initialize(weapon);
			summonerCount = $"{summonerPool.Roll(new RunBuild(), 40).Count} 条（期望 19）";
		}

		GameLog.Write("SelfTest",
			$"配置表比对·升级池：武器 PoolId「{poolIdText}」（期望 pool_pistol） 池表 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.UpgradePoolConfig>()} 行（期望 3）/ 选项行里指定了池的 {pooledOptions} 条 ｜ 专属层 {exactLayer} 条（命中 {exactHit}）· 类别层 {tagLayer} 条（命中 {tagHit}）· 通用层 {genericLayer} 条 ｜ 召唤控制池 {summonerCount}");

		// 敌人表（阶段 3）：行数 + 白名单按 Id 匹配的结果 + 一行逐项数值。
		ActRoguelike.Config.EnemyConfig? global = ActRoguelike.Config.ConfigRows.ById<ActRoguelike.Config.EnemyConfig>("enemy_global");
		ActRoguelike.Config.EnemyConfig? chaser = ActRoguelike.Config.ConfigRows.ById<ActRoguelike.Config.EnemyConfig>("enemy_chaser");
		EncounterController? encounter = stage?.CurrentNode;
		GameLog.Write("SelfTest",
			$"配置表比对·敌人表：{ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.EnemyConfig>()} 行（期望 10 = 1 全局 + 9 敌种） 全局行 生命×{global?.HealthMultiplier:0.##}/伤害×{global?.DamageMultiplier:0.##}（期望 0.75/0.3） 本关白名单 [{string.Join(",", stage?.Definition?.AllowedEnemyIds ?? System.Array.Empty<string>())}]（期望按 Id） 实际可生成 [{encounter?.AllowedEntrySummary}]");
		GameLog.Write("SelfTest",
			chaser == null
				? "配置表比对·敌人行：读不到 enemy_chaser（应回退到场景值）"
				: $"配置表比对·敌人行 enemy_chaser：生命 {chaser.MaxHealth:0}（期望 60） 移速 {chaser.MoveSpeed:0.##}（期望 4.2） 攻击 {chaser.AttackDamage:0}（期望 12） 间隔 {chaser.AttackInterval:0.##}（期望 1.3） 射程 {chaser.AttackRange:0.##}（期望 2.3） 击退 {chaser.AttackKnockback:0.##}（期望 3） 标签 [{string.Join(",", chaser.Tags)}] 成本 {chaser.Cost}/权重 {chaser.Weight:0.##} 持有技能 [{string.Join(",", System.Array.ConvertAll(chaser.Skills, s => s?.Id ?? "null"))}]");

		// 技能表（阶段 3）：行数 + 首领强化的两套来源必须能分开。
		ActRoguelike.Config.AbilityConfig? dashRow = ActRoguelike.Config.ConfigRows.ById<ActRoguelike.Config.AbilityConfig>("skl_dash");
		GameLog.Write("SelfTest",
			$"配置表比对·技能表：{ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.AbilityConfig>()} 行（期望 25 = 5 玩家 + 16 单位 + 4 首领行为） dash 行：冷却 {dashRow?.Cooldown:0.##}（期望 1.2） 前摇 {dashRow?.Windup:0.##}（期望 0.3） 节点 {dashRow?.NodeName}（期望 AbilityDash） ｜ 首领强化来源：Boss Build 行为型（**技能**）{ActRoguelike.Boss.BossPerkCatalog.BehaviorOnly.Count} 条（期望 4）/ Pressure 数值型（**词条**）{ActRoguelike.Boss.BossPerkCatalog.PressureOnly.Count} 条（期望 7 = 4 数值型参数栏 + 3 技能参数栏）");

		// AI 行为表（阶段 5）：行数 + "敌人行 → 档案"的关联 + **真的写进去了**的硬证据。
		ActRoguelike.Config.AiProfileConfig? chaserAi = ActRoguelike.Config.ConfigRows.ById<ActRoguelike.Config.AiProfileConfig>("ai_chaser");
		GameLog.Write("SelfTest",
			$"配置表比对·AI 档案表：{ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.AiProfileConfig>()} 行（期望 11 = 9 敌种 + 首领 + 召唤物） 敌人行 enemy_chaser 挂的档案「{chaser?.AiProfile?.Id}」（期望 ai_chaser） 档案行 SearchRange {chaserAi?.SearchRange:0.##}（期望 40）");
		if (chaser != null && GD.Load<PackedScene>("res://Scenes/Enemies/Chaser.tscn") is { } chaserScene)
		{
			// 探针：先把预制体上的 AI 值改成 -1（伪造"错的值"）再 ApplyTo ——
			// 值回到档案里的数才能证明档案**确实写进了** Brain / 行为节点，而不是"碰巧与场景相等"。
			var probe = chaserScene.Instantiate<Unit>();
			ActRoguelike.AI.EnemyBrain? probeBrain = NodeSearch.FindInSubtree<ActRoguelike.AI.EnemyBrain>(probe);
			ActRoguelike.AI.Behavior? probeChase = probeBrain?.GetNodeOrNull<ActRoguelike.AI.Behavior>("Chase");
			ActRoguelike.AI.Behavior? probeAttack = probeBrain?.GetNodeOrNull<ActRoguelike.AI.Behavior>("Attack");
			if (probeBrain != null)
			{
				probeBrain.SearchRange = -1f;
			}

			if (probeChase != null)
			{
				probeChase.Priority = -1;
			}

			chaser.ApplyTo(probe);
			GameLog.Write("SelfTest",
				$"AI 档案实测（Chaser 预制体：先把 SearchRange/Chase.Priority 伪造为 -1，再 ApplyTo，与生成路径同序）：SearchRange {probeBrain?.SearchRange:0.##}（期望 40） Chase.Priority {probeChase?.Priority}（期望 10） Chase.StopDistance {(probeChase == null ? 0f : probeChase.Get("StopDistance").AsSingle()):0.##}（期望 1.6） Attack.Priority {probeAttack?.Priority}（期望 100）");
			probe.Free();
		}

		// 玩家侧实测：技能行 + 角色行攻击组是否真的套上了（这条是"技能表接线"的硬证据）。
		if (player != null)
		{
			ActRoguelike.Abilities.Ability? dash = player.GetNodeOrNull<ActRoguelike.Abilities.Ability>("AbilityDash");
			ActRoguelike.Abilities.Ability? melee = player.GetNodeOrNull<ActRoguelike.Abilities.Ability>("AbilityMelee");
			AttackHitbox? meleeHit = melee == null ? null : NodeSearch.FindInSubtree<AttackHitbox>(melee);
			GameLog.Write("SelfTest",
				$"技能表实测（玩家）：滑铲 冷却 {dash?.Cooldown:0.##}（期望 1.2） 位移 {(dash == null ? 0f : dash.Get("Distance").AsSingle()):0.##}（期望 6） AbilityId {dash?.AbilityId}（期望 skl_dash） ｜ 近战 伤害 {meleeHit?.Damage:0}（期望 32，来自角色行攻击组） 间隔 {melee?.Cooldown:0.##}（期望 0.75，证明技能行的 0 = 不覆盖） 射程 {(melee == null ? 0f : melee.Get("AttackRange").AsSingle()):0.##}（期望 2.4） 击退 {meleeHit?.Knockback:0.##}（期望 5） 命中标签 [{string.Join(",", meleeHit?.AttackTags ?? System.Array.Empty<string>())}]（期望 Melee,Heavy）");
		}

		// Pressure 词条池（3 条拆成 5 条 + 技能参数入池）与**限定词的刷新判定**取证。
		// 全部逻辑放在 `Scripts/Boss/BossPerkProbe.cs`（跟着 SelfTest 走，但别把这里撑长）。
		ActRoguelike.Boss.BossPerkProbe.Report(stage);
	}

	private static void ReportCombat(StageController? stage)
	{
		EncounterController? encounter = stage?.CurrentNode;
		if (encounter == null)
		{
			GameLog.Write("SelfTest", "战斗检查失败：找不到 Encounter");
			return;
		}

		var enemies = new List<string>();
		foreach (Node node in encounter.GetTree().GetNodesInGroup("enemy_units"))
		{
			if (node is Unit unit && !unit.IsDead)
			{
				enemies.Add(unit.Name.ToString());
			}
		}

		int players = encounter.GetTree().GetNodesInGroup("player_units").Count;
		int enemiesInGroup = encounter.GetTree().GetNodesInGroup("enemy_units").Count;
		int summons = encounter.GetTree().GetNodesInGroup("summon_units").Count;
		GameLog.Write("SelfTest", $"分组：player_units={players} enemy_units={enemiesInGroup} summon_units={summons}");
		GameLog.Write("SelfTest", $"诊断：EntryCount={encounter.EntryCount} Started={encounter.Started} WaveMode={encounter.WaveModeOn} IsRunning={encounter.IsRunning} IsCompleted={encounter.IsCompleted} WaveIndex={encounter.WaveIndex} WaveRemaining={encounter.WaveRemaining} 预算={encounter.RemainingBudget}");
		GameLog.Write("SelfTest", $"战斗检查：本场 WaveCount={encounter.WaveCount}，场上存活 {enemies.Count} 只");
		GameLog.Write("SelfTest", $"敌种：{(enemies.Count == 0 ? "-" : string.Join(", ", enemies))}");

		// 单位表（阶段 3）实测：生成后的**实际值**（表值 + 全局倍率），证明行真的套到了单位身上。
		Unit? sample = null;
		foreach (Node node in encounter.GetTree().GetNodesInGroup("enemy_units"))
		{
			if (node is Unit unit && !unit.IsDead)
			{
				sample = unit;
				break;
			}
		}

		if (sample != null)
		{
			var abilities = new List<ActRoguelike.Abilities.Ability>();
			NodeSearch.CollectInSubtree(sample, abilities);
			ActRoguelike.Abilities.Ability? attacker = null;
			foreach (ActRoguelike.Abilities.Ability ability in abilities)
			{
				if (NodeSearch.FindInSubtree<AttackHitbox>(ability) != null)
				{
					attacker = ability;
					break;
				}
			}

			AttackHitbox? hit = attacker == null ? null : NodeSearch.FindInSubtree<AttackHitbox>(attacker);
			GameLog.Write("SelfTest", $"单位表实测（{sample.Name}）：生命 {sample.MaxHealth:0} 移速 {sample.MoveSpeed:0.##} 伤害 {hit?.Damage:0} 间隔 {attacker?.Cooldown:0.##} 击退 {hit?.Knockback:0.##} 标签 [{string.Join(",", sample.Tags)}] 技能 {abilities.Count} 个 技能行 Id {attacker?.AbilityId}（非类名 = 技能行真的套上了）");

			// AI 行为表（阶段 5）实测：同一个**真的生成出来的**敌人，读回它的 Brain 与行为节点 ——
			// 证明生成路径（EncounterController.Spawn → UnitConfig.ApplyTo → ApplyAiProfileTo）也走了档案。
			if (NodeSearch.FindInSubtree<ActRoguelike.AI.EnemyBrain>(sample) is { } liveBrain)
			{
				var slots = new List<string>();
				foreach (Node child in liveBrain.GetChildren())
				{
					if (child is ActRoguelike.AI.Behavior live)
					{
						slots.Add($"{live.Name}=P{live.Priority}");
					}
				}

				GameLog.Write("SelfTest", $"AI 档案实测（场上 {sample.Name}）：SearchRange {liveBrain.SearchRange:0.##} 目标刷新 {liveBrain.TargetRefreshInterval:0.##} 行为 [{string.Join(" ", slots)}]");
			}
		}
	}

	/// <summary>
	/// Pressure 段：**先验 §8.2 的免费挑战闸门，再验 §7.2–§7.5 的数值链路**。
	///
	/// 为什么这么改：闸门接上之后，"挑战数 0"就是免费挑战（额度内），结算**必须**一点都不累积 ——
	/// 所以原来那两行直接调 `AddPressureForNode` 的硬指标 `+187.5 / +225` **会变成 0**（预期内的变化）。
	/// 要让自测仍然能验证 Pressure 机制，就先走一遍真实记账把免费额度用掉
	/// （<see cref="StageController.DebugExhaustFreeQuota"/>，与 StartEncounter 同一套账：额度随挑战数扣），
	/// 之后再用**同一个公开入口** <see cref="StageController.AddPressureForNode"/> 结算 —— 闸门本身一行都没放宽。
	/// </summary>
	private void ReportPressureGate(StageController? stage)
	{
		if (stage == null)
		{
			GameLog.Write("SelfTest", "Pressure 检查失败：找不到 Stage");
			return;
		}

		// ① 闸门关着（这次结算算免费挑战）：Pressure 一点都不能累积。
		GameLog.Write("SelfTest", $"§8.2 免费挑战闸门（当前 {stage.DebugState}）：先按「免费挑战」结算一次进化节点");
		stage.AddPressureForNode(RewardNodeKind.Evolution);
		GameLog.Write("SelfTest", $"§8.2 免费挑战闸门（额度内不加 Pressure）：{stage.DebugState}（期望 challenges=0 freeQuota={stage.FreeChallengeQuota} free=True pressure=0 pmods=0 bank=0）");

		// ② 把免费额度用掉（走真实记账），闸门打开后再结算 —— 数值必须与接闸门之前逐位一致。
		int left = stage.DebugExhaustFreeQuota();
		GameLog.Write("SelfTest", $"§8.2 消耗免费额度后：{stage.DebugState}（期望 challenges={stage.FreeChallengeQuota + 1} freeQuota={left} free=False）");
		stage.AddPressureForNode(RewardNodeKind.Evolution);
		stage.AddPressureForNode(RewardNodeKind.Mutation);
		GameLog.Write("SelfTest", $"Pressure 测试（进化 150×1.25 + 异化 150×1.5）：{stage.DebugState}（期望 pressure=3 pmods=3 bank=0）");
		stage.AddPressureForNode(RewardNodeKind.Enhancement);
		GameLog.Write("SelfTest", $"Pressure 测试（再加一次中价值，应不变）：{stage.DebugState}（期望 pressure=3 pmods=3）");

		// ③ 面板文案：自测不用开界面就能断言"当前首领状况"里真的有 Pressure 与词条（用户需求 3）。
		GameLog.Write("SelfTest", $"首领状况面板（{BossPreviewScreen.Describe(stage, stage.CurrentNode).Replace("\n", " ／ ")}");

		// ④ 血条栏那一行强化（用户需求 2）：技能 + 词条合并显示、同名折叠成 ×N、计数带名词。
		//    这里场上还没有 Boss，所以技能那一侧为空、只有本关抽到的数值型词条 —— 折叠规则照常生效。
		int total = BossPerkText.Total(null, stage.PressurePerks);
		GameLog.Write("SelfTest", $"强化显示（血条栏一行，上限 5：同名折叠成 ×N、超出折叠成 …+N）：{BossPerkText.Summary(null, stage.PressurePerks)} {BossPerkText.FormatLine(null, stage.PressurePerks, 5)}（本关抽到 {total} 条，期望总数 {total} = 显示条数之和）");

		// ⑤ 两套来源**同时有**时的计数名词（「技能 N · 词条 N」）—— 这里场上还没有首领，
		//    所以拿行为型池（`bhv_*` = 界面上说的"技能"）当样本，纯文案断言、不用开界面。
		//    没有这一行的话，"技能 + 词条并存"那一条分支在自测里永远不会被走到。
		GameLog.Write("SelfTest", $"强化显示（技能 + 词条并存，计数名词 = 「技能 N · 词条 N」）：{BossPerkText.Summary(ActRoguelike.Boss.BossPerkCatalog.BehaviorOnly, stage.PressurePerks)} {BossPerkText.FormatLine(ActRoguelike.Boss.BossPerkCatalog.BehaviorOnly, stage.PressurePerks, 5)}");
	}

	/// <summary>
	/// 关卡内定期刷怪（用户需求 #6）的取证。三件事：
	/// ① 打出配置（间隔 / 每档秒数 / 每档强度 / 上限，来源 `ConfigIndex.tres` 的全局标量 `AmbientSpawn*`）；
	/// ② **真的走一遍正式入口**刷两只"游离怪"：一只在第 0 档，一只在把本关时钟拨快 120 秒之后，
	///    并排量出"生命 / 伤害被增强了多少"（这就是"每过 120 秒就增强接下来刷出的怪"的实测）；
	/// ③ 量出落点与玩家的距离 + 导航状态（= 落点复用了 `MapManager` 那一份能力，不是新造的一套）。
	///
	/// 量完立刻把这两只清掉（不给后面的阶段留残留），并顺带证明"游离怪**不进** Encounter 的预算与
	/// 清场判定"（不然节点挑战会因为"场外还有别的怪"永远结不了算）。
	/// </summary>
	private void ReportAmbientSpawns(StageController? stage)
	{
		if (stage == null)
		{
			GameLog.Write("SelfTest", "定期刷怪检查失败：找不到 Stage");
			return;
		}

		ConfigIndexData config = ConfigIndex.Settings;
		EncounterController? encounter = stage.CurrentNode;
		GameLog.Write("SelfTest", $"关卡内定期刷怪（需求 #6）：{stage.AmbientSummary}（来源：ConfigIndex.tres 全局标量 AmbientSpawn*）｜ 本关可生成项 [{encounter?.AllowedEntrySummary}]");
		string before = $"Encounter 自己的账：预算 {encounter?.RemainingBudget:0.#} / 存活 {encounter?.AliveCount} / 已生成 {encounter?.TotalSpawned}";

		Unit? first = stage.DebugSpawnAmbientNow();
		GameLog.Write("SelfTest", $"定期刷怪·第 0 档（间隔到时刷出的第一只）：{AmbientLine(first)}");
		GameLog.Write("SelfTest", $"定期刷怪·不碰 Encounter 的账：{before} → 现在 预算 {encounter?.RemainingBudget:0.#}（期望不变）/ 存活 {encounter?.AliveCount}（期望不变）/ 游离怪 {stage.AmbientAliveCount} 只");

		int step = stage.DebugAdvanceAmbientClock(config.AmbientSpawnEscalateSeconds);
		Unit? second = stage.DebugSpawnAmbientNow();
		GameLog.Write("SelfTest", $"定期刷怪·第 {step} 档（本关时钟拨快 {config.AmbientSpawnEscalateSeconds:0.#}s 之后才刷的）：{AmbientLine(second)}");
		GameLog.Write("SelfTest", stage.AmbientStrengthStep == step
			? $"定期刷怪·增强口径：每档 ×{1f + config.AmbientSpawnStrengthStep:0.##}，第 {step} 档 → 生命与伤害都 ×{1f + (config.AmbientSpawnStrengthStep * step):0.##}（只影响**之后**刷出的怪，已刷出的不动）"
			: "定期刷怪·增强口径异常：档位没跟上（期望和刷怪时用的档位一致）");

		first?.ApplyDirectDamage(99999f, null);
		second?.ApplyDirectDamage(99999f, null);
		GameLog.Write("SelfTest", $"定期刷怪·清理：两只已击杀 → 场上游离怪 {stage.AmbientAliveCount} 只（期望 0，不给后面的阶段留残留）");
	}

	/// <summary>一只游离怪的一行读数（生命 / 主攻击伤害 / 落点 / 离玩家多远 / 导航是否就绪）。</summary>
	private static string AmbientLine(Unit? unit)
	{
		if (unit == null || !IsInstanceValid(unit))
		{
			return "没刷出来（期望刷出一只）";
		}

		var abilities = new List<ActRoguelike.Abilities.Ability>();
		NodeSearch.CollectInSubtree(unit, abilities);
		AttackHitbox? hit = null;
		foreach (ActRoguelike.Abilities.Ability ability in abilities)
		{
			hit = NodeSearch.FindInSubtree<AttackHitbox>(ability);
			if (hit != null)
			{
				break;
			}
		}

		Unit? player = PlayerLocator.Player;
		Vector3 position = unit.GlobalPosition;
		float distance = player == null ? -1f : position.DistanceTo(player.GlobalPosition);
		bool navReady = ActRoguelike.World.MapManager.Instance is { IsReady: true };

		// 生命/伤害的**倍率账**：全局难度（敌种 0.75 / 0.3）+ 本关缩放 + 强度档位都在这两个簿记里，
		// 所以"每 120 秒增强一档"这件事在**伤害**那一侧也能被量出来（只看命中体的 Damage 是基础值，量不出倍率）。
		float healthScale = unit.Stats.Get("MaxHealthMultiplier");
		float damageScale = unit.Stats.Get("DamageMultiplier");
		return $"{unit.Name} 生命 {unit.MaxHealth:0.#}（倍率账 生命×{healthScale:0.####} / 伤害×{damageScale:0.####}）伤害 {hit?.Damage:0.#} 落点 {position}（离玩家 {distance:0.#}m，导航网格就绪={navReady}）";
	}

	// === 命中特效位置探针（用户要求 5：「实际应该在命中的位置发生特效」）===
	// 真打一次近战：把玩家身上那个近战命中体挪到"贴着敌人、但中心在敌人体外"的位置再激活 ——
	// 于是走的是**与正式战斗完全相同**的一条路（重叠判定 → ReceiveHit → HitEffect.Spawn）。
	// 探针只做两件事：① 结算时回读**场景里刚出生的那个特效节点**（不是重算一遍公式）；
	// ② 把它与"旧做法的基线"（目标原点 + 全局高度偏移，本次已删除）并排量出来。
	// ⚠️ 命中前把目标**临时冻住**（`ProcessMode.Disabled`）：否则敌人 AI 会在"命中"与"结算"之间跑掉，
	// 量出来的距离里会混进它自己的位移，看起来像"特效位置不对"（那是假象，不是 bug）。
	private readonly HashSet<ulong> _fxBefore = new();
	private Unit? _fxTarget;
	private AttackHitbox? _fxHitbox;
	private Transform3D _fxHitboxHome;
	private Vector3 _fxHitboxOrigin;
	private Vector3 _fxBodyCenter;
	private float _fxTargetRadius;
	private ProcessModeEnum _fxTargetProcessMode = ProcessModeEnum.Inherit;

	/// <summary>命中特效探针·准备（在 <c>ReportCombat</c> 之后调用；结算在下一个阶段）。</summary>
	private void BeginHitEffectProbe()
	{
		_fxTarget = null;
		_fxHitbox = null;
		_fxBefore.Clear();

		Node? scene = GetTree().CurrentScene;
		Unit? player = PlayerLocator.Player;
		if (scene == null || player == null)
		{
			GameLog.Write("SelfTest", "命中特效探针：找不到当前场景或玩家，跳过。");
			return;
		}

		foreach (Node child in scene.GetChildren())
		{
			_fxBefore.Add(child.GetInstanceId());
		}

		AttackHitbox? hitbox = player.GetNodeOrNull<ActRoguelike.Abilities.Ability>("AbilityMelee") is { } melee
			? NodeSearch.FindInSubtree<AttackHitbox>(melee)
			: null;

		// 挑**血最少**的那一只：这样大概率会打出一击致死，正好覆盖"死亡会把 CollisionLayer 归零、
		// 之后任何射线都找不到它"这条最容易踩的边界（命中点必须在结算之前取，见 `AttackHitbox`）。
		Unit? enemy = null;
		foreach (Node node in GetTree().GetNodesInGroup("enemy_units"))
		{
			if (node is Unit candidate && !candidate.IsDead && (enemy == null || candidate.Health < enemy.Health))
			{
				enemy = candidate;
			}
		}

		if (hitbox == null || enemy == null)
		{
			GameLog.Write("SelfTest", $"命中特效探针：找不到玩家近战命中体（{hitbox != null}）或场上没有活着的敌人（{enemy != null}），跳过。");
			return;
		}

		_fxHitbox = hitbox;
		_fxHitboxHome = hitbox.GlobalTransform;
		_fxTarget = enemy;
		_fxTargetProcessMode = enemy.ProcessMode;
		enemy.ProcessMode = ProcessModeEnum.Disabled; // 冻住：命中与结算之间它不能动（结算时还原）
		_fxBodyCenter = BodyCenterOf(enemy);
		_fxTargetRadius = BodyRadiusOf(enemy);

		// ⚠️ **刻意不挪目标**：实测（2026-10，本探针第四轮）把 `CharacterBody3D` 直接 `GlobalPosition += 10m`
		// 之后，脚本这边位置变了、**物理空间里的位置没变**（同一帧同参数的射线直接"打空"），
		// 于是探针量到的是"脚本位置 vs 物理位置"的错位，不是命中点算法的问题。
		// 真实战斗里目标都是自己走过来的（物理位置可信），所以探针也不挪它 —— 只挪**命中体**（Area3D，
		// 实测它的位移对射线是即时可见的，第一轮就是这么量出"落在碰撞体表面"的）。
		// 人堆里挡在前面的怪由 `AttackHitbox.ResolveHitPoint` 逐个排除后继续往前打（不会退化成兜底点）。

		// 命中体中心 = 目标正东 1.0m：命中体盒子 2.6×1.8×2.6、半长 1.3 → 敌人在盒**内**（重叠成立），
		// 而命中体中心在敌人体**外** → 射线必然"从外面打到表面上"，正是要测的那条路。
		hitbox.GlobalPosition = enemy.GlobalPosition + new Vector3(1f, 0f, 0f);
		_fxHitboxOrigin = hitbox.GlobalPosition;
		hitbox.Activate(player);

		GameLog.Write("SelfTest",
			$"命中特效探针·准备：玩家近战命中体已挪到 {enemy.Name} 正东 1.0m 并激活 ｜ 命中体原点 {Fmt(_fxHitboxOrigin)} ｜ 目标身体中心 {Fmt(_fxBodyCenter)}（= 它 `Body` 碰撞体自己的位置）· 碰撞体半径 {_fxTargetRadius:0.###} ｜ 目标已临时冻结（结算时还原）");
	}

	/// <summary>
	/// 命中特效探针·结算。两路证据并排给出：
	/// ① **正式代码写下的出生点**（`HitEffect.RecentSpawns`，DEBUG 才有）= 结论；
	/// ② 同时刻场景里新出生的节点 = 旁证（人堆里一次挥砍会同时打中好几只，所以这一路只用来对照）。
	/// </summary>
	private void ReportHitEffectProbe()
	{
		AttackHitbox? hitbox = _fxHitbox;
		Unit? target = _fxTarget;
		if (hitbox != null)
		{
			hitbox.Deactivate();
			hitbox.GlobalTransform = _fxHitboxHome; // 还原：探针不该改变这一局的近战命中体位置
		}

		if (target == null || !IsInstanceValid(target))
		{
			return;
		}

		target.ProcessMode = _fxTargetProcessMode; // 解冻

		// ② 旁证：`HitEffect.Spawn` 把特效挂在 `CurrentScene` 下，所以"新出生的直接子节点"里就有它。
		var born = new List<string>();
		Node? scene = GetTree().CurrentScene;
		if (scene != null)
		{
			foreach (Node child in scene.GetChildren())
			{
				if (_fxBefore.Contains(child.GetInstanceId()) || child is not Node3D node)
				{
					continue;
				}

				born.Add($"「{child.Name}」({CountParticles(node)} 粒子) 距身体中心 {node.GlobalPosition.DistanceTo(_fxBodyCenter):0.##}");
			}
		}

		// ① 结论：正式代码最近 8 次出生点里**离目标身体中心最近**的那一个（= 这一只怪的特效）。
		Vector3? spawn = null;
		float nearestSpawn = float.MaxValue;
		foreach (Vector3 candidate in HitEffect.RecentSpawns)
		{
			float distance = candidate.DistanceTo(_fxBodyCenter);
			if (distance < nearestSpawn)
			{
				nearestSpawn = distance;
				spawn = candidate;
			}
		}

		if (spawn is not { } point)
		{
			GameLog.Write("SelfTest", $"命中特效位置实测：`HitEffect` 一次都没记录到出生点（场景里新出生节点 {born.Count} 个：[{string.Join(" · ", born)}]）—— 没打中或开关关着，跳过。");
			return;
		}

		// 判据（**与命中点算法解耦**）：这个点离目标碰撞体**表面**有多远。
		// 落在表面上 → 偏差 ≈ 0；落进身体里 → 偏差 = 半径量级。旧做法（目标原点 + 高度 1.0）
		// 正好落在中轴上，所以它的偏差 ≈ 半径 —— 一眼就能看出两种做法的差别。
		float deviation = SurfaceDeviation(target, point);
		float oldDeviation = SurfaceDeviation(target, target.GlobalPosition + (Vector3.Up * 1f));
		float centerHeight = _fxBodyCenter.Y - target.GlobalPosition.Y;
		GameLog.Write("SelfTest",
			$"命中特效位置实测（用户要求 5：特效出现在**实际命中的位置**）：正式代码写下的出生点 {Fmt(point)}（`HitEffect.RecentSpawns` 最近 {HitEffect.RecentSpawns.Count} 次里离目标最近的一个）｜ 到 {target.Name} 碰撞体**表面**的偏差 {deviation:0.###} 米（半径 {_fxTargetRadius:0.###} → 落在表面上；偏差 ≈ 半径 才叫「掉进身体里」）· 离地 {point.Y - target.GlobalPosition.Y:0.###}（身体中心离地 {centerHeight:0.###}）｜ 对照·旧做法基线（目标原点 + 高度 1.0）到表面偏差 {oldDeviation:0.###} 米（= 掉在身体里、离地写死 1.0）｜ 目标这次被打死了={target.IsDead} ｜ 旁证·同批新出生节点 {born.Count} 个：[{string.Join(" · ", born)}]");
	}

	/// <summary>
	/// 点到目标碰撞体**表面**的距离（探针的判据，故意不复用 `AttackHitbox` 里那套"造点"的代码）：
	/// 胶囊 = |到中轴段线的距离 − 半径|；球 = |到球心的距离 − 半径|；盒 = 到最近盒面的距离。认不出的形状返回 0。
	/// </summary>
	private static float SurfaceDeviation(Unit unit, Vector3 point)
	{
		foreach (Node child in unit.GetChildren())
		{
			if (child is not CollisionShape3D { Disabled: false } shapeNode || shapeNode.Shape == null)
			{
				continue;
			}

			Transform3D shape = shapeNode.GlobalTransform;
			float scale = shape.Basis.Y.Length();
			switch (shapeNode.Shape)
			{
				case CapsuleShape3D capsule:
				{
					Vector3 axis = shape.Basis.Y.Normalized();
					float radius = capsule.Radius * scale;
					float half = Mathf.Max(0f, (capsule.Height * 0.5f) - capsule.Radius) * scale;
					Vector3 center = shape.Origin;
					Vector3 projected = center + (axis * Mathf.Clamp((point - center).Dot(axis), -half, half));
					return Mathf.Abs(point.DistanceTo(projected) - radius);
				}

				case SphereShape3D sphere:
					return Mathf.Abs(point.DistanceTo(shape.Origin) - (sphere.Radius * scale));

				case BoxShape3D box:
				{
					Vector3 local = shape.AffineInverse() * point;
					Vector3 half = box.Size * 0.5f;
					Vector3 delta = new(
						Mathf.Max(0f, Mathf.Abs(local.X) - half.X),
						Mathf.Max(0f, Mathf.Abs(local.Y) - half.Y),
						Mathf.Max(0f, Mathf.Abs(local.Z) - half.Z));
					return delta.Length() * scale;
				}
			}
		}

		return 0f;
	}

	/// <summary>子树里的粒子节点数（用来确认"新出生的那个节点"确实是那份命中特效，而不是别人）。</summary>
	private static int CountParticles(Node node)
	{
		int count = node is GpuParticles3D ? 1 : 0;
		foreach (Node child in node.GetChildren())
		{
			count += CountParticles(child);
		}

		return count;
	}

	private static CollisionShape3D? BodyShapeOf(Unit unit)
	{
		foreach (Node child in unit.GetChildren())
		{
			if (child is CollisionShape3D shape && !shape.Disabled)
			{
				return shape;
			}
		}

		return null;
	}

	private static Vector3 BodyCenterOf(Unit unit) => BodyShapeOf(unit)?.GlobalPosition ?? unit.GlobalPosition;

	private static float BodyRadiusOf(Unit unit)
		=> BodyShapeOf(unit)?.Shape is CapsuleShape3D capsule ? capsule.Radius : 0f;

	private static string Fmt(Vector3 v) => $"({v.X:0.##}, {v.Y:0.##}, {v.Z:0.##})";

	/// <summary>当前角色（供验证读取）。</summary>
	public static bool Active { get; private set; }
#endif
}




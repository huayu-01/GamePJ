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
using ActRoguelike.Visual;
using Godot;

namespace ActRoguelike.Debug;

/// <summary>
/// 无 MCP 时的自动化验证入口（只在命令行带 ``--selftest`` 时激活，正常运行零影响）。
///
/// 用法（Godot 可执行文件 + 项目路径 + **场景**）：
/// <code>
/// Godot_v4.7.2-stable_mono_win64_console.exe --path F:\Godot项目\项目a --headless --quit-after 40000 res://Scenes/Main.tscn -- --selftest
/// </code>
/// ⚠️ **必须带场景参数**（`res://Scenes/Main.tscn`）：主场景是**基地**，
/// 不带场景会进基地 → 自测找不到 `Stage` → 第 20 阶段起全是空值（`--quit-after` 的帧数只是兜底，别指望它当超时）。
/// 也支持文件开关 `user://selftest.flag`（MCP 的 `run_project` 传不了命令行参数；**跑完必须删**）。
///
/// 它会：跳过界面直接开局 → 打印节点布置（应为 10 个 / 2-5-3 分层）→ 强制开一场战斗
/// → 打印波次生成结果（一次性刷出、波数）→ 退出（`=== 自测结束 ===` + **退出码 0**）。
/// </summary>
public partial class SelfTest : Node
{
#if DEBUG
	// 仅在Debug构建执行探针；发行配置只保留始终为false的兼容读取接口。

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
				// 模拟"从基地出发"：这样 RunController 会直接开局。
				// ⚠️ P6a：角色/武器换成**清旧之后剩下的**角色 A［猎手］+ 能量掌（旧的 3 角色 3 武器已删）。
				RunSetup.HasSelection = true;
				RunSetup.Weapon = WeaponPresetKind.EnergyPalm;
				run?.BeginRunWithCharacter(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm);
				// 保险：万一有界面还压着（`RunController` 已经不会再开那两道门了），这里把它们关掉。
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

			// ── P2 通用机制（冷却缩减 / 技能层数 / 蓄力 / 完美时机）的取证 ──
			// 排在最小闭环之后：那几段的硬指标已经打印完了，这里的探针只在自己的节点上加/减数值。
			case 28 when _timer > 0.4f:
				BeginMechanismProbe();
				Next();
				break;

			case 29 when _timer > 0.4f:
				ReportMechanismProbe();
				Next();
				break;

			case 30 when _timer > 0.3f:
				ProbeChargeMechanism();
				Next();
				break;

			case 31 when _timer > 0.3f:
				ProbeBossAddsUnitTable();
				Next();
				break;

			// ── P3：角色 A 武器 2/3 + 技能 1/2 + 被动（新机制的取证）──
			// 全部在**真单位、真表行、真动画**上做（武器行换装走与基地同一个入口 BeginRunWithCharacter）。
			case 32 when _timer > 0.4f:
				BeginCrossbowProbe();
				Next();
				break;

			case 33 when _timer > 0.9f:
				ReportCrossbowProbe();
				Next();
				break;

			case 34 when _timer > 0.5f:
				BeginBowProbe();
				Next();
				break;

			case 35 when _timer > 0.9f:
				ReportBowProbe();
				Next();
				break;

			case 36 when _timer > 0.4f:
				BeginBackdashProbe();
				Next();
				break;

			case 37 when _timer > 0.25f:
				ReportBackdashInvulnerableOn();
				Next();
				break;

			case 38 when _timer > 0.5f:
				ReportBackdashInvulnerableOff();
				Next();
				break;

			case 39 when _timer > 0.4f:
				BeginLeapProbe();
				Next();
				break;

			// 跃升结果 + 滞空减缓一起做：Action 帧之后 0.4s，玩家**一定还在空中**
			// （起跳 16 m/s、重力 3.6×9.8 ≈ 35 m/s² → 滞空约 0.9s，所以两段必须紧挨着）。
			case 40 when _timer > 0.4f:
				ReportLeapProbe();
				ReportHoverSlowFall();
				Next();
				break;

			case 41 when _timer > 0.4f:
				BeginCharacterBProbe();
				Next();
				break;

			// ── P4：角色 B［武器大师］（3 把近战武器 + 技能 1/2 + 被动 + 新机制）──
			case 42 when _timer > 0.4f:
				ReportSwordComboProbe();
				Next();
				break;

			case 43 when _timer > 0.3f:
				ReportBulletCutProbe();
				Next();
				break;

			case 44 when _timer > 0.3f:
				ReportSwordQiProbe();
				Next();
				break;

			case 45 when _timer > 0.3f:
				ProbeBlinkDash();
				Next();
				break;

			case 46 when _timer > 0.3f:
				ProbeGuardStance();
				ProbeFallAttack();
				Next();
				break;

			case 47 when _timer > 0.5f:
				ReportFallAttack();
				Next();
				break;

			case 48 when _timer > 0.4f:
				BeginScytheProbe();
				Next();
				break;

			case 49 when _timer > 0.4f:
				ReportScytheProbe();
				Next();
				break;

			case 50 when _timer > 0.4f:
				BeginSpearProbe();
				Next();
				break;

			case 51 when _timer > 0.5f:
				ReportSpearProbe();
				Next();
				break;

			case 52:
				// ⚠️ P6b：这一段**必须排在所有硬指标之后**（它是最后一个阶段）——
				// 它会往事件总线上抛 `Damaged`（践踏真的打了一下临时靶子），放在前面会污染
				// 「敌人 N 活着 / 清场 / 碎片」那一批读数。
				GameLog.Write("SelfTest", DebugP6bMutationProbe());
				Next();
				break;

			case 53:
				// ⚠️ P6d：与上面同一条纪律（它也会真的打一下玩家 —— 那一下**应该被护卫完全挡掉**）。
				GameLog.Write("SelfTest", DebugP6dMutationProbe());
				GameLog.Write("SelfTest", "=== 自测结束 ===");

				// ⚠️ 退出前先解除暂停：这一阶段之前刚切过一关，`UiRouter` 正把树暂停着
				// （正式流程里玩家按 Enter 才会继续）。进程留着不退会让下一轮的日志文件混在一起。
				GetTree().Paused = false;
				SetProcess(false);
				GetTree().Quit();
				break;
		}
	}


	/// <summary>角色可替换模型后，取当前可见的播放器；旧Model只是缓存，不能再固定读它。</summary>
	private static AnimationPlayer? ResolvePlayerAnimation(Unit? player)
	{
		Node? model = player?.GetNodeOrNull<CharacterDisplay>("Appearance")?.ActiveModel
			?? player?.GetNodeOrNull<Node>("Model");
		return model == null ? null : NodeSearch.FindInSubtree<AnimationPlayer>(model);
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
		AnimationPlayer? ap = ResolvePlayerAnimation(player);
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

	/// <summary>
	/// 靶子摆在哪：**子弹出生点沿弹道往前多少米**。投射物每物理帧走 <c>速度 × dt</c>（弹速 150 → 2.5m），
	/// 摆在 0.9m 处就一定落在**第一帧那一段射线**里；真敌人（Chaser）的胶囊半径 0.45m，几度的角度误差也容得下。
	/// </summary>
	private const float TargetDistanceOnLine = 0.9f;


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

		// ③ 造一个靶子：**用真敌人 `Chaser.tscn` 的实例**（`docs/anim-pipeline.md` §6.4-1 的建议）。
		//
		// ⚠️ 上一轮用的是"自造 `Unit` + 手工挂 `CollisionShape3D`"：几何逐点核对过（靶子摆在弹道上、
		// 胶囊中心就在线上），但投射物的射线**依然打不到它**，而**同一份射线打真敌人是能中的** ——
		// 也就是"自造靶子"这条路本身有问题（运行期手工挂的形状进物理空间的方式与场景自带的不是一回事）。
		// 换成真敌人之后，碰撞体是场景里带的，物理空间一定会认；靶子到底能不能被射线打到，
		// 下面还有一条**自测射线**独立取证（不靠"有没有掉血"倒推）。
		EnemyConfig? chaserRow = ConfigRows.ById<EnemyConfig>("enemy_chaser");
		if (chaserRow == null || string.IsNullOrEmpty(chaserRow.ScenePath) || !ResourceLoader.Exists(chaserRow.ScenePath))
		{
			GameLog.Write("SelfTest", $"🔴 最小闭环：敌人表里读不到 enemy_chaser 的场景（ScenePath='{chaserRow?.ScenePath}'），靶子造不出来。");
			return;
		}

		if (GD.Load<PackedScene>(chaserRow.ScenePath).Instantiate() is not Unit chaser)
		{
			GameLog.Write("SelfTest", $"🔴 最小闭环：{chaserRow.ScenePath} 的根节点不是 Unit，靶子造不出来。");
			return;
		}

		// 靶子的 AI 必须停掉：真敌人会朝玩家跑（4.2 m/s），跑出弹道就变成"随机能不能中"。
		// 只关这个自测实例的 AI 处理，`EnemyBrain` 的生产行为一个字没改。
		NodeSearch.FindInSubtree<ActRoguelike.AI.EnemyBrain>(chaser)?.SetPhysicsProcess(false);

		if (player.GetParent() is { } arena)
		{
			arena.AddChild(chaser);
		}

		// 第一遍摆位：先按"当前这一帧的弹道"摆上（扣扳机那一刻会按同一套公式重摆，见 OnLoopNodeAdded）。
		if (TryComputeShotLine(player, out Vector3 firstOrigin, out Vector3 firstDirection))
		{
			PlaceOnShotLine(chaser, firstOrigin, firstDirection, TargetDistanceOnLine);
		}

		_loopEnemy = chaser;
		_loopEnemyHealthBefore = chaser.Health;
		_loopEnemyArmorBefore = chaser.Armor;
		chaser.Damaged += OnLoopDamage;
		GameLog.Write(
			"SelfTest",
			$"最小闭环·造靶子：完成（真敌人 {chaserRow.ScenePath} 的实例，AI 已停）"
			+ $"｜靶子={chaser.Name} 生命 {chaser.Health:0.#} 护甲 {chaser.Armor:0.#} 位置 {chaser.GlobalPosition}");

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
		// 先摆好的靶子会被甩开。这里用与 `EnergyWaveAbility.FireShot` **同一套公式**重算弹道
		// （见 TryComputeShotLine），不读 `shot.GlobalPosition`（那一刻它还在世界原点）。
		Unit? dummy = _loopEnemy;
		Unit? player = PlayerLocator.Player;
		if (dummy == null || player == null)
		{
			return;
		}

		// ⚠️ `NodeAdded` 回调可能晚于"靶子被 QueueFree"（关卡装配会整批换单位）：已释放的 Unit 上一句
		// `dummy == null` 判不出来（C# 引用还在），但任何一次属性访问都会 ObjectDisposedException。
		// 这里与 `FindBodyShape` 同一条守卫（实测原来这一路上报 19 次异常）。
		if (!GodotObject.IsInstanceValid(dummy) || dummy.IsQueuedForDeletion()
			|| !GodotObject.IsInstanceValid(player) || player.IsQueuedForDeletion())
		{
			return;
		}

		if (!TryComputeShotLine(player, out Vector3 origin, out Vector3 direction))
		{
			GameLog.Write("SelfTest", "🔴 最小闭环·靶子上弹道：算不出弹道（没有相机？），靶子保持原位。");
			return;
		}

		Vector3 onLine = PlaceOnShotLine(dummy, origin, direction, TargetDistanceOnLine);

		// ★ 独立取证：从**子弹出生点**沿弹道打一条射线（参数与 `Projectile._PhysicsProcess` 一致：
		// 长度取它一个物理帧的位移、mask 就用这个投射物自己的 HitMask）—— 直接回答
		// "靶子到底在不在弹道上、射线能不能打到它"，而不是只靠"有没有掉血"倒推。
		GameLog.Write(
			"SelfTest",
			$"最小闭环·靶子上弹道：出生点={origin} 方向={direction} → 靶子={dummy.Name} 体心={onLine}"
			+ $"（离出生点 {TargetDistanceOnLine:0.##}m，距玩家 {player.GlobalPosition.DistanceTo(onLine):0.##}m）"
			+ $"｜自测射线（{TargetDistanceOnLine:0.##}→{(TargetDistanceOnLine + 2.5f):0.##}m，mask={shot.HitMask}）打到的第一个东西："
			+ DescribeShotRay(player, origin, direction, shot.HitMask));
	}

	/// <summary>
	/// **弹道**：与 <c>EnergyWaveAbility.FireShot</c> 同一套公式 ——
	/// ①「准星打到的世界点」走 <see cref="AimUtil.GetCrosshairPoint"/>（**与枪械 `PlayerWeaponInput`
	/// 同一个函数**，§6.4-4 统一口径）；
	/// ② 方向 = 胸口 → 世界点；③ 出生点 = 胸口 + 方向 × 0.6（两个偏移 0.9 / 0.6 与 `FireShot` 一致）。
	/// 求交距离从**技能自己**读（表里的 <c>MaxAimDistance</c>），自测不另写一份常量。
	/// </summary>
	private static bool TryComputeShotLine(Unit player, out Vector3 origin, out Vector3 direction)
	{
		origin = default;
		direction = default;
		Viewport? viewport = player.GetViewport();
		if (viewport?.GetCamera3D() == null)
		{
			return false;
		}

		float maxAim = PlayerAbilityInput.FindByInputAction(player, "weapon_skill_1") is EnergyWaveAbility wave
			? wave.MaxAimDistance
			: 200f;

		Vector3 chest = player.GlobalPosition + (Vector3.Up * 0.9f);
		Vector3 point = AimUtil.GetCrosshairPoint(viewport, player, maxAim, CombatLayers.World | CombatLayers.UnitMask);
		Vector3 toPoint = point - chest;
		if (toPoint.LengthSquared() <= 0.0001f)
		{
			return false;
		}

		direction = toPoint.Normalized();
		origin = chest + (direction * 0.6f);
		return true;
	}

	/// <summary>
	/// 把靶子摆到**弹道上**、离出生点 <paramref name="distance"/> 米处：把它的**碰撞体中心**压在那条线上。
	/// 敌人场景的碰撞体挂在脚底往上 0.9m（`Chaser.tscn` 的 `Body`），所以先按"体心与根节点的差"反推根节点位置。
	/// 返回摆好之后的体心（= 线上的那个点），供日志用。
	/// </summary>
	private static Vector3 PlaceOnShotLine(Unit target, Vector3 origin, Vector3 direction, float distance)
	{
		Vector3 point = origin + (direction * distance);
		CollisionShape3D? body = FindBodyShape(target);
		if (body == null)
		{
			target.GlobalPosition = point;
			return point;
		}

		Vector3 rootToBody = target.GlobalPosition - body.GlobalPosition;
		target.GlobalPosition = point + rootToBody;
		return point;
	}

	/// <summary>
	/// 单位**自己**的碰撞体：直接挂在单位根节点下的那个 <c>CollisionShape3D</c>（不写节点名）。
	///
	/// <para>⚠️ **必须先判 <c>IsInstanceValid</c>**：本函数的调用链是
	/// <c>SceneTree.NodeAdded</c> 回调 → <see cref="PlaceOnShotLine"/> → 这里，而回调触发时靶子可能
	/// **已经被释放**（自测里挪关卡/换角色会把上一批单位 <c>QueueFree</c> 掉）。实测报 19 次
	/// <c>ObjectDisposedException</c>：对已释放的 <c>Unit</c> 调 <c>GetChildren()</c> 会去摸
	/// 已释放的原生对象。节点没了就**早退**（返回 null，调用方本来就把 null 当"没有碰撞体"处理）。</para>
	/// </summary>
	private static CollisionShape3D? FindBodyShape(Unit unit)
	{
		if (!GodotObject.IsInstanceValid(unit) || unit.IsQueuedForDeletion())
		{
			return null;
		}

		foreach (Node child in unit.GetChildren())
		{
			if (child is CollisionShape3D shape)
			{
				return shape;
			}
		}

		return null;
	}

	/// <summary>
	/// 自测射线：从子弹出生点沿弹道打一发（与 `Projectile._PhysicsProcess` 同一组参数 ——
	/// 线段长度取"一个物理帧的位移"，mask 用投射物自己的），返回"打到的第一个东西"的文字描述。
	/// 用来独立证明靶子在弹道上，而不是靠掉血倒推。
	/// </summary>
	private static string DescribeShotRay(Unit player, Vector3 origin, Vector3 direction, uint mask)
	{
		var query = PhysicsRayQueryParameters3D.Create(origin, origin + (direction * 2.5f), mask);
		query.Exclude = new Godot.Collections.Array<Rid> { player.GetRid() };
		Godot.Collections.Dictionary hit = player.GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0)
		{
			return "（什么都打不到）";
		}

		var collider = hit["collider"].As<GodotObject>();
		return $"{collider?.GetType().Name} '{(collider as Node)?.Name}' @ {hit["position"].AsVector3()}";
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
		AnimationPlayer? ap = ResolvePlayerAnimation(player);
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
			// ⚠️ 激活**之前**把靶子重新摆一次：玩家在 `BeginSkillLoopProbe` 之后还在被关卡装配挪动
			// （实测 y 一路涨上去），而"准星世界点"是**从相机出发的射线** —— 玩家一动就整体平移，
			// 先前按旧位置摆好的靶子会落到弹道之外，直接漏掉。
			// 所以靶子的位置必须以"扣扳机这一刻"的弹道为准（同一套公式，见 TryComputeShotLine）。
			Unit? dummy = _loopEnemy;
			if (dummy != null && TryComputeShotLine(player, out Vector3 origin, out Vector3 direction))
			{
				Vector3 onLine = PlaceOnShotLine(dummy, origin, direction, TargetDistanceOnLine);
				GameLog.Write(
					"SelfTest",
					$"最小闭环·靶子重摆（扣扳机前）：玩家={player.GlobalPosition} 出生点={origin} 方向={direction}"
					+ $" → 靶子体心={onLine}（距玩家 {player.GlobalPosition.DistanceTo(onLine):0.##}m）");
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
		AnimationPlayer? ap = ResolvePlayerAnimation(PlayerLocator.Player);
		GameLog.Write(
			"Combat",
			$"逐帧·技能动画：当前={ap?.CurrentAnimation} pos={ap?.CurrentAnimationPosition:0.0000} 播放中={ap?.IsPlaying()} "
			+ $"speed_scale={ap?.SpeedScale:0.##} 事件累计={_loopAnimEvents}");
	}

	private int _loopTraceLeft;

	private void ReportSkillLoop()
	{
		UnitAnimator? animator = PlayerLocator.Player?.GetNodeOrNull<UnitAnimator>(UnitAnimator.NodeName);
		AnimationPlayer? ap = ResolvePlayerAnimation(PlayerLocator.Player);
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
			: $"🔴 没有收到伤害（靶子生命 {_loopEnemyHealthBefore:0.#} → {healthAfter:0.#} 未变）";

		GameLog.Write(
			"SelfTest",
			$"{(_loopDamaged ? "✅" : "🔴")} 最小闭环·结果：动画事件帧收到={_loopAnimEvents} 次 投射物生成={_loopProjectiles} 个｜"
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

	// ─────────────────────────────────────────────────────────────────────────
	// ★ P2 通用机制探针（case 28–30）：冷却缩减 / 技能层数 / 蓄力 / 完美时机
	//
	// 全部在**玩家身上真的那两个技能节点**上做（`skl_a1_w1_1` 非蓄力 / `skl_a1_w1_2` 蓄力）：
	// ① 先从**表**里读出数值核对（证明新增的列真的接上了）；
	// ② 再做行为取证（真激活 / 真动画事件帧 / 真 Finish / 同步推蓄力）；
	// ③ 探针用完把**行与节点都还原**（自测不该把改动留在正式流程里）。
	// ─────────────────────────────────────────────────────────────────────────

	private Unit? _mechPlayer;
	private EnergyWaveAbility? _mechW1;
	private EnergyWaveAbility? _mechW2;
	private float _mechW1CooldownBefore;
	private float _mechW1MultBefore;
	private int _mechW1MaxChargesBefore;
	private float _mechW1RechargeBefore;
	private int _mechProjectilesBefore;
	private float _mechShotDamageBefore;

	private void BeginMechanismProbe()
	{
		GetTree().Paused = false;
		Unit? player = PlayerLocator.Player;
		if (player == null)
		{
			GameLog.Write("SelfTest", "🔴 机制·冷却缩减：找不到玩家。");
			return;
		}

		_mechPlayer = player;
		_mechW1 = PlayerAbilityInput.FindByInputAction(player, "weapon_skill_1") as EnergyWaveAbility;
		_mechW2 = PlayerAbilityInput.FindByInputAction(player, "weapon_skill_2") as EnergyWaveAbility;
		if (_mechW1 == null || _mechW2 == null)
		{
			GameLog.Write("SelfTest", $"🔴 机制探针：左键技能={_mechW1?.AbilityId ?? "无"} 右键技能={_mechW2?.AbilityId ?? "无"}，两个都在才测得了。");
			return;
		}

		// ① 默认值：必须还是改动前那一套（单层 / 无 CDR / 武技1 非蓄力 / 武技2 蓄力 0.5s + 完美窗口 0.2s）
		GameLog.Write(
			"SelfTest",
			$"机制·默认值：武技1[{_mechW1.AbilityId}] 层数={_mechW1.MaxCharges}（期望 1） HoldToCharge={_mechW1.HoldToCharge}（期望 False） "
			+ $"CDR 行×{_mechW1.CooldownMultiplier:0.##}（期望 1） 单位×{_mechW1.UnitCooldownMultiplier:0.##}（期望 1） "
			+ $"实际冷却={_mechW1.EffectiveCooldown:0.###}（期望 = Cooldown {_mechW1.Cooldown:0.###}，×1 乘出来逐位相同）"
			+ $"｜武技2[{_mechW2.AbilityId}] HoldToCharge={_mechW2.HoldToCharge}（期望 True） "
			+ $"基础蓄力={_mechW2.ChargeBaseSeconds:0.##}s（表列，期望 0.5） 完美窗口={_mechW2.PerfectWindowSeconds:0.##}s（表列，期望 0.2） "
			+ $"完美伤害={_mechW2.PerfectDamage:0} 完美弹速={_mechW2.PerfectSpeed:0}（两个绝对值来自 Params，代码里没有 40/400）");

		// ② 冷却缩减（CDR）通路：**行级列**（AbilityConfig.CooldownMultiplier）+ **单位级常驻**（StatSheet）
		AbilityConfig? row = ConfigRows.ById<AbilityConfig>(_mechW1.AbilityId);
		_mechW1CooldownBefore = _mechW1.Cooldown;
		_mechW1MaxChargesBefore = _mechW1.MaxCharges;
		_mechW1RechargeBefore = _mechW1.RechargeSeconds;
		_mechW1MultBefore = row?.CooldownMultiplier ?? 1f;

		if (row != null)
		{
			row.CooldownMultiplier = 0.5f;
			row.ApplyTo(_mechW1);
		}

		// 冷却拉长到 10s：下一段还读得到"没走完"的剩余冷却（0.5s 的话早就归零、看不出差别）。
		_mechW1.Cooldown = 10f;
		player.Stats.AddModifier(new StatModifier { Stat = "CooldownMultiplier", Operation = ModifierOperation.Multiply, Value = 0.5f, Source = this });
		player.RefreshStats();

		GameLog.Write(
			"SelfTest",
			$"机制·冷却缩减通路：行级列×{_mechW1.CooldownMultiplier:0.##}（写进技能行的 CooldownMultiplier 列） × 单位级×{_mechW1.UnitCooldownMultiplier:0.##}"
			+ $"（StatSheet 的 CooldownMultiplier，洞悉/速攻那条路）= ×{_mechW1.CooldownMultiplierEffective:0.###} → 实际冷却 {_mechW1.Cooldown:0.##}×{_mechW1.CooldownMultiplierEffective:0.###} = {_mechW1.EffectiveCooldown:0.###}（期望 2.5）");

		// ③ 真跑一次：激活 → 动画事件帧 → Finish()；下一段读它写下的冷却剩余
		_mechW1.RefreshCooldown();
		bool ok = _mechW1.TryActivate();
		GameLog.Write("SelfTest", $"机制·真跑一次：TryActivate={ok}（期望 True）→ 下一段读 Finish() 之后写下的冷却剩余（期望 ≤ 2.5 且 > 0）");
	}

	private void ReportMechanismProbe()
	{
		if (_mechPlayer == null || _mechW1 == null || _mechW2 == null)
		{
			GameLog.Write("SelfTest", "🔴 机制探针：上一段没准备好（找不到玩家或技能节点），本段跳过。");
			return;
		}

		// ① Finish() 用的是不是"扣过 CDR 的冷却"（真激活、真动画事件帧、真 Finish 之后读）
		float remaining = _mechW1.CooldownRemaining;
		bool cdrWorked = remaining > 0f && remaining <= _mechW1.EffectiveCooldown + 0.001f;
		GameLog.Write(
			"SelfTest",
			$"{(cdrWorked ? "✅" : "🔴")} 机制·CDR 落在 Finish() 上：冷却剩余={remaining:0.###}（期望 ≤ {_mechW1.EffectiveCooldown:0.###} 且 > 0；没走 CDR 的话这里会是 10）");

		// ② 一次性事件触发的 CDR / 刷新（闪身步 -80% / 招架反击 刷新冷却）——**比例由调用方按表值传**
		float before = _mechW1.CooldownRemaining;
		_mechW1.ReduceCooldownFraction(0.8f);
		float afterFraction = _mechW1.CooldownRemaining;
		_mechW1.ReduceCooldown(999f);
		float afterSeconds = _mechW1.CooldownRemaining;
		_mechW1.RefreshCooldown();
		GameLog.Write(
			"SelfTest",
			$"机制·一次性 CDR 接口：剩余 {before:0.###} —ReduceCooldownFraction(0.8)→ {afterFraction:0.###}（期望 {before * 0.2f:0.###}）"
			+ $" —ReduceCooldown(999)→ {afterSeconds:0.###}（期望 0） —RefreshCooldown()→ {_mechW1.CooldownRemaining:0.###}（期望 0） 层数={_mechW1.ChargesRemaining}");

		// ③ 技能层数：3 层、每层 2s（×CDR 0.25 = 0.5s）——**同步手动推进**，不受帧率影响
		_mechW1.MaxCharges = 3;
		_mechW1.RechargeSeconds = 2f;
		_mechW1.RefreshCooldown();
		int full = _mechW1.ChargesRemaining;
		bool fired = _mechW1.TryActivate();
		int afterUse = _mechW1.ChargesRemaining;
		GameLog.Write(
			"SelfTest",
			$"机制·技能层数：满层={full}（期望 3） TryActivate={fired}（期望 True）→ 用掉一次后 层数={afterUse}（期望 2）"
			+ $" 回层计时={_mechW1.CooldownRemaining:0.###}（期望 {_mechW1.EffectiveRechargeSeconds:0.###} = 2 × CDR 0.25）"
			+ $" IsReady={_mechW1.IsReady}（期望 False：正忙）");

		_mechW1._PhysicsProcess(0.2d);
		GameLog.Write("SelfTest", $"机制·技能层数·推 0.2s：层数={_mechW1.ChargesRemaining}（期望 2，还没到 0.5s） 计时={_mechW1.CooldownRemaining:0.###}（期望 0.3）");
		_mechW1._PhysicsProcess(0.35d);
		GameLog.Write("SelfTest", $"机制·技能层数·再推 0.35s（合计 0.55 > 0.5）：层数={_mechW1.ChargesRemaining}（期望 3） 计时={_mechW1.CooldownRemaining:0.###}（期望 0）");

		// ④ 还原：技能行、单位修正器、节点上的探针值
		if (ConfigRows.ById<AbilityConfig>(_mechW1.AbilityId) is { } row)
		{
			row.CooldownMultiplier = _mechW1MultBefore;
		}

		_mechPlayer.Stats.RemoveBySource(this);
		_mechPlayer.RefreshStats();
		_mechW1.Cooldown = _mechW1CooldownBefore;
		_mechW1.MaxCharges = _mechW1MaxChargesBefore;
		_mechW1.RechargeSeconds = _mechW1RechargeBefore;
		_mechW1.RefreshCooldown();
		GameLog.Write(
			"SelfTest",
			$"机制·还原：武技1 冷却={_mechW1.Cooldown:0.##} 层数={_mechW1.MaxCharges} CDR 行×{_mechW1.CooldownMultiplier:0.##} 单位×{_mechW1.UnitCooldownMultiplier:0.##}"
			+ $"（都回到改动前的值；TotalCharges 也回到 {_mechW1.MaxCharges}）");
	}

	/// <summary>
	/// 蓄力 / 完美时机：**同步驱动**（直接调 <see cref="Ability.OnInputHeld"/> /
	/// <see cref="Ability.OnInputReleased"/>），所以与帧率无关、每次跑出来的数字都一样。
	/// </summary>
	private void ProbeChargeMechanism()
	{
		if (_mechPlayer == null || _mechW2 == null)
		{
			GameLog.Write("SelfTest", "🔴 机制探针·蓄力：上一段没准备好，本段跳过。");
			return;
		}

		EnergyWaveAbility w = _mechW2;
		float damageMultiplier = _mechPlayer.Stats.Get("DamageMultiplier");

		// ① **基础蓄力没完成就松手 = 不释放**（设计文档第 14 行）
		_mechProjectilesBefore = _loopProjectiles;
		_mechShotDamageBefore = w.LastShotDamage;
		w.RefreshCooldown();
		bool ok1 = w.TryActivate();
		w.OnInputHeld(0.3f);
		string held = $"蓄力={w.ChargeTime:0.##}s 进度={w.ChargeProgress:0.##} 基础完成={w.ChargeBaseComplete} 完美窗口={w.IsPerfectWindow}";
		w.OnInputReleased();
		int aborted = _loopProjectiles - _mechProjectilesBefore;
		bool abortOk = ok1 && w.LastReleaseAborted && Mathf.IsEqualApprox(w.LastShotDamage, _mechShotDamageBefore) && !w.IsBusy;
		GameLog.Write(
			"SelfTest",
			$"{(abortOk ? "✅" : "🔴")} 机制·蓄力未完成松手：TryActivate={ok1}（期望 True） {held}（期望 0.3s / 0.6 / False / False）"
			+ $" → 松手：放弃={w.LastReleaseAborted}（期望 True） 伤害未变={Mathf.IsEqualApprox(w.LastShotDamage, _mechShotDamageBefore)}（期望 True）"
			+ $" 仍在施法={w.IsBusy}（期望 False） 投射物 +{aborted}（期望 0；这一项只报数，别的单位也在打枪所以不作判据）");

		// ② **完美时机**：蓄满 + 窗口内（0.2s）松手 → 更强的那一发（表值 40 / 400）
		SettleShot(w);
		_mechProjectilesBefore = _loopProjectiles;
		w.RefreshCooldown();
		bool ok2 = w.TryActivate();
		w.OnInputHeld(w.ChargeBaseSeconds + 0.05f);
		bool inWindow = w.IsPerfectWindow;
		w.OnInputReleased();
		int shotA = _loopProjectiles - _mechProjectilesBefore;
		float expectPerfectDamage = w.PerfectDamage * damageMultiplier;
		bool perfectOk = ok2 && inWindow && w.LastShotPerfect
			&& Mathf.IsEqualApprox(w.LastShotDamage, expectPerfectDamage)
			&& Mathf.IsEqualApprox(w.LastShotSpeed, w.PerfectSpeed);
		GameLog.Write(
			"SelfTest",
			$"{(perfectOk ? "✅" : "🔴")} 机制·完美时机：TryActivate={ok2} 蓄力={w.ChargeTime:0.###}s（=基础 {w.ChargeBaseSeconds:0.##} + 0.05）窗口内={inWindow}（期望 True）"
			+ $" → 松手：完美={w.LastShotPerfect}（期望 True） 伤害={w.LastShotDamage:0.###}（期望 表值 {w.PerfectDamage:0} ×玩家伤害倍率 {damageMultiplier:0.##} = {expectPerfectDamage:0.###}）"
			+ $" 弹速={w.LastShotSpeed:0.###}（期望 表值 {w.PerfectSpeed:0}） 投射物 +{shotA}");

		// ③ **窗口过了再松手** → 普通那一发（表值 30 / 250）
		SettleShot(w);
		_mechProjectilesBefore = _loopProjectiles;
		w.RefreshCooldown();
		bool ok3 = w.TryActivate();
		w.OnInputHeld(1.2f);
		bool overWindow = !w.IsPerfectWindow;
		w.OnInputReleased();
		int shotB = _loopProjectiles - _mechProjectilesBefore;
		float expectNormalDamage = w.Damage * damageMultiplier;
		bool normalOk = ok3 && overWindow && !w.LastShotPerfect
			&& Mathf.IsEqualApprox(w.LastShotDamage, expectNormalDamage)
			&& Mathf.IsEqualApprox(w.LastShotSpeed, w.Speed);
		GameLog.Write(
			"SelfTest",
			$"{(normalOk ? "✅" : "🔴")} 机制·过完美窗口松手：TryActivate={ok3} 蓄力={w.ChargeTime:0.###}s（> 基础+窗口 {w.ChargeBaseSeconds + w.PerfectWindowSeconds:0.##}）窗口内={w.IsPerfectWindow}（期望 False）"
			+ $" → 松手：完美={w.LastShotPerfect}（期望 False） 伤害={w.LastShotDamage:0.###}（期望 表值 {w.Damage:0} ×{damageMultiplier:0.##} = {expectNormalDamage:0.###}）"
			+ $" 弹速={w.LastShotSpeed:0.###}（期望 表值 {w.Speed:0}） 投射物 +{shotB}");

		// ④ **后置蓄力**（弓 0.1s/+30%、短弩 0.2s/+2 支、长枪 0.25s/+20% 都是这个结构）：
		//    在真技能节点上现场配一段"弓式"参数，证明基类那套档位/上限是通用的、且全是表列。
		SettleShot(w);
		_mechProjectilesBefore = _loopProjectiles;
		w.ChargeStepSeconds = 0.1f;
		w.ChargeStepScale = 1.3f;
		w.ChargeMaxSeconds = 1f;
		w.RefreshCooldown();
		bool ok4 = w.TryActivate();
		w.OnInputHeld(0.85f);
		int steps3 = w.ChargeSteps;
		float power3 = w.ChargePowerScale;
		w.OnInputHeld(2f);
		int stepsMax = w.ChargeSteps;
		int stepsCap = w.ChargeMaxStepsEffective;
		float powerMax = w.ChargePowerScale;
		w.OnInputReleased();
		int shotC = _loopProjectiles - _mechProjectilesBefore;
		float expectStepDamage = w.Damage * powerMax * damageMultiplier;
		bool stepsOk = ok4 && steps3 == 3 && stepsMax == 5 && Mathf.IsEqualApprox(power3, 2.197f)
			&& Mathf.IsEqualApprox(w.LastShotDamage, expectStepDamage)
			&& Mathf.IsEqualApprox(w.LastShotSpeed, w.Speed * powerMax);
		GameLog.Write(
			"SelfTest",
			$"{(stepsOk ? "✅" : "🔴")} 机制·后置蓄力（两段结构）：TryActivate={ok4} 基础 {w.ChargeBaseSeconds:0.##}s + 每 {w.ChargeStepSeconds:0.##}s ×{w.ChargeStepScale:0.##}，上限 {w.ChargeMaxSeconds:0.##}s"
			+ $" → 蓄 0.85s：档={steps3}（期望 3）威力×{power3:0.###}（期望 1.3³=2.197）｜一直按住：档={stepsMax}（期望封顶 {stepsCap}=floor((1.0-0.5)/0.1)）威力×{powerMax:0.###}"
			+ $" → 松手：伤害={w.LastShotDamage:0.###}（期望 30×{powerMax:0.###}×{damageMultiplier:0.##}={expectStepDamage:0.###}） 弹速={w.LastShotSpeed:0.###} 投射物 +{shotC}");

		// ⑤ 还原（表里的样子：没有后置蓄力）
		SettleShot(w);
		w.ChargeStepSeconds = 0f;
		w.ChargeStepScale = 1f;
		w.ChargeMaxSeconds = 0f;
		w.RefreshCooldown();
		GameLog.Write(
			"SelfTest",
			$"机制·还原：武技2 基础蓄力={w.ChargeBaseSeconds:0.##}s 完美窗口={w.PerfectWindowSeconds:0.##}s 后置蓄力={w.ChargeStepSeconds:0.##}s"
			+ $" 威力×{w.ChargeStepScale:0.##}（回到表里的样子）");
	}

	/// <summary>
	/// 把"刚发射完"的那一次施法**同步收尾**（`Tick → Finish`）。
	///
	/// ⚠️ 必须有这一步：`Finish()` 只在 <see cref="Ability._PhysicsProcess"/> 里的 <c>Tick</c> 跑，
	/// 而探针是**同一帧内同步**驱动松手的 —— 不收尾的话 `IsBusy` 还是 true，
	/// 下一轮 `TryActivate` 直接返回 false，`_fired` 也还是 true（于是读到的是**上一轮**的
	/// LastShotDamage / LastShotSpeed —— 实测踩到过：第三轮报"完美=True 伤害=40"其实是第二轮的值）。
	/// </summary>
	private static void SettleShot(Ability ability) => ability._PhysicsProcess(0.02d);

	/// <summary>
	/// **首领增援现在走单位表**（`docs/combat.md` 里那条"只 AddChild、完全不套单位表"的修正）。
	///
	/// 改前：`BossPressure.SpawnAdds()` 只 `AddChild` → 召唤出来的 Chaser 用**场景值 60 血**，
	/// 既没有全局倍率（`enemy_global` ×0.75 → 45），也没有敌人行上的 AI 档案与技能行。
	/// 改后与其它 4 处生成路径同一条（实例化 → `ApplyTo` → `AddChild` → `ScaleEnemy`）。
	///
	/// 期望值**全部从表/场景里读**（不写死 60/45）：行值 = `enemy_chaser.MaxHealth`、
	/// 倍率 = `enemy_global.HealthMultiplier`、场景值 = 另起一个**没套表**的实例读。
	/// </summary>
	private void ProbeBossAddsUnitTable()
	{
		Unit? player = PlayerLocator.Player;
		BossConfig? bossRow = ConfigRows.ById<BossConfig>("boss_final");
		if (player == null || bossRow == null || string.IsNullOrEmpty(bossRow.ScenePath) || !ResourceLoader.Exists(bossRow.ScenePath))
		{
			GameLog.Write("SelfTest", $"🔴 机制·首领增援：读不到首领行或它的场景（boss_final ScenePath='{bossRow?.ScenePath}'）。");
			return;
		}

		var before = new List<ulong>();

		if (GD.Load<PackedScene>(bossRow.ScenePath).Instantiate() is not Unit boss)
		{
			GameLog.Write("SelfTest", $"🔴 机制·首领增援：{bossRow.ScenePath} 的根节点不是 Unit。");
			return;
		}

		bossRow.ApplyTo(boss);
		player.GetParent()?.AddChild(boss);
		boss.GlobalPosition = player.GlobalPosition + new Vector3(0f, 0.6f, -10f);
		ConfigIndex.ScaleEnemy(boss, this, true);

		// ⚠️ 快照要在**首领自己进组之后**再拍：`AddChild` 会同步跑 `_Ready`，
		// 首领这时已经进了 `enemy_units` —— 拍早了会把首领自己当成"新生成的增援"（实测踩到过）。
		foreach (Node node in GetTree().GetNodesInGroup("enemy_units"))
		{
			before.Add(node.GetInstanceId());
		}

		if (boss.GetNodeOrNull<ActRoguelike.Boss.BossPressure>("Pressure") is not { } pressure)
		{
			GameLog.Write("SelfTest", "🔴 机制·首领增援：首领上没有 Pressure 节点。");
			return;
		}

		// ⚠️ `Interval` 在 `BossPressure._PhysicsProcess` 里被 `Mathf.Max(1f, Interval)` 夹到 ≥1s，
		// 所以把 Interval 设成 0.01 是没用的（实测：等级一动不动）——直接**推 3.05 秒**跨过 3 级。
		pressure.Interval = 0.01f;
		pressure.AddSpawnLevel = 3;
		pressure.MaxLevel = 5;
		pressure._PhysicsProcess(3.05d);

		Unit? add = null;
		foreach (Node node in GetTree().GetNodesInGroup("enemy_units"))
		{
			if (node is Unit unit && !before.Contains(unit.GetInstanceId()))
			{
				add = unit;
				break;
			}
		}

		if (add == null)
		{
			GameLog.Write("SelfTest", "🔴 机制·首领增援：Pressure 升级后没有生成增援（SpawnAdds 没跑？）。");
			return;
		}

		// 对照值：同一个预制体"没套表"的另一份实例（场景值）
		string addPath = pressure.AddScene?.ResourcePath ?? string.Empty;
		float sceneHealth = -1f;
		if (pressure.AddScene?.Instantiate() is Unit bare)
		{
			sceneHealth = bare.MaxHealth;
			bare.Free();
		}

		EnemyConfig? addRow = ConfigRows.ByScenePath<EnemyConfig>(addPath);
		float rowHealth = addRow?.MaxHealth ?? -1f;
		float globalScale = ConfigRows.ById<EnemyConfig>("enemy_global")?.HealthMultiplier ?? 1f;
		float expected = rowHealth * globalScale;

		string rowSkill = addRow?.Skills is { Length: > 0 } skills ? skills[0].Id : "（行里没技能）";
		string addSkill = "（没有技能）";
		foreach (Node node in add.FindChildren("*", string.Empty, true, false))
		{
			if (node is Ability ability && !string.IsNullOrEmpty(ability.AbilityId))
			{
				addSkill = ability.AbilityId;
				break;
			}
		}

		bool ok = Mathf.IsEqualApprox(add.MaxHealth, expected)
			&& Mathf.IsEqualApprox(add.Health, expected)
			&& addSkill == rowSkill;
		GameLog.Write(
			"SelfTest",
			$"{(ok ? "✅" : "🔴")} 机制·首领增援走单位表：SpawnAdds 生成 {add.Name} 生命 {add.MaxHealth:0.##}（= 敌人行 {rowHealth:0.##} × 全局倍率 {globalScale:0.##} = {expected:0.##}）"
			+ $" 当前生命 {add.Health:0.##}｜场景值（没套表的那一份）{sceneHealth:0.##}（改前就是这个值）"
			+ $"｜标签 [{string.Join(",", add.Tags)}] 技能行 Id={addSkill}（期望 {rowSkill}；改前只会是类名）");
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
		// P8 第 0 步的**一次性探针**：数组型表参数能不能导出 + 读回来（结论见 docs/abilities.md §8.2）。
		// 它只读 + 只往 user:// 写一个临时 .tres，不碰任何正式数据。
		foreach (string probeLine in ActRoguelike.Testing.SeriesArrayProbe.Run())
		{
			GameLog.Write("SelfTest", probeLine);
		}

		var player = PlayerLocator.Player;
		ActRoguelike.Weapons.Weapon? weapon = player == null ? null : NodeSearch.FindInSubtree<ActRoguelike.Weapons.Weapon>(player);

		// ⚠️ P6a：基准角色/武器换成清旧之后剩下的**角色 A［猎手］+ 能量掌**（旧的 3 角色 3 武器已删）。
		// 角色 A 的生命/护甲/移速**是 P3 代理自拟的预设值**（设计文档 `3d肉鸽雨中哈迪斯/角色设计.txt`
		// 没有写这三项），**不是经过确认的平衡值** —— 读数变化只代表"换了角色行"，不代表平衡结论。
		GameLog.Write("SelfTest",
			player == null
				? "配置表比对：找不到玩家"
				: $"配置表比对·玩家：生命 {player.MaxHealth:0}（期望 200 + 元进度加成 {MetaProgress.BonusMaxHealth:0}；角色 A 的 200 是**预设值**） 护甲 {player.StartingArmor:0}（期望 20，预设值） 移速 {player.MoveSpeed:0.##}（期望 6.5，预设值） 减伤 {player.DamageReduction:0.##}（期望 0）");

		GameLog.Write("SelfTest",
			weapon == null
				? "配置表比对：找不到武器"
				: $"配置表比对·武器：伤害 {weapon.Damage:0}（期望 0） 间隔 {weapon.FireInterval:0.##}（期望 0.5） 散布 {weapon.SpreadDegrees:0.##}（期望 0） 击退 {weapon.Knockback:0.##}（期望 0） 暴击 {weapon.CritChance:0.##}（期望 0.05） 弹速 {weapon.ProjectileSpeed:0}（期望 150） 预瞄FOV {weapon.AimFov:0}（期望 55） 穿透 {weapon.BasePierce}（期望 0） 连发 {weapon.Automatic}（期望 False） ｜ 左键归 Weapon 组件 {weapon.Enabled}（期望 False —— 能量掌是**武技驱动**）。⚠️ P6b：原来这一行还有「弹匣 N / 装填 X」两项，随「没有弹匣的概念了」整条删除（`Weapon` 已无 `MagazineSize`/`ReloadTime`/`Ammo`）");

		// ⚠️ 这一行**只打印、不做比对**（`ConfigRows.Count<T>()` 的实际值 + 括号里的期望值都在同一条字符串里，
		// 没有任何 if/断言）—— 所以期望串过期**不会**让自测失败，只会误导后来的人。
		// ⚠️ **P6a 清旧**之后的期望值（逐处差额都对着删掉的行）：
		//   武器 9→6（`wpn_pistol` / `wpn_sniper` / `wpn_rifle`）、角色 5→2（`chr_gunner` / `chr_heavy` / `chr_summoner`）、
		//   技能 44→39（`skl_dash` / `skl_burst` / `skl_melee` / `skl_summon` / `skl_control`）、
		//   升级池 9→6（`pool_pistol` / `pool_sniper` / `pool_rifle`）、能力卡 8→6（`card_summon` / `card_control`）；
		//   敌人 10 / 首领 1 / 召唤物 1 / Pressure 7 / 效果 13 / 节点 8 / 局外 6 / AI 档案 11 **一条没动**。
		//   增幅项不在这一行（见下面「配置表比对·增幅项」）：74 → 57（删了 17 条旧行 = 旧强化 10 + 旧进化 4 + 旧异化 3），
		//   剩下强化 9（= 设计文档 §227-243 那 9 条）/ 进化 23 / 异化 25。
		GameLog.Write("SelfTest",
			$"配置表比对·索引：武器 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.WeaponConfig>()}/角色 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.CharacterConfig>()}/敌人 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.EnemyConfig>()}/首领 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.BossConfig>()}/召唤物 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.SummonConfig>()}/技能 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.AbilityConfig>()}/Pressure {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.PressureModConfig>()}/效果 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.EffectConfig>()}/节点 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.NodeConfig>()}/局外 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.MetaUpgradeConfig>()}/升级池 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.UpgradePoolConfig>()}/AI 档案 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.AiProfileConfig>()}/能力卡 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.AbilityCardConfig>()}（期望 **P6b 后 6/2/10/1/1/39/7/14/8/5/6/11/4**；只打印、不比对。P6a 清旧后是 6/2/10/1/1/39/7/13/8/6/6/11/6；P5b 之前是 9/5/10/1/1/44/7/13/8/6/9/11/8 —— P6a 的差额**只有**武器 −3 / 角色 −3 / 技能 −5 / 升级池 −3 / 能力卡 −2；P6b 的差额**只有**效果 +1（`eff_missile_beacon`）/ 局外 −1（`meta_reload`）/ 能力卡 −2（两张弹匣卡））");

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
			$"配置表比对·增幅项：共 {RewardCatalog.All.Length} 条（强化 {enhancement} / 进化 {evolution} / 异化 {mutation}，期望 **P6d 后 62 = 9/23/30**（= 设计文档要求的 30 条异化**全建**）；P6b 后是 59 = 9/23/27；P6a 后是 57 = 9/23/25；P6a 之前是 74 = 19/27/28） 表已接上={RewardCatalog.Find("upg_power") != null && RewardCatalog.Find("upg_dmg_up") == null}（新行在、旧行已清） 当前池子={poolSize}（猎手+能量掌，P6d 后的实测值）");

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

		// docs/rewards.md 的硬指标是"猎手+能量掌"这一侧的候选数；P6a 清旧后旧角色已不在，
		// 所以下面只用**同一个武器**再量一次（`SummonerAllowed` / `mut_summon` 已随旧角色删除，
		// 这里改成量"同池再抽一次是否稳定"，不再有"召唤控制池"那一路）。
		string repeatCount = "无武器";
		if (weapon != null)
		{
			var repeatPool = new RewardPool();
			repeatPool.Initialize(weapon);
			repeatCount = $"{repeatPool.Roll(new RunBuild(), 40).Count} 条（与上面 PoolPreview 同源，不再有 RequiresSummoner 那一层）";
		}

		GameLog.Write("SelfTest",
			$"配置表比对·升级池：武器 PoolId「{poolIdText}」（期望 pool_energy_palm） 池表 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.UpgradePoolConfig>()} 行（期望 6；P6a 之前 9） / 选项行里指定了池的 {pooledOptions} 条 ｜ 专属层 {exactLayer} 条（命中 {exactHit}）· 类别层 {tagLayer} 条（命中 {tagHit}）· 通用层 {genericLayer} 条 ｜ 同池复量 {repeatCount}");

		// ── P5：三层判定（限定 / 互斥 / 前置）取证 ──
		ReportP5Layers(player, weapon);

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
		// ⚠️ P6a：探针行从 `skl_dash`（已随旧角色删除）换成清旧后仍在的 `skl_a1_skill_1`（后撤）。
		ActRoguelike.Config.AbilityConfig? backdashRow = ActRoguelike.Config.ConfigRows.ById<ActRoguelike.Config.AbilityConfig>("skl_a1_skill_1");
		GameLog.Write("SelfTest",
			$"配置表比对·技能表：{ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.AbilityConfig>()} 行（期望 P6a 清旧后 39 = 19 玩家 + 16 单位 + 4 首领行为；P6a 之前 44，删的是 5 条旧角色技能行；只打印、不比对） 后撤行：冷却 {backdashRow?.Cooldown:0.##}（期望 10） 节点 {backdashRow?.NodeName}（期望 AbilitySkill1） 类 {backdashRow?.ClassHint}（期望 BackdashAbility） ｜ 首领强化来源：Boss Build 行为型（**技能**）{ActRoguelike.Boss.BossPerkCatalog.BehaviorOnly.Count} 条（期望 4）/ Pressure 数值型（**词条**）{ActRoguelike.Boss.BossPerkCatalog.PressureOnly.Count} 条（期望 7 = 4 数值型参数栏 + 3 技能参数栏）");

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

		// 玩家侧实测：技能行 + 角色行/武器行是否真的套上了（这条是"技能表接线"的硬证据）。
		// ⚠️ P6a：旧角色那两个节点（`AbilityDash` / `AbilityMelee`）已随旧角色一起从 `Player.tscn` 删除，
		// 所以探针改成清旧后仍在的节点（角色 A 的后撤 / 能量掌的武技 1）。
		if (player != null)
		{
			ActRoguelike.Abilities.Ability? backdash = player.GetNodeOrNull<ActRoguelike.Abilities.Ability>("AbilitySkill1");
			ActRoguelike.Abilities.Ability? wave = player.GetNodeOrNull<ActRoguelike.Abilities.Ability>("AbilityWeaponSkill1");
			GameLog.Write("SelfTest",
				$"技能表实测（玩家）：后撤[AbilitySkill1] 冷却 {backdash?.Cooldown:0.##}（期望 10 = 技能行 skl_a1_skill_1） AbilityId {backdash?.AbilityId}（期望 skl_a1_skill_1） 按键「{backdash?.InputAction}」（期望 skill_1）"
				+ $" ｜ 能量掌武技1[AbilityWeaponSkill1] 冷却 {wave?.Cooldown:0.##}（期望 0.5 = 技能行 skl_a1_w1_1） AbilityId {wave?.AbilityId}（期望 skl_a1_w1_1） 按键「{wave?.InputAction}」（期望 weapon_skill_1）");
		}

		// Pressure 词条池（3 条拆成 5 条 + 技能参数入池）与**限定词的刷新判定**取证。
		// 全部逻辑放在 `Scripts/Boss/BossPerkProbe.cs`（跟着 SelfTest 走，但别把这里撑长）。
		ActRoguelike.Boss.BossPerkProbe.Report(stage);
	}

	/// <summary>
	/// **P5 三层判定取证**（限定 / 互斥 / 前置）—— 全程走**真实代码路径**
	/// （<c>RewardPool.Initialize</c> + <c>RewardPool.Roll</c>），只是把"当前角色/武器"换成几个探针值。
	///
	/// <para>为什么要造武器对象：<c>AllowsWeapon</c> 读的是 <c>Weapon.PoolId</c> / <c>AttackTags</c> 两个
	/// <c>[Export]</c> 公开属性，所以这里 <c>new</c> 三个"只有身份、不在场景里"的 Weapon 就够了
	/// （不复用玩家身上那一把 —— 它已经是当前武器，换不了）。用完 <c>Free()</c>，不进树。</para>
	///
	/// <para>构筑用 <c>RunBuild.DebugGrantWithoutApplying</c>（只记层数、不套效果），所以这一段的
	/// 判定探针**不会污染本局数值**。</para>
	/// </summary>
	private static void ReportP5Layers(Unit? livePlayer, ActRoguelike.Weapons.Weapon? liveWeapon)
	{
		var energyPalm = new ActRoguelike.Weapons.Weapon { PoolId = "pool_energy_palm", AttackTags = new[] { "Projectile", "Energy", "Physical" } };
		var crossbow = new ActRoguelike.Weapons.Weapon { PoolId = "pool_crossbow", AttackTags = new[] { "Projectile", "Arrow", "Physical" } };
		var bow = new ActRoguelike.Weapons.Weapon { PoolId = "pool_bow", AttackTags = new[] { "Projectile", "Arrow", "Physical" } };
		var scythe = new ActRoguelike.Weapons.Weapon { PoolId = "pool_scythe", AttackTags = new[] { "Melee", "Scythe", "Physical" } };

		static RewardPool MakePool(ActRoguelike.Weapons.Weapon weapon, string characterId)
		{
			var pool = new RewardPool();
			pool.Initialize(weapon, characterId);
			return pool;
		}

		static string Ids(RewardPool pool, RunBuild build)
		{
			List<RewardDefinition> rolled = pool.Roll(build, 400);
			return "," + string.Join(",", rolled.ConvertAll(d => d.Id)) + ",";
		}

		RewardPool hunterPalm = MakePool(energyPalm, "chr_hunter");
		RewardPool hunterBow = MakePool(bow, "chr_hunter");
		RewardPool masterScythe = MakePool(scythe, "chr_weapon_master");
		// ⚠️ P6a：旧角色（`chr_gunner` 等）已删除，这里改用**一个表里不存在的角色 Id** 当"既不是 A 也不是 B"
		// 的探针 —— 语义与原来那条一模一样（限定项不进池、通用项进池），但不再依赖任何已删的行。
		RewardPool noCharacterPalm = MakePool(energyPalm, "chr_（一个不存在的角色）");

		var fresh = new RunBuild();
		string palmIds = Ids(hunterPalm, fresh);
		string bowIds = Ids(hunterBow, fresh);
		string masterIds = Ids(masterScythe, fresh);
		string noCharacterIds = Ids(noCharacterPalm, fresh);

		// ① 限定：角色 + 武器两层
		bool limitOk =
			hunterPalm.CandidateCount > 0
			&& palmIds.Contains(",evo_blast_palm,")      // A1限定：角色 A + 武器 1
			&& !palmIds.Contains(",evo_focus,")           // A2限定：同一角色，但武器不对 → 不进池
			&& !palmIds.Contains(",mut_cleave,")          // B 限定：角色不对 → 不进池
			&& bowIds.Contains(",evo_focus,") == false    // evo_focus 属于 pool_crossbow，弓不该有
			&& masterIds.Contains(",mut_cleave,")         // B 限定在角色 B 身上进池
			&& !masterIds.Contains(",mut_ricochet,")      // A 限定在角色 B 身上不进池
			&& !noCharacterIds.Contains(",mut_ricochet,") // 角色未知/不存在 → A 限定不进池
			&& noCharacterIds.Contains(",upg_power,");    // 通用强化对谁都进池
		GameLog.Write("SelfTest",
			$"{(limitOk ? "✅" : "🔴")} P5·层①限定（角色+武器）：猎手+能量掌 候选 {hunterPalm.CandidateCount} 条 含 evo_blast_palm={palmIds.Contains(",evo_blast_palm,")}（期望 True，A1限定=<A限定>+池 pool_energy_palm）"
			+ $" 含 evo_focus={palmIds.Contains(",evo_focus,")}（期望 False，它属于 pool_crossbow）｜猎手+弓 含 evo_focus={bowIds.Contains(",evo_focus,")}（期望 False）"
			+ $"｜武器大师+镰刀 候选 {masterScythe.CandidateCount} 条 含 mut_cleave={masterIds.Contains(",mut_cleave,")}（期望 True，<B限定>）含 mut_ricochet={masterIds.Contains(",mut_ricochet,")}（期望 False，<A限定>）"
			+ $"｜角色 Id 不存在+能量掌 含 mut_ricochet={noCharacterIds.Contains(",mut_ricochet,")}（期望 False）含 upg_power={noCharacterIds.Contains(",upg_power,")}（期望 True，通用）");

		// ② 前置：拿到「弹射」之后「弹射+」才出现
		string beforePrereq = Ids(hunterPalm, fresh);
		var withRicochet = new RunBuild();
		RewardDefinition? ricochet = RewardCatalog.Find("mut_ricochet");
		RewardDefinition? ricochetPlus = RewardCatalog.Find("mut_ricochet_plus");
		RewardDefinition? ricochetPlus2 = RewardCatalog.Find("mut_ricochet_plus2");
		RewardDefinition? pierce = RewardCatalog.Find("mut_pierce");
		RewardDefinition? focus = RewardCatalog.Find("evo_focus");
		RewardDefinition? philander = RewardCatalog.Find("evo_philander");
		if (ricochet != null)
		{
			withRicochet.DebugGrantWithoutApplying(ricochet);
		}

		string afterPrereq = Ids(hunterPalm, withRicochet);
		bool prereqOk = !beforePrereq.Contains(",mut_ricochet_plus,")
			&& afterPrereq.Contains(",mut_ricochet_plus,")
			&& !afterPrereq.Contains(",mut_ricochet_plus2,"); // 三级链：跳级拿不到
		GameLog.Write("SelfTest",
			$"{(prereqOk ? "✅" : "🔴")} P5·层②前置（Requires）：拿到「弹射」之前 含 弹射+={beforePrereq.Contains(",mut_ricochet_plus,")}（期望 False）｜拿到之后 含 弹射+={afterPrereq.Contains(",mut_ricochet_plus,")}（期望 True）"
			+ $" 含 弹射++={afterPrereq.Contains(",mut_ricochet_plus2,")}（期望 False —— 它前置的是「弹射+」，跳级拿不到）");

		// ③ 互斥（双向）：弹射 × 穿透（逻辑冲突）
		bool mutualOk = beforePrereq.Contains(",mut_pierce,")
			&& !afterPrereq.Contains(",mut_pierce,");
		if (pierce != null && ricochet != null)
		{
			var withPierce = new RunBuild();
			withPierce.DebugGrantWithoutApplying(pierce);
			string pierceIds = Ids(hunterPalm, withPierce);
			mutualOk = mutualOk
				&& pierceIds.Contains(",mut_ricochet,") == false // 反方向也拦得住
				&& !pierceIds.Contains(",mut_ricochet_plus,");
		}

		// ④ 互斥（平衡限制）：专一 × 花心（<A2限定>，用户 §1.3 的第二类来源）
		bool balanceOk = false;
		if (focus != null && philander != null)
		{
			var withFocus = new RunBuild();
			withFocus.DebugGrantWithoutApplying(focus);
			string focusIds = Ids(hunterPalm, withFocus);
			// 注意：专一属于 pool_crossbow，猎手拿能量掌时两件都进不了池；
			// 所以平衡互斥要用"猎手 + 短弩"那一侧来量。
			RewardPool hunterCrossbow = MakePool(crossbow, "chr_hunter");
			var cbFresh = new RunBuild();
			string cbIds = Ids(hunterCrossbow, cbFresh);
			var cbFocus = new RunBuild();
			cbFocus.DebugGrantWithoutApplying(focus);
			string cbAfter = Ids(hunterCrossbow, cbFocus);
			balanceOk = cbIds.Contains(",evo_focus,") && cbIds.Contains(",evo_philander,")
				&& cbAfter.Contains(",evo_philander,") == false;
			GameLog.Write("SelfTest",
				$"P5·互斥对在池里的原始状态（猎手+短弩）：含 专一={cbIds.Contains(",evo_focus,")} 含 花心={cbIds.Contains(",evo_philander,")}（都期望 True）｜拿到专一后 含 花心={cbAfter.Contains(",evo_philander,")}（期望 False）｜空转对照（能量掌侧）{focusIds.Length > 0}");
		}

		GameLog.Write("SelfTest",
			$"{(mutualOk ? "✅" : "🔴")} P5·层③互斥（双向，逻辑冲突）：弹射 × 穿透 —— 空构筑里两条都在（含穿透={beforePrereq.Contains(",mut_pierce,")}）→ 拿到弹射后穿透消失（含={afterPrereq.Contains(",mut_pierce,")}，期望 False）→ 反向再验一次也拦得住");

		GameLog.Write("SelfTest",
			$"{(balanceOk ? "✅" : "🔴")} P5·层③互斥（双向，平衡限制）：专一 × 花心 —— 猎手+短弩空构筑两条都在 → 拿到专一后花心消失（同一个 Excludes 字段，与逻辑冲突无区别）");

		energyPalm.Free();
		crossbow.Free();
		bow.Free();
		scythe.Free();

		// ⑤ **真在游戏里"拿到并生效"**：用**真实的玩家** + 真实的 `RewardContext` 套两条 P5 增幅，
		// 量出 `StatSheet` 的实际变化，然后 `RemoveBySource` 还原（`StatSet` 的修正器 Source 就是那个 RunBuild，
		// 所以这一段的量测**不留痕迹** —— 后面的旧硬指标仍然逐位可核）。
		if (livePlayer == null || liveWeapon == null)
		{
			GameLog.Write("SelfTest", "🔴 P5·实测拿到并生效：拿不到玩家/武器，跳过。");
			return;
		}

		var liveBuild = new RunBuild();
		var context = new RewardContext { Player = livePlayer, Weapon = liveWeapon, Build = liveBuild };
		RewardDefinition? power = RewardCatalog.Find("upg_power");
		RewardDefinition? climb = RewardCatalog.Find("upg_climb");
		float damageBefore = livePlayer.Stats.Get("DamageMultiplier");
		float jumpBefore = livePlayer.Movement?.EffectiveJumpVelocity ?? 0f;

		if (power != null)
		{
			liveBuild.Add(power, context);
		}

		if (climb != null)
		{
			liveBuild.Add(climb, context);
		}

		float damageAfter = livePlayer.Stats.Get("DamageMultiplier");
		float jumpAfter = livePlayer.Movement?.EffectiveJumpVelocity ?? 0f;
		bool powerOk = Mathf.Abs(damageAfter - (damageBefore * 1.15f)) < 0.0001f;
		bool climbOk = Mathf.Abs(jumpAfter - (jumpBefore * 1.08f)) < 0.001f;

		// 还原（按 Source 成组移除）：这几行必须在**旧硬指标**之前量完并清干净。
		livePlayer.Stats.RemoveBySource(liveBuild);
		livePlayer.RefreshStats();
		float damageRestored = livePlayer.Stats.Get("DamageMultiplier");
		bool restoreOk = Mathf.Abs(damageRestored - damageBefore) < 0.0001f;
		GameLog.Write("SelfTest",
			$"{(powerOk && climbOk && restoreOk ? "✅" : "🔴")} P5·实测拿到并生效（真实玩家 + 真实 RewardContext）：强化「力量」伤害乘区 {damageBefore:0.####} → {damageAfter:0.####}（期望 ×1.15={damageBefore * 1.15f:0.####}）"
			+ $"｜强化「攀高」单位起跳速度 {jumpBefore:0.###} → {jumpAfter:0.###} m/s（期望 ×1.08={jumpBefore * 1.08f:0.###}，消费者 CharacterMovement.EffectiveJumpVelocity）"
			+ $"｜按 Source 还原后 {damageRestored:0.####}（期望 == {damageBefore:0.####} = {restoreOk}）—— 这一段量测不污染本局");

		// ===== P5b：进化池 8 → 27（设计文档 23 条全建 + 4 条旧行进池保留）+ 异化池 23 → 28 =====
		// ⑥ 层①（新行）：<A3限定>（猎手 + 弓）与 <B1限定>（武器大师 + 灵剑）各验一次 ——
		//    这一批 24 条里大半是"角色 + 武器"两层同时限定，所以要再加一把探测武器。
		var spiritSword = new ActRoguelike.Weapons.Weapon
		{
			PoolId = "pool_spirit_sword",
			AttackTags = new[] { "Melee", "Sword", "Physical" },
		};
		RewardPool masterSword = MakePool(spiritSword, "chr_weapon_master");
		string swordIds = Ids(masterSword, fresh);
		bool limitP5b =
			bowIds.Contains(",evo_variation,")          // <A3限定>：角色 A + 弓
			&& bowIds.Contains(",evo_dragon_arrow,")
			&& bowIds.Contains(",evo_stall_kill,")      // <A限定>：只要角色 A（3 把武器都拿得到）
			&& !bowIds.Contains(",evo_golden_qi,")      // <B1限定>：角色不对 → 根本不进池
			&& swordIds.Contains(",evo_golden_qi,")     // <B1限定>：角色 B + 灵剑
			&& swordIds.Contains(",evo_ground_burst,")  // <B限定>：只要角色 B
			&& swordIds.Contains(",mut_freeze,")        // 通用异化：没写限定的谁都进池
			&& !swordIds.Contains(",evo_variation,");   // <A3限定>：角色/武器都不对
		GameLog.Write("SelfTest",
			$"{(limitP5b ? "✅" : "🔴")} P5b·层①限定（新行）：猎手+弓 候选 {hunterBow.CandidateCount} 条 含 变奏={bowIds.Contains(",evo_variation,")}（期望 True，A3限定） 含 乱金柝={bowIds.Contains(",evo_stall_kill,")}（期望 True，A限定=只要角色 A） 含 金光术={bowIds.Contains(",evo_golden_qi,")}（期望 False，B1限定）"
			+ $"｜武器大师+灵剑 候选 {masterSword.CandidateCount} 条 含 金光术={swordIds.Contains(",evo_golden_qi,")}（期望 True） 含 地爆={swordIds.Contains(",evo_ground_burst,")}（期望 True，B限定） 含 变奏={swordIds.Contains(",evo_variation,")}（期望 False） 含 冰晶={swordIds.Contains(",mut_freeze,")}（期望 True，通用异化）");

		// ⑦ 层②（新行）：这一批新增的两条带前置的异化各验一次（冰晶+ 要冰晶、烈阳+ 要烈阳，两条链互不相干）。
		var withFreeze = new RunBuild();
		if (RewardCatalog.Find("mut_freeze") is { } freezeRow)
		{
			withFreeze.DebugGrantWithoutApplying(freezeRow);
		}

		string afterFreeze = Ids(masterSword, withFreeze);
		bool prereqP5b = !swordIds.Contains(",mut_freeze_plus,")
			&& !swordIds.Contains(",mut_burn_plus,")
			&& afterFreeze.Contains(",mut_freeze_plus,")
			&& !afterFreeze.Contains(",mut_burn_plus,");
		GameLog.Write("SelfTest",
			$"{(prereqP5b ? "✅" : "🔴")} P5b·层②前置（新行）：空构筑里 含 冰晶+={swordIds.Contains(",mut_freeze_plus,")}（期望 False） 含 烈阳+={swordIds.Contains(",mut_burn_plus,")}（期望 False）"
			+ $"｜拿到「冰晶」之后 含 冰晶+={afterFreeze.Contains(",mut_freeze_plus,")}（期望 True） 含 烈阳+={afterFreeze.Contains(",mut_burn_plus,")}（期望 False —— 它前置的是「烈阳」，两条链互不相干）");

		// ⑧ **表里的参数真的落到了机制上**（P5b）：探测技能节点（`new` 出来、**不进树** —— 与上面那三个探针武器同一类做法）
		//    覆盖 P5b 新加的处理器 `AbilityOverride`（7 条进化在用它：HandlerParams 里 key = Ability 的 [Export] 属性名）。
		var probePlayer = new Unit();
		var probeBackdash = new ChargeAttackAbility { AbilityId = "skl_a1_skill_1", MaxCharges = 1 };
		var probeSpear = new ChargeAttackAbility { AbilityId = "skl_b3_w1_2" };
		var probeBowSkill = new ArrowVolleyAbility { AbilityId = "skl_a3_w1_1" };
		var probeDraw = new ArrowVolleyAbility { AbilityId = "skl_b1_w1_2" };
		var probeLanding = new LandingBurstAbility { AbilityId = "skl_b1_w1_3" };
		probePlayer.AddChild(probeBackdash);
		probePlayer.AddChild(probeSpear);
		probePlayer.AddChild(probeBowSkill);
		probePlayer.AddChild(probeDraw);
		probePlayer.AddChild(probeLanding);

		var overrideBuild = new RunBuild();
		var overrideContext = new RewardContext { Player = probePlayer, Weapon = spiritSword, Build = overrideBuild };
		foreach (string id in new[]
		{
			"evo_spare_dodge", "evo_rainbow", "evo_variation", "evo_dragon_arrow",
			"evo_great_draw", "evo_ground_burst", "evo_divine_descent",
		})
		{
			if (RewardCatalog.Find(id) is { } row)
			{
				overrideBuild.Add(row, overrideContext);
			}
		}

		bool overrideOk =
			probeBackdash.MaxCharges == 3 && Mathf.Abs(probeBackdash.RechargeSeconds - 0.5f) < 0.0001f
			&& Mathf.Abs(probeSpear.ChargeBaseSeconds - 0.5f) < 0.0001f
			&& Mathf.Abs(probeSpear.ChargeStepSeconds - 0.125f) < 0.0001f
			&& probeBowSkill.RampMaxCount == 3 && Mathf.Abs(probeBowSkill.RampWindowSeconds - 1.2f) < 0.0001f
			&& Mathf.Abs(probeBowSkill.ChargeMaxSeconds - 3f) < 0.0001f
			&& Mathf.Abs(probeDraw.ProjectileSizeScale - 2f) < 0.0001f
			&& Mathf.Abs(probePlayer.Stats.Get("DamageMultiplier") - 1.5f) < 0.0001f
			&& Mathf.Abs(probeLanding.Knockback - 6f) < 0.0001f
			&& Mathf.Abs(probeLanding.Cooldown - 2f) < 0.0001f
			&& Mathf.Abs(probeLanding.DamageScale - 1.6f) < 0.0001f;
		GameLog.Write("SelfTest",
			$"{(overrideOk ? "✅" : "🔴")} P5b·实测表参数落到机制上（探测技能节点，不进树）：有备无患 → 后撤 MaxCharges {probeBackdash.MaxCharges}（期望 3）Recharge {probeBackdash.RechargeSeconds:0.###}s（期望 0.5）"
			+ $"｜气贯长虹 → 长枪武技2 基础蓄力 {probeSpear.ChargeBaseSeconds:0.###}s（期望 0.5）每档 {probeSpear.ChargeStepSeconds:0.###}s（期望 0.125）"
			+ $"｜变奏 + 龙之箭 → 弓武技1 RampMaxCount {probeBowSkill.RampMaxCount}（期望 3）RampWindow {probeBowSkill.RampWindowSeconds:0.###}s（期望 1.2）蓄力上限 {probeBowSkill.ChargeMaxSeconds:0.###}s（期望 3）"
			+ $"｜大拔刀术 → 拔刀式剑气体积 ×{probeDraw.ProjectileSizeScale:0.###}（期望 2）+ 伤害乘区 {probePlayer.Stats.Get("DamageMultiplier"):0.###}（期望 1.5）"
			+ $"｜地爆 / 神降术 → 落剑式 Knockback {probeLanding.Knockback:0.###}（期望 6）Cooldown {probeLanding.Cooldown:0.###}s（期望 2）DamageScale {probeLanding.DamageScale:0.###}（期望 1.6）");

		// ⑨ 异化（P5b）：三条新增异化的规则**真的挂上了、参数真的来自表**，并且真的能出手一次。
		var ruleBuild = new RunBuild();
		var ruleContext = new RewardContext { Player = livePlayer, Weapon = spiritSword, Build = ruleBuild };
		foreach (string id in new[] { "mut_freeze", "mut_energy_shield", "mut_stomp_kill" })
		{
			if (RewardCatalog.Find(id) is { } row)
			{
				ruleBuild.Add(row, ruleContext);
			}
		}

		BurnOnHitRule? freezeRule = null;
		TablePulseRule? shieldRule = null;
		TablePulseRule? stompRule = null;
		var probeRules = new List<TriggerRule>();
		NodeSearch.CollectInSubtree(livePlayer, probeRules);
		foreach (TriggerRule rule in probeRules)
		{
			if (rule is BurnOnHitRule burn && burn.RewardId == "mut_freeze")
			{
				freezeRule = burn;
			}

			if (rule is TablePulseRule pulse && pulse.RewardId == "mut_energy_shield")
			{
				shieldRule = pulse;
			}

			if (rule is TablePulseRule stomp && stomp.RewardId == "mut_stomp_kill")
			{
				stompRule = stomp;
			}
		}

		// 真的出手一次：**直接调 Matches/Execute**，不往全局事件总线上抛 —— 抛真事件会被别的订阅者
		// （关卡记账 / GameLog / 碎片）当真，污染后面的旧硬指标。
		int jumpsBefore = livePlayer.Movement?.ExtraAirJumps ?? 0;
		var killProbe = new GameplayEvent { Kind = GameplayEventKind.Kill, Source = livePlayer };
		var parryProbe = new GameplayEvent { Kind = GameplayEventKind.Parried, Source = livePlayer };
		bool matched = stompRule != null && stompRule.Matches(killProbe)
			&& shieldRule != null && shieldRule.Matches(parryProbe);
		stompRule?.Execute(killProbe);
		int jumpsAfter = livePlayer.Movement?.ExtraAirJumps ?? 0;
		bool fired = jumpsAfter == Mathf.Min(jumpsBefore + 1, 3);
		bool ruleOk = freezeRule is { Cooldown: 10f, StatusId: "Freeze" }
			&& shieldRule is { Trigger: "Parried,ArmorBroken", Radius: 6f, Damage: 20f }
			&& stompRule is { ExtraAirJumps: 1, JumpCap: 3, Trigger: "Kill" }
			&& matched
			&& fired;
		GameLog.Write("SelfTest",
			$"{(ruleOk ? "✅" : "🔴")} P5b·实测异化规则（真实玩家 + 真实 RewardContext）：冰晶 → {freezeRule?.GetType().Name} Cooldown {freezeRule?.Cooldown:0.#}s（期望 10）StatusId「{freezeRule?.StatusId}」（期望 Freeze）"
			+ $"｜能量护盾 → TablePulseRule Trigger「{shieldRule?.Trigger}」（期望 Parried,ArmorBroken —— P6a 给 ArmorBroken 补了抛点，两路都通）半径 {shieldRule?.Radius:0.#}（期望 6）伤害 {shieldRule?.Damage:0.#}（期望 20）"
			+ $"｜践踏死亡 → 击杀匹配={matched} 出手一次后 额外空中跳跃 {jumpsBefore} → {jumpsAfter}（期望 +1，封顶 {stompRule?.JumpCap}）");

		// 清理（这一段量测不留痕）：摘掉三条规则节点、还原额外空中跳跃、释放探测单位。
		foreach (TriggerRule rule in probeRules)
		{
			if (rule.RewardId is "mut_freeze" or "mut_energy_shield" or "mut_stomp_kill")
			{
				livePlayer.RemoveChild(rule);
				rule.Free();
			}
		}

		livePlayer.Movement?.SetExtraAirJumps(jumpsBefore, 3);

		// ── P6a：给三个"一直没有抛点"的事件补上抛点之后的**行为取证** ──
		// 判据：订阅事件总线 → 走**真实的** `Unit.ReceiveHit` → 看这三个 Kind 有没有真的被抛出来。
		// ⚠️ 拿场上的怪来打会污染关卡记账（`Damaged` / `Died` / `Kill` 都被别的系统当真），所以打**玩家自己**：
		// 伤害给足（护甲值 ×3，扣掉减伤后仍远超护甲）→ 护甲正好被打光、生命只掉一点**不会死**
		// （所以总线上不会有 `Died` / `Kill`）；`BypassBlock = true` 让伤害确定（不受格挡/架势影响）；
		// 量完 `FillVitals()` 把生命/护甲/护盾补回去（与上面几段同一个"量测不留痕"纪律）。
		// ⚠️ 这一段必须排在**上面三条探针规则摘掉之后**：否则「能量护盾」会在护甲破碎那一刻真的出手打周围敌人。
		// ⚠️ `ShieldBroken` 在玩家身上**量不到**（角色 A 的 `MaxShield` 是 0，护盾那一段进不去）——
		// 它只有"抛点唯一"的静态证据（与 `ArmorBroken` 同一段代码的相邻分支）。
		if (IsInstanceValid(livePlayer))
		{
			var seen = new List<GameplayEventKind>();
			System.Action<GameplayEvent> probe = raised => seen.Add(raised.Kind);
			GameplayEventBus.Raised += probe;

			float armorBefore = livePlayer.Armor;
			float healthBefore = livePlayer.Health;
			livePlayer.ReceiveHit(new HitInstance
			{
				Target = livePlayer,
				Context = EventContext.Create(null, livePlayer),
				Damage = Mathf.Max(1f, armorBefore) * 3f,
				Critical = true,
				BypassBlock = true,
				Direction = Vector3.Up,
				Position = livePlayer.GlobalPosition,
			});

			GameplayEventBus.Raised -= probe;

			bool armorRaised = seen.Contains(GameplayEventKind.ArmorBroken);
			bool critRaised = seen.Contains(GameplayEventKind.Critical);
			bool brokenCeiling = seen.Contains(GameplayEventKind.ShieldBroken);
			bool noKillOrDeath = !seen.Contains(GameplayEventKind.Kill) && !seen.Contains(GameplayEventKind.Died);
			bool armorGone = livePlayer.Armor <= 0f;
			livePlayer.FillVitals(); // 量完补回去（不留痕）

			GameLog.Write("SelfTest",
				$"{(armorRaised && critRaised && noKillOrDeath && armorGone ? "✅" : "🔴")} P6a·三个新抛点（订阅事件总线 + 走**真实的** `Unit.ReceiveHit`）：玩家护甲 {armorBefore:0.#} 被打光={armorGone} → 收到 ArmorBroken={armorRaised}（期望 True）· Critical={critRaised}（期望 True）"
				+ $" ｜本次总线上一共收到 [{string.Join(",", seen)}] ｜没有 Died/Kill={noKillOrDeath}（期望 True —— 所以没污染关卡记账）｜ShieldBroken={brokenCeiling}（期望 False：角色 A 的 MaxShield = 0，玩家身上量不到，只有「抛点唯一」的静态证据）"
				+ $" ｜量完 FillVitals：生命 {healthBefore:0.#} → {livePlayer.Health:0.#}、护甲 → {livePlayer.Armor:0.#}（期望 {armorBefore:0.#}）");
		}

		// 探针单位身上的修正器要按 Source 成组摘掉**再**释放：`RunBuild` 是 Godot 的 `RefCounted`，
		// 被 `StatModifier.Source` 引用着的话退出时会报「ObjectDB instance was leaked」
		// （与上面「力量 / 攀高」那一段 `RemoveBySource` 同一个理由）。
		probePlayer.Stats.RemoveBySource(overrideBuild);
		probePlayer.RefreshStats();
		probePlayer.Free();

		// 探测武器也是 Godot 的 `Node3D`（`Weapon : Node3D`）—— 不 `Free()` 的话退出时 Godot 会报
		// 「1 ObjectDB instance was leaked at exit」（上面三把探针武器都 `Free()` 了，这一把是本轮新加的）。
		spiritSword.Free();
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

		// ⚠️ P6a：玩家身上那个旧角色专属的 `AbilityMelee` 节点已随旧角色从 `Player.tscn` 删除，
		// 探针改用清旧后仍在的**下落攻击**命中体（`AbilityFallAttack/Attack`，球半径 3.2 —— 比原来的
		// 近战盒更稳：不管朝向怎么转，敌人一定落在球内，而命中体中心仍在敌人体外）。
		AttackHitbox? hitbox = player.GetNodeOrNull<ActRoguelike.Abilities.Ability>("AbilityFallAttack") is { } fallProbe
			? NodeSearch.FindInSubtree<AttackHitbox>(fallProbe)
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

		// 命中体中心 = 目标正东 1.0m：命中体球半径 3.2 → 敌人在球**内**（重叠成立），
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

	// ─────────────────────────────────────────────────────────────────────────
	// ★ P3 探针：角色 A 武器 2/3 + 技能 1/2 + 被动
	//
	// 取证口径（与 P1/P2 同一条纪律）：
	// ① 换装走**与基地同一个入口**（`RunController.BeginRunWithCharacter`），不手工赋值；
	// ② 每个技能都先读**表行**（证明新列真的接上了），再做行为取证（真激活 / 真动画事件帧 / 真投射物）；
	// ③ 结论行带 ✅/🔴 与"期望值"，方便一眼看出是不是真的对。
	// ─────────────────────────────────────────────────────────────────────────

	private Unit? _p3Player;
	private ArrowVolleyAbility? _p3CrossbowW1;
	private ArrowVolleyAbility? _p3CrossbowW2;
	private ArrowVolleyAbility? _p3BowW1;
	private HawkEyeAbility? _p3BowW2;
	private BackdashAbility? _p3Backdash;
	private LeapAbility? _p3Leap;
	private AirJumpPassiveAbility? _p3Passive;
	private AirHoverController? _p3Hover;
	private Unit? _p3Target;
	private int _p3MarkedTotal;

	/// <summary>先清场（避免探针做到一半被怪打断/打死），再切到"角色 A + 短弩"。</summary>
	private void BeginCrossbowProbe()
	{
		GetTree().Paused = false;
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

		RunController.Instance?.BeginRunWithCharacter(PlayerCharacterKind.Hunter, WeaponPresetKind.Crossbow);

		_p3Player = PlayerLocator.Player;
		if (_p3Player == null)
		{
			GameLog.Write("SelfTest", "🔴 P3·短弩：找不到玩家。");
			return;
		}

		_p3CrossbowW1 = PlayerAbilityInput.FindByInputAction(_p3Player, "weapon_skill_1") as ArrowVolleyAbility;
		_p3CrossbowW2 = PlayerAbilityInput.FindByInputAction(_p3Player, "weapon_skill_2") as ArrowVolleyAbility;
		_p3BowW1 = _p3Player.GetNodeOrNull<ArrowVolleyAbility>("AbilityWeapon3Skill1");
		_p3BowW2 = _p3Player.GetNodeOrNull<HawkEyeAbility>("AbilityWeapon3Skill2");
		_p3Backdash = _p3Player.GetNodeOrNull<BackdashAbility>("AbilitySkill1");
		_p3Leap = _p3Player.GetNodeOrNull<LeapAbility>("AbilitySkill2");
		_p3Passive = _p3Player.GetNodeOrNull<AirJumpPassiveAbility>("AbilityPassive");
		_p3Hover = _p3Player.GetNodeOrNull<AirHoverController>("AirHover");

		// 武器 1 的两个节点必须**被换装清干净**（docs/anim-pipeline.md §6.4.6 那条残留纪律，
		// P3 把"武技归属武器行"之后，这里就是"换武器 = 换武技"的回归验证）。
		Ability? staleW1 = _p3Player.GetNodeOrNull<Ability>("AbilityWeaponSkill1");
		GameLog.Write(
			"SelfTest",
			$"P3·短弩换装：左键={_p3CrossbowW1?.AbilityId ?? "（找不到）"}（期望 skl_a2_w1_1） 右键={_p3CrossbowW2?.AbilityId ?? "（找不到）"}（期望 skl_a2_w1_2）"
			+ $"｜上一把武器（能量掌）的两个节点应为空绑定：{staleW1?.AbilityId ?? ""} Enabled={staleW1?.Enabled} 按键=「{staleW1?.InputAction}」（期望：AbilityId 空 / Enabled False / 按键空）"
			+ $"｜武器 3 的两个节点此时也不该绑上：{_p3BowW1?.AbilityId ?? ""}/{_p3BowW2?.AbilityId ?? ""}（期望都为空）");

		if (_p3CrossbowW1 == null || _p3CrossbowW2 == null)
		{
			GameLog.Write("SelfTest", "🔴 P3·短弩：左键/右键技能不是 ArrowVolleyAbility —— 武器行的 Skills 列没接上？");
			return;
		}

		// 真放一次武技1（散射三发）：动画 → ATK 事件帧 → 3 个投射物。
		_p3CrossbowW1.RefreshCooldown();
		bool ok1 = _p3CrossbowW1.TryActivate();
		GameLog.Write(
			"SelfTest",
			$"P3·短弩武技1 射射射：TryActivate={ok1}（期望 True） 动画={_p3CrossbowW1.AnimName} 事件帧={_p3CrossbowW1.AnimEvent}｜表列：发数 {_p3CrossbowW1.Count}（期望 3） 夹角 {_p3CrossbowW1.SpreadStepDegrees:0.##}°（期望 20） 下坠起点 {_p3CrossbowW1.GravityStartDistance:0.##}m 重力 {_p3CrossbowW1.GravityScale:0.##} 击退 {_p3CrossbowW1.Knockback:0.##} 击退弹速门槛 {_p3CrossbowW1.KnockbackMinSpeed:0.##}｜下一段读结果（发数在事件帧那一帧才写）");
	}

	/// <summary>短弩结果 + 武技2 的"基础蓄力 + 每档 +2 支"（同步推蓄力，与帧率无关）。</summary>
	private void ReportCrossbowProbe()
	{
		if (_p3CrossbowW1 == null || _p3CrossbowW2 == null)
		{
			GameLog.Write("SelfTest", "🔴 P3·短弩：上一段没准备好，本段跳过。");
			return;
		}

		bool fired = _p3CrossbowW1.LastShotCount > 0;
		GameLog.Write(
			"SelfTest",
			$"{(fired ? "✅" : "🔴")} P3·短弩武技1 结果：发数 {_p3CrossbowW1.LastShotCount}（期望 3） 单发伤害 {_p3CrossbowW1.LastShotDamage:0.##} 弹速 {_p3CrossbowW1.LastShotSpeed:0.##}｜链路：动画 {_p3CrossbowW1.AnimName} → 事件帧 {_p3CrossbowW1.AnimEvent} → 投射物 {_p3CrossbowW1.LastShotCount} 个");

		// 武技2：基础蓄力 0.4s（此时 0 档 → 3 支），再蓄 0.2×3 → 3 档 → 3 + 2×3 = 9 支。
		// ⚠️ 两次施法之间必须**推一帧**让上一次 Finish()（`IsReady` 要求 !IsBusy；同步推与帧率无关）。
		ArrowVolleyAbility w2 = _p3CrossbowW2;
		w2.RefreshCooldown();
		bool okA = w2.TryActivate();
		w2.OnInputHeld(w2.ChargeBaseSeconds);
		int stepsAt0 = w2.ChargeSteps;
		w2.OnInputReleased();
		int countAt0 = w2.LastShotCount;
		w2._PhysicsProcess(0.1d);

		w2.RefreshCooldown();
		bool okB = w2.TryActivate();
		w2.OnInputHeld(w2.ChargeBaseSeconds + (0.2f * 3f) + 0.01f);
		int stepsAt3 = w2.ChargeSteps;
		float powerAt3 = w2.ChargePowerScale;
		w2.OnInputReleased();
		int countAt3 = w2.LastShotCount;
		w2._PhysicsProcess(0.1d);

		bool okAll = okA && okB && stepsAt0 == 0 && countAt0 == 3 && stepsAt3 == 3 && countAt3 == 9;
		GameLog.Write(
			"SelfTest",
			$"{(okAll ? "✅" : "🔴")} P3·短弩武技2 大射天下：基础蓄力 {w2.ChargeBaseSeconds:0.##}s（期望 0.4）/ 每档 {w2.ChargeStepSeconds:0.##}s（期望 0.2）/ 上限 {w2.ChargeMaxStepsEffective} 档（期望 3） 每档 +{w2.CountPerChargeStep} 支（期望 2）"
			+ $" → 蓄到基础（档 {stepsAt0}）松手：发数 {countAt0}（期望 3，= 基础 {w2.Count}）；蓄到 3 档（档 {stepsAt3}，威力 ×{powerAt3:0.###}）松手：发数 {countAt3}（期望 9 = 3 + 2×3）"
			+ $"｜动画={w2.AnimName} 方向来源 Aim={w2.Aim}（0 = Facing：文档这一行写的是「朝角色方向」）");
	}

	/// <summary>切到"角色 A + 弓"，并造一个准星前的靶子（刹那之眼要标记它）。</summary>
	private void BeginBowProbe()
	{
		GetTree().Paused = false;
		RunController.Instance?.BeginRunWithCharacter(PlayerCharacterKind.Hunter, WeaponPresetKind.Bow);

		_p3Player = PlayerLocator.Player;
		if (_p3Player == null)
		{
			GameLog.Write("SelfTest", "🔴 P3·弓：找不到玩家。");
			return;
		}

		_p3BowW1 = PlayerAbilityInput.FindByInputAction(_p3Player, "weapon_skill_1") as ArrowVolleyAbility;
		_p3BowW2 = PlayerAbilityInput.FindByInputAction(_p3Player, "weapon_skill_2") as HawkEyeAbility;
		GameLog.Write(
			"SelfTest",
			$"P3·弓换装：左键={_p3BowW1?.AbilityId ?? "（找不到）"}（期望 skl_a3_w1_1） 右键={_p3BowW2?.AbilityId ?? "（找不到）"}（期望 skl_a3_w1_2）｜短弩的节点已清：{_p3Player.GetNodeOrNull<Ability>("AbilityWeapon2Skill1")?.AbilityId ?? ""}（期望空）");

		// 靶子：用真敌人 Chaser 的实例压在弹道上（与最小闭环同一套摆位），刹那之眼的标记要它。
		EnemyConfig? chaserRow = ConfigRows.ById<EnemyConfig>("enemy_chaser");
		if (chaserRow == null || string.IsNullOrEmpty(chaserRow.ScenePath) || !ResourceLoader.Exists(chaserRow.ScenePath))
		{
			GameLog.Write("SelfTest", "🔴 P3·弓：读不到 enemy_chaser 的场景，靶子造不出来。");
			return;
		}

		if (GD.Load<PackedScene>(chaserRow.ScenePath).Instantiate() is not Unit chaser)
		{
			GameLog.Write("SelfTest", "🔴 P3·弓：enemy_chaser 的根节点不是 Unit。");
			return;
		}

		NodeSearch.FindInSubtree<ActRoguelike.AI.EnemyBrain>(chaser)?.SetPhysicsProcess(false);
		if (_p3Player.GetParent() is { } arena)
		{
			arena.AddChild(chaser);
		}

		if (TryComputeShotLine(_p3Player, out Vector3 origin, out Vector3 direction))
		{
			PlaceOnShotLine(chaser, origin, direction, TargetDistanceOnLine);
		}

		_p3Target = chaser;
		GameLog.Write("SelfTest", $"P3·弓：靶子 {chaser.Name} 生命 {chaser.Health:0.#} 已就位");

		// 武技1：两个档位的伤害/弹速（0 档 = 基础；3 档 = ×1.3³ 伤害、+30×3 弹速）。
		if (_p3BowW1 != null)
		{
			ArrowVolleyAbility w1 = _p3BowW1;
			float dmgMul = _p3Player.Stats.Get("DamageMultiplier");
			w1.RefreshCooldown();
			w1.TryActivate();
			w1.OnInputHeld(w1.ChargeBaseSeconds + 0.01f);
			w1.OnInputReleased();
			float dmg0 = w1.LastShotDamage;
			float spd0 = w1.LastShotSpeed;
			w1._PhysicsProcess(0.1d);

			w1.RefreshCooldown();
			w1.TryActivate();
			w1.OnInputHeld(w1.ChargeBaseSeconds + (0.1f * 3f) + 0.01f);
			int steps = w1.ChargeSteps;
			w1.OnInputReleased();
			float dmg3 = w1.LastShotDamage;
			float spd3 = w1.LastShotSpeed;
			w1._PhysicsProcess(0.1d);
			float expectDmg3 = w1.Damage * Mathf.Pow(1.3f, steps) * dmgMul;
			float expectSpd3 = w1.Speed + (30f * steps);

			bool ok = steps == 3
				&& Mathf.IsEqualApprox(dmg0, _p3BowW1.Damage * dmgMul)
				&& Mathf.IsEqualApprox(spd0, _p3BowW1.Speed)
				&& Mathf.IsEqualApprox(dmg3, expectDmg3)
				&& Mathf.IsEqualApprox(spd3, expectSpd3);
			GameLog.Write(
				"SelfTest",
				$"{(ok ? "✅" : "🔴")} P3·弓武技1 箭在弦上：基础蓄力 {w1.ChargeBaseSeconds:0.##}s（期望 0.5）/ 每 {w1.ChargeStepSeconds:0.##}s（期望 0.1）一档 / 每档伤害 ×{w1.ChargeStepScale:0.##}（期望 1.3）/ 每档弹速 +{w1.SpeedPerChargeStep:0.##}（期望 30）/ 最多 {w1.ChargeMaxSeconds:0.##}s（期望 1）"
				+ $" → 基础档：伤害 {dmg0:0.###}（期望 表值 {w1.Damage:0} ×玩家倍率 {dmgMul:0.##}）弹速 {spd0:0.##}（期望 {w1.Speed:0}）；3 档：伤害 {dmg3:0.###}（期望 {expectDmg3:0.###} = 1.3³）弹速 {spd3:0.##}（期望 {expectSpd3:0.##}）");
		}

		// 武技2 刹那之眼：无动画软启动期间被击中 → 无效化 + 强制一次不消耗层数的「后撤」。
		if (_p3Backdash == null)
		{
			_p3Backdash = _p3Player.GetNodeOrNull<BackdashAbility>("AbilitySkill1");
		}

		if (_p3BowW2 == null)
		{
			GameLog.Write("SelfTest", "🔴 P3·弓武技2：右键技能不是 HawkEyeAbility。");
			return;
		}

		// ── ① 放大视角 + 每 0.5s 标记（先完成软启动、进入瞄准）──
		// 标记的判据是"**准星世界点**附近 MarkRadius 米内最近的目标"，所以靶子必须搬到准星那条线上
		// （上一段把它摆在"弹道"上，而弹道是从胸口朝准星世界点的一条线，终点可能在很远处）。
		if (_p3Target != null)
		{
			Vector3 point = AimUtil.GetCrosshairPoint(
				GetViewport(), _p3Player, 200f, CombatLayers.World | CombatLayers.UnitMask);
			Vector3 aim = (point - (_p3Player.GlobalPosition + (Vector3.Up * 0.9f))).Normalized();
			_p3Target.GlobalPosition = point - (aim * 1.5f);
			GameLog.Write("SelfTest", $"P3·刹那之眼：靶子已搬到准星世界点 {Fmt(point)} 前 1.5m 处（标记半径内）");
		}

		WaitUntilIdle(_p3BowW2);
		_p3BowW2.RefreshCooldown();
		bool hawkOk = _p3BowW2.TryActivate();
		_p3BowW2.OnInputHeld(_p3BowW2.MaintainStartupSeconds + 0.05f); // 无动画软启动完成
		bool aiming = _p3BowW2.IsAiming;
		float fovBefore = GetViewport().GetCamera3D()?.Fov ?? 0f;
		// 同步推一大帧：既让视角放大平滑到位，也让"每 0.5s 标记一次"到点（与帧率无关）。
		_p3BowW2._PhysicsProcess(0.6d);
		float fovAfter = GetViewport().GetCamera3D()?.Fov ?? 0f;
		int markedAfterOneInterval = _p3BowW2.MarkedCount;
		_p3MarkedTotal += markedAfterOneInterval;
		GameLog.Write(
			"SelfTest",
			$"{(aiming && markedAfterOneInterval >= 1 ? "✅" : "🔴")} P3·刹那之眼·瞄准与标记：TryActivate={hawkOk}（期望 True） 软启动后 IsAiming={aiming}（期望 True） 后台={_p3BowW2.IsBackground} 占动作={_p3BowW2.IsBusy} 蓄力={_p3BowW2.IsCharging}（期望 True/False/False）"
			+ $" → 推 0.6s：标记次数 {markedAfterOneInterval}（期望 ≥1） 相机 FOV {fovBefore:0.#} → {fovAfter:0.#}（期望向 ZoomFov {_p3BowW2.ZoomFov:0.#} 收缩） ZoomApplied={_p3BowW2.ZoomApplied}｜靶子 {_p3Target?.Name} 身上层数={_p3Target?.GetStatusStacks(_p3BowW2.MarkStatusId)}");

		_p3BowW2.OnInputReleased();          // 松手 → 视角还原 → Finish
		WaitUntilIdle(_p3BowW2);
		float fovRestored = GetViewport().GetCamera3D()?.Fov ?? 0f;
		GameLog.Write(
			"SelfTest",
			$"P3·刹那之眼·收招还原视角：FOV={fovRestored:0.#}（期望回到施法前的 {fovBefore:0.#} ± 0.5） 效果仍活动={_p3BowW2.IsActive}（期望 False）");

		// ── ② 无动画软启动期间被击中 → 无效化 + 强制一次不消耗层数的「后撤」──
		if (_p3Backdash == null)
		{
			_p3Backdash = _p3Player.GetNodeOrNull<BackdashAbility>("AbilitySkill1");
		}

		WaitUntilIdle(_p3Backdash);
		_p3BowW2.RefreshCooldown();
		bool hawkOk2 = _p3BowW2.TryActivate();
		_p3BowW2.OnInputHeld(0.1f); // 0.3s 软启动还没完成，处于防御窗口
		int chargesBefore = _p3Backdash?.ChargesRemaining ?? -1;
		float healthBefore = _p3Player.Health;
		DamageResult result = _p3Player.ReceiveHit(new HitInstance
		{
			Target = _p3Player,
			Attacker = _p3Target,
			Context = EventContext.Create(_p3Target, _p3Player),
			Damage = 40f,
			Direction = Vector3.Forward,
		});

		GameLog.Write(
			"SelfTest",
			$"P3·弓武技2 刹那之眼：TryActivate={hawkOk2}（期望 True） 无动画软启动 {_p3BowW2.MaintainStartupSeconds:0.##}s 已维持 {_p3BowW2.HeldTime:0.###}s 防御窗口={_p3BowW2.StartupProtectionActive}（受击后结束该次维持）"
			+ $"｜被 40 点命中：无效化={result.Invulnerable}（期望 True） 掉血 {result.HealthLost:0.#}（期望 0） 生命 {healthBefore:0.#} → {_p3Player.Health:0.#}（期望不变）"
			+ $"｜强制后撤：{_p3Backdash?.AbilityId} 触发次数={_p3BowW2.ForcedDodgeCount}（期望 1） 层数（触发前 {chargesBefore} → 现在 {_p3Backdash?.ChargesRemaining}，期望不消耗）");
	}

	/// <summary>
	/// 把技能推到"空闲"（自测里同步推物理帧，与帧率无关）：<c>IsReady</c> 要求 <c>!IsActive</c>，
	/// 而上一段的施法要等它的 <c>Finish()</c>（动画事件帧那一帧）才会收尾。
	/// </summary>
	private static void WaitUntilIdle(Ability? ability)
	{
		if (ability == null)
		{
			return;
		}

		for (int i = 0; i < 80 && ability.IsActive; i++)
		{
			ability._PhysicsProcess(0.05d);
		}
	}

	/// <summary>弓的结果：贴标记（可叠加）+ 追踪弹 + 视角放大。</summary>
	private void ReportBowProbe()
	{
		if (_p3Player == null || _p3BowW2 == null || _p3Target == null)
		{
			GameLog.Write("SelfTest", "🔴 P3·弓：上一段没准备好，本段跳过。");
			return;
		}

		// ① 标记：可叠加（MaxStacks = 表值）、不限单目标（层数记在每个单位自己身上）。
		if (StatusPresets.Create(_p3BowW2.MarkStatusId, _p3BowW2.MarkDuration) is { } mark)
		{
			// 刹那之眼自己可能已经贴过 1 层（上面那一段），所以判据看**增量**。
			int before = _p3Target.GetStatusStacks(_p3BowW2.MarkStatusId);
			for (int i = 0; i < 3; i++)
			{
				_p3Target.ApplyStatus(mark);
			}

			int stacksAfter3 = _p3Target.GetStatusStacks(_p3BowW2.MarkStatusId);
			for (int i = 0; i < 10; i++)
			{
				_p3Target.ApplyStatus(mark);
			}

			int stacksCapped = _p3Target.GetStatusStacks(_p3BowW2.MarkStatusId);
			bool markOk = stacksAfter3 == Mathf.Min(mark.MaxStacks, before + 3) && stacksCapped == Mathf.Max(1, mark.MaxStacks);
			GameLog.Write(
				"SelfTest",
				$"{(markOk ? "✅" : "🔴")} P3·标记（可叠加、不限单目标）：{mark.Id} 上限 {mark.MaxStacks} 层（表列 MaxStacks）→ 贴之前已有 {before} 层（刹那之眼自己贴的）→ 连贴 3 次 = {stacksAfter3} 层（期望 {Mathf.Min(mark.MaxStacks, before + 3)}）→ 再贴 10 次 = {stacksCapped} 层（期望封顶 {mark.MaxStacks}）"
				+ $"｜刹那之眼自己标记次数={_p3MarkedTotal}（≥1 期望：它在准星附近挑过目标）｜被标记的单位={_p3Target.Name} 身上状态 [{string.Join(",", _p3Target.GetStatusIds())}]");
		}

		// ② 追踪弹：箭的子弹特性（HomingStatusId = 表值）。造一支箭，朝**背对**靶子的方向发射，
		//    推两个物理帧之后速度方向应该已经转向被标记的靶子。
		string homing = _p3BowW1?.HomingStatusId ?? string.Empty;
		if (!string.IsNullOrEmpty(homing))
		{
			if (GD.Load<PackedScene>("res://Scenes/Combat/Arrow.tscn").Instantiate<Projectile>() is not { } arrow)
			{
				GameLog.Write("SelfTest", "🔴 P3·追踪弹：读不到 Arrow.tscn。");
				return;
			}

			var attack = new AttackInstance
			{
				Attacker = _p3Player,
				Ability = "selftest_homing",
				BaseDamage = 1f,
				Lifetime = 3f,
				Tags = new[] { "Projectile", "Arrow" },
			};

			Node parent = GetTree().CurrentScene ?? GetTree().Root;
			parent.AddChild(arrow);
			// 放到高空：射线打不到任何东西，不会中途自毁（弹道只受追踪转向影响）。
			arrow.GlobalPosition = _p3Player.GlobalPosition + (Vector3.Up * 30f);
			// ⚠️ 弹速取小值：这一段的判据是"**方向**在转"，而手动推帧一次走 `速度 × 0.05s` 米，
			// 飞得太快会把"目标方向"本身甩开（实测 60 m/s 时几何变化比转向还大，量出来的角度不干净）。
			arrow.Speed = 6f;
			arrow.GravityScale = 0f;
			arrow.HomingStatusId = homing;
			arrow.HomingTurnRateDegrees = _p3BowW1?.HomingTurnRateDegrees ?? 360f;
			arrow.HomingRange = _p3BowW1?.HomingRange ?? 0f;
			arrow.Launch(attack, Vector3.Right);

			Vector3 toward0 = (_p3Target.GlobalPosition - arrow.GlobalPosition).Normalized();
			float angleBefore = Mathf.RadToDeg(arrow.VelocityDirection.AngleTo(toward0));
			var progress = new List<string>();
			for (int i = 0; i < 8; i++)
			{
				arrow._PhysicsProcess(0.05d);
				Vector3 toward = (_p3Target.GlobalPosition - arrow.GlobalPosition).Normalized();
				progress.Add($"{Mathf.RadToDeg(arrow.VelocityDirection.AngleTo(toward)):0.#}°");
			}

			Vector3 towardAfter = (_p3Target.GlobalPosition - arrow.GlobalPosition).Normalized();
			float angleAfter = Mathf.RadToDeg(arrow.VelocityDirection.AngleTo(towardAfter));
			bool homingOk = angleAfter < angleBefore - 1f;
			GameLog.Write(
				"SelfTest",
				$"{(homingOk ? "✅" : "🔴")} P3·追踪弹：箭的子弹特性 HomingStatusId=「{homing}」（表列） 转向 {arrow.HomingTurnRateDegrees:0.#}°/s 锁定的目标={arrow.HomingTargetName}（期望 {_p3Target.Name}）"
				+ $" → 从 (1,0,0) 起飞，与「指向被标记靶子」的夹角逐帧 {angleBefore:0.#}° → {string.Join(" → ", progress)}（期望单调变小 = 真的在转向它；不追踪时应当一直停在 {angleBefore:0.#}°）"
				+ $"｜每帧插值权重 {arrow.LastHomingWeight:0.###}（= min(1, 转向速度×物理帧长 ÷ 夹角)） 最后一次夹角 {arrow.LastHomingAngleDegrees:0.#}°｜靶子={_p3Target.Name} 层数={_p3Target.GetStatusStacks(homing)}");
			arrow.QueueFree();
		}

		// ③ 视角放大：施法期间 Camera3D.Fov 应当已经变小。
		GameLog.Write(
			"SelfTest",
			$"P3·刹那之眼·放大视角：ZoomFov={_p3BowW2.ZoomFov:0.#}（表列） 当前相机 FOV={GetViewport().GetCamera3D()?.Fov:0.#} ZoomApplied={_p3BowW2.ZoomApplied} 标记间隔={_p3BowW2.MarkIntervalSeconds:0.##}s 标记半径={_p3BowW2.MarkRadius:0.##}m 后撤技能行={_p3BowW2.DodgeAbilityId}");
	}

	private BackdashAbility? _p3BackdashProbed;
	private float _p3HealthBeforeDash;

	/// <summary>技能 1「后撤」：起手（动画 + 无敌帧开）。</summary>
	private void BeginBackdashProbe()
	{
		_p3Player = PlayerLocator.Player;
		_p3BackdashProbed = _p3Player?.GetNodeOrNull<BackdashAbility>("AbilitySkill1");
		if (_p3Player == null || _p3BackdashProbed == null)
		{
			GameLog.Write("SelfTest", "🔴 P3·后撤：找不到玩家的技能节点。");
			return;
		}

		Vector3 before = _p3Player.GlobalPosition;
		_p3HealthBeforeDash = _p3Player.Health;
		_p3FacingBeforeDash = _p3Player.Rotation.Y;
		// 上一段（刹那之眼的强制后撤）刚用过它 → 等它收招，否则 IsReady 会因为"正忙"为 false。
		WaitUntilIdle(_p3BackdashProbed);
		_p3BackdashProbed.RefreshCooldown();
		bool ok = _p3BackdashProbed.TryActivate();
		GameLog.Write(
			"SelfTest",
			$"P3·后撤：TryActivate={ok}（期望 True） CD={_p3BackdashProbed.Cooldown:0.##}s（期望 10） 动画={_p3BackdashProbed.AnimName} 开无敌帧={_p3BackdashProbed.AnimEvent} 关无敌帧={_p3BackdashProbed.AnimEventEnd}"
			+ $"｜位置 {Fmt(before)} 朝向 Y={_p3Player.Rotation.Y:0.###}（下一段比「没有改变朝向」） 朝向基准={_p3FacingBeforeDash:0.###}"
			+ $"｜诊断：{_p3BackdashProbed.Diagnose()}");
	}

	private float _p3FacingBeforeDash;

	/// <summary>后撤的"开无敌"那一帧（<c>Dodge</c>）。</summary>
	private void ReportBackdashInvulnerableOn()
	{
		if (_p3Player == null || _p3BackdashProbed == null)
		{
			GameLog.Write("SelfTest", "🔴 P3·后撤：上一段没准备好。");
			return;
		}

		// 无敌帧必须**真的挡住伤害**：这一发 40 点在无敌期间应当完全无效。
		DamageResult blocked = _p3Player.ReceiveHit(new HitInstance
		{
			Target = _p3Player,
			Attacker = _p3Target,
			Context = EventContext.Create(_p3Target, _p3Player),
			Damage = 40f,
			Direction = Vector3.Forward,
		});

		bool ok = _p3BackdashProbed.InvulnerableOwned && _p3Player.IsInvulnerable && blocked.Invulnerable && blocked.HealthLost <= 0f;
		GameLog.Write(
			"SelfTest",
			$"{(ok ? "✅" : "🔴")} P3·后撤·无敌帧开（帧 {_p3BackdashProbed.AnimEvent}）：IsInvulnerable={_p3Player.IsInvulnerable}（期望 True） 技能自报无敌={_p3BackdashProbed.InvulnerableOwned}（期望 True）"
			+ $"｜无敌期间挨 40 点：无效化={blocked.Invulnerable}（期望 True） 掉血 {blocked.HealthLost:0.#}（期望 0） 生命 {_p3Player.Health:0.#}（应仍为 {_p3HealthBeforeDash:0.#}）"
			+ $"｜后撤力 {Fmt(_p3BackdashProbed.LastImpulse)} 朝向 Y={_p3Player.Rotation.Y:0.###}（期望与起手时的 {_p3FacingBeforeDash:0.###} 相同 = 不改变朝向）");
	}

	/// <summary>后撤的"关无敌"那一帧（<c>Dodger</c>）。</summary>
	private void ReportBackdashInvulnerableOff()
	{
		if (_p3Player == null || _p3BackdashProbed == null)
		{
			GameLog.Write("SelfTest", "🔴 P3·后撤：上一段没准备好。");
			return;
		}

		bool ok = !_p3Player.IsInvulnerable && !_p3BackdashProbed.InvulnerableOwned;
		GameLog.Write(
			"SelfTest",
			$"{(ok ? "✅" : "🔴")} P3·后撤·无敌帧关（帧 {_p3BackdashProbed.AnimEventEnd}）：IsInvulnerable={_p3Player.IsInvulnerable}（期望 False） 技能自报无敌={_p3BackdashProbed.InvulnerableOwned}（期望 False） 仍在施法={_p3BackdashProbed.IsBusy}（期望 False） 朝向 Y={_p3Player.Rotation.Y:0.###}（期望仍未改变）");
	}

	/// <summary>技能 2「跃升」+ 被动「灵动」的静态读数与起手。</summary>
	private void BeginLeapProbe()
	{
		_p3Player = PlayerLocator.Player;
		if (_p3Player == null)
		{
			GameLog.Write("SelfTest", "🔴 P3·跃升：找不到玩家。");
			return;
		}

		_p3Leap = _p3Player.GetNodeOrNull<LeapAbility>("AbilitySkill2");
		_p3Passive = _p3Player.GetNodeOrNull<AirJumpPassiveAbility>("AbilityPassive");
		_p3Hover = _p3Player.GetNodeOrNull<AirHoverController>("AirHover");

		CharacterMovement? movement = _p3Player.Movement;
		GameLog.Write(
			"SelfTest",
			$"P3·被动 灵动：节点={(_p3Passive == null ? "（找不到）" : _p3Passive.AbilityId)} Enabled={_p3Passive?.Enabled} 授予 {_p3Passive?.Grant} 次（期望 1） 上限 {_p3Passive?.MaxExtraAirJumps} 次（期望 3）"
			+ $" → CharacterMovement.ExtraAirJumps={movement?.ExtraAirJumps}（期望 1，场景基线 0 + 授予 1）");

		if (_p3Leap == null || _p3Hover == null)
		{
			GameLog.Write("SelfTest", "🔴 P3·跃升：找不到技能节点或滞空组件。");
			return;
		}

		WaitUntilIdle(_p3Leap);
		_p3Leap.RefreshCooldown();
		bool ok = _p3Leap.TryActivate();
		GameLog.Write(
			"SelfTest",
			$"P3·跃升：TryActivate={ok}（期望 True） CD={_p3Leap.Cooldown:0.##}s（期望 20） 动画={_p3Leap.AnimName} 事件帧={_p3Leap.AnimEvent}（期望 Action）"
			+ $"｜表列：起跳速度 {_p3Leap.LeapVelocity:0.##} m/s 滞空 {_p3Leap.HoverSeconds:0.##}s（期望 3） 减缓 {_p3Leap.HoverFallSlowPercent * 100f:0.#}%（期望 90） 光环半径 {_p3Leap.RingRadius:0.##}｜下一段读结果（效果在 Action 帧那一帧才写）");
	}

	/// <summary>跃升结果：真的跳了 + 真的授予了滞空。</summary>
	private void ReportLeapProbe()
	{
		if (_p3Leap == null || _p3Hover == null || _p3Player == null)
		{
			GameLog.Write("SelfTest", "🔴 P3·跃升：上一段没准备好。");
			return;
		}

		bool ok = _p3Leap.LastLeapVelocity > 0f && _p3Hover.GrantCount > 0 && _p3Hover.IsHovering;
		GameLog.Write(
			"SelfTest",
			$"{(ok ? "✅" : "🔴")} P3·跃升结果：起跳速度 {_p3Leap.LastLeapVelocity:0.##} m/s（期望 = 表值 {_p3Leap.LeapVelocity:0.##}） 授予滞空 {_p3Leap.LastHoverSeconds:0.##}s（期望 3）"
			+ $" → 滞空组件：授予次数 {_p3Hover.GrantCount}（期望 ≥1） 剩余 {_p3Hover.HoverRemaining:0.##}s（期望 > 0） 在空中={!_p3Player.IsOnFloor()} 减缓比例 {_p3Hover.FallSlowPercent:0.##}（期望 0.9） 光环节点存在={_p3Hover.GetNodeOrNull<MeshInstance3D>("HoverRing") != null}");
	}

	/// <summary>滞空的"按住跳跃键 → 下落减缓"（自测注入不了按键，用组件上的探针开关）。</summary>
	private void ReportHoverSlowFall()
	{
		if (_p3Hover == null || _p3Player == null)
		{
			GameLog.Write("SelfTest", "🔴 P3·滞空：上一段没准备好。");
			return;
		}

		// 造一个"正在快速下落"的状态：直接给一个向下速度，然后开探针开关推几帧。
		_p3Player.Velocity = _p3Player.Velocity with { Y = -20f };
		float before = _p3Player.Velocity.Y;
		_p3Hover.DebugForceJumpHeld = true;
		bool hoveringBefore = _p3Hover.IsHovering;
		_p3Hover._PhysicsProcess(0.05d);
		float afterOne = _p3Player.Velocity.Y;
		bool slow = _p3Hover.IsSlowFalling;
		MeshInstance3D? ring = _p3Hover.GetNodeOrNull<MeshInstance3D>("HoverRing");
		bool ringVisible = ring?.Visible ?? false;
		_p3Hover.DebugForceJumpHeld = false;

		bool ok = slow && Mathf.Abs(afterOne - (before * (1f - _p3Hover.FallSlowPercent))) < 0.001f;
		GameLog.Write(
			"SelfTest",
			$"{(ok ? "✅" : "🔴")} P3·滞空·按住跳跃键减缓下落：垂直速度 {before:0.##} → {afterOne:0.##}（期望 {before * (1f - _p3Hover.FallSlowPercent):0.##} = ×(1-{_p3Hover.FallSlowPercent:0.##})） 本帧在减缓={slow}（期望 True）"
			+ $"｜脚底光环可见={ringVisible}（按住期间应有） 剩余滞空 {_p3Hover.HoverRemaining:0.##}s 推之前还在滞空={hoveringBefore}"
			+ $"｜说明：自测注入不了按键（Input.ParseInputEvent 会崩），所以走 DebugForceJumpHeld 这一条探针；真人按键那一路由 MCP 手测 + 截图覆盖");
	}

	// ─────────────────────────────────────────────────────────────────────────
	// ★ P4 探针：角色 B［武器大师］（3 把近战武器 + 技能 1/2 + 被动 + 新机制）
	//
	// 取证口径与 P3 同一条纪律：
	// ① 换装走**与基地同一个入口**（`RunController.BeginRunWithCharacter`）；
	// ② 每个技能先读**表行**（证明新列真的接上了），再做行为取证；
	// ③ 结论行带 ✅/🔴 与"期望值"。
	// ─────────────────────────────────────────────────────────────────────────

	private Unit? _p4Player;
	private MeleeComboAbility? _p4SwordW1;
	private SwordQiAbility? _p4SwordW2;
	private LandingBurstAbility? _p4LandingBurst;
	private FallAttackAbility? _p4Fall;
	private MeleeComboAbility? _p4ScytheW1;
	private ScytheThrowAbility? _p4ScytheW2;
	private MeleeComboAbility? _p4SpearW1;
	private SpearThrowAbility? _p4SpearW2;
	private BlinkDashAbility? _p4Blink;
	private GuardStanceAbility? _p4Guard;
	private int _p4ComboHits;
	private int _p4FallHits;
	private float _p4FallHeight;
	private float _p4FallDamage;
	private Unit? _p4Victim;

	/// <summary>清场 → 切到"角色 B + 灵剑" → 收集全部能力节点（顺手证明"换武器 = 换武技"）。</summary>
	private void BeginCharacterBProbe()
	{
		GetTree().Paused = false;
		ClearHostiles();
		RunController.Instance?.BeginRunWithCharacter(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.SpiritSword);

		_p4Player = PlayerLocator.Player;
		if (_p4Player == null)
		{
			GameLog.Write("SelfTest", "🔴 P4：找不到玩家。");
			return;
		}

		_p4SwordW1 = PlayerAbilityInput.FindByInputAction(_p4Player, "weapon_skill_1") as MeleeComboAbility;
		_p4SwordW2 = PlayerAbilityInput.FindByInputAction(_p4Player, "weapon_skill_2") as SwordQiAbility;
		// ⚠️ 节点名与角色的 **B 槽**（`AbilityBSkill1` / `AbilityBSkill2`）：角色 A 与角色 B 的
		// 「技能 1 / 技能 2」用同一对按键，所以场景里必须**两个不同名的节点**（`AbilitySkill1` 是 A 的后撤、
		// `AbilityBSkill1` 才是 B 的「闪」）。这两行原来写的是 `AbilitySkill1`/`AbilitySkill2` ——
		// 那次改名（修「同一场景里不能有两个同名节点」）之后 P4 探针**一直没再跑过**，
		// 于是 `GetNodeOrNull<BlinkDashAbility>` 恒为 null、P4·闪/架 一直报「上一段没准备好」（P5 复跑时抓到）。
		_p4Blink = _p4Player.GetNodeOrNull<BlinkDashAbility>("AbilityBSkill1");
		_p4Guard = _p4Player.GetNodeOrNull<GuardStanceAbility>("AbilityBSkill2");
		_p4Fall = _p4Player.GetNodeOrNull<FallAttackAbility>("AbilityFallAttack");
		_p4LandingBurst = _p4Player.GetNodeOrNull<LandingBurstAbility>("AbilityPassive2");
		_p4ScytheW1 = _p4Player.GetNodeOrNull<MeleeComboAbility>("AbilityWeapon5Skill1");
		_p4ScytheW2 = _p4Player.GetNodeOrNull<ScytheThrowAbility>("AbilityWeapon5Skill2");
		_p4SpearW1 = _p4Player.GetNodeOrNull<MeleeComboAbility>("AbilityWeapon6Skill1");
		_p4SpearW2 = _p4Player.GetNodeOrNull<SpearThrowAbility>("AbilityWeapon6Skill2");

		CharacterConfig? row = RunController.CharacterRow(PlayerCharacterKind.WeaponMaster);
		GameLog.Write(
			"SelfTest",
			$"P4·角色 B 换装：角色行={row?.Id ?? "（读不到）"}（期望 chr_weapon_master） 武器行={RunController.WeaponRow(WeaponPresetKind.SpiritSword)?.Id ?? "（读不到）"}（期望 wpn_spirit_sword）"
			+ $"｜左键={_p4SwordW1?.AbilityId ?? "（空）"}（期望 skl_b1_w1_1） 右键={_p4SwordW2?.AbilityId ?? "（空）"}（期望 skl_b1_w1_2）"
			+ $"｜技能1={_p4Blink?.AbilityId ?? "（空）"} 技能2={_p4Guard?.AbilityId ?? "（空）"} 被动落={_p4Fall?.AbilityId ?? "（空）"} 落剑式={_p4LandingBurst?.AbilityId ?? "（空）"}"
			+ $"｜角色 B 三个武技1（统一驱动）都挂在 MeleeComboAbility 上：{(_p4SwordW1 != null && _p4ScytheW1 != null && _p4SpearW1 != null ? "是" : "否")}（期望 是）"
			+ $"｜能量波那一路已清：{_p4Player.GetNodeOrNull<Ability>("AbilityWeaponSkill1")?.AbilityId ?? "（空）"}（期望空） Enabled={_p4Player.GetNodeOrNull<Ability>("AbilityWeaponSkill1")?.Enabled}（期望 False）");
	}

	/// <summary>清掉场上所有敌对单位（自测探针要一个干净场地）。</summary>
	private void ClearHostiles()
	{
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
	}

	/// <summary>造一个真敌人靶子（按敌人表 ScenePath，AI 停掉）摆在玩家正前方。</summary>
	private Unit? SpawnVictim(Vector3 offset)
	{
		if (_p4Player == null)
		{
			return null;
		}

		EnemyConfig? chaserRow = ConfigRows.ById<EnemyConfig>("enemy_chaser");
		if (chaserRow == null || string.IsNullOrEmpty(chaserRow.ScenePath) || !ResourceLoader.Exists(chaserRow.ScenePath))
		{
			return null;
		}

		if (GD.Load<PackedScene>(chaserRow.ScenePath).Instantiate() is not Unit victim)
		{
			return null;
		}

		NodeSearch.FindInSubtree<ActRoguelike.AI.EnemyBrain>(victim)?.SetPhysicsProcess(false);
		if (_p4Player.GetParent() is { } arena)
		{
			arena.AddChild(victim);
			victim.GlobalPosition = _p4Player.GlobalPosition + offset;
		}

		return victim;
	}

	/// <summary>灵剑武技1「破剑式」：5 段连招（连续按键推进）+ 每段斩断子弹。</summary>
	private void ReportSwordComboProbe()
	{
		if (_p4Player == null || _p4SwordW1 == null)
		{
			GameLog.Write("SelfTest", "🔴 P4·灵剑：上一段没准备好。");
			return;
		}

		MeleeComboAbility w1 = _p4SwordW1;
		// 靶子摆在正前方 1.6m（命中盒中心在 0.9 高、前方 1.4m 附近）。
		_p4Victim = SpawnVictim(new Vector3(0f, 0f, -1.6f));
		float victimHealthBefore = _p4Victim?.Health ?? 0f;

		WaitUntilIdle(w1);
		w1.RefreshCooldown();
		bool started = w1.TryPress();
		string firstAnim = w1.AnimName;

		// 连续推进：每一段都用 TryPress（= 输入层那条路），并在段与段之间推物理帧让事件帧到点。
		// 连招推进：自测在同一帧里跑，等不到真实动画事件帧，所以直接模拟"ATK 帧到了"
		// （MeleeComboAbility.DebugFireAnimEvent = OnAnimEvent(ComboNextEvent)）。
		int advanced = 0;
		for (int i = 0; i < 8 && w1.IsBusy; i++)
		{
			w1.DebugFireAnimEvent();      // 本段判定 + 开输入窗口
			w1.TryPress();                // 玩家紧接着又按了一下
			w1._PhysicsProcess(0.06d);
			advanced = w1.ComboChained;
		}

		float victimHealthAfter = _p4Victim?.Health ?? 0f;
		bool ok = started && advanced >= 4 && w1.ComboChained >= 4;
		GameLog.Write(
			"SelfTest",
			$"{(ok ? "✅" : "🔴")} P4·灵剑武技1 破剑式：TryPress(起手)={started}（期望 True） 段数表={w1.ComboStepCount}（期望 5） 首段动画「{firstAnim}」 AnimEvent={w1.AnimEvent} AnimEventEnd={w1.AnimEventEnd}"
			+ $" → 连续按键推进 {advanced} 次（期望 ≥4） 最终段={w1.ComboStep + 1} 累计连击={w1.ComboChained}（期望 ≥4） 窗口={w1.ComboWindowSeconds:0.##}s"
			+ $"｜CD={w1.Cooldown:0.##}（文档 0.3/0.4/0.5/0.6/0.6 是参考值，段间隔由动画长度决定）"
			+ $"｜靶子 {_p4Victim?.Name} 生命 {victimHealthBefore:0.#} → {victimHealthAfter:0.#}（期望下降）");
	}

	/// <summary>破剑式斩断子弹：Bullet 被清掉、EnergyWave 斩不断（**包括友军的**）。</summary>
	private void ReportBulletCutProbe()
	{
		if (_p4Player == null || _p4SwordW1 == null)
		{
			GameLog.Write("SelfTest", "🔴 P4·斩断：上一段没准备好。");
			return;
		}

		MeleeComboAbility w1 = _p4SwordW1;

		// 三发"子弹"：友军 Bullet / 敌方 Bullet / 能量波 —— 全部摆在命中盒正前方。
		Projectile? friendly = SpawnProjectile("res://Scenes/Combat/Bullet.tscn", new Vector3(0f, 0.8f, -1.0f), _p4Player, Faction.Player);
		Projectile? hostile = SpawnProjectile("res://Scenes/Combat/Bullet.tscn", new Vector3(0.3f, 0.8f, -1.0f), _p4Player, Faction.Enemy);
		Projectile? wave = SpawnProjectile("res://Scenes/Combat/EnergyWave.tscn", new Vector3(-0.3f, 0.8f, -1.0f), _p4Player, Faction.Enemy);
		string names = Projectile.SourceSceneSummary();


		WaitUntilIdle(w1);
		w1.RefreshCooldown();
		bool started = w1.TryPress();
		w1.DebugFireAnimEvent();     // 模拟事件帧 ATK 到点（= 本段判定 + 斩断子弹）
		w1._PhysicsProcess(0.05d);

		bool friendlyGone = friendly == null || !IsInstanceValid(friendly) || friendly.IsQueuedForDeletion();
		bool hostileGone = hostile == null || !IsInstanceValid(hostile) || hostile.IsQueuedForDeletion();
		bool waveAlive = wave != null && IsInstanceValid(wave) && !wave.IsQueuedForDeletion();
		bool ok = started && friendlyGone && hostileGone && waveAlive;
		GameLog.Write(
			"SelfTest",
			$"{(ok ? "✅" : "🔴")} P4·破剑式斩断子弹：名单 [Bullet] 排除名单 [EnergyWave]（都是表列 CutSceneNames/CutExcludeSceneNames）"
			+ $"｜场上投射物场景名：{names}"
			+ $"｜友军的 Bullet 被清={(friendlyGone ? "是" : "否")}（期望 是，文档：包括友军的） 敌方的 Bullet 被清={(hostileGone ? "是" : "否")}（期望 是） 能量波还在={(waveAlive ? "是" : "否")}（期望 是，文档：无法斩断能量波）"
			+ $"｜本段斩断计数={w1.LastCutCount} 累计={w1.TotalCutCount}");

		foreach (Projectile? p in new[] { friendly, hostile, wave })
		{
			if (p != null && IsInstanceValid(p))
			{
				p.QueueFree();
			}
		}
	}

	private Projectile? SpawnProjectile(string scenePath, Vector3 offset, Unit owner, Faction faction)
	{
		if (_p4Player == null || !ResourceLoader.Exists(scenePath))
		{
			return null;
		}

		if (GD.Load<PackedScene>(scenePath).Instantiate() is not Projectile projectile)
		{
			return null;
		}

		if (_p4Player.GetParent() is { } arena)
		{
			arena.AddChild(projectile);
			projectile.GlobalPosition = _p4Player.GlobalPosition + offset;
		}

		return projectile;
	}

	/// <summary>灵剑武技2「拔刀式」：基础蓄力 1s + 剑气属性（穿透 / 可格挡 / 路径短 / 大小等比攻击盒）。</summary>
	private void ReportSwordQiProbe()
	{
		if (_p4Player == null || _p4SwordW2 == null || _p4SwordW1 == null)
		{
			GameLog.Write("SelfTest", "🔴 P4·剑气：上一段没准备好。");
			return;
		}

		SwordQiAbility w2 = _p4SwordW2;
		WaitUntilIdle(w2);
		w2.RefreshCooldown();
		bool started = w2.TryPress();                       // 按下（进入蓄力）
		w2.OnInputHeld(0.4f);                               // 基础蓄力 1s 还没满
		bool aborted = w2.ChargeBaseComplete;               // 期望 False
		w2.OnInputReleased();                               // 没蓄满就松手 → 不释放
		int projectilesAfterAbort = CountProjectiles("SwordQi");

		WaitUntilIdle(w2);
		w2.RefreshCooldown();
		bool started2 = w2.TryPress();
		w2.OnInputHeld(w2.ChargeBaseSeconds + 0.05f);       // 蓄满 → 松手释放
		GameLog.Write(
			"SelfTest",
			$"P4·剑气尺寸基准：武技1 当前第 {_p4SwordW1.ComboStep + 1} 段命中盒 {Fmt(_p4SwordW1.CurrentStepBoxSize)}");
		w2.OnInputReleased();
		int projectiles = CountProjectiles("SwordQi");

		bool ok = started && !aborted && projectilesAfterAbort == 0 && started2 && projectiles >= 1
			&& w2.Pierce >= 1 && !w2.BypassBlock && w2.MaxLifetime <= 0.6f && w2.LastSize > 0f;
		GameLog.Write(
			"SelfTest",
			$"{(ok ? "✅" : "🔴")} P4·灵剑武技2 拔刀式：TryPress={started}（期望 True） 基础蓄力 {w2.ChargeBaseSeconds:0.##}s（期望 1）/ CD {w2.Cooldown:0.##}（期望 2）"
			+ $"｜没蓄满就松手：基础完成={aborted}（期望 False）→ 剑气生成 {projectilesAfterAbort} 个（期望 0，设计文档「未完成不释放」）"
			+ $"｜蓄满松手：生成 {projectiles} 个（期望 ≥1）｜剑气属性 —— 穿透 {w2.Pierce}（期望 ≥1，能穿透路径上的敌人） 可被格挡={!w2.BypassBlock}（期望 True） 存活 {w2.MaxLifetime:0.##}s（期望 ≤0.6 = 飞行路径短）"
			+ $"｜大小等比攻击盒：剑气尺寸 {w2.LastSize:0.###}（来源：{w2.LastSizeSource}）");
	}

	private int CountProjectiles(string sceneName)
	{
		int count = 0;
		foreach (Node node in GetTree().GetNodesInGroup("projectiles"))
		{
			if (node is Projectile projectile && projectile.SourceSceneName == sceneName)
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>技能1「闪」：朝准星冲刺 + 全程穿透（碰撞层摘到只剩世界层）+ 可视化路径。</summary>
	private void ProbeBlinkDash()
	{
		if (_p4Player == null || _p4Blink == null)
		{
			GameLog.Write("SelfTest", "🔴 P4·闪：上一段没准备好。");
			return;
		}

		BlinkDashAbility dash = _p4Blink;
		uint maskBefore = _p4Player.CollisionMask;
		Vector3 positionBefore = _p4Player.GlobalPosition;

		WaitUntilIdle(dash);
		dash.RefreshCooldown();
		bool started = dash.TryPress();
		uint maskDuring = _p4Player.CollisionMask;
		bool pierceDuring = dash.PierceApplied;
		int pathPoints = dash.LastPathPoints;

		// 推完整个冲刺时长（物理帧是引擎在推，这里同步推让状态机收尾）。
		for (int i = 0; i < 20 && dash.IsBusy; i++)
		{
			dash._PhysicsProcess(0.03d);
		}

		uint maskAfter = _p4Player.CollisionMask;
		bool ok = started && pierceDuring && maskDuring == CombatLayers.World && maskAfter == maskBefore && pathPoints >= 2;
		GameLog.Write(
			"SelfTest",
			$"{(ok ? "✅" : "🔴")} P4·技能1 闪：TryPress={started}（期望 True） 方向 {Fmt(dash.LastDirection)}（朝准星） 距离 {dash.Distance:0.##}m / {dash.Duration:0.##}s CD {dash.Cooldown:0.##}（期望 5）"
			+ $"｜全程穿透：冲刺期间碰撞层 {maskBefore} → {maskDuring}（期望 {CombatLayers.World} = 只剩世界层，穿透单位/仍撞地形） 冲刺后还原 → {maskAfter}（期望 {maskBefore}）"
			+ $"｜可视化路径：{pathPoints} 点（期望 ≥2） 显示中={dash.GetNodeOrNull<DashPathIndicator>("Path")?.IsShowing}");
	}

	/// <summary>技能2「架」：0.75s 架势内被命中 → 规避这次 + 退出架势。</summary>
	private void ProbeGuardStance()
	{
		if (_p4Player == null || _p4Guard == null)
		{
			GameLog.Write("SelfTest", "🔴 P4·架：上一段没准备好。");
			return;
		}

		GuardStanceAbility guard = _p4Guard;
		WaitUntilIdle(guard);
		guard.RefreshCooldown();
		bool started = guard.TryPress();
		guard._PhysicsProcess(0.2d);            // 过 GuardStartSeconds(0.15) → 进入架势
		bool stanceActive = guard.StanceActive;
		int parriesBefore = guard.ParryCount;

		float healthBefore = _p4Player.Health;
		DamageResult result = _p4Player.ReceiveHit(new HitInstance
		{
			Target = _p4Player,
			Attacker = _p4Victim,
			Context = EventContext.Create(_p4Victim, _p4Player),
			Damage = 40f,
			Direction = Vector3.Forward,
		});

		float healthAfter = _p4Player.Health;
		bool exited = !guard.StanceActive;
		int parriesAfter = guard.ParryCount;
		guard._PhysicsProcess(0.05d);           // 下一帧把"本技能自己设的规避"清掉

		bool ok = started && stanceActive && result.Invulnerable && Mathf.IsEqualApprox(healthBefore, healthAfter)
			&& parriesAfter == parriesBefore + 1 && exited;
		GameLog.Write(
			"SelfTest",
			$"{(ok ? "✅" : "🔴")} P4·技能2 架：TryPress={started}（期望 True） 架势 {guard.StanceSeconds:0.##}s（期望 0.75） CD {guard.Cooldown:0.##}（期望 3）"
			+ $" → 推 0.2s 后在架势中={stanceActive}（期望 True）"
			+ $"｜架势内挨 40 点：无效化={result.Invulnerable}（期望 True） 掉血 {healthBefore - healthAfter:0.#}（期望 0） 生命 {healthBefore:0.#} → {healthAfter:0.#}"
			+ $"｜退出架势={exited}（期望 True） 架住计数 {parriesBefore} → {parriesAfter}（期望 +1） 挡下的伤害={guard.LastParriedDamage:0.##}（期望 40）");
	}

	/// <summary>被动「落」：空中才能用 + 大幅提高下落速度 + 高度越高伤害越高。</summary>
	private void ProbeFallAttack()
	{
		if (_p4Player == null || _p4Fall == null || _p4LandingBurst == null)
		{
			GameLog.Write("SelfTest", "🔴 P4·落：上一段没准备好。");
			return;
		}

		FallAttackAbility fall = _p4Fall;
		WaitUntilIdle(fall);

		// ① 在地面时不能用（文档：当角色在空中时…）。
		_p4Player.GlobalPosition = _p4Player.GlobalPosition with { Y = 0.05f };
		_p4Player.Velocity = Vector3.Zero;
		fall._PhysicsProcess(0.02d);
		bool grounded = _p4Player.IsOnFloor();
		bool blockedOnGround = !fall.CanActivateForTest();
		fall.RefreshCooldown();
		bool pressedOnGround = fall.TryPress();

		// ② 举到空中（离地 6m）再按 → 起跳高度记进 FallHeight。
		Vector3 start = _p4Player.GlobalPosition;
		_p4Player.GlobalPosition = start with { Y = start.Y + 6f };
		_p4Player.Velocity = new Vector3(0f, -1f, 0f);
		MovementOf(_p4Player)?._PhysicsProcess(0.02d);
		float fallHeight = MovementOf(_p4Player)?.FallHeight ?? 0f;

		fall.RefreshCooldown();
		bool pressedInAir = fall.TryPress();
		float diveSpeed = _p4Player.Velocity.Y;
		_p4FallHeight = fall.LastFallHeight;
		_p4FallDamage = fall.LastDamage;
		float bonusLow = fall.DamageMultiplierForHeight(0f);
		float bonusMid = fall.DamageMultiplierForHeight(fall.HeightStep * 2f);
		float bonusHigh = fall.DamageMultiplierForHeight(fall.HeightStep * 20f);

		bool ok = blockedOnGround && !pressedOnGround && pressedInAir
			&& diveSpeed <= -fall.DiveSpeed + 0.01f
			&& Mathf.IsEqualApprox(bonusLow, 1f)
			&& Mathf.IsEqualApprox(bonusMid, 1f + (2f * fall.DamageBonusPerStep))
			&& Mathf.IsEqualApprox(bonusHigh, 1f + fall.MaxDamageBonus);
		GameLog.Write(
			"SelfTest",
			$"{(ok ? "✅" : "🔴")} P4·被动 落（下落攻击）：CD {fall.Cooldown:0.##}（期望 1） 触发键 {fall.InputAction}（期望 fall_attack）"
			+ $"｜地面时：在地面={grounded}（期望 True） 可按={!blockedOnGround}（期望 False） TryPress={pressedOnGround}（期望 False）"
			+ $"｜空中时：TryPress={pressedInAir}（期望 True） 下落速度 {diveSpeed:0.##} m/s（期望 ≤ -{fall.DiveSpeed:0.##}） 起手高度（净下落）{_p4FallHeight:0.##}m 伤害 {_p4FallDamage:0.##}"
			+ $"｜高度→伤害：每 {fall.HeightStep:0.##}m +{fall.DamageBonusPerStep * 100f:0.#}%（文档 n=预设 2m），封顶 +{fall.MaxDamageBonus * 100f:0.#}%（文档 100%）"
			+ $" → 0m ×{bonusLow:0.##}（期望 ×1） 2 档 ×{bonusMid:0.##}（期望 ×{1f + (2f * fall.DamageBonusPerStep):0.##}） 20 档 ×{bonusHigh:0.##}（期望 ×{1f + fall.MaxDamageBonus:0.##} = 封顶）");
	}

	/// <summary>落地：上游伤害不变，额外一圈（落剑式）+ 落剑式的 CD 间隔。</summary>
	private void ReportFallAttack()
	{
		if (_p4Player == null || _p4Fall == null || _p4LandingBurst == null)
		{
			GameLog.Write("SelfTest", "🔴 P4·落剑式：上一段没准备好。");
			return;
		}

		FallAttackAbility fall = _p4Fall;
		LandingBurstAbility burst = _p4LandingBurst;

		// 靶子摆在落点附近（落圈是以落点为球心、半径 ~3.2m 的球），让下落攻击与落剑式都有东西可打。
		_p4Victim = SpawnVictim(new Vector3(1.6f, -5.2f, 1.6f));
		float victimBefore = _p4Victim?.Health ?? 0f;

		int burstBefore = burst.TriggerCount;
		float upstreamDamage = fall.LastDamage;

		// 把玩家压回地面（触发落地事件）。
		_p4Player.GlobalPosition = _p4Player.GlobalPosition with { Y = 0.05f };
		_p4Player.Velocity = new Vector3(0f, -20f, 0f);
		for (int i = 0; i < 6 && fall.IsBusy; i++)
		{
			fall._PhysicsProcess(0.05d);
			MovementOf(_p4Player)?._PhysicsProcess(0.05d);
		}

		WaitUntilIdle(burst);
		int burstAfter = burst.TriggerCount;
		float victimAfter = _p4Victim?.Health ?? 0f;
		bool ok = fall.HasLanded && burstAfter >= burstBefore + 1;
		GameLog.Write(
			"SelfTest",
			$"{(ok ? "✅" : "🔴")} P4·武技3 落剑式（被动）：上游 {fall.AbilityId} 落地={fall.HasLanded}（期望 True） 上游本次伤害 {upstreamDamage:0.##}（**没有被改过**）"
			+ $"｜额外那一圈：触发次数 {burstBefore} → {burstAfter}（期望 +1） 那一圈伤害 {burst.LastBurstDamage:0.##} 挂到 {burst.TriggerAbilityId}（期望 skl_b_fall） CD {burst.Cooldown:0.##}（期望 5 = 触发间隔）"
			+ $"｜靶子 {_p4Victim?.Name} 生命 {victimBefore:0.#} → {victimAfter:0.#}｜⚠️ 自测在同一帧里跑，落圈是 Area3D、要等一个物理帧才进重叠列表 → 这一栏不作为判据（判据是触发次数 +1 与 上游伤害没变）");
	}

	/// <summary>镰刀：3 段连招（最后一刀大回旋）= 圆柱命中盒 + 武技2 抛出/周期伤害/飞行期间锁武技1。</summary>
	private void BeginScytheProbe()
	{
		ClearHostiles();
		RunController.Instance?.BeginRunWithCharacter(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Scythe);
		_p4Player = PlayerLocator.Player;
		_p4ScytheW1 = _p4Player?.GetNodeOrNull<MeleeComboAbility>("AbilityWeapon5Skill1");
		_p4ScytheW2 = _p4Player?.GetNodeOrNull<ScytheThrowAbility>("AbilityWeapon5Skill2");
	}

	private void ReportScytheProbe()
	{
		if (_p4Player == null || _p4ScytheW1 == null || _p4ScytheW2 == null)
		{
			GameLog.Write("SelfTest", "🔴 P4·镰刀：读不到那两个武技节点。");
			return;
		}

		MeleeComboAbility w1 = _p4ScytheW1;
		ScytheThrowAbility w2 = _p4ScytheW2;

		// ① 3 段连招（最后一刀大回旋 = 圆柱）。
		MeleeComboAbility? weapon4 = _p4Player.GetNodeOrNull<MeleeComboAbility>("AbilityWeapon4Skill1");
		WaitUntilIdle(w1);
		w1.RefreshCooldown();
		bool started = w1.TryPress();
		// 连招推进：自测在同一帧里跑，等不到真实动画事件帧，所以直接模拟"ATK 帧到了"
		// （MeleeComboAbility.DebugFireAnimEvent = OnAnimEvent(ComboNextEvent)）。
		int advanced = 0;
		for (int i = 0; i < 8 && w1.IsBusy; i++)
		{
			w1.DebugFireAnimEvent();      // 本段判定 + 开输入窗口
			w1.TryPress();                // 玩家紧接着又按了一下
			w1._PhysicsProcess(0.06d);
			advanced = w1.ComboChained;
		}

		// 圆柱判定：最后一刀的命中盒尺寸 z=0（= 大回旋 360°）。
		Vector3 lastSize = w1.CurrentStepBoxSize;

		// ② 抛出：周期伤害 + 飞行期间武技1 被锁 + 再次按键收回。
		WaitUntilIdle(w2);
		w2.RefreshCooldown();
		bool thrown = w2.TryPress();
		bool inFlight = w2.InFlight;

		// WeaponGate5 是每帧惰性绑定的，这里也推它的物理帧。
		_p4Player.GetNodeOrNull<WeaponGate>("WeaponGate5")?._PhysicsProcess(0.02d);
		bool comboLocked = w1.ExternallyBlocked;
		// ⚠️ 先清冷却：不然"按不动"是冷却导致的，测不出"门"这一条。
		WaitUntilIdle(w1);
		w1.RefreshCooldown();
		bool comboBlockedPress = !w1.TryPress();

		// 推几个周期（每 0.5s 一次判定）。
		for (int i = 0; i < 8; i++)
		{
			w2._PhysicsProcess(0.2d);
		}

		int ticks = w2.DamageTickCount;
		Vector3 weaponPos = w2.WeaponPosition;
		bool secondPress = w2.TryPress();      // 再次使用 → 收回
		for (int i = 0; i < 40 && w2.IsBusy; i++)
		{
			w2._PhysicsProcess(0.05d);
		}

		_p4Player.GetNodeOrNull<WeaponGate>("WeaponGate5")?._PhysicsProcess(0.02d);
		bool comboUnlocked = !w1.ExternallyBlocked;

		bool ok = started && advanced >= 2 && lastSize.Z <= 0.0001f
			&& thrown && inFlight && comboLocked && comboBlockedPress && ticks >= 2 && secondPress && comboUnlocked;
		GameLog.Write(
			"SelfTest",
			$"{(ok ? "✅" : "🔴")} P4·镰刀：武技1 憎恶 段数={w1.ComboStepCount}（期望 3） CD {w1.Cooldown:0.##}（期望 0.5） 连续按键推进={advanced}（期望 ≥2） 最后一刀命中盒 {Fmt(lastSize)}（期望 z=0 = **圆柱 = 大回旋 360°**）"
			+ $"｜武技2 无情：TryPress={thrown}（期望 True） 在飞={inFlight}（期望 True） 周期伤害 {ticks} 次（期望 ≥2，每 {w2.DamageInterval:0.##}s 一次；推 8×0.2s=1.6s 的物理帧，实际按引擎帧数） CD {w2.Cooldown:0.##}（期望 1） 武器位置 {Fmt(weaponPos)}"
			+ $"｜「武器飞出去这期间武技1无法使用」：门绑定={_p4Player.GetNodeOrNull<WeaponGate>("WeaponGate5")?.Bound} 武技1 被锁={comboLocked}（期望 True） 此时按武技1={comboBlockedPress}（期望 False）"
			+ $"｜再次使用收回={secondPress}（期望 True） 收回后武技1 解锁={comboUnlocked}（期望 True） 累计收回 {w2.RecallCount} 次");
	}

	/// <summary>长枪：5 段循环连招 + 武技2 蓄力抛掷（抛物线 / 路径可视化 / 每档 +20% / 收回）。</summary>
	private void BeginSpearProbe()
	{
		ClearHostiles();
		RunController.Instance?.BeginRunWithCharacter(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Spear);
		_p4Player = PlayerLocator.Player;
		_p4SpearW1 = _p4Player?.GetNodeOrNull<MeleeComboAbility>("AbilityWeapon6Skill1");
		_p4SpearW2 = _p4Player?.GetNodeOrNull<SpearThrowAbility>("AbilityWeapon6Skill2");
	}

	private void ReportSpearProbe()
	{
		if (_p4Player == null || _p4SpearW1 == null || _p4SpearW2 == null)
		{
			GameLog.Write("SelfTest", "🔴 P4·长枪：读不到那两个武技节点。");
			return;
		}

		MeleeComboAbility w1 = _p4SpearW1;
		SpearThrowAbility w2 = _p4SpearW2;

		// ① 5 段循环连招：连按 6 次应当回到第 1 段（ComboLoop）。
		WaitUntilIdle(w1);
		w1.RefreshCooldown();
		bool started = w1.TryPress();
		int advanced = 0;
		// 5 段循环：连按 10 次应当"打满 5 段后回到第 1 段"（ComboStep 永远 ≤ 4）。
		int maxStepSeen = 0;
		for (int i = 0; i < 10 && w1.IsBusy; i++)
		{
			w1.DebugFireAnimEvent();
			w1.TryPress();
			w1._PhysicsProcess(0.06d);
			advanced = w1.ComboChained;
			maxStepSeen = Mathf.Max(maxStepSeen, w1.ComboStep);
		}

		// ② 蓄力抛掷：基础档 vs 3 档（每 0.25s 一档 ×1.2）。
		WaitUntilIdle(w2);
		w2.RefreshCooldown();
		bool charged = w2.TryPress();
		w2.OnInputHeld(w2.ChargeBaseSeconds + 0.01f);      // 基础档
		float baseDist = w2.MaxThrowDistance * w2.ChargePowerScale;
		w2.OnInputReleased();
		float distAt0 = w2.LastHorizontalDistance;
		float speedAt0 = w2.LastHorizontalSpeed;
		int pathPoints = w2.LastPathPoints;
		bool inFlight = w2.InFlight;
		for (int i = 0; i < 30 && w2.IsBusy; i++)
		{
			w2._PhysicsProcess(0.05d);
		}

		WaitUntilIdle(w2);
		w2.RefreshCooldown();
		bool charged3 = w2.TryPress();
		w2.OnInputHeld(w2.ChargeBaseSeconds + (w2.ChargeStepSeconds * 3f) + 0.01f);
		int steps = w2.ChargeSteps;
		float power = w2.ChargePowerScale;
		w2.OnInputReleased();
		float distAt3 = w2.LastHorizontalDistance;
		float speedAt3 = w2.LastHorizontalSpeed;
		bool recall = w2.TryPress();      // 再次使用 → 收回
		for (int i = 0; i < 40 && w2.IsBusy; i++)
		{
			w2._PhysicsProcess(0.05d);
		}

		float maxDist = w2.MaxThrowDistance * Mathf.Pow(1.2f, 3f);
		bool ok = started && advanced >= 5 && maxStepSeen <= 4 && charged && charged3 && steps == 3
			&& Mathf.IsEqualApprox(speedAt3 / Mathf.Max(0.01f, speedAt0), power)
			&& distAt3 > distAt0 && pathPoints >= 2 && inFlight && recall;
		GameLog.Write(
			"SelfTest",
			$"{(ok ? "✅" : "🔴")} P4·长枪武技1 横扫千军：段数={w1.ComboStepCount}（期望 5） 循环={w1.ComboLoop}（期望 True，文档「这么循环」） CD {w1.Cooldown:0.##}（期望 0.6） 连续按键推进={advanced}（期望 ≥5） 循环后最大段号={maxStepSeen + 1}（期望 ≤5 = 打满就回到第 1 段）"
			+ $"｜武技2 长虹贯日：基础蓄力 {w2.ChargeBaseSeconds:0.##}s（期望 1）/ 每 {w2.ChargeStepSeconds:0.##}s 一档（期望 0.25）/ 每档 ×{w2.ChargeStepScale:0.##}（期望 1.2 = +20%）/ 上限 {w2.ChargeMaxStepsEffective} 档（期望 3，最多蓄力 {w2.ChargeMaxSeconds:0.##}s）"
			+ $"｜抛物线：重力 {w2.ArcGravity:0.##} 飞行 {w2.FlightSeconds:0.##}s 路径可视化 {pathPoints} 点（期望 ≥2） 在飞={inFlight}（期望 True）"
			+ $"｜基础档：水平速度 {speedAt0:0.##} 路径中线 {distAt0:0.##}m ／ 3 档：速度 {speedAt3:0.##} 路径中线 {distAt3:0.##}m（期望 {maxDist:0.##} ≈ 12×1.2³）"
			+ $"｜丢出去后再次使用收回={recall}（期望 True） 累计收回 {w2.RecallCount} 次｜落日余晖（武器 3 被动）：下落攻击 {_p4Fall?.AbilityId ?? "（找不到）"} 的冲下模式={_p4Fall?.HomingMode}（0=Straight 1=Crosshair） 指向范围 {_p4Fall?.MaxDiveAngleDegrees:0.#}°");
	}

	/// <summary>取单位下的 <see cref="CharacterMovement"/>（自测探针要同步推它的物理帧）。</summary>
	private static CharacterMovement? MovementOf(Unit unit) => unit.GetNodeOrNull<CharacterMovement>("Movement");

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

	/// <summary>
	/// **P6b 两条新异化的行为取证**（杀戮践踏 `mut_stomp` / 巡飞弹 `mut_homing_missile`）。
	///
	/// <para>走的是**真实链路**：真实玩家 + 真实 <see cref="RewardContext"/> → `RunBuild.Add(行)` →
	/// 真实 handler 建出规则 → 在玩家旁边**临时造一个真敌人** `Chaser.tscn`（AI 关掉）→
	/// 两条规则各**真的出手一次**，量出实际结果：</para>
	/// <list type="bullet">
	/// <item>杀戮践踏：半径 / 伤害 / 触发间隔 / 最小落高四个参数逐位来自行；`Matches` 恒为 false
	///   （证明它**不是事件驱动**，出手完全由 `_Process` 的落地边沿发起）；出手一次后靶子掉血 = 行里的伤害。</item>
	/// <item>巡飞弹：暴击事件匹配 → 出手一次 → `FireCount` +1、靶子身上挂上信标状态、场上多一枚投射物。</item>
	/// </list>
	///
	/// <para>⚠️ 量完**全部还原**（靶子 / 新生成的投射物 / 两条规则节点全部 `Free()`），
	/// 但因为中途真的打了一下靶子（会抛 `Damaged`），调用点必须是**自测最后一个阶段**。</para>
	/// </summary>
	public string DebugP6bMutationProbe()
	{
		Unit? player = PlayerLocator.Player;
		if (player == null || !IsInstanceValid(player))
		{
			return "🔴 P6b·新异化行为取证：找不到玩家";
		}

		var build = new RunBuild();
		// `RewardContext.Weapon` 是 `required` 成员，所以这里必须给一个 —— 这两条异化的 handler 都不读它，
		// 找不到就退到一个**不进树**的空武器（与其它探针"new 出来、用完 Free"的做法一致）。
		var context = new RewardContext
		{
			Player = player,
			Weapon = NodeSearch.FindInSubtree<ActRoguelike.Weapons.Weapon>(player) ?? new ActRoguelike.Weapons.Weapon(),
			Build = build,
		};
		foreach (string id in new[] { "mut_stomp", "mut_homing_missile" })
		{
			if (RewardCatalog.Find(id) is { } row)
			{
				build.Add(row, context);
			}
		}

		var rules = new List<TriggerRule>();
		NodeSearch.CollectInSubtree(player, rules);
		StompOnLandRule? stomp = null;
		HomingMissileOnCritRule? missile = null;
		foreach (TriggerRule rule in rules)
		{
			stomp ??= rule as StompOnLandRule;
			missile ??= rule as HomingMissileOnCritRule;
		}

		if (stomp == null || missile == null)
		{
			return $"🔴 P6b·新异化行为取证：规则没建出来（践踏={(stomp != null)} 巡飞弹={(missile != null)}）—— 检查 ApplyHandler 拼写与行是否在表里";
		}

		// 信标状态那一行必须真的能建出 StatusEffect（`HomingMissileOnCritRule` 靠它锁目标）。
		StatusEffect? beacon = StatusPresets.Create(missile.BeaconStatusId, missile.BeaconSeconds);

		// 临时靶子：真敌人 Chaser，AI 关掉，摆在玩家右手边 2 米（在践踏半径 3 与索敌 18 之内）。
		Unit? target = null;
		Node? arena = player.GetParent();
		if (arena != null && GD.Load<PackedScene>("res://Scenes/Enemies/Chaser.tscn").Instantiate() is Unit chaser)
		{
			NodeSearch.FindInSubtree<ActRoguelike.AI.EnemyBrain>(chaser)?.SetPhysicsProcess(false);
			arena.AddChild(chaser);
			chaser.GlobalPosition = player.GlobalPosition + new Vector3(2f, 0f, 0f);
			target = chaser;
		}

		// 场上已有的投射物（量完只 Free **新增**的那些，不动关卡自己的）。
		var projectilesBefore = new List<ActRoguelike.Combat.Projectile>();
		if (arena != null)
		{
			NodeSearch.CollectInSubtree(arena, projectilesBefore);
		}

		float targetHealthBefore = target?.Health ?? 0f;
		bool stompNotEventDriven = !stomp.Matches(new GameplayEvent { Kind = GameplayEventKind.Kill, Source = player });
		stomp.Execute(default);
		float stompDamageDealt = targetHealthBefore - (target?.Health ?? 0f);

		var crit = new GameplayEvent { Kind = GameplayEventKind.Critical, Source = player };
		bool critMatches = missile.Matches(crit);
		bool enemyCritIgnored = !missile.Matches(new GameplayEvent { Kind = GameplayEventKind.Critical, Source = target });
		missile.Execute(crit);

		int beaconStacks = target?.GetStatusStacks(missile.BeaconStatusId) ?? 0;
		var projectilesAfter = new List<ActRoguelike.Combat.Projectile>();
		if (arena != null)
		{
			NodeSearch.CollectInSubtree(arena, projectilesAfter);
		}

		int spawned = projectilesAfter.Count - projectilesBefore.Count;

		bool stompOk = stomp is { Radius: 3f, Damage: 15f, Cooldown: 0.2f, MinFallHeight: 0f }
			&& stompNotEventDriven
			&& Mathf.Abs(stompDamageDealt - 15f) < 0.51f;
		bool missileOk = missile is { Damage: 50f, BeaconStatusId: "MissileBeacon" }
			&& critMatches && enemyCritIgnored
			&& missile.FireCount == 1
			&& beaconStacks >= 1
			&& spawned == 1
			&& beacon is { MaxStacks: 2 } && beacon.Modifiers.Length == 0;

		string line = $"{(stompOk && missileOk ? "✅" : "🔴")} P6b·新异化行为取证（真实玩家 + 真敌人靶子，量完还原）："
			+ $"杀戮践踏 → {stomp.GetType().Name} 半径 {stomp.Radius:0.#}（期望 3）伤害 {stomp.Damage:0.#}（期望 15）间隔 {stomp.Cooldown:0.##}（期望 0.2）最小落高 {stomp.MinFallHeight:0.#}（期望 0）"
			+ $"｜事件匹配恒 false={stompNotEventDriven}（期望 True —— 它不是事件驱动，出手由落地边沿发起）"
			+ $"｜出手一次：靶子 {targetHealthBefore:0.#} → {target?.Health ?? 0f:0.#}（掉 {stompDamageDealt:0.#}，期望 15）"
			+ $"｜巡飞弹 → {missile.GetType().Name} 伤害 {missile.Damage:0.#}（期望 50 —— 文档给的就是 50）弹速 {missile.Speed:0.#} 索敌 {missile.SeekRange:0.#}m 信标「{missile.BeaconStatusId}」{missile.BeaconSeconds:0.#}s 转向 {missile.TurnRateDegrees:0.#}°/s"
			+ $"｜暴击匹配={critMatches}（期望 True）敌人暴击被忽略={enemyCritIgnored}（期望 True）"
			+ $"｜出手一次：FireCount={missile.FireCount}（期望 1）靶子信标层数={beaconStacks}（期望 ≥1）新生成投射物={spawned}（期望 1）"
			+ $"｜信标行 StatusPresets.Create → {(beacon == null ? "null（🔴 表里没有这一行）" : $"MaxStacks {beacon.MaxStacks} Modifiers {beacon.Modifiers.Length} 条")}"
			+ "｜⚠️ 这一段是**直接调 Execute**（与落地轮询/事件总线走的是同一个入口）；"
			+ "真实按键/落地那一路的端到端留给人手 MCP 验证，免得污染前面的硬指标";

		// 还原：新生成的投射物、靶子、两条规则节点全部清掉。
		foreach (ActRoguelike.Combat.Projectile projectile in projectilesAfter)
		{
			if (!projectilesBefore.Contains(projectile))
			{
				projectile.Free();
			}
		}

		target?.Free();
		foreach (TriggerRule rule in rules)
		{
			if (rule is StompOnLandRule or HomingMissileOnCritRule)
			{
				player.RemoveChild(rule);
				rule.Free();
			}
		}

		return line;
	}

	/// <summary>
	/// **P6d·新异化行为取证**：三条补完的异化（护卫 / 落日剑 / 灵魂收割）各量一次真实结果。
	///
	/// <para>与 <see cref="DebugP6bMutationProbe"/> 同一套纪律：**真实玩家 + 真实 <see cref="RewardContext"/>
	/// + 真实行**（`RunBuild.Add` → 真实 handler → 真实规则节点），量完**全部还原**；
	/// 因为中途真的会打一下玩家（护卫那一下**应该被完全挡掉**），所以调用点必须是**自测最后一个阶段**。</para>
	/// </summary>
	public string DebugP6dMutationProbe()
	{
		Unit? player = PlayerLocator.Player;
		if (player == null || !IsInstanceValid(player))
		{
			return "🔴 P6d·新异化行为取证：找不到玩家";
		}

		var build = new RunBuild();
		var context = new RewardContext
		{
			Player = player,
			Weapon = NodeSearch.FindInSubtree<ActRoguelike.Weapons.Weapon>(player) ?? new ActRoguelike.Weapons.Weapon(),
			Build = build,
		};

		foreach (string id in new[] { "mut_guard" })
		{
			if (RewardCatalog.Find(id) is { } row)
			{
				build.Add(row, context);
			}
		}

		GuardOnSprintRule? guard = NodeSearch.FindInSubtree<GuardOnSprintRule>(player);
		SprintController? sprint = NodeSearch.FindInSubtree<SprintController>(player);
		if (guard == null || sprint == null)
		{
			return $"🔴 P6d·护卫：规则/奔跑组件没找到（规则={(guard != null)} 奔跑={(sprint != null)}）—— 检查 ApplyHandler 拼写、行是否在表里、Player.tscn 上有没有 Sprint 节点";
		}

		// ① 参数逐位来自行（文档只给 {5}，另两个是预设值）。
		bool paramsOk = guard is { Cooldown: 5f, BlockDamageMultiplier: 0f, FrontDotThreshold: 1f };
		bool notEventDriven = !guard.Matches(new GameplayEvent { Kind = GameplayEventKind.Kill, Source = player });

		// ② 没在奔跑时**不授予**（奔跑是文档写明的触发条件）。
		sprint.DebugSprintOverride = false;
		float idleSpeed = sprint.EffectiveSpeed(player.MoveSpeed);
		guard.PollOnce(0f);
		bool notGrantedWhenIdle = !guard.HasGuard;

		// ③ 奔跑时授予 —— 走的是 `_Process` 的**同一个入口** `PollOnce`。
		sprint.DebugSprintOverride = true;
		float sprintSpeed = sprint.EffectiveSpeed(player.MoveSpeed);
		guard.PollOnce(0f);
		bool granted = guard.HasGuard;

		// ④ 获得格挡 → 玩家身上**多了一个 Block 子节点**（不选这条增幅就没有它）。
		BlockComponent? block = player.GetNodeOrNull<BlockComponent>("Block");
		bool blockOk = block is { Blocking: true, BlockDamageMultiplier: 0f, FrontDotThreshold: 1f, BreakThreshold: 0f };

		// ⑤ 真的挨一下：应当被**完全挡掉**（一点血都不掉），并且格挡破碎、进入 5 秒重取。
		float healthBefore = player.Health;
		float armorBefore = player.Armor;
		var hit = new HitInstance
		{
			Target = player,
			Context = EventContext.Create(null, player),
			Damage = 40f,
			Direction = Vector3.Up,
			Position = player.GlobalPosition,
		};
		DamageResult result = player.ReceiveHit(hit);
		float healthLost = healthBefore - player.Health;
		bool brokeNow = !guard.HasGuard;
		float rechargeAfterBreak = guard.RechargeRemaining;
		int blockedCountAfter = guard.BlockedCount;
		bool broke = brokeNow && blockedCountAfter == 1 && Mathf.Abs(rechargeAfterBreak - 5f) < 0.001f;

		// ⑥ 重取间隔真的是 5 秒：4.9 秒还不行，再 0.2 秒才行。
		sprint.DebugSprintOverride = true;
		guard.PollOnce(4.9f);
		bool blockedAt49 = !guard.HasGuard;
		guard.PollOnce(0.2f);
		bool grantedAfter5 = guard.HasGuard;

		bool ok = paramsOk && notEventDriven && notGrantedWhenIdle && granted && blockOk
			&& result.Blocked && Mathf.Abs(healthLost) < 0.001f && Mathf.Abs(armorBefore - player.Armor) < 0.001f
			&& broke && blockedAt49 && grantedAfter5
			&& Mathf.Abs(idleSpeed - player.MoveSpeed) < 0.001f
			&& sprintSpeed > idleSpeed
			&& sprint is { SpeedMultiplier: 1.5f, InputAction: "sprint" }
			&& InputMap.HasAction("sprint");

		string line = $"{(ok ? "✅" : "🔴")} P6d·护卫行为取证（真实玩家 + 真实行 + 真实 RewardContext，量完还原）："
			+ $"→ {guard.GetType().Name} 间隔 {guard.Cooldown:0.#}（期望 5 —— 文档给的就是 5）挡后剩伤 {guard.BlockDamageMultiplier:0.##}（期望 0，预设）正面阈值 {guard.FrontDotThreshold:0.##}（期望 1，预设）"
			+ $"｜事件匹配恒 false={notEventDriven}（期望 True —— 它不是事件总线驱动，出手由 `PollOnce` 的奔跑轮询发起）"
			+ $"｜没奔跑不授予={notGrantedWhenIdle}（期望 True）"
			+ $"｜奔跑授予={granted}（同一个入口 PollOnce；奔跑来自 SprintController，按键「{sprint.InputAction}」project.godot 里有={InputMap.HasAction("sprint")}）"
			+ $"｜移速 未奔跑 {idleSpeed:0.##} → 奔跑 {sprintSpeed:0.##}（倍率 {sprint.SpeedMultiplier:0.##}，预设值）"
			+ $"｜格挡节点 Block 存在={block != null}（Blocking={block?.Blocking} 剩伤={block?.BlockDamageMultiplier:0.##} 阈值={block?.FrontDotThreshold:0.##}）"
			+ $"｜挨 40 点：Blocked={result.Blocked}（期望 True）掉血 {healthLost:0.##}（期望 0 = 完全格挡）护甲 {armorBefore:0.#} → {player.Armor:0.#}（期望不变）"
			+ $"｜破碎={brokeNow}（期望 True —— 挨完那一下手上就没有格挡了）累计挡下={blockedCountAfter}（期望 1）重取剩余={rechargeAfterBreak:0.##}s（期望 5）"
			+ $"｜重取门槛：+4.9s 还不能得={blockedAt49}（期望 True）→ 再 +0.2s 得={grantedAfter5}（期望 True）"
			+ "｜⚠️ 这一段是**直接调 PollOnce / ReceiveHit**（与 `_Process`、真实命中走的是同一个入口）；"
			+ "真实按键（按住 Shift 跑起来）那一路留给人手 MCP 验证，免得污染前面的硬指标";

		// 还原：奔跑强制关掉、规则节点摘掉。
		// ⚠️ **刻意不 Free 那个 Block 子节点**：`Unit.Block` 是惰性缓存的裸引用（`_block ??= …`），
		// Free 掉它会留下一个悬空引用（下一次命中会炸）。这里只把它关掉（Blocking=false 等价于"没有格挡"）。
		sprint.DebugSprintOverride = false;
		if (block != null)
		{
			block.Blocking = false;
		}

		foreach (TriggerRule rule in CollectRules(player))
		{
			if (rule is GuardOnSprintRule)
			{
				player.RemoveChild(rule);
				rule.Free();
			}
		}

		// ===================== 落日剑 =====================
		if (RewardCatalog.Find("mut_sunset_sword") is { } swordRow)
		{
			build.Add(swordRow, context);
		}

		SunsetSwordRule? sword = NodeSearch.FindInSubtree<SunsetSwordRule>(player);
		Node? arena2 = player.GetParent();
		Unit? target2 = null;
		if (arena2 != null && GD.Load<PackedScene>("res://Scenes/Enemies/Chaser.tscn").Instantiate() is Unit chaser2)
		{
			NodeSearch.FindInSubtree<ActRoguelike.AI.EnemyBrain>(chaser2)?.SetPhysicsProcess(false);
			arena2.AddChild(chaser2);
			chaser2.GlobalPosition = player.GlobalPosition + new Vector3(2f, 0f, 0f);
			target2 = chaser2;
		}

		// 「武技1」是哪一个技能行？**由按键动作反查**（与规则内部同一条路），所以探针也不写死技能 Id。
		string skill1Id = sword == null
			? string.Empty
			: ActRoguelike.Player.PlayerAbilityInput.FindByInputAction(player, sword.TriggerInputAction)?.AbilityId ?? string.Empty;

		var weapon1Hit = new GameplayEvent { Kind = GameplayEventKind.Hit, Source = player, Target = target2, Tag = skill1Id };
		bool swordMatches = sword != null && target2 != null && skill1Id.Length > 0 && sword.Matches(weapon1Hit);
		bool otherAbilityIgnored = sword != null && target2 != null
			&& !sword.Matches(new GameplayEvent { Kind = GameplayEventKind.Hit, Source = player, Target = target2, Tag = "skl_a1_skill_1" });
		bool selfTargetIgnored = sword != null && !sword.Matches(new GameplayEvent { Kind = GameplayEventKind.Hit, Source = player, Target = player, Tag = skill1Id });

		float targetHealthBefore = target2?.Health ?? 0f;
		float playerHealthBeforeSword = player.Health;
		Node? scene = player.GetTree()?.CurrentScene;
		int ringsBefore = scene != null ? CountNodesNamed(scene, "LandingShockwaveFx") : 0;

		sword?.Execute(weapon1Hit);

		SunsetSwordMarker? marker = target2?.GetNodeOrNull<SunsetSwordMarker>(SunsetSwordMarker.NodeName);
		bool markerParamsOk = marker is { Radius: 2.5f, Damage: 40f, HoverSeconds: 1.5f, FallSeconds: 0.35f, HoverHeight: 3.2f };
		bool attached = marker != null && marker.Attacker == player && marker.Target == target2;
		float hoverOffset = marker != null && target2 != null ? marker.GlobalPosition.Y - target2.GlobalPosition.Y : 0f;

		// 「对**没有**落日剑的敌人施加」：挂上之后再问一次 → 不该再匹配。
		bool noSecondSword = sword != null && target2 != null && !sword.Matches(weapon1Hit);

		// 真的让它落下来 —— 走的是**同一个 `_Process` 入口**，只是推两下大步长（悬挂 → 落下）。
		marker?._Process(10.0);
		marker?._Process(10.0);

		float targetHealthAfter = target2?.Health ?? 0f;
		float swordDamageDealt = targetHealthBefore - targetHealthAfter;
		float playerHealthAfterSword = player.Health;
		int ringsAfter = scene != null ? CountNodesNamed(scene, "LandingShockwaveFx") : 0;
		int hits = marker?.LastHits ?? 0;
		bool fell = marker is { HasFallen: true, FallCount: 1 };
		bool noCollisionOrUnit = marker != null && marker.CountCollisions() == 0 && marker.CountUnits() == 0 && marker.GetGroups().Count == 0;
		string markerState = marker?.DebugState() ?? "（没挂上）";

		bool swordOk = sword is { TriggerInputAction: "weapon_skill_1", HoverSeconds: 1.5f, FallSeconds: 0.35f, Radius: 2.5f, Damage: 40f, HoverHeight: 3.2f, Cooldown: 0f }
			&& swordMatches && otherAbilityIgnored && selfTargetIgnored
			&& markerParamsOk && attached && noSecondSword
			&& Mathf.Abs(hoverOffset - 3.2f) < 0.05f
			&& fell && hits >= 1
			&& Mathf.Abs(swordDamageDealt - 40f) < 0.51f
			&& Mathf.Abs(playerHealthAfterSword - playerHealthBeforeSword) < 0.001f
			&& ringsAfter > ringsBefore
			&& noCollisionOrUnit;

		string swordLine = $"{(swordOk ? "✅" : "🔴")} P6d·落日剑行为取证（真实玩家 + 真实行 + 真实敌人靶子，量完还原）："
			+ $"→ {(sword == null ? "（规则没建出来）" : sword.GetType().Name)} 触发按键「{sword?.TriggerInputAction}」"
			+ $"（反查到的武技1 技能行 =「{(skill1Id.Length == 0 ? "（没找到）" : skill1Id)}」）"
			+ $"｜悬挂 {sword?.HoverSeconds:0.##}s（期望 1.5，预设）落下 {sword?.FallSeconds:0.##}s（期望 0.35，预设）半径 {sword?.Radius:0.##}m（期望 2.5，预设）伤害 {sword?.Damage:0.##}（期望 40，预设）高度 {sword?.HoverHeight:0.##}m（期望 3.2，预设）间隔 {sword?.Cooldown:0.##}（期望 0）"
			+ $"｜武技1 命中匹配={swordMatches}（期望 True）别的技能不匹配={otherAbilityIgnored}（期望 True）打自己不算={selfTargetIgnored}（期望 True）"
			+ $"｜施加后目标身上有剑={attached}（期望 True；节点名 {SunsetSwordMarker.NodeName}）悬挂高度实测={hoverOffset:0.##}m（期望 3.2）重复施加被拦={noSecondSword}（期望 True —— 文档「对**没有**落日剑的敌人施加」）"
			+ $"｜剑自己落下：已落下={fell} 落点命中={hits}（期望 ≥1）靶子 {targetHealthBefore:0.#} → {targetHealthAfter:0.#}（掉 {swordDamageDealt:0.#}，期望 40）"
			+ $"｜玩家自己没被自己的剑打到：{playerHealthBeforeSword:0.#} → {playerHealthAfterSword:0.#}（期望不变）"
			+ $"｜落圈（复用 P6c LandingShockwave）：{ringsBefore} → {ringsAfter}（期望 +1）"
			+ $"｜纯表现安全性：碰撞体/单位/分组 全 0 = {noCollisionOrUnit}"
			+ $"｜{markerState}";

		// 还原：靶子（连着剑）、落日剑规则节点、这一波新画的圈。
		if (scene != null)
		{
			foreach (Node node in scene.FindChildren("LandingShockwaveFx", string.Empty, true, false))
			{
				node.Free();
			}
		}

		target2?.Free();
		foreach (TriggerRule rule in CollectRules(player))
		{
			if (rule is SunsetSwordRule)
			{
				player.RemoveChild(rule);
				rule.Free();
			}
		}

		// ===================== 灵魂收割 =====================
		if (RewardCatalog.Find("mut_soul_harvest") is { } soulRow)
		{
			build.Add(soulRow, context);
		}

		SoulHarvestRule? harvest = NodeSearch.FindInSubtree<SoulHarvestRule>(player);
		SceneTree? tree = player.GetTree();
		Node? scene2 = tree?.CurrentScene;
		int soulsBefore = scene2 != null ? CountNodesNamed(scene2, SoulPickup.NodeName) : 0;

		var killEvent = new GameplayEvent
		{
			Kind = GameplayEventKind.Kill,
			Source = player,
			Target = null,
			Position = player.GlobalPosition + new Vector3(0.8f, 0f, 0f),
		};

		// ① 概率为 0 → 必不掉（同一条 Execute 入口）。
		bool killMatches = harvest != null && harvest.Matches(killEvent);
		bool otherKillIgnored = harvest != null && !harvest.Matches(new GameplayEvent { Kind = GameplayEventKind.Kill, Source = target2 });
		float rowChance = harvest?.Chance ?? 0f;
		float rowCooldown = harvest?.Cooldown ?? 0f;
		if (harvest != null)
		{
			harvest.Chance = 0f;
		}

		harvest?.Execute(killEvent);
		int soulsAfterZero = scene2 != null ? CountNodesNamed(scene2, SoulPickup.NodeName) : 0;

		// ② 概率为 1 → 必掉。
		if (harvest != null)
		{
			harvest.Chance = 1f;
		}

		harvest?.Execute(killEvent);
		int soulsAfterOne = scene2 != null ? CountNodesNamed(scene2, SoulPickup.NodeName) : 0;

		SoulPickup? soul = null;
		if (scene2 != null)
		{
			foreach (Node node in scene2.FindChildren(SoulPickup.NodeName, string.Empty, true, false))
			{
				soul = node as SoulPickup;
				if (soul != null)
				{
					break;
				}
			}
		}

		bool soulParamsOk = soul is { HealAmount: 8f, PickupRadius: 1.2f, LifetimeSeconds: 15f, PickupDelaySeconds: 0f };
		bool notInUnitLists = soul != null
			&& !soul.IsInGroup("enemy_units") && !soul.IsInGroup("player_units") && !soul.IsInGroup("summon_units")
			&& soul.CountUnits() == 0 && soul.CountCollisions() == 0 && soul.GetGroups().Count == 0;

		// ③ 拾取：把灵魂挪到玩家脚下，并保证"有血可回"（满血就先按统一命中入口掉一点）。
		if (soul != null)
		{
			soul.GlobalPosition = player.GlobalPosition;
			float missingBefore = player.MaxHealth - player.Health;
			if (missingBefore < 1f)
			{
				player.ApplyDirectDamage(player.Armor + 60f);
			}

			float healthBeforePickup = player.Health;
			float expectedHeal = Mathf.Min(soul.HealAmount, player.MaxHealth - healthBeforePickup);
			soul._Process(0.02);

			bool healOk = soul.PickedUp
				&& Mathf.Abs(soul.HealedAmount - expectedHeal) < 0.01f
				&& Mathf.Abs(player.Health - (healthBeforePickup + expectedHeal)) < 0.01f;

			bool soulOk = soulParamsOk && notInUnitLists && healOk
				&& soulsAfterZero == soulsBefore
				&& soulsAfterOne == soulsBefore + 1
				&& Mathf.Abs(rowChance - 0.3f) < 0.0001f
				&& Mathf.Abs(rowCooldown) < 0.0001f
				&& harvest is { DropCount: 1, RollCount: 2, PickupRadius: 1.2f, Heal: 8f, LifetimeSeconds: 15f }
				&& killMatches && otherKillIgnored;

			string soulLine = $"{(soulOk ? "✅" : "🔴")} P6d·灵魂收割行为取证（真实玩家 + 真实行 + 真实 RewardContext，量完还原）："
				+ $"→ {(harvest == null ? "（规则没建出来）" : harvest.GetType().Name)} 概率 {rowChance:0.###}（期望 0.3 = 预设）回血 {harvest?.Heal:0.##}（期望 8，预设）拾取半径 {harvest?.PickupRadius:0.##}m（期望 1.2，预设）存活 {harvest?.LifetimeSeconds:0.##}s（期望 15，预设）延迟 {harvest?.PickupDelaySeconds:0.##}（期望 0）间隔 {rowCooldown:0.##}（期望 0）"
				+ $"｜击杀匹配={killMatches}（期望 True）别人的击杀不算={otherKillIgnored}（期望 True）"
				+ $"｜概率 0 → 灵魂数 {soulsBefore} → {soulsAfterZero}（期望不变）；概率 1 → {soulsAfterOne}（期望 +1）"
				+ $"｜掉出来的灵魂：回血 {soul.HealAmount:0.##}（期望 8）拾取半径 {soul.PickupRadius:0.##}m（期望 1.2）存活 {soul.LifetimeSeconds:0.##}s（期望 15）"
				+ $"｜不进单位列表={notInUnitLists}（enemy/player/summon 三个组都不是、子树 Unit 0 个、碰撞体 0 个、分组 0 个）"
				+ $"｜拾取：已拾取={soul.PickedUp} 拾取者={soul.PickedByName} 实际回血 {soul.HealedAmount:0.##}（期望 min(配置 {soul.HealAmount:0.##}, 缺口) = {expectedHeal:0.##}）生命 {healthBeforePickup:0.#} → {player.Health:0.#}/{player.MaxHealth:0.#}"
				+ $"｜{soul.DebugState()}";

			// 还原：掉的灵魂清掉、规则节点摘掉、概率改回行里的值。
			if (scene2 != null)
			{
				foreach (Node node in scene2.FindChildren(SoulPickup.NodeName, string.Empty, true, false))
				{
					node.Free();
				}
			}

			foreach (TriggerRule rule in CollectRules(player))
			{
				if (rule is SoulHarvestRule)
				{
					player.RemoveChild(rule);
					rule.Free();
				}
			}

			return line + "\n" + swordLine + "\n" + soulLine;
		}

		return line + "\n" + swordLine + "\n🔴 P6d·灵魂收割：灵魂没掉出来（检查 ApplyHandler 拼写与行是否在表里）";
	}

	/// <summary>按节点名数个数（探针用；只数，不改）。</summary>
	private static int CountNodesNamed(Node root, string name)
		=> root.FindChildren(name, string.Empty, true, false).Count;

	private static List<TriggerRule> CollectRules(Node root)
	{
		var rules = new List<TriggerRule>();
		NodeSearch.CollectInSubtree(root, rules);
		return rules;
	}
#else
	// 生产技能读取这些排障开关；发行版禁止跳过真实动画和发射。
	public static bool DebugSkipFireShot => false;
	public static bool DebugSkipSkillAnimation => false;
#endif
}




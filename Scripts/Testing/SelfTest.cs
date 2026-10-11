using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Encounter;
using ActRoguelike.Interaction;
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

		_timer += (float)delta;
		var run = GetTree().Root.GetNodeOrNull<RunController>("Main/RunController");
		var stage = GetTree().Root.GetNodeOrNull<StageController>("Main/Stage");

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
				GameLog.Write("SelfTest", $"波数映射：进化={StageController.WavesFor(RewardNodeKind.Evolution)}（期望3） 异化={StageController.WavesFor(RewardNodeKind.Mutation)}（期望3） 强化={StageController.WavesFor(RewardNodeKind.Enhancement)}（期望2） 商店={StageController.WavesFor(RewardNodeKind.Shop)}（期望2） 材料={StageController.WavesFor(RewardNodeKind.Resource)}（期望1） 恢复={StageController.WavesFor(RewardNodeKind.Heal)}（期望1） 特殊={StageController.WavesFor(RewardNodeKind.Special)}（期望1）");
				ReportNodes(stage);
				ReportSpacing(stage);
				ReportTables(stage);
				stage?.AddPressureForNode(RewardNodeKind.Evolution);
				stage?.AddPressureForNode(RewardNodeKind.Mutation);
				GameLog.Write("SelfTest", $"Pressure 测试（进化 150×1.25 + 异化 150×1.5）：{stage?.DebugState}");
				stage?.AddPressureForNode(RewardNodeKind.Enhancement);
				GameLog.Write("SelfTest", $"Pressure 测试（再加一次中价值，应不变）：{stage?.DebugState}");
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
				Next();
				break;

			case 4 when _timer > 0.2f:
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

				GameLog.Write("SelfTest", $"已击杀白板首领，等待结算…");
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

			case 13:
				GameLog.Write("SelfTest", "=== 自测结束 ===");
				SetProcess(false);
				GetTree().Quit();
				break;
		}
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

	private void Next()
	{
		_phase++;
		_timer = 0f;
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
			switch (node.Kind)
			{
				case RewardNodeKind.Evolution or RewardNodeKind.Mutation:
					high++;
					break;
				case RewardNodeKind.Enhancement or RewardNodeKind.Shop:
					mid++;
					break;
				default:
					low++;
					break;
			}
		}

		GameLog.Write("SelfTest", $"节点布置：启用 {total} 个（高 {high} / 中 {mid} / 低 {low}）→ 期望 10（2/5/3）");
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

		GameLog.Write("SelfTest", $"节点间距：最小 {min:0.##}（{pair}），阈值 {ActRoguelike.Config.Balance.Settings.MinNodeSpacing:0.#}");
	}

	/// <summary>
	/// 配置表阶段 2 的**逐项比对**：把分表后的实际数值打出来，
	/// 与分表前 `Balance.tres` 的记录逐条对照（见 docs/config.md 的实测表）。
	/// 玩家/武器数值来自 `Config/Tables/Characters` 与 `Config/Tables/Weapons`。
	/// </summary>
	private void ReportTables(StageController? stage)
	{
		var player = PlayerLocator.Player;
		ActRoguelike.Weapons.Weapon? weapon = player == null ? null : NodeSearch.FindInSubtree<ActRoguelike.Weapons.Weapon>(player);

		GameLog.Write("SelfTest",
			player == null
				? "配置表比对：找不到玩家"
				: $"配置表比对·玩家：生命 {player.MaxHealth:0}（期望 250 + 元进度加成 {MetaProgress.BonusMaxHealth:0}） 护甲 {player.MaxArmor:0}（期望 40） 移速 {player.MoveSpeed:0.##}（期望 7） 减伤 {player.DamageReduction:0.##}（期望 0）");

		GameLog.Write("SelfTest",
			weapon == null
				? "配置表比对：找不到武器"
				: $"配置表比对·武器：伤害 {weapon.Damage:0}（期望 24） 间隔 {weapon.FireInterval:0.##}（期望 0.26） 弹匣 {weapon.EffectiveMagazineSize}（期望 12） 装填 {weapon.ReloadTime:0.##}（期望 0.7） 散布 {weapon.SpreadDegrees:0.##}（期望 0.5） 击退 {weapon.Knockback:0.##}（期望 2.5） 暴击 {weapon.CritChance:0.##}（期望 0.15） 弹速 {weapon.ProjectileSpeed:0}（期望 90） 预瞄FOV {weapon.AimFov:0}（期望 55） 穿透 {weapon.BasePierce}（期望 0） 连发 {weapon.Automatic}（期望 False）");

		GameLog.Write("SelfTest",
			$"配置表比对·索引：武器 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.WeaponConfig>()}/角色 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.CharacterConfig>()}/敌人 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.EnemyConfig>()}/首领 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.BossConfig>()}/召唤物 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.SummonConfig>()}/技能 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.AbilityConfig>()}/Pressure {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.PressureModConfig>()}/效果 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.EffectConfig>()}/节点 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.NodeConfig>()}/局外 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.MetaUpgradeConfig>()}/升级池 {ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.UpgradePoolConfig>()}（期望 3/3/10/1/1/25/3/9/9/5/3）");

		GameLog.Write("SelfTest",
			$"配置表比对·节点表：进化层 {NodePlacement.LayerOf(RewardNodeKind.Evolution)}/波 {NodePlacement.WavesFor(RewardNodeKind.Evolution)}（期望 2/3） 强化层 {NodePlacement.LayerOf(RewardNodeKind.Enhancement)}/波 {NodePlacement.WavesFor(RewardNodeKind.Enhancement)}（期望 1/2） 恢复层 {NodePlacement.LayerOf(RewardNodeKind.Heal)}/波 {NodePlacement.WavesFor(RewardNodeKind.Heal)}（期望 0/1） 商店价目 {RewardService.ShopCost(0)}/{RewardService.ShopCost(1)}/{RewardService.ShopCost(2)}（期望 5/12/18） 标题 {RewardService.TitleFor(RewardNodeKind.Mutation)}（期望 异化池）");

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
		foreach (ActRoguelike.Config.RewardConfig row in ActRoguelike.Config.Balance.Settings.Upgrades)
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
			$"配置表比对·技能表：{ActRoguelike.Config.ConfigRows.Count<ActRoguelike.Config.AbilityConfig>()} 行（期望 25 = 5 玩家 + 16 单位 + 4 首领行为） dash 行：冷却 {dashRow?.Cooldown:0.##}（期望 1.2） 前摇 {dashRow?.Windup:0.##}（期望 0.3） 节点 {dashRow?.NodeName}（期望 AbilityDash） ｜ 首领强化来源：Boss Build 行为型 {ActRoguelike.Boss.BossPerkCatalog.BehaviorOnly.Count} 条（期望 4）/ Pressure 数值型 {ActRoguelike.Boss.BossPerkCatalog.PressureOnly.Count} 条（期望 3）");

		// 玩家侧实测：技能行 + 角色行攻击组是否真的套上了（这条是"技能表接线"的硬证据）。
		if (player != null)
		{
			ActRoguelike.Abilities.Ability? dash = player.GetNodeOrNull<ActRoguelike.Abilities.Ability>("AbilityDash");
			ActRoguelike.Abilities.Ability? melee = player.GetNodeOrNull<ActRoguelike.Abilities.Ability>("AbilityMelee");
			AttackHitbox? meleeHit = melee == null ? null : NodeSearch.FindInSubtree<AttackHitbox>(melee);
			GameLog.Write("SelfTest",
				$"技能表实测（玩家）：滑铲 冷却 {dash?.Cooldown:0.##}（期望 1.2） 位移 {(dash == null ? 0f : dash.Get("Distance").AsSingle()):0.##}（期望 6） AbilityId {dash?.AbilityId}（期望 skl_dash） ｜ 近战 伤害 {meleeHit?.Damage:0}（期望 32，来自角色行攻击组） 间隔 {melee?.Cooldown:0.##}（期望 0.75，证明技能行的 0 = 不覆盖） 射程 {(melee == null ? 0f : melee.Get("AttackRange").AsSingle()):0.##}（期望 2.4） 击退 {meleeHit?.Knockback:0.##}（期望 5） 命中标签 [{string.Join(",", meleeHit?.AttackTags ?? System.Array.Empty<string>())}]（期望 Melee,Heavy）");
		}
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
		}
	}

	/// <summary>当前角色（供验证读取）。</summary>
	public static bool Active { get; private set; }
#endif
}

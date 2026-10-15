using System;
using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.AbilityCards;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Encounter;
using ActRoguelike.Interaction;
using ActRoguelike.Run;
using ActRoguelike.UI;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>
/// 本局控制器：持有构筑与奖励池，驱动 Stage/Node 流程，并处理胜负结算。
/// </summary>
public partial class RunController : Node
{
	[Export] public bool AutoStartStage { get; set; } = true;

	private readonly RunBuild _build = new();
	private readonly RewardPool _pool = new();
	private readonly List<RewardDefinition> _current = new();

	/// <summary>
	/// 玩家能力节点的**场景基线**（第一次套用角色/武器时抓的那份 `Enabled` / `InputAction` / `AbilityId`）。
	/// 换角色/换武器时先恢复它，再让表写一遍 —— 否则上一套技能的按键绑定会留在能力节点上
	/// （"上一把武器的左键武技还留在节点上"，见 <see cref="ResetAbilityBindings"/>）。
	/// </summary>
	private readonly Dictionary<Ability, (bool Enabled, string InputAction, string AbilityId)> _abilityBaseline = new();

	private Unit? _player;
	private Weapon? _weapon;
	private RewardScreen? _screen;
	private StageController? _stage;
	private bool _poolReady;
	private bool _runEnded;
	private bool _awaitingAdvance;
	private Action? _chosenCallback;
	private StageDefinition[] _stages = Array.Empty<StageDefinition>();
	private int _stageIndex;
	private bool _restartOnDismiss;
	private WeaponPresetKind _weaponKind = WeaponPresetKind.EnergyPalm;
	private ChooseScreen? _chooseScreen;
	private PlayerCharacterKind _character = PlayerCharacterKind.Hunter;
	private int _boot;

	public RunBuild Build => _build;
	public int CandidateCount => _pool.CandidateCount;
	public bool RunEnded => _runEnded;

	/// <summary>本局控制器单例（地面节点通过它领奖励）。</summary>
	public static RunController? Instance { get; private set; }

	public override void _Ready()
	{
		Instance = this;
		AddToGroup("run_controller");
		_screen = UiService.Instance?.Screen<RewardScreen>();
		if (_screen != null)
		{
			_screen.RewardChosen += OnRewardChosen;
			_screen.MessageDismissed += OnMessageDismissed;
		}

		// 兄弟节点的 _Ready 顺序不定（RunController 早于 Stage），这里只尝试一次，失败就在首帧惰性绑定。
		BindStage();
	}

	private void BindStage()
	{
		_stage = GetTree().GetFirstNodeInGroup("stage") as StageController;
		if (_stage == null)
		{
			return;
		}

		_stage.NodeCleared += OnNodeCleared;
		_stage.Victory += OnVictory;
	}

	/// <summary>把 ConfigIndex.tres 的数值套到玩家、武器与节点节奏上（开局与换角色时调用）。</summary>
	private void ApplyBalance()
	{
		ConfigIndexData config = ConfigIndex.Settings;

		// 角色数值来自角色表（`Config/Tables/Characters/chr_*.tres`）；读不到行就用玩家场景里的原始数值。
		CharacterConfig? character = CharacterRow(_character);
		float maxHealth = character?.MaxHealth ?? _player!.MaxHealth;
		float armor = character?.StartingArmor ?? _player!.StartingArmor;
		float moveSpeed = character?.MoveSpeed ?? _player!.MoveSpeed;
		float reduction = character?.DamageReduction ?? _player!.DamageReduction;

		_player!.ApplyBaseStats(maxHealth + MetaProgress.BonusMaxHealth, armor, character?.MaxShield ?? _player.MaxShield, moveSpeed);
		_player.Stats.AddModifier(new StatModifier { Stat = "DamageMultiplier", Operation = ModifierOperation.Multiply, Value = MetaProgress.BonusDamageMultiplier, Source = this });
		_player.Stats.AddModifier(new StatModifier { Stat = "MoveSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = MetaProgress.BonusMoveSpeedMultiplier, Source = this });
		_player.Stats.AddModifier(new StatModifier { Stat = "CritChanceAdd", Operation = ModifierOperation.Add, Value = MetaProgress.BonusCritChance, Source = this });
		// ⚠️ P6b：这里原来还有一条 `ReloadSpeedMultiplier × MetaProgress.BonusReloadSpeed` ——
		// 随"没有弹匣的概念了"整条下线（局外成长行 `meta_reload` 与 `Weapon` 的装填逻辑同时删除）。
		_player.RefreshStats();
		_player.DamageReduction = reduction;

		if (_weapon != null)
		{
			// 武器行的数值（含 `UsesWeaponComponent` → `Weapon.Enabled`）都在 `WeaponConfig.ApplyTo` 里。
			// ⚠️ P6a 起**不再**在这里按角色行把枪收起来（用户 2026-10-15：他没设计过这个，进关卡"只是切场景"）。
			EquipWeapon(_weaponKind);
		}

		// 角色表的能力开关矩阵：取代原来三段硬编码的 `melee.Enabled = heavy` / `summon/control.Enabled = summoner`。
		// 读不到角色行时回退到与分表前逐位一致的那套规则。
		//
		// ⚠️ P6b 核对（**只改注释，没动顺序**）：这一行跑在下面 `character.ApplySkillsTo(_player)` **之前**，
		// 而矩阵的首选匹配键是 `Ability.AbilityId`，那个字段正是 `ApplySkillsTo` 写进去的 ——
		// 于是**第一次套用**（刚进关卡）时 `AbilityId` 还是空串，矩阵只能退到"类名匹配"，
		// 而角色行的两个名单里写的是**技能行 Id**（`skl_a1_skill_1` …），所以这一趟实际匹配不到任何东西。
		// 真正决定开关的是**技能行自己的 `AbilityConfig.Enabled`**，所以现状无害（自测逐位一致）。
		// **为什么不改成"先 ApplySkillsTo 再矩阵"**：矩阵内部的 `ResetAbilityBindings` 会把能力恢复到
		// 场景基线（InputAction 也要恢复）—— 它必须排在**所有**"行 → 能力"的写入之前；挪到技能行之后
		// 会把技能行刚写上的 `InputAction`（后撤的 Q、武技的左右键）一起清掉，是实打实的回归。
		// 真要修得拆成三步（先 reset → 再技能行 → 再矩阵），超出 P6b 范围，留给后续。
		ApplyAbilityMatrix(character);

		if (character != null)
		{
			// ⚠️ 顺序：先技能表（前摇/后摇/朝向/命中体细节/子类参数），再单位的攻击组（伤害/间隔/范围/击退）。
			// 主攻击的这四个数以**单位行**为准，所以角色行写在技能行之后 —— 只留一个真源。
			character.ApplySkillsTo(_player);
			character.ApplyAttackTo(_player);
			// 角色的「表现 · 动画」6 列也要应用 —— 否则表里填了动画名/阈值对玩家无效（死配置）。
			character.ApplyAnimationTo(_player);
		}

		// **当前武器自己的武技**（`Config/Tables/Weapons/wpn_*.tres` 的 `Skills` 列）：
		// 武技属于武器，而一个角色的 3 把武器都用同样的两个按键（weapon_skill_1/2），
		// 所以必须"换武器就换武技行"——挂在角色行上会让上一把的武技留在节点上抢同一个按键。
		// 旧 3 把枪的 `Skills` 是空数组 → 这一行什么都不做（与改动前逐位一致）。
		if (WeaponRow(_weaponKind) is { } weaponRow)
		{
			UnitConfig.ApplySkills(_player, weaponRow.Skills, weaponRow.Id);
		}

		// 能力卡（局外装备层）：激活的卡在这里套到玩家身上（数值修饰 + 授予技能）。
		// 位置刻意放在**角色表的能力开关矩阵与技能行之后** —— 一张"授予技能"的卡要赢过角色的
		// DisabledAbilityIds（卡 = 玩家花局外资源买的额外能力）。规则与存盘在 AbilityStore，见 docs/ability-cards.md。
		AbilityEffects.ApplyToPlayer(_player, this);

		// 奖励候选池：**进关卡初始化时**就按"当前角色 + 当前武器"把池内容定下来（用户 2026-10-12 C2：
		// 「进化池里会出什么选项是根据交互的角色持有的攻击方式（武器）……池里一开始有什么，
		// 应该在进入关卡初始化的时候就判断好的」）。放在这里而不是第一次交互时 ——
		// ApplyBalance 正是"角色/武器都定了、进关卡"的那一刻（BeginRunWithCharacter → ApplyStage(0)）。
		InitializePool();

		if (_stage == null)
		{
			return;
		}

		foreach (Node child in _stage.GetChildren())
		{
			if (child is not EncounterController node)
			{
				continue;
			}

			node.TotalBudget *= config.NodeBudgetMultiplier;
			node.SpawnInterval *= config.SpawnIntervalMultiplier;
		}
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}

		if (_screen != null)
		{
			_screen.RewardChosen -= OnRewardChosen;
			_screen.MessageDismissed -= OnMessageDismissed;
		}

		if (_stage != null)
		{
			_stage.NodeCleared -= OnNodeCleared;
			_stage.Victory -= OnVictory;
		}
	}

	public override void _Process(double delta)
	{
		_build.TickTriggers((float)delta);

		if (_stage == null)
		{
			BindStage();
		}

		if (_player == null || !IsInstanceValid(_player))
		{
			ResolvePlayer();
		}

		// 开局引导：等玩家、武器、Stage 都就绪 → 套用平衡配置 → 应用第一关配置。
		if (_boot == 0 && _player != null && _weapon != null && _stage != null)
		{
			_boot = 1;
			ApplyBalance();
			_stages = ResolveStages();
			MetaProgress.Load();

			// ⚠️ **命令行旁路必须判在「基地已经选过一局」之前**（`docs/anim-pipeline.md` §6.4-2）：
			// `RunSetup.HasSelection` 是**静态**的，基地里选过一局之后就一直是 true，
			// 而它下面那条分支会 `return`，于是 `BeginConfigure`（只有它读命令行）永远轮不到 ——
			// `--char/--weapon` 就"静默失效"了。放在这里，两边都成立：
			//   ① 给了参数 → 直接按参数开局（跳过基地/选人界面）；
			//   ② 没给参数 → 完全走原来的流程（基地已配置过就直接开局，否则开基地界面）。
			if (TryApplyLoadoutOverride())
			{
				_boot = 2;
				return;
			}

			// 主循环起点：**基地场景**（`Scenes/Levels/Base.tscn`）→ 出击门（`BaseTerminal` 的
			// `Kind = Gate`，走到跟前按 F）→ 本场景。出击门已经先把角色/武器写进 `RunSetup` 再切场景，
			// 所以到这里"只是切场景"：直接按那份配置开局。
			// ⚠️ P6a **删掉了「开始探索」按钮与它后面那道关卡内选人/选武器界面** —— 用户 2026-10-15：
			// 「那个本身按了没意义……后来要求的进关卡就是去交互一个对象来进关卡，基地里是有做了一个的」。
			// 没走基地就直接跑 `Main.tscn`（MCP / 命令行调试）时，`RunSetup` 的默认值
			// （角色 A［猎手］+ 能量掌）就是这一局的配置，这条路一样成立。
			ActRoguelike.Core.GameLog.Write(
				"Flow",
				$"开局配置：角色 {RunSetup.Character} / 武器 {RunSetup.Weapon}（基地里选过={RunSetup.HasSelection}）—— 进关卡只是切场景，直接开局");
			BeginRunWithCharacter(RunSetup.Character, RunSetup.Weapon);
			_boot = 2;
		}

		if (_player is { IsDead: true } && !_runEnded)
		{
			EndRun(false);
		}
	}

	/// <summary>战斗清场：现在只是回到自由探索，不再自动发奖励（策划文档 §6.2 的流程由地面节点驱动）。</summary>
	private void OnNodeCleared(int index, EncounterController node)
	{
	}

	private void OnVictory()
	{
		// 打完一关的 Boss：还有下一关就推进，否则通关。
		int cleared = _stageIndex;
		if (_stageIndex + 1 < _stages.Length)
		{
			ApplyStage(_stageIndex + 1);
			_restartOnDismiss = false;
			_screen?.OpenMessage(
				$"{_stages[cleared].DisplayName} 完成",
				$"第 {cleared + 1} 关结束，构筑与生命保留。\n{_build.Describe()}\n\n进入 {_stages[_stageIndex].DisplayName}",
				"按 Enter 或点击继续");
			return;
		}

		EndRun(true);
	}

	/// <summary>应用第 index 关（切关时玩家回出生点、回满，关卡状态由 Stage 自己重置）。</summary>
	private void ApplyStage(int index)
	{
		_stageIndex = index;
		ActRoguelike.Core.GameLog.Write("Flow", $"进入第 {index + 1} 关：{_stages[index].DisplayName}");
		_stage?.ApplyStage(_stages[index], index + 1);

		if (_player != null && IsInstanceValid(_player))
		{
			_player.GlobalPosition = new Vector3(0f, 0.8f, 0f);
			_player.FillVitals();
		}
	}

	/// <summary>
	/// 调试用的"直接开局"：命令行 <c>--char=&lt;角色行 Id&gt; --weapon=&lt;武器行 Id&gt;</c>
	/// （也可以传枚举名，例如 <c>--char=Hunter</c>）。
	///
	/// **按表行 Id 选**（不是按索引）—— 这正是"加角色/武器不改代码"该有的样子。
	/// 一个参数都没给就返回 false，走正常流程（基地里选好的那份配置，或默认值）。
	///
	/// <para>⚠️ P6a：关卡内那道 <c>BeginConfigure</c> 选人界面已随「开始探索」按钮一起删除
	/// （用户 2026-10-15：「后来要求的进关卡就是去交互一个对象来进关卡」）。这条命令行旁路保留 ——
	/// 它只改"这一局用哪个角色/武器"，与界面无关。</para>
	/// </summary>
	private bool TryApplyLoadoutOverride()
	{
		string? charArg = null;
		string? weaponArg = null;
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--char=", StringComparison.Ordinal))
			{
				charArg = arg.Substring("--char=".Length);
			}
			else if (arg.StartsWith("--weapon=", StringComparison.Ordinal))
			{
				weaponArg = arg.Substring("--weapon=".Length);
			}
		}

		if (string.IsNullOrEmpty(charArg) && string.IsNullOrEmpty(weaponArg))
		{
			return false;
		}

		PlayerCharacterKind character = _character;
		WeaponPresetKind weapon = _weaponKind;

		if (!string.IsNullOrEmpty(charArg) && !TryResolveCharacter(charArg, out character))
		{
			GD.PushWarning($"[RunController] --char={charArg} 在角色表里找不到（也没有同名枚举），忽略。");
		}

		if (!string.IsNullOrEmpty(weaponArg) && !TryResolveWeapon(weaponArg, out weapon))
		{
			GD.PushWarning($"[RunController] --weapon={weaponArg} 在武器表里找不到（也没有同名枚举），忽略。");
		}

		ActRoguelike.Core.GameLog.Write("Flow", $"命令行指定开局：角色 {charArg ?? "（默认）"} → {character} / 武器 {weaponArg ?? "（默认）"} → {weapon}");
		RunSetup.HasSelection = true;
		RunSetup.Character = character;
		RunSetup.Weapon = weapon;
		BeginRunWithCharacter(character, weapon);
		return true;
	}

	/// <summary>按角色表行 Id（或枚举名）解析角色。表是权威 —— 加一行角色就能直接 --char 用。</summary>
	public static bool TryResolveCharacter(string idOrName, out PlayerCharacterKind kind)
	{
		foreach (CharacterConfig row in ConfigIndex.Settings.Characters)
		{
			if (row != null && string.Equals(row.Id, idOrName, StringComparison.OrdinalIgnoreCase))
			{
				kind = (PlayerCharacterKind)(row.Slot - 1);
				return true;
			}
		}

		return Enum.TryParse(idOrName, true, out kind);
	}

	/// <summary>按武器表行 Id（或枚举名）解析武器。</summary>
	public static bool TryResolveWeapon(string idOrName, out WeaponPresetKind kind)
	{
		foreach (WeaponConfig row in ConfigIndex.Settings.Weapons)
		{
			if (row != null && string.Equals(row.Id, idOrName, StringComparison.OrdinalIgnoreCase))
			{
				kind = (WeaponPresetKind)(row.Slot - 1);
				return true;
			}
		}

		return Enum.TryParse(idOrName, true, out kind);
	}

	/// <summary>
	/// 关卡序列的来源（设计文档 config-schema.md §4.2 / §5 阶段 1）：
	/// **只有一个真源** —— <see cref="ActRoguelike.Config.ConfigIndexData.Stages"/>（`Config/ConfigIndex.tres`
	/// 上的数组，元素指向 `Config/Tables/Stages/stg_*.tres`）。
	/// 读不到 / 空表 → 回退到 <see cref="StageDefinition"/> 自身的 `[Export]` 默认值，
	/// **不再有第二份兜底表**（原来的 `DefaultStages()` 与 `ConfigIndex.tres` 已经漂移了 5 处，
	/// 最严重的是 Stage2 的 AllowedEnemies 走兜底会变成"不限制"）。
	/// ⚠️ 实测：删/改一个被引用的行文件 → Godot 会**整份** `ConfigIndex.tres` 加载失败，
	/// 所以走的是"0 关 → 单关默认值"这条粗回退（其他数值也一起回退）。别再删行文件，见 docs/config-schema.md §5。
	/// 回退留痕：日志里能直接看到这一局走的是"表"还是"默认值"。
	/// </summary>
	private static StageDefinition[] ResolveStages()
	{
		StageDefinition[] table = ActRoguelike.Config.ConfigIndex.Settings.Stages;
		if (table is not { Length: > 0 })
		{
			// 连"整张表"都读不到时，把全局数值的来源也打出来：这样一眼能区分
			// 「只是关卡表为空」和「连 ConfigIndex.tres 都没加载上（所有数值都回退了）」。
			string source = ActRoguelike.Config.ConfigIndex.Instance?.FileLoaded == true ? "ConfigIndex.tres 已加载" : "ConfigIndex.tres 未加载（全部数值回退到代码默认值）";
			ActRoguelike.Core.GameLog.Write("Flow", $"关卡表：0 关（来源：默认值）—— ConfigIndex 的 Stages 为空或未找到，使用 StageDefinition 自身的默认值。（{source}）");
			GD.PushWarning("[Config] 关卡表为空，使用 StageDefinition 自身默认值。");
			return new[] { new StageDefinition() };
		}

		var stages = new List<StageDefinition>(table.Length);
		int missing = 0;
		foreach (StageDefinition? row in table)
		{
			// ⚠️ 实测（2026-10）：行文件被删/改名时，**在游戏里根本走不到这里** —— 缺依赖会让
			// `GD.Load<ConfigIndexData>` 整份失败（于是走上面"0 关"那条分支，所有数值一起回退到代码默认值，
			// 实测玩家生命 250→160）。这个空档分支只在"数组里真的出现空元素"时兜底，纯粹是防 NullReference。
			if (row == null)
			{
				missing++;
				stages.Add(new StageDefinition());
				continue;
			}

			stages.Add(row);
		}

		var summary = new System.Text.StringBuilder();
		foreach (StageDefinition row in stages)
		{
			summary.Append($" | {row.Id} {row.DisplayName} 允许敌人[{row.AllowedEnemyIds.Length}] 敌伤×{row.EnemyDamageMultiplier:0.##} Boss伤×{row.BossDamageMultiplier:0.##} Boss词条{row.BossPerkCount}");
		}

		ActRoguelike.Core.GameLog.Write("Flow", $"关卡表：{stages.Count} 关（来源：表{(missing > 0 ? $"，缺 {missing} 行→该关用默认值" : string.Empty)}）{summary}");
		if (missing > 0)
		{
			GD.PushWarning($"[Config] 关卡表有 {missing} 行读不到（文件被删/改名？），这些关使用 StageDefinition 自身默认值。");
		}

		return stages.ToArray();
	}

	private void EndRun(bool victory)
	{
		if (_runEnded)
		{
			return;
		}

		_runEnded = true;
		ActRoguelike.Core.GameLog.Write("Flow", $"本局结束 victory={victory} 关卡={_stage?.Definition?.DisplayName} 用时={(_stage?.ElapsedSeconds ?? 0f):0}s");
		_restartOnDismiss = true;
		float seconds = _stage?.ElapsedSeconds ?? 0f;
		string body = victory
			? $"通关！三关全部清理，用时 {seconds:0} 秒\n{_build.Describe()}"
			: $"你在 {_stage?.Definition?.DisplayName ?? "关卡"} 阵亡\n用时 {seconds:0} 秒\n{_build.Describe()}";
		int earned = RunSettlement.ShardsFor(_stageIndex, _stage?.ChallengesUsed ?? 0, victory);
		MetaProgress.AddShards(earned);
		body += $"\n获得碎片 +{earned}（累计 {MetaProgress.Shards}）";

		// 阵亡 → **死亡流程**（用户需求 §五 #3）：死亡动画 → 演示过渡面板（占位）→ 回基地。
		// 这一段**替代**原来的"立刻弹结算消息、按 Enter 回基地"，所以要在 OpenMessage 之前返回；
		// 场景里没有 DeathScreen 时（老场景 / 单独跑某个测试场景）自动落回原来的消息路径，不会卡住。
		// ⚠️ 顺带修掉一个既有 bug：原来连着调了两次 `OpenMessage`（第二次想补上碎片那行），
		// 而 `UiService.OpenMessage` 要求 `IsOpen == false` —— 第二次必然被拒，所以"获得碎片 +N"从来没显示过。
		if (!victory && GetTree().CurrentScene?.GetNodeOrNull<DeathScreen>("DeathScreen") is { } death)
		{
			// 回基地由 DeathScreen 自己切场景，不再需要"按 Enter 重开"那条路。
			_restartOnDismiss = false;
			death.BeginDeath("本局结束", body);
			return;
		}

		_screen?.OpenMessage(victory ? "胜利" : "本局结束", body, "按 Enter 或点击重新开始");
	}

	private void OnMessageDismissed()
	{
		_screen?.Close();

		// 只有结算（胜利/失败）才重开一局；关卡之间的提示是继续本局。
		if (!_restartOnDismiss)
		{
			return;
		}

		GetTree().Paused = false;
		// 结算后回基地场景（主场景是基地：出征 → 关卡 → 回基地）。
		RunSetup.Clear();
		ActRoguelike.Core.GameLog.Write("Flow", "本局结束 → 返回基地场景");
		GetTree().ChangeSceneToFile("res://Scenes/Levels/Base.tscn");
	}

	/// <summary>直接发放指定奖励（保底奖励、脚本奖励与自动化验证用）。</summary>
	public bool GrantReward(string id)
	{
		if (_player == null || !IsInstanceValid(_player))
		{
			ResolvePlayer();
		}

		RewardDefinition? definition = RewardCatalog.Find(id);
		if (definition == null || _player == null || _weapon == null)
		{
			return false;
		}

		_build.Add(definition, new RewardContext { Player = _player, Weapon = _weapon, Build = _build });
		return true;
	}

	/// <summary>换装：三把武器共用一个 Weapon 节点，只换数值（数值权威在 `Config/Tables/Weapons/wpn_*.tres`）。
	/// 这样不用重建节点，也就不会把挂在武器下的输入组件一起弄丢。</summary>
	public void EquipWeapon(WeaponPresetKind kind)
	{
		_weaponKind = kind;
		if (_weapon == null || !IsInstanceValid(_weapon))
		{
			return;
		}

		WeaponConfig? row = WeaponRow(kind);
		if (row != null)
		{
			// 表是权威：一行 `.tres` 描述整把武器（含击退、暴击、弹速）。读不到行时才走下面的回退。
			row.ApplyTo(_weapon);
			return;
		}

		// 回退（表为空 / 整份 ConfigIndex.tres 未加载）：保留"什么都没做"的语义 ——
		// 武器组件自己的 [Export] 默认值就是这一把枪的数值。
		GD.PushWarning($"[Config] 武器表里没有 {kind}（Slot {(int)kind + 1}）的行，保留武器组件自身的默认值。");
	}

	/// <summary>按 <see cref="WeaponPresetKind"/> 取武器表的行（Slot 从 1 开始，与枚举顺序一致）。</summary>
	public static WeaponConfig? WeaponRow(WeaponPresetKind kind)
		=> ConfigRows.BySlot<WeaponConfig>((int)kind + 1);

	/// <summary>按 <see cref="PlayerCharacterKind"/> 取角色表的行（Slot 从 1 开始，与枚举顺序一致）。</summary>
	public static CharacterConfig? CharacterRow(PlayerCharacterKind kind)
		=> ConfigRows.BySlot<CharacterConfig>((int)kind + 1);

	/// <summary>
	/// 角色选择界面的卡片文案（标题 + 数值**全部来自角色表**：`DisplayName` + `DescriptionTemplate`，
	/// 后者支持 <c>{字段名}</c> 占位符）。
	///
	/// <para>⚠️ P6a **删掉了旧的 <c>DefaultCharacterTitle</c> 那张"角色枚举 → 手写中文标题"的映射表**
	/// —— 那正是"代码里写死角色名"（用户 §1.5 的硬要求）。现在加一个角色 = 加一行 `.tres`，
	/// 标题跟 `DisplayName` 走，C# 一行不动。</para>
	///
	/// <para>**public**：基地的装备配置台（<c>BaseTerminal</c>）复用它，角色卡的文案就只有这一处来源。</para>
	/// </summary>
	public static string CharacterCardText(PlayerCharacterKind kind)
	{
		CharacterConfig? row = CharacterRow(kind);
		if (row == null)
		{
			// 整张角色表都读不到：连名字都编不出来（这时玩家用的是场景里的原始数值）。
			return "（角色表读不到）";
		}

		string title = string.IsNullOrEmpty(row.DisplayName) ? row.Id : row.DisplayName;
		string detail = ConfigText.Format(
			string.IsNullOrEmpty(row.DescriptionTemplate)
				? "（{MaxHealth:0} 生命 / {StartingArmor:0} 护甲 / 减伤 {DamageReduction}）"
				: row.DescriptionTemplate,
			row,
			row.Params);
		return $"{title}（{detail}）";
	}

	/// <summary>
	/// 角色表的能力开关矩阵。两个名单里写 <b>技能行 Id</b>（`skl_a1_w1_1` —— 可读、稳定、一个技能一个名字）
	/// **或**脚本类名（`MeleeAttackAbility` —— 旧写法，仍然认）。
	///
	/// ⚠️ 按类名匹配有个硬伤：**同一个类的两个技能没法分开开关**（角色 A 的两个武技都是
	/// <c>EnergyWaveAbility</c>）。所以本轮把首选键改成 <c>Ability.AbilityId</c>：它在
	/// <c>AbilityConfig.ApplyTo</c> 里被写成技能行的 Id。
	///
	/// <para>⚠️ <b>P6b 核对（2026-10-16）：这里原来写的是"时机在矩阵之前（先 ApplySkillsTo 再矩阵）"，
	/// 与代码相反 —— 实际是矩阵先跑、技能行后跑</b>（见 <c>ApplyBalance</c> 里那一大段说明）。
	/// 所以在**第一次**套用（刚进关卡）时 <c>AbilityId</c> 还是空串 → 只能退到类名匹配 →
	/// 与角色行里按**技能行 Id** 写的名单对不上 → 这一趟矩阵**实际什么都没改**。
	/// 真正决定开关的是技能行自己的 <c>AbilityConfig.Enabled</c>，所以现状无害（自测逐位一致）。
	/// 修法见 <c>ApplyBalance</c> 的注释：要拆成"先 reset → 再技能行 → 再矩阵"三步，本轮不做。</para>
	///
	/// 读不到角色行时**只恢复场景基线**（不再有 `melee.Enabled = heavy` / `summon = summoner` 那套
	/// 旧角色的硬编码规则 —— 那些角色已在 P6a 删除，`heavy` / `summoner` 两个变量也随之消失）。
	/// </summary>
	private void ApplyAbilityMatrix(CharacterConfig? character)
	{
		var abilities = new System.Collections.Generic.List<Ability>();
		NodeSearch.CollectInSubtree(_player!, abilities);

		// ⚠️ 先把能力恢复到**场景基线**再做矩阵：能力节点是 `Player.tscn` 的固定节点，而按键绑定/开关是
		// **上一个角色/武器的技能行**写上去的，只套用"新角色列的那些行"清不掉它们（§6.4.6 的残留）。
		// 这一步是幂等的：换回同一个角色与一开局就是它得到同一份状态。
		ResetAbilityBindings(abilities);

		foreach (Ability ability in abilities)
		{
			if (character == null)
			{
				continue;
			}

			string classId = ability.GetType().Name;
			string rowId = ability.AbilityId;

			if (Matches(character.EnabledAbilityIds, rowId, classId))
			{
				ability.Enabled = true;
			}
			else if (Matches(character.DisabledAbilityIds, rowId, classId))
			{
				ability.Enabled = false;
			}
		}

		ActRoguelike.Core.GameLog.Write(
			"Flow",
			$"能力开关矩阵（角色 {character?.Id ?? "（表读不到，走代码默认值）"}）：{DescribeAbilities(abilities)}");
	}

	/// <summary>
	/// 把玩家身上的能力**恢复到场景基线**（幂等）：每次套用角色/武器之前先做一次，之后由技能行、开关矩阵与
	/// 能力卡重新写一遍。
	///
	/// <para><b>为什么需要它</b>（docs/anim-pipeline.md §6.4.6）：<c>Ability.InputAction</c> / <c>Ability.Enabled</c>
	/// 是**技能行**写在 `Player.tscn` 那两个能力节点上的，而换角色只套用"新角色 <c>Skills</c> 里列的那些行" ——
	/// 上一套写下的绑定不会自己消失。`weapon_skill_1` 与 `fire` 都是**鼠标左键**，所以残留表现为
	/// "Gunner + 手枪的左键除了手枪还打出一发能量波"。正常流程（死亡 → 回基地 → 出击门）会重建整个 <c>Main</c>，
	/// 只有"同一个场景里换角色/换武器"（自测与 MCP 调试）才会踩到。</para>
	///
	/// <para>基线在**第一次**调用时抓：那一刻玩家的能力节点还是 `Player.tscn` 的原始值（这个函数排在
	/// <see cref="ApplyBalance"/> 里所有"行 → 能力"的写入之前，包括能力卡）。</para>
	/// </summary>
	private void ResetAbilityBindings(System.Collections.Generic.List<Ability> abilities)
	{
		foreach (Ability ability in abilities)
		{
			if (!IsInstanceValid(ability))
			{
				continue;
			}

			if (!_abilityBaseline.TryGetValue(ability, out (bool Enabled, string InputAction, string AbilityId) baseline))
			{
				baseline = (ability.Enabled, ability.InputAction, ability.AbilityId);
				_abilityBaseline[ability] = baseline;
			}

			ability.Enabled = baseline.Enabled;
			ability.InputAction = baseline.InputAction;
			ability.AbilityId = baseline.AbilityId;
		}
	}

	/// <summary>名单命中判定：技能行 Id 优先，其次脚本类名（旧写法）。</summary>
	private static bool Matches(string[] names, string rowId, string classId)
	{
		if (names.Length == 0)
		{
			return false;
		}

		return (!string.IsNullOrEmpty(rowId) && System.Array.IndexOf(names, rowId) >= 0)
			|| System.Array.IndexOf(names, classId) >= 0;
	}

	/// <summary>把"哪个技能开、哪个关"写进日志 —— 加技能后"按了没反应"最常见的原因就是这一行没配对。</summary>
	private static string DescribeAbilities(System.Collections.Generic.List<Ability> abilities)
	{
		var text = new System.Text.StringBuilder();
		foreach (Ability ability in abilities)
		{
			if (text.Length > 0)
			{
				text.Append('、');
			}

			string key = string.IsNullOrEmpty(ability.InputAction) ? "-" : ability.InputAction;
			text.Append($"{ability.AbilityId}[{key}]={(ability.Enabled ? "开" : "关")}");
		}

		return text.ToString();
	}

	/// <summary>选好角色与武器 → 套用数值并进入第一关。</summary>
	public void BeginRunWithCharacter(PlayerCharacterKind character, WeaponPresetKind kind)
	{
		_character = character;
		ActRoguelike.Core.GameLog.Write("Flow", $"选择角色 {character} / 武器 {kind}");
		_weaponKind = kind;
		ApplyBalance();

		if (_stages.Length == 0)
		{
			_stages = ResolveStages();
		}

		ApplyStage(0);
	}

	/// <summary>
	/// **按当前角色与武器把奖励候选池定下来**（池里有什么 = 交互的角色 × 攻击方式，用户 C2）。
	///
	/// 调用点只有一处：<see cref="ApplyBalance"/>（= 进关卡初始化）。在此之前池是"每次交互时惰性建"的
	/// 四个分支 —— 谁先被交互（例如先撞上特殊奖励节点）就会拿到不同的池。
	/// 现在初始化只有这一条路，角色/武器限定层一起定，交互时只从定好的池里抽。
	/// </summary>
	private void InitializePool()
	{
		if (_weapon == null)
		{
			return;
		}

		// ① 限定（角色层）：**当前角色的表行 Id**（`chr_hunter` …）。`<A限定>` 就是不填 Pool、只填这一列；
		//    `<A1限定>` = 这一列 + 选项行 Pool 指向该武器行的 Pool（资源引用，见 RewardConfig.RequiredCharacterIds）。
		string characterId = CharacterRow(_character)?.Id ?? string.Empty;
		// ② 限定（武器层）：专属池（ApplicablePoolIds）→ 武器标签（RequiredWeaponTags）→ 通用。
		_pool.Initialize(_weapon, characterId);
		_poolReady = true;

		// 留痕（用户 C2 问的就是"池是什么时候定的"）：这一行只在进关卡初始化时出现，
		// 之后每次交互只从这份池里抽（抽取时仍然要按当前构筑过滤互斥/前置/层数/节点类型）。
		string poolId = string.IsNullOrEmpty(_weapon.PoolId) ? "留空（走标签匹配 → 通用）" : _weapon.PoolId;
		ActRoguelike.Core.GameLog.Write(
			"Reward",
			$"奖励池按当前角色/武器定下来了（进关卡初始化）：角色 {_character}（行 Id「{(characterId.Length == 0 ? "（角色表读不到）" : characterId)}」）· 武器 {_weaponKind}（PoolId {poolId}）→ 候选 {_pool.CandidateCount} 条（限定两层之后）");
	}

	/// <summary>兜底：池子还没定就先定一次（正常路径一定在进关卡时已经定过）。</summary>
	private void EnsurePool()
	{
		if (!_poolReady)
		{
			InitializePool();
		}
	}

	/// <summary>稀有池三选一（特殊奖励节点）：只出进化与异化。</summary>
	private bool OfferRareRewards(Action onChosen)
	{
		if (_player == null || _weapon == null || _screen == null || _screen.IsOpen || _runEnded)
		{
			return false;
		}

		EnsurePool();

		_current.Clear();
		_current.AddRange(_pool.Roll(_build, ConfigIndex.Settings.RewardChoiceCount, null, new[] { RewardKind.Evolution, RewardKind.Mutation }));
		if (_current.Count == 0)
		{
			return false;
		}

		_chosenCallback = onChosen;
		_screen.Open(_current, "特殊奖励 · 稀有");
		return true;
	}

	/// <summary>打开一次强化三选一（商店购买、保底奖励用）。</summary>
	private bool OfferRewards()
	{
		if (_player == null || _weapon == null || _screen == null || _screen.IsOpen || _runEnded)
		{
			return false;
		}

		EnsurePool();

		_current.Clear();
		_current.AddRange(_pool.Roll(_build, ConfigIndex.Settings.RewardChoiceCount));
		if (_current.Count == 0)
		{
			return false;
		}

		_chosenCallback = null;
		_screen.Open(_current, RewardService.TitleFor(RewardNodeKind.Enhancement));
		return true;
	}

	/// <summary>通用多选一界面（进化池、商店等）。allowCancel = 允许 Esc 放弃（回调收到 -1）。</summary>
	public bool OpenChoice(string title, IReadOnlyList<string> options, Action<int> onChosen, bool allowCancel = false)
	{
		_chooseScreen ??= UiService.Instance?.Screen<ChooseScreen>();
		if (_chooseScreen == null || _chooseScreen.IsOpen)
		{
			return false;
		}

		_chooseScreen.AllowCancel = allowCancel;
		_chooseScreen.Open(title, options, onChosen);
		return true;
	}

	/// <summary>调试 / 验证用：当前构筑下池子里还能抽到的奖励 Id（逗号分隔）。</summary>
	public string PoolPreview
	{
		get
		{
			if (_weapon == null)
			{
				return string.Empty;
			}

			EnsurePool();

			List<RewardDefinition> list = _pool.Roll(_build, 40);
			return string.Join(",", list.ConvertAll(reward => reward.Id));
		}
	}

	/// <summary>按节点类型决定池过滤，选完之后回调（由 Stage 接着开一场 Encounter）。</summary>
	public bool OpenRewardForNode(RewardNodeKind kind, Action onChosen)
	{
		if (_player == null || !IsInstanceValid(_player) || _weapon == null || _screen == null || _screen.IsOpen || _runEnded)
		{
			return false;
		}

		// 商店节点：花局外碎片买局内收益（策划文档 §6.2 的节点类型扩展）。
		// 无论买不买都会触发本场 Encounter（节点 = 一次收益 + 一次挑战）。
		if (kind == RewardNodeKind.Shop)
		{
			// 价目来自节点表的 node_shop 行（`Config/Tables/Nodes/node_shop.tres`）。
			int healCost = RewardService.ShopCost(0);
			int rewardCost = RewardService.ShopCost(1);
			int weaponCost = RewardService.ShopCost(2);

			return OpenChoice(RewardService.ShopTitle(MetaProgress.Shards), RewardService.ShopOffers(MetaProgress.Shards), index =>
			{
				if (index == 0 && MetaProgress.TrySpend(healCost))
				{
					_player?.FillVitals();
				}
				else if (index == 1 && MetaProgress.TrySpend(rewardCost))
				{
					OfferRewards();
				}
				else if (index == 2 && MetaProgress.TrySpend(weaponCost))
				{
					if (!OpenRewardForNode(RewardNodeKind.Evolution, onChosen))
					{
						onChosen();
					}

					return;
				}

				onChosen();
			}, false);
		}

		// 局外资源节点：直接给碎片（策划文档 §6.2 的「局外资源」）。
		if (kind == RewardNodeKind.Resource)
		{
			MetaProgress.AddShards(5);
			onChosen();
			return true;
		}

		// 特殊奖励节点：连着开两次「稀有池」（进化 / 异化），相当于双倍且有保底价值。
		if (kind == RewardNodeKind.Special)
		{
			return OfferRareRewards(() => OfferRareRewards(onChosen));
		}

		// 战斗型回复节点：先免费恢复一部分生命值，随后照常进 Encounter（关卡核心文档 L180-183）。
		if (kind == RewardNodeKind.Heal)
		{
			_player.Heal(_player.MaxHealth * ConfigIndex.Settings.NodeHealRatio);
			onChosen();
			return true;
		}

		EnsurePool();

		// 进化（没有「武器池」这个类型，见用户 A3）/ 局外资源 / 特殊奖励暂时没有独立系统，先走通用池。
		RewardKind? filter = RewardService.FilterFor(kind);

		_current.Clear();
		_current.AddRange(_pool.Roll(_build, ConfigIndex.Settings.RewardChoiceCount, filter));
		if (_current.Count == 0)
		{
			return false;
		}

		_chosenCallback = onChosen;
		_screen.Open(_current, RewardService.TitleFor(kind));
		return true;
	}


	private void OnRewardChosen(int index)
	{
		if (index >= 0 && index < _current.Count && _player != null && _weapon != null)
		{
			_build.Add(_current[index], new RewardContext { Player = _player, Weapon = _weapon, Build = _build });
		ActRoguelike.Core.GameLog.Write("Reward", $"选择奖励 {_current[index].Name}（{_current[index].Kind}）");
		}

		_screen?.Close();

		Action? callback = _chosenCallback;
		_chosenCallback = null;
		callback?.Invoke();

		if (_awaitingAdvance)
		{
			_awaitingAdvance = false;
			_stage?.Advance();
		}
	}

	private void ResolvePlayer()
	{
		foreach (Node node in GetTree().GetNodesInGroup("player_units"))
		{
			if (node is Unit unit)
			{
				_player = unit;
				_weapon = NodeSearch.FindInSubtree<Weapon>(unit);
				return;
			}
		}
	}
}
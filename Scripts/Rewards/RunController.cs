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
	private WeaponPresetKind _weaponKind = WeaponPresetKind.Pistol;
	private StartScreen? _startScreen;
	private BaseScreen? _baseScreen;
	private ChooseScreen? _chooseScreen;
	private PlayerCharacterKind _character = PlayerCharacterKind.Gunner;
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

	/// <summary>把 Balance.tres 的数值套到玩家、武器与节点节奏上（开局与换角色时调用）。</summary>
	private void ApplyBalance()
	{
		BalanceConfig config = Balance.Settings;
		bool heavy = _character == PlayerCharacterKind.Heavy;
		bool summoner = _character == PlayerCharacterKind.Summoner;

		// 角色数值来自角色表（`Config/Tables/Characters/chr_*.tres`）；读不到行就用玩家场景里的原始数值。
		CharacterConfig? character = CharacterRow(_character);
		float maxHealth = character?.MaxHealth ?? _player!.MaxHealth;
		float armor = character?.MaxArmor ?? _player!.MaxArmor;
		float moveSpeed = character?.MoveSpeed ?? _player!.MoveSpeed;
		float reduction = character?.DamageReduction ?? _player!.DamageReduction;

		_player!.ApplyBaseStats(maxHealth + MetaProgress.BonusMaxHealth, armor, character?.MaxShield ?? _player.MaxShield, moveSpeed);
		_player.Stats.AddModifier(new StatModifier { Stat = "DamageMultiplier", Operation = ModifierOperation.Multiply, Value = MetaProgress.BonusDamageMultiplier, Source = this });
		_player.Stats.AddModifier(new StatModifier { Stat = "MoveSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = MetaProgress.BonusMoveSpeedMultiplier, Source = this });
		_player.Stats.AddModifier(new StatModifier { Stat = "CritChanceAdd", Operation = ModifierOperation.Add, Value = MetaProgress.BonusCritChance, Source = this });
		_player.Stats.AddModifier(new StatModifier { Stat = "ReloadSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = MetaProgress.BonusReloadSpeed, Source = this });
		_player.RefreshStats();
		_player.DamageReduction = reduction;

		if (_weapon != null)
		{
			EquipWeapon(_weaponKind);
			// 近战角色收起枪械：左键改由近战能力接管（PlayerWeaponInput 里分流）。
			// 开关矩阵也来自角色表（读不到行时回退到原来的 `!heavy` 规则）。
			_weapon.Enabled = character?.WeaponEnabled ?? !heavy;
		}

		// 角色表的能力开关矩阵：取代原来三段硬编码的 `melee.Enabled = heavy` / `summon/control.Enabled = summoner`。
		// 读不到角色行时回退到与分表前逐位一致的那套规则。
		ApplyAbilityMatrix(character, heavy, summoner);

		if (character != null)
		{
			// ⚠️ 顺序：先技能表（前摇/后摇/朝向/命中体细节/子类参数），再单位的攻击组（伤害/间隔/范围/击退）。
			// 主攻击的这四个数以**单位行**为准，所以角色行写在技能行之后 —— 只留一个真源。
			character.ApplySkillsTo(_player);
			character.ApplyAttackTo(_player);
			// 角色的「表现 · 动画」6 列也要应用 —— 否则表里填了动画名/阈值对玩家无效（死配置）。
			character.ApplyAnimationTo(_player);
		}

		// 能力卡（局外装备层）：激活的卡在这里套到玩家身上（数值修饰 + 授予技能）。
		// 位置刻意放在**角色表的能力开关矩阵与技能行之后** —— 一张"授予技能"的卡要赢过角色的
		// DisabledAbilityIds（卡 = 玩家花局外资源买的额外能力）。规则与存盘在 AbilityStore，见 docs/ability-cards.md。
		AbilityEffects.ApplyToPlayer(_player, this);

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
			_startScreen = UiService.Instance?.Screen<StartScreen>();
			_baseScreen = UiService.Instance?.Screen<BaseScreen>();
			MetaProgress.Load();
			if (_baseScreen != null)
			{
				// 主循环起点：Base（局外强化）→ Configure（选武器）→ Stage。
				// 从基地场景出发时已经配置过，直接开局（见 BaseTerminal 的出击门）。
		if (RunSetup.HasSelection)
		{
			ActRoguelike.Core.GameLog.Write("Flow", $"来自基地的配置：角色 {RunSetup.Character} / 武器 {RunSetup.Weapon}");
			BeginRunWithCharacter(RunSetup.Character, RunSetup.Weapon);
			return;
		}

		_baseScreen.Open(BeginConfigure);
			}
			else
			{
				BeginConfigure();
			}

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

	/// <summary>Configure 步：先选角色，枪手再选武器（没有界面就直接开局）。</summary>
	private void BeginConfigure()
	{
		var options = new List<string>
		{
			CharacterCardText(PlayerCharacterKind.Gunner, "远程枪手 — 三把枪、灵活走位"),
			CharacterCardText(PlayerCharacterKind.Heavy, "近战重装 — 高血高甲，左键近战重击"),
			CharacterCardText(PlayerCharacterKind.Summoner, "召唤控制 — G 键召唤无人机（最多 3 架）"),
		};

		if (OpenChoice("选择角色", options, ChooseCharacter))
		{
			return;
		}

		ChooseCharacter(0);
	}

	private void ChooseCharacter(int index)
	{
		// 1 = 近战重装（不带枪）；其余角色继续选武器。
		if (index == 1)
		{
			BeginRunWithCharacter(PlayerCharacterKind.Heavy, WeaponPresetKind.Pistol);
			return;
		}

		PlayerCharacterKind character = index == 2 ? PlayerCharacterKind.Summoner : PlayerCharacterKind.Gunner;
		if (_startScreen != null)
		{
			_startScreen.Open(kind => BeginRunWithCharacter(character, kind));
			return;
		}

		BeginRunWithCharacter(character, _weaponKind);
	}

	/// <summary>
	/// 关卡序列的来源（设计文档 config-schema.md §4.2 / §5 阶段 1）：
	/// **只有一个真源** —— <see cref="ActRoguelike.Config.BalanceConfig.Stages"/>（`Config/Balance.tres`
	/// 上的数组，元素指向 `Config/Tables/Stages/stg_*.tres`）。
	/// 读不到 / 空表 → 回退到 <see cref="StageDefinition"/> 自身的 `[Export]` 默认值，
	/// **不再有第二份兜底表**（原来的 `DefaultStages()` 与 `Balance.tres` 已经漂移了 5 处，
	/// 最严重的是 Stage2 的 AllowedEnemies 走兜底会变成"不限制"）。
	/// ⚠️ 实测：删/改一个被引用的行文件 → Godot 会**整份** `Balance.tres` 加载失败，
	/// 所以走的是"0 关 → 单关默认值"这条粗回退（其他数值也一起回退）。别再删行文件，见 docs/config-schema.md §5。
	/// 回退留痕：日志里能直接看到这一局走的是"表"还是"默认值"。
	/// </summary>
	private static StageDefinition[] ResolveStages()
	{
		StageDefinition[] table = ActRoguelike.Config.Balance.Settings.Stages;
		if (table is not { Length: > 0 })
		{
			// 连"整张表"都读不到时，把全局数值的来源也打出来：这样一眼能区分
			// 「只是关卡表为空」和「连 Balance.tres 都没加载上（所有数值都回退了）」。
			string source = ActRoguelike.Config.Balance.Instance?.FileLoaded == true ? "Balance.tres 已加载" : "Balance.tres 未加载（全部数值回退到代码默认值）";
			ActRoguelike.Core.GameLog.Write("Flow", $"关卡表：0 关（来源：默认值）—— Balance 的 Stages 为空或未找到，使用 StageDefinition 自身的默认值。（{source}）");
			GD.PushWarning("[Config] 关卡表为空，使用 StageDefinition 自身默认值。");
			return new[] { new StageDefinition() };
		}

		var stages = new List<StageDefinition>(table.Length);
		int missing = 0;
		foreach (StageDefinition? row in table)
		{
			// ⚠️ 实测（2026-10）：行文件被删/改名时，**在游戏里根本走不到这里** —— 缺依赖会让
			// `GD.Load<BalanceConfig>` 整份失败（于是走上面"0 关"那条分支，所有数值一起回退到代码默认值，
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
			// 表是权威：一行 `.tres` 描述整把武器（含击退、暴击、弹速、装填延迟）。读不到行时才走下面的回退。
			row.ApplyTo(_weapon);
			return;
		}

		// 回退（表为空 / 整份 Balance.tres 未加载）：保留"什么都没做"的语义 ——
		// 武器组件自己的 [Export] 默认值就是这一把枪的数值。
		GD.PushWarning($"[Config] 武器表里没有 {kind}（Slot {(int)kind + 1}）的行，保留武器组件自身的默认值。");
		_weapon.RefillMagazine();
	}

	/// <summary>按 <see cref="WeaponPresetKind"/> 取武器表的行（Slot 从 1 开始，与枚举顺序一致）。</summary>
	public static WeaponConfig? WeaponRow(WeaponPresetKind kind)
		=> ConfigRows.BySlot<WeaponConfig>((int)kind + 1);

	/// <summary>按 <see cref="PlayerCharacterKind"/> 取角色表的行（Slot 从 1 开始，与枚举顺序一致）。</summary>
	public static CharacterConfig? CharacterRow(PlayerCharacterKind kind)
		=> ConfigRows.BySlot<CharacterConfig>((int)kind + 1);

	/// <summary>
	/// 角色的默认标题（关卡里的 Configure 步与基地的配置台共用这一份措辞；
	/// 基地那边不传标题时就走这里，所以两处不会漂移）。
	/// </summary>
	public static string DefaultCharacterTitle(PlayerCharacterKind kind) => kind switch
	{
		PlayerCharacterKind.Heavy => "近战重装 — 高血高甲，左键近战重击",
		PlayerCharacterKind.Summoner => "召唤控制 — G 键召唤无人机（最多 3 架）",
		_ => "远程枪手 — 三把枪、灵活走位",
	};

	/// <summary>
	/// 角色选择界面的卡片文案。数值来自角色表（`DescriptionTemplate` 支持 <c>{字段名}</c> 占位符）；
	/// 读不到角色行时退回原来的硬编码写法（用 BalanceConfig 上的旧字段），保证界面不会空掉。
	///
	/// <para>**public**：基地的装备配置台（<c>BaseTerminal</c>）复用它，角色卡的文案就只有这一处来源。</para>
	/// </summary>
	public static string CharacterCardText(PlayerCharacterKind kind, string? title = null)
	{
		title ??= DefaultCharacterTitle(kind);
		CharacterConfig? row = CharacterRow(kind);
		if (row != null)
		{
			string detail = ConfigText.Format(
				string.IsNullOrEmpty(row.DescriptionTemplate)
					? "（{MaxHealth:0} 生命 / {MaxArmor:0} 护甲 / 减伤 {DamageReduction}）"
					: row.DescriptionTemplate,
				row,
				row.Params);
			return $"{title}（{detail}）";
		}

		// 整张角色表都读不到：只留标题（这时玩家用的是场景里的原始数值，编不出具体数字）。
		return title;
	}

	/// <summary>
	/// 角色表的能力开关矩阵。读不到角色行时回退到分表前那段硬编码规则
	/// （`melee.Enabled = heavy`、`summon/control.Enabled = summoner`），保证行为逐位一致。
	///
	/// ⚠️ 这里按**脚本类名**匹配（不是 <c>Ability.AbilityId</c>）：技能表落地后 <c>AbilityId</c> 会被
	/// 写成技能行的 Id（`skl_melee` 这种），而名单里写的是类名（`MeleeAttackAbility`）。
	/// 类名在"表在"与"表不在"两种情况下都成立（`Ability.EnsureInitialized` 用类名兜底），
	/// 所以按类名匹配既与改动前逐位一致，又不会在回退路径上让近战/召唤角色丢掉技能。
	/// </summary>
	private void ApplyAbilityMatrix(CharacterConfig? character, bool heavy, bool summoner)
	{
		var abilities = new System.Collections.Generic.List<Ability>();
		NodeSearch.CollectInSubtree(_player!, abilities);

		foreach (Ability ability in abilities)
		{
			string id = ability.GetType().Name;

			if (character == null)
			{
				// 回退：与分表前的规则一模一样。
				ability.Enabled = id switch
				{
					nameof(MeleeAttackAbility) => heavy,
					nameof(SummonAbility) or nameof(AreaAttackAbility) => summoner,
					_ => ability.Enabled,
				};
				continue;
			}

			if (System.Array.IndexOf(character.EnabledAbilityIds, id) >= 0)
			{
				ability.Enabled = true;
			}
			else if (System.Array.IndexOf(character.DisabledAbilityIds, id) >= 0)
			{
				ability.Enabled = false;
			}
		}
	}

	/// <summary>开局配置完成（选好武器）→ 进入第一关。</summary>
	public void BeginRunWith(WeaponPresetKind kind) => BeginRunWithCharacter(PlayerCharacterKind.Gunner, kind);

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

	/// <summary>稀有池三选一（特殊奖励节点）：只出进化与异化。</summary>
	private bool OfferRareRewards(Action onChosen)
	{
		if (_player == null || _weapon == null || _screen == null || _screen.IsOpen || _runEnded)
		{
			return false;
		}

		if (!_poolReady)
		{
			_pool.Initialize(_weapon);
			_poolReady = true;
		}

		_current.Clear();
		_current.AddRange(_pool.Roll(_build, Balance.Settings.RewardChoiceCount, null, new[] { RewardKind.Evolution, RewardKind.Mutation }));
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

		if (!_poolReady)
		{
			_pool.Initialize(_weapon);
			_pool.SummonerAllowed = _character == PlayerCharacterKind.Summoner;
			_poolReady = true;
		}

		_current.Clear();
		_current.AddRange(_pool.Roll(_build, Balance.Settings.RewardChoiceCount));
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

			if (!_poolReady)
			{
				_pool.Initialize(_weapon);
				_poolReady = true;
			}

			_pool.SummonerAllowed = _character == PlayerCharacterKind.Summoner;
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
			_player.Heal(_player.MaxHealth * Balance.Settings.NodeHealRatio);
			onChosen();
			return true;
		}

		if (!_poolReady)
		{
			_pool.Initialize(_weapon);
			_poolReady = true;
		}

		// 进化（没有「武器池」这个类型，见用户 A3）/ 局外资源 / 特殊奖励暂时没有独立系统，先走通用池。
		RewardKind? filter = RewardService.FilterFor(kind);

		_current.Clear();
		_current.AddRange(_pool.Roll(_build, Balance.Settings.RewardChoiceCount, filter));
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
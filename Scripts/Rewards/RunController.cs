using System;
using System.Collections.Generic;
using ActRoguelike.Abilities;
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

		float maxHealth = heavy ? config.HeavyMaxHealth : summoner ? config.SummonerMaxHealth : config.PlayerMaxHealth;
		float armor = heavy ? config.HeavyArmor : summoner ? config.SummonerArmor : config.PlayerMaxArmor;
		float moveSpeed = heavy ? config.HeavyMoveSpeed : summoner ? config.SummonerMoveSpeed : config.PlayerMoveSpeed;
		float reduction = heavy ? config.HeavyDamageReduction : config.PlayerDamageReduction;

		_player!.ApplyBaseStats(maxHealth + MetaProgress.BonusMaxHealth, armor, config.PlayerMaxShield, moveSpeed);
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
			_weapon.Enabled = !heavy;
		}

		if (NodeSearch.FindInSubtree<MeleeAttackAbility>(_player) is { } melee)
		{
			melee.Enabled = heavy;
			if (melee.GetNodeOrNull<AttackHitbox>("Attack") is { } hit)
			{
				hit.Damage = config.HeavyMeleeDamage;
			}
		}

		// 角色 3：开放主动召唤（G 键）与范围定身（V 键），其余角色保持关闭。
		if (NodeSearch.FindInSubtree<SummonAbility>(_player) is { } summon)
		{
			summon.Enabled = summoner;
		}

		if (NodeSearch.FindInSubtree<AreaAttackAbility>(_player) is { } control)
		{
			control.Enabled = summoner;
		}

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
			_stages = Balance.Settings.Stages.Length > 0 ? Balance.Settings.Stages : DefaultStages();
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
			$"远程枪手 — 三把枪、灵活走位（{Balance.Settings.PlayerMaxHealth:0} 生命 / {Balance.Settings.PlayerMaxArmor:0} 护甲）",
			$"近战重装 — 高血高甲，左键近战重击（{Balance.Settings.HeavyMaxHealth:0} 生命 / {Balance.Settings.HeavyArmor:0} 护甲 / 减伤 {Balance.Settings.HeavyDamageReduction * 100f:0}%）",
			$"召唤控制 — G 键召唤无人机（最多 3 架），{Balance.Settings.SummonerMaxHealth:0} 生命 / {Balance.Settings.SummonerArmor:0} 护甲",
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

	/// <summary>配置里没写关卡序列时的兜底（三关：外围 / 内环 / 最终 Boss）。</summary>
	private static StageDefinition[] DefaultStages() => new[]
	{
		new StageDefinition
		{
			DisplayName = "Stage 1 · 外围",
			CombatBudget = 10f,
			MaxConcurrent = 3,
			SpawnInterval = 2f,
			RewardNodeCount = 3,
			FreeChallengeQuota = 2,
			AllowedEnemies = new[] { "基础近战", "快速近战", "自爆" },
		},
		new StageDefinition
		{
			DisplayName = "Stage 2 · 内环",
			CombatBudget = 16f,
			MaxConcurrent = 4,
			SpawnInterval = 1.8f,
			EnemyHealthMultiplier = 1.15f,
			EliteChanceScale = 0.15f,
			RewardNodeCount = 4,
			BossHealthMultiplier = 1.4f,
		},
		new StageDefinition
		{
			DisplayName = "Stage 3 · 核心（最终 Boss）",
			CombatBudget = 22f,
			MaxConcurrent = 5,
			SpawnInterval = 1.6f,
			EnemyHealthMultiplier = 1.3f,
			EnemyDamageMultiplier = 1.2f,
			EliteChanceScale = 0.25f,
			RewardNodeCount = 4,
			FreeChallengeQuota = 1,
			BossGuardCount = 3,
			BossHealthMultiplier = 2.2f,
			BossDamageMultiplier = 1.25f,
			IsFinalBoss = true,
		},
	};

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
		_screen?.OpenMessage(victory ? "胜利" : "本局结束", body, "按 Enter 或点击重新开始");
		int earned = RunSettlement.ShardsFor(_stageIndex, _stage?.ChallengesUsed ?? 0, victory);
		MetaProgress.AddShards(earned);
		body += $"\n获得碎片 +{earned}（累计 {MetaProgress.Shards}）";
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

	/// <summary>
	/// 换装：两把武器共用同一个 Weapon 节点，只换数值（配置集中在 BalanceConfig）。
	/// 这样不用重建节点，也就不会把挂在武器下的输入组件一起弄丢。
	/// </summary>
	public void EquipWeapon(WeaponPresetKind kind)
	{
		_weaponKind = kind;
		if (_weapon == null || !IsInstanceValid(_weapon))
		{
			return;
		}

		BalanceConfig config = Balance.Settings;
		bool sniper = kind == WeaponPresetKind.Sniper;
		bool rifle = kind == WeaponPresetKind.Rifle;

		_weapon.Damage = rifle ? config.RifleDamage : sniper ? config.SniperDamage : config.PistolDamage;
		_weapon.FireInterval = rifle ? config.RifleFireInterval : sniper ? config.SniperFireInterval : config.PistolFireInterval;
		_weapon.MagazineSize = rifle ? config.RifleMagazineSize : sniper ? config.SniperMagazineSize : config.PistolMagazineSize;
		_weapon.ReloadTime = rifle ? config.RifleReloadTime : sniper ? config.SniperReloadTime : config.PistolReloadTime;
		_weapon.SpreadDegrees = rifle ? config.RifleSpreadDegrees : sniper ? config.SniperSpreadDegrees : config.PistolSpreadDegrees;
		_weapon.Knockback = rifle ? config.RifleKnockback : sniper ? config.SniperKnockback : config.PistolKnockback;
		_weapon.ProjectilesPerShot = 1;
		_weapon.BasePierce = sniper ? config.SniperPierce : 0;
		_weapon.Automatic = rifle && config.RifleAutomatic;
		_weapon.CritChance = config.PistolCritChance;
		_weapon.CritMultiplier = config.PistolCritMultiplier;
		_weapon.ProjectileSpeed = config.PistolProjectileSpeed;
		_weapon.AimFov = config.PistolAimFov;
		_weapon.AutoReloadDelay = config.PistolAutoReloadDelay;
		_weapon.RefillMagazine();
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
			_stages = Balance.Settings.Stages.Length > 0 ? Balance.Settings.Stages : DefaultStages();
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

	/// <summary>通用多选一界面（武器池、商店等）。allowCancel = 允许 Esc 放弃（回调收到 -1）。</summary>
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
			BalanceConfig shop = Balance.Settings;
			var offers = new List<string>
			{
				$"恢复全部生命与护甲（{shop.ShopHealCost} 碎片）",
				$"随机一件强化奖励（{shop.ShopRewardCost} 碎片）",
				$"随机一件进化（改攻击方式）（{shop.ShopWeaponCost} 碎片）",
			};

			return OpenChoice($"补给站 · 当前碎片 {MetaProgress.Shards}", offers, index =>
			{
				if (index == 0 && MetaProgress.TrySpend(shop.ShopHealCost))
				{
					_player?.FillVitals();
				}
				else if (index == 1 && MetaProgress.TrySpend(shop.ShopRewardCost))
				{
					OfferRewards();
				}
				else if (index == 2 && MetaProgress.TrySpend(shop.ShopWeaponCost))
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

		// 武器池 / 局外资源 / 特殊奖励暂时没有独立系统，先走通用池。
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
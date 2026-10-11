using Godot;

namespace ActRoguelike.Config;

/// <summary>可选武器（开局配置与武器池节点用）。数值都在 BalanceConfig 里，不写死进预制体。</summary>
public enum WeaponPresetKind
{
	Pistol,
	Sniper,
	Rifle,
}

/// <summary>可选角色（策划文档 §7）。</summary>
public enum PlayerCharacterKind
{
	/// <summary>角色 1：远程枪手（三把枪）。</summary>
	Gunner,

	/// <summary>角色 2：近战与重型防御（左键近战重击）。</summary>
	Heavy,

	/// <summary>角色 3：召唤与控制（G 键主动召唤无人机）。</summary>
	Summoner,
}

/// <summary>
/// 全局平衡配置（唯一数值入口）。
/// 改 `res://Config/Balance.tres` 即可调整全局强度：玩家、武器、敌人倍率、节奏、Boss、HUD 表现。
/// 代码里不要写死数值 —— 需要新的可调项就在这里加一个 [Export]，然后在套用它的地方读 Balance.Settings。
/// </summary>
[GlobalClass]
public partial class BalanceConfig : Resource
{
	[ExportGroup("玩家")]
	[Export] public float PlayerMaxHealth { get; set; } = 160f;
	[Export] public float PlayerMaxArmor { get; set; } = 40f;
	[Export] public float PlayerMaxShield { get; set; }
	[Export] public float PlayerMoveSpeed { get; set; } = 7f;
	[Export(PropertyHint.Range, "0,0.9,0.01")] public float PlayerDamageReduction { get; set; }

	[ExportGroup("角色 · 近战重装（角色 2）")]
	[Export] public float HeavyMaxHealth { get; set; } = 380f;
	[Export] public float HeavyArmor { get; set; } = 140f;
	[Export] public float HeavyMoveSpeed { get; set; } = 6.2f;
	[Export(PropertyHint.Range, "0,0.9,0.01")] public float HeavyDamageReduction { get; set; } = 0.12f;

	/// <summary>近战重击的伤害（左键触发，范围与冷却在 Player.tscn 的 AbilityMelee 上）。</summary>
	[Export] public float HeavyMeleeDamage { get; set; } = 32f;

	[ExportGroup("角色 · 召唤控制（角色 3）")]
	[Export] public float SummonerMaxHealth { get; set; } = 210f;
	[Export] public float SummonerArmor { get; set; } = 60f;
	[Export] public float SummonerMoveSpeed { get; set; } = 6.8f;

	[ExportGroup("武器 · 手枪")]
	[Export] public float PistolDamage { get; set; } = 24f;
	[Export] public float PistolFireInterval { get; set; } = 0.26f;
	[Export] public int PistolMagazineSize { get; set; } = 12;
	[Export] public float PistolReloadTime { get; set; } = 0.7f;

	/// <summary>被动装填：这么久没开火且弹匣不满就自动装填。</summary>
	[Export] public float PistolAutoReloadDelay { get; set; } = 3f;
	[Export(PropertyHint.Range, "0,15,0.1")] public float PistolSpreadDegrees { get; set; } = 0.5f;
	[Export] public float PistolKnockback { get; set; } = 2.5f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float PistolCritChance { get; set; } = 0.15f;
	[Export] public float PistolCritMultiplier { get; set; } = 2f;
	[Export] public float PistolProjectileSpeed { get; set; } = 90f;
	[Export] public float PistolAimFov { get; set; } = 55f;

	[ExportGroup("武器 · 激光狙击枪")]
	[Export] public float SniperDamage { get; set; } = 60f;
	[Export] public float SniperFireInterval { get; set; } = 0.95f;
	[Export] public int SniperMagazineSize { get; set; } = 5;
	[Export] public float SniperReloadTime { get; set; } = 1.4f;
	[Export(PropertyHint.Range, "0,15,0.1")] public float SniperSpreadDegrees { get; set; } = 0.2f;
	[Export] public float SniperKnockback { get; set; } = 4f;

	/// <summary>激光狙击枪的基础穿透数量（策划文档 §8.1 的「激光狙击枪」）。</summary>
	[Export] public int SniperPierce { get; set; } = 2;

	[ExportGroup("武器 · 步枪")]
	[Export] public float RifleDamage { get; set; } = 13f;
	[Export] public float RifleFireInterval { get; set; } = 0.12f;
	[Export] public int RifleMagazineSize { get; set; } = 30;
	[Export] public float RifleReloadTime { get; set; } = 1.2f;
	[Export(PropertyHint.Range, "0,15,0.1")] public float RifleSpreadDegrees { get; set; } = 1.6f;
	[Export] public float RifleKnockback { get; set; } = 1.2f;

	/// <summary>按住左键持续开火。</summary>
	[Export] public bool RifleAutomatic { get; set; } = true;

	[ExportGroup("敌人 · 全局倍率")]
	[Export] public float EnemyHealthMultiplier { get; set; } = 1f;
	[Export] public float EnemyDamageMultiplier { get; set; } = 0.8f;
	[Export] public float EnemyMoveSpeedMultiplier { get; set; } = 1f;
	[Export] public float EliteChanceMultiplier { get; set; } = 1f;

	[ExportGroup("节奏 · Stage")]
	[Export] public float NodeBudgetMultiplier { get; set; } = 1f;
	[Export] public float SpawnIntervalMultiplier { get; set; } = 1f;

	[ExportGroup("Boss")]
	[Export] public float BossHealthMultiplier { get; set; } = 1f;
	[Export] public float BossDamageMultiplier { get; set; } = 0.9f;
	[Export] public float BossPressureInterval { get; set; } = 12f;
	[Export] public float BossPressureDamagePerLevel { get; set; } = 0.08f;
	[Export] public float BossPressureAttackSpeedPerLevel { get; set; } = 0.06f;

	[ExportGroup("关卡序列")]
	/// <summary>本局的 Stage 顺序；留空时使用代码内置的三关默认序列。</summary>
	[Export] public StageDefinition[] Stages { get; set; } = System.Array.Empty<StageDefinition>();

	[ExportGroup("成长")]
	[Export] public int RewardChoiceCount { get; set; } = 3;

	[ExportGroup("节点 · 商店（花局外碎片）")]
	[Export] public int ShopHealCost { get; set; } = 5;
	[Export] public int ShopRewardCost { get; set; } = 12;
	[Export] public int ShopWeaponCost { get; set; } = 18;

	[ExportGroup("表现 · 伤害跳字")]
	[Export] public bool ShowDamageNumbers { get; set; } = true;
	[Export] public float DamageNumberLifetime { get; set; } = 0.8f;
	[Export] public float DamageNumberRise { get; set; } = 70f;
	[Export] public int DamageNumberFontSize { get; set; } = 24;

	[ExportGroup("表现 · 血条")]
	[Export] public bool ShowEnemyHealthBars { get; set; } = true;
	[Export] public float EnemyHealthBarWidth { get; set; } = 64f;
	[Export] public float EnemyHealthBarHeight { get; set; } = 7f;
	/// <summary>血条相对单位原点的垂直偏移（现在沿 -Up 使用，即放在脚下）。</summary>
	[Export] public float EnemyHealthBarTopOffset { get; set; } = 0.25f;

	/// <summary>「白板首领副本」的血量倍率（相对普通首领，关卡核心文档 L231/L315）。</summary>
	[Export(PropertyHint.Range, "0.05,1,0.05")] public float EliteBossHealthScale { get; set; } = 0.35f;

	/// <summary>节点之间的最小间距（关卡核心文档 L106）。</summary>
	[Export] public float MinNodeSpacing { get; set; } = 8f;

	/// <summary>完成一次高价值节点挑战增加的 Pressure 点数（关卡核心文档 L352）。</summary>
	[Export] public int PressurePerHighValueNode { get; set; } = 150;

	/// <summary>进化奖励对 Pressure 增加值的额外比例（关卡核心文档 L356）。</summary>
	[Export] public float PressureEvolutionBonus { get; set; } = 1.25f;

	/// <summary>异化奖励对 Pressure 增加值的额外比例（关卡核心文档 L356）。</summary>
	[Export] public float PressureMutationBonus { get; set; } = 1.5f;

	/// <summary>每多少点 Pressure 转换成 1 点首领压力；余数清零（关卡核心文档 L370）。</summary>
	[Export] public int PressurePerPoint { get; set; } = 100;

	/// <summary>战斗型回复节点先免费恢复的生命比例（关卡核心文档 L180-183）。</summary>
	[Export(PropertyHint.Range, "0,1,0.05")] public float NodeHealRatio { get; set; } = 0.3f;

	/// <summary>免费挑战额度：初始值（关卡核心文档 L409）。</summary>
	[Export] public int FreeChallengeBaseQuota { get; set; } = 1;

	/// <summary>免费挑战额度上限（关卡核心文档 L409-416）。</summary>
	[Export] public int FreeChallengeMaxQuota { get; set; } = 2;

	/// <summary>地图下方的区域杀高度：玩家 Y 低于它直接被击杀（用户需求）。</summary>
	[Export] public float KillPlaneY { get; set; } = -8f;

	/// <summary>与首领的距离小于它时，屏幕上方显示完整血条；否则只显示首领头像。</summary>
	[Export] public float BossBarFullRange { get; set; } = 22f;
	[Export] public float BossHealthBarScale { get; set; } = 4f;
}

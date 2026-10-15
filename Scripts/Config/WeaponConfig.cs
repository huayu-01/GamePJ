using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 武器表的一行 —— **一个 `.tres` 文件就是一行**（`Config/Tables/Weapons/wpn_*.tres`）。
/// 索引用 <see cref="ConfigIndexData.Weapons"/>（元素是独立 `ExtResource`）。
///
/// 数值权威：**这一行**（原 `ConfigIndex.tres` 的 <c>Pistol*</c> / <c>Sniper*</c> / <c>Rifle*</c> 已搬到这里）。
/// <see cref="ApplyTo"/> 逐字段套到 <see cref="Weapon"/> 组件上；表读不到时由运行期回退到
/// <see cref="Weapon"/> 自身的 <c>[Export]</c> 默认值（"读不到则使用自身的默认值"）。
/// </summary>
[Tool]
[GlobalClass]
public partial class WeaponConfig : ConfigRow
{
	[ExportGroup("身份 · 预制体与标签")]
	/// <summary>武器预制体。当前三把武器共用一个预制体，只换数值（不重建节点，避免弄丢输入组件）。</summary>
	[Export(PropertyHint.File, "*.tscn")] public string ScenePath { get; set; } = "res://Scenes/Weapons/Pistol.tscn";

	/// <summary>手上一模型路径（空 = 用预制体自带的）。</summary>
	[Export(PropertyHint.File, "*.glb,*.gltf,*.tscn")] public string ModelPath { get; set; } = string.Empty;

	/// <summary>图标路径（空 = 无图标）。</summary>
	[Export(PropertyHint.File, "*.png,*.svg")] public string IconPath { get; set; } = string.Empty;

	/// <summary>与 <c>RewardPool</c> 的 <c>RequiredWeaponTags</c> 配对（决定这条武器能吃到哪些进化）。</summary>
	[Export] public string[] AttackTags { get; set; } = { "Projectile", "Gun", "Physical" };

	[ExportGroup("升级池")]
	/// <summary>
	/// 本武器对应的**升级池**（`Config/Tables/UpgradePools/pool_*.tres`，一行一个池）。
	///
	/// **正向引用**：武器指池、**池不指武器**（用户 2026-10 拍板；取代 config-schema.md §4.5 / §6.9
	/// "增幅项反向指武器"的推荐方案）。"哪个选项属于哪个池"写在**选项行**的 `RewardConfig.Pool` 列上
	/// —— 一个选项占一行 + 一个引用列，池行不重复列一遍。
	///
	/// 读表时经 <see cref="ApplyTo"/> 写进 <see cref="Weapon.PoolId"/>，供奖励池做"精确池"匹配（第一层）。
	/// **留空** = 本武器不吃"专属池"这一层，只走"标签匹配 → 通用"两层（与分表前逐位一致）。
	/// </summary>
	[Export] public UpgradePoolConfig? Pool { get; set; }

	[ExportGroup("数值（伤害 / 射速 / 弹匣 / 散布 / 暴击 / 弹速 …）")]
	/// <summary>单发伤害。</summary>
	[Export] public float Damage { get; set; } = 24f;

	/// <summary>射击间隔（秒）。</summary>
	[Export] public float FireInterval { get; set; } = 0.26f;

	/// <summary>弹匣容量。</summary>
	[Export] public int MagazineSize { get; set; } = 12;

	/// <summary>换弹时间（秒）。</summary>
	[Export] public float ReloadTime { get; set; } = 0.7f;

	/// <summary>被动装填：这么久没开火且弹匣不满就自动装填。</summary>
	[Export] public float AutoReloadDelay { get; set; } = 3f;

	/// <summary>基础散布（度）。腰射时的弹道随机偏移范围。</summary>
	[Export(PropertyHint.Range, "0,15,0.1")] public float SpreadDegrees { get; set; } = 0.5f;

	/// <summary>预瞄（ADS）时散布的缩放（0.35 = 瞄准后散布只剩 35%）。</summary>
	[Export(PropertyHint.Range, "0,1,0.05")] public float AimSpreadScale { get; set; } = 0.35f;

	/// <summary>一次击发多颗弹丸时，每颗之间的横向散开角度。</summary>
	[Export] public float MultiShotSpreadDegrees { get; set; } = 1.2f;

	/// <summary>一次击发的弹丸数（霰弹类 &gt; 1）。</summary>
	[Export] public int ProjectilesPerShot { get; set; } = 1;

	/// <summary>基础穿透数量（激光狙击枪 = 2）。</summary>
	[Export] public int BasePierce { get; set; }

	/// <summary>击退力（<b>效果数值</b>：命中时传给攻击实例，见 <c>Config/Tables/Effects/</c> 的击退行）。</summary>
	[Export] public float Knockback { get; set; } = 2.5f;

	/// <summary>暴击率（0–1；0.15 = 15%）。</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float CritChance { get; set; } = 0.15f;

	/// <summary>暴击伤害倍率（2 = 双倍伤害）。</summary>
	[Export] public float CritMultiplier { get; set; } = 2f;

	/// <summary>弹速（只写进投射物发射时的初速）。</summary>
	[Export] public float ProjectileSpeed { get; set; } = 90f;

	/// <summary>投射物存活时间（秒）。</summary>
	[Export] public float MaxLifetime { get; set; } = 2.2f;

	/// <summary>预瞄（ADS）时的 FOV。</summary>
	[Export] public float AimFov { get; set; } = 55f;

	/// <summary>按住左键持续开火（决定"火力倾泻"走哪种模式）。</summary>
	[Export] public bool Automatic { get; set; }

	[ExportGroup("持有武技（指向 Config/Tables/Skills/ 的行；资源引用不是字符串）")]
	/// <summary>
	/// **这把武器的武技行**（`skl_*.tres`）。
	///
	/// <para>为什么是"武器 → 武技"而不是"角色 → 武技"（2026-10-15 本轮新增这一列的判据）：
	/// 武技是**武器**的属性（能量掌的"波波"只可能配能量掌），而一个角色有 3 把武器、每把的
	/// 武技 1/2 都绑在同样的两个按键上（<c>weapon_skill_1</c> / <c>weapon_skill_2</c>）。
	/// 若把这些行挂在**角色行**上，换武器时上一把的武技行不会被清掉，两个技能会同时抢同一个按键。</para>
	///
	/// <para>套用时机：<c>RunController.ApplyBalance</c> 在**角色行的技能之后**套用当前武器行的这一列
	/// （角色行放"技能 1 / 技能 2 / 被动"这类与武器无关的技能，武器行放"武技 1 / 武技 2"）。
	/// 每一行用 <see cref="AbilityConfig.NodeName"/> 找能力节点，所以**同一把武器的两个武技要挂在
	/// 两个不同的节点上**（`AbilityWeaponNSkill1` / `AbilityWeaponNSkill2`）。</para>
	///
	/// <para>留空 = 这把武器没有武技（旧 3 把枪就是这种：开火由 <c>Weapon</c> 组件负责）。</para>
	/// </summary>
	[Export] public AbilityConfig[] Skills { get; set; } = System.Array.Empty<AbilityConfig>();

	/// <summary>把自己套到武器组件上。字段清单与 <c>RunController.EquipWeapon</c> 原来的逐项赋值一一对应。</summary>
	public override void ApplyTo(Node target)
	{
		if (target is not Weapon weapon)
		{
			return;
		}

		weapon.Damage = Damage;
		weapon.FireInterval = FireInterval;
		weapon.MagazineSize = MagazineSize;
		weapon.ReloadTime = ReloadTime;
		weapon.AutoReloadDelay = AutoReloadDelay;
		weapon.SpreadDegrees = SpreadDegrees;
		weapon.AimSpreadScale = AimSpreadScale;
		weapon.MultiShotSpreadDegrees = MultiShotSpreadDegrees;
		weapon.ProjectilesPerShot = ProjectilesPerShot;
		weapon.BasePierce = BasePierce;
		weapon.Knockback = Knockback;
		weapon.CritChance = CritChance;
		weapon.CritMultiplier = CritMultiplier;
		weapon.ProjectileSpeed = ProjectileSpeed;
		weapon.MaxLifetime = MaxLifetime;
		weapon.AimFov = AimFov;
		weapon.Automatic = Automatic;
		weapon.AttackTags = AttackTags;
		// 身份：本武器属于哪个升级池（`RewardDefinition.AllowsWeapon` 的第一层用它做精确匹配）。
		// 池行被删 / 没填时 Pool 为 null → 留空 → 自动落到"标签匹配 → 通用"两层。
		weapon.PoolId = Pool?.Id ?? string.Empty;
		weapon.RefillMagazine();
	}
}

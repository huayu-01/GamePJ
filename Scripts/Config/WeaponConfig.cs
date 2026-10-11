using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Config;

/// <summary>
/// 武器表的一行 —— **一个 `.tres` 文件就是一行**（`Config/Tables/Weapons/wpn_*.tres`）。
/// 索引用 <see cref="BalanceConfig.Weapons"/>（元素是独立 `ExtResource`）。
///
/// 数值权威：**这一行**（原 `Balance.tres` 的 <c>Pistol*</c> / <c>Sniper*</c> / <c>Rifle*</c> 已搬到这里）。
/// <see cref="ApplyTo"/> 逐字段套到 <see cref="Weapon"/> 组件上；表读不到时由运行期回退到
/// <see cref="Weapon"/> 自身的 <c>[Export]</c> 默认值（"读不到则使用自身的默认值"）。
/// </summary>
[GlobalClass]
public partial class WeaponConfig : ConfigRow
{
	/// <summary>武器预制体。当前三把武器共用一个预制体，只换数值（不重建节点，避免弄丢输入组件）。</summary>
	[Export(PropertyHint.File, "*.tscn")] public string ScenePath { get; set; } = "res://Scenes/Weapons/Pistol.tscn";

	/// <summary>手上一模型路径（空 = 用预制体自带的）。</summary>
	[Export(PropertyHint.File, "*.glb,*.gltf,*.tscn")] public string ModelPath { get; set; } = string.Empty;

	/// <summary>图标路径（空 = 无图标）。</summary>
	[Export(PropertyHint.File, "*.png,*.svg")] public string IconPath { get; set; } = string.Empty;

	/// <summary>与 <c>RewardPool</c> 的 <c>RequiredWeaponTags</c> 配对（决定这条武器能吃到哪些进化）。</summary>
	[Export] public string[] AttackTags { get; set; } = { "Projectile", "Gun", "Physical" };

	[ExportGroup("数值")]
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

	[Export(PropertyHint.Range, "0,15,0.1")] public float SpreadDegrees { get; set; } = 0.5f;

	[Export(PropertyHint.Range, "0,1,0.05")] public float AimSpreadScale { get; set; } = 0.35f;

	/// <summary>一次击发多颗弹丸时，每颗之间的横向散开角度。</summary>
	[Export] public float MultiShotSpreadDegrees { get; set; } = 1.2f;

	/// <summary>一次击发的弹丸数（霰弹类 &gt; 1）。</summary>
	[Export] public int ProjectilesPerShot { get; set; } = 1;

	/// <summary>基础穿透数量（激光狙击枪 = 2）。</summary>
	[Export] public int BasePierce { get; set; }

	/// <summary>击退力（<b>效果数值</b>：命中时传给攻击实例，见 <c>Config/Tables/Effects/</c> 的击退行）。</summary>
	[Export] public float Knockback { get; set; } = 2.5f;

	[Export(PropertyHint.Range, "0,1,0.01")] public float CritChance { get; set; } = 0.15f;

	[Export] public float CritMultiplier { get; set; } = 2f;

	/// <summary>弹速（只写进投射物发射时的初速）。</summary>
	[Export] public float ProjectileSpeed { get; set; } = 90f;

	/// <summary>投射物存活时间（秒）。</summary>
	[Export] public float MaxLifetime { get; set; } = 2.2f;

	/// <summary>预瞄（ADS）时的 FOV。</summary>
	[Export] public float AimFov { get; set; } = 55f;

	/// <summary>按住左键持续开火（决定"火力倾泻"走哪种模式）。</summary>
	[Export] public bool Automatic { get; set; }

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
		weapon.RefillMagazine();
	}
}

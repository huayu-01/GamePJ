using System;
using ActRoguelike.Audio;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Weapons;

/// <summary>
/// 武器基类：定义攻击节奏与攻击侧参数，攻击统一生成投射物。
/// 具体武器只改参数与技能；敌人用同一套武器时由自己的 Behavior 调用 TryFire。
///
/// <para>⚠️ <b>P6b：本类**不再有弹匣/装填的概念**</b>（用户 2026-10-12 裁定「没有弹匣的概念了」，
/// 见 <c>docs/character-redesign.md</c> §五）。原来那一整套（<c>MagazineSize</c> / <c>ReloadTime</c> /
/// <c>AutoReloadDelay</c> / <c>Ammo</c> / <c>IsReloading</c> / <c>EffectiveMagazineSize</c> /
/// <c>TryReload</c> / <c>RefillMagazine</c> / <c>FireFullMagazine</c> / <c>BeginInfiniteFire</c> /
/// <c>InfiniteAmmo</c> 与 <c>ReloadStarted</c>/<c>ReloadCompleted</c>/<c>AmmoChanged</c> 三个事件）**整组删除**。
/// 开火的唯一门槛是 <c>Enabled</c> + 冷却 + 有投射物预制体（<see cref="CanFire"/>）。</para>
/// </summary>
public partial class Weapon : Node3D
{
	[Export] public float Damage { get; set; } = 22f;
	[Export] public float FireInterval { get; set; } = 0.32f;
	[Export] public float ProjectileSpeed { get; set; } = 75f;
	[Export] public float MaxLifetime { get; set; } = 2.2f;
	[Export(PropertyHint.Range, "0,15,0.1")] public float SpreadDegrees { get; set; } = 0.6f;
	[Export] public float Knockback { get; set; } = 2.5f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float CritChance { get; set; } = 0.15f;
	[Export] public float CritMultiplier { get; set; } = 2f;
	[Export] public bool Automatic { get; set; }
	[Export] public PackedScene? ProjectileScene { get; set; }
	[Export] public Node3D? Muzzle { get; set; }
	[Export] public string[] AttackTags { get; set; } = { "Projectile", "Gun", "Physical" };

	/// <summary>预瞄（ADS）时的 FOV，供玩家表现层使用。</summary>
	[Export] public float AimFov { get; set; } = 55f;
	[Export(PropertyHint.Range, "0,1,0.05")] public float AimSpreadScale { get; set; } = 0.35f;

	/// <summary>多发射击时的横向散开角度（每发之间）。</summary>
	[Export] public float MultiShotSpreadDegrees { get; set; } = 1.2f;

	/// <summary>一次击发的弹丸数（霰弹枪 &gt; 1；进化「双管齐下」在此之上再加）。</summary>
	[Export] public int ProjectilesPerShot { get; set; } = 1;

	public event Action? Fired;

	/// <summary>关掉后这把武器不参与射击（近战角色用），左键改由近战能力接管。</summary>
	[Export] public bool Enabled { get; set; } = true;

	/// <summary>
	/// 本武器对应的**升级池 Id**（配置表阶段 4），由 <c>WeaponConfig.ApplyTo</c> 从武器表的
	/// <c>Pool</c> 列写入。奖励池用它做"精确池"匹配（`RewardDefinition.AllowsWeapon` 的第一层）；
	/// **留空** = 这一局只走"标签匹配（<see cref="AttackTags"/>）→ 通用"两层。
	/// 表读不到时这里就是场景里的值（默认空），行为与分表前逐位一致。
	/// </summary>
	[Export] public string PoolId { get; set; } = string.Empty;

	public Unit? OwnerUnit { get; private set; }
	public bool IsAiming { get; private set; }

	/// <summary>当前瞄准点，由驱动方（玩家输入 / 敌人能力）设置，射击时使用。</summary>
	public Vector3 AimPoint { get; set; }

	/// <summary>临时覆盖射击间隔（火力倾泻用），null 表示使用基础值。</summary>
	public float? FireIntervalOverride { get; set; }

	private float _cooldown;
	private float _sinceLastFire;

	/// <summary>枪口火光的 <c>VFXController</c>（Godot 侧脚本）。节点名固定为 <c>Muzzle/VFX</c>，缺失时为 null。</summary>
	private Node? _muzzleVfx;

	/// <summary>能不能开火：启用 + 冷却走完 + 有投射物预制体（**P6b 起不再看弹药**）。</summary>
	public bool CanFire => Enabled && _cooldown <= 0f && ProjectileScene != null;

	/// <summary>距上次开火经过的秒数（HUD 与调参用）。</summary>
	public float SinceLastFire => _sinceLastFire;

	/// <summary>累计发射的弹丸数（一次击发的多弹丸都算），用于验证多弹丸武器。</summary>
	public int PelletsFired { get; private set; }

	/// <summary>基础穿透数量（激光狙击枪 &gt; 0；进化「穿甲弹」在此之上再加）。</summary>
	[Export] public int BasePierce { get; set; }

	private float AttackSpeedMultiplier => OwnerUnit?.Stats.Get("AttackSpeedMultiplier") ?? 1f;

	private float EffectiveFireInterval => (FireIntervalOverride ?? FireInterval) / Mathf.Max(0.05f, AttackSpeedMultiplier);

	public bool HasTag(string tag) => System.Array.IndexOf(AttackTags, tag) >= 0;

	public override void _Ready()
	{
		OwnerUnit = FindOwnerUnit();
		Muzzle ??= GetNodeOrNull<Node3D>("Muzzle");
		_muzzleVfx = Muzzle?.GetNodeOrNull<Node>("VFX");
		AddToGroup("weapons");
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (_cooldown > 0f)
		{
			_cooldown -= dt;
		}

		_sinceLastFire += dt;
	}

	public void SetAiming(bool aiming) => IsAiming = aiming;

	public bool TryFire(Vector3 aimPoint)
	{
		if (!Enabled || ProjectileScene == null)
		{
			return false;
		}

		if (_cooldown > 0f)
		{
			return false;
		}

		Vector3 origin = (Muzzle ?? this).GlobalPosition;
		Vector3 direction = aimPoint - origin;
		direction = direction.LengthSquared() < 0.0001f ? -GlobalTransform.Basis.Z : direction.Normalized();

		float spread = IsAiming ? SpreadDegrees * AimSpreadScale : SpreadDegrees;

		// 攻击产生时快照攻击侧属性（战斗规则说明 §4）。
		StatSheet? stats = OwnerUnit?.Stats;
		AttackOffense offense = AttackOffense.Capture(OwnerUnit, CritChance, CritMultiplier);
		int projectileCount = Mathf.Max(1, ProjectilesPerShot + (int)(stats?.Get("ProjectileCountAdd") ?? 0f));
		var attack = new AttackInstance
		{
			Attacker = OwnerUnit,
			Ability = "WeaponFire",
			BaseDamage = Damage * offense.DamageMultiplier,
			CanCrit = true,
			CritChance = offense.CritChance,
			CritMultiplier = offense.CritMultiplier,
			Knockback = Knockback,
			Policy = HitPolicy.OncePerTarget,
			Lifetime = MaxLifetime,
			Tags = AttackTags,
			ProjectileCount = projectileCount,
			Pierce = BasePierce + (int)(stats?.Get("PierceAdd") ?? 0f),
			Ricochet = (int)(stats?.Get("RicochetAdd") ?? 0f),
			ExplosionRadius = stats?.Get("ExplosionRadiusAdd") ?? 0f,
			ExplosionDamage = Damage * (stats?.Get("ExplosionDamageScale") ?? 0f),
		};

		Node parent = GetTree().CurrentScene ?? GetTree().Root;
		for (int i = 0; i < projectileCount; i++)
		{
			float lateral = projectileCount > 1 ? (i - ((projectileCount - 1) * 0.5f)) * MultiShotSpreadDegrees : 0f;
			Vector3 shotDirection = direction.Rotated(Vector3.Up, Mathf.DegToRad(lateral));
			shotDirection = ApplySpread(shotDirection, spread);

			var projectile = ProjectileScene.Instantiate<Projectile>();
			parent.AddChild(projectile);
			projectile.GlobalPosition = origin;
			projectile.Launch(attack, shotDirection);
			PelletsFired++;
		}

		_cooldown = EffectiveFireInterval;
		_sinceLastFire = 0f;
		_muzzleVfx?.Call("play");
		AudioManager.PlaySfx(AudioIds.WeaponFire); // 开火音效：按 id 播，文件/音量/总线在音效表里（调用方不知道路径）
		Fired?.Invoke();

		return true;
	}

	private Unit? FindOwnerUnit()
	{
		Node? node = GetParent();
		while (node != null)
		{
			if (node is Unit unit)
			{
				return unit;
			}

			node = node.GetParent();
		}

		return null;
	}

	private static Vector3 ApplySpread(Vector3 direction, float degrees)
	{
		if (degrees <= 0f)
		{
			return direction;
		}

		Vector3 axis = direction.Cross(Vector3.Up);
		if (axis.LengthSquared() < 0.0001f)
		{
			axis = direction.Cross(Vector3.Right);
		}

		float angle = Mathf.DegToRad((float)GD.RandRange(-degrees, degrees));
		return direction.Rotated(axis.Normalized(), angle);
	}
}

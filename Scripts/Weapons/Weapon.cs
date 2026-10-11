using System;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Weapons;

/// <summary>
/// 武器基类：定义攻击节奏、资源（弹匣/换弹）与攻击侧参数，攻击统一生成投射物。
/// 具体武器只改参数与技能；敌人用同一套武器时由自己的 Behavior 调用 TryFire。
/// “换弹事件”与“实际弹匣补充”分开：<see cref="ReloadStarted"/>/<see cref="ReloadCompleted"/>
/// 是可供构筑监听的事件，弓箭这类没有弹匣的武器后续可以只触发事件。
/// </summary>
public partial class Weapon : Node3D
{
	[Export] public float Damage { get; set; } = 22f;
	[Export] public float FireInterval { get; set; } = 0.32f;
	[Export] public int MagazineSize { get; set; } = 8;
	[Export] public float ReloadTime { get; set; } = 1.15f;
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

	/// <summary>装填是被动的：最后一次开火后超过这个秒数、且弹匣没满就自动装填（打空也会立刻装填）。</summary>
	[Export] public float AutoReloadDelay { get; set; } = 3f;

	public event Action? Fired;
	public event Action? ReloadStarted;
	public event Action? ReloadCompleted;
	public event Action<int>? AmmoChanged;

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
	public bool IsReloading { get; private set; }
	public int Ammo { get; private set; }

	/// <summary>当前瞄准点，由驱动方（玩家输入 / 敌人能力）设置，射击时使用。</summary>
	public Vector3 AimPoint { get; set; }

	/// <summary>临时覆盖射击间隔（火力倾泻用），null 表示使用基础值。</summary>
	public float? FireIntervalOverride { get; set; }

	private float _cooldown;
	private float _reloadRemaining;
	private float _sinceLastFire;
	private float _infiniteFireRemaining;

	/// <summary>枪口火光的 <c>VFXController</c>（Godot 侧脚本）。节点名固定为 <c>Muzzle/VFX</c>，缺失时为 null。</summary>
	private Node? _muzzleVfx;

	public bool CanFire => Enabled && !IsReloading && Ammo > 0 && _cooldown <= 0f && ProjectileScene != null;

	/// <summary>距上次开火经过的秒数（HUD 与调参用）。</summary>
	public float SinceLastFire => _sinceLastFire;

	/// <summary>累计发射的弹丸数（一次击发的多弹丸都算），用于验证多弹丸武器。</summary>
	public int PelletsFired { get; private set; }

	/// <summary>基础穿透数量（激光狙击枪 &gt; 0；进化「穿甲弹」在此之上再加）。</summary>
	[Export] public int BasePierce { get; set; }

	/// <summary>是否处在「不消耗子弹」的状态（火力倾泻的步枪模式）。</summary>
	public bool InfiniteAmmo => _infiniteFireRemaining > 0f;

	/// <summary>算上扩容弹匣等修正后的弹匣容量。</summary>
	public int EffectiveMagazineSize => Mathf.Max(1, MagazineSize + (int)(OwnerUnit?.Stats.Get("MagazineSizeAdd") ?? 0f));

	private float AttackSpeedMultiplier => OwnerUnit?.Stats.Get("AttackSpeedMultiplier") ?? 1f;

	private float EffectiveFireInterval => (FireIntervalOverride ?? FireInterval) / Mathf.Max(0.05f, AttackSpeedMultiplier);

	private float EffectiveReloadTime => ReloadTime / Mathf.Max(0.1f, OwnerUnit?.Stats.Get("ReloadSpeedMultiplier") ?? 1f);

	public bool HasTag(string tag) => System.Array.IndexOf(AttackTags, tag) >= 0;

	public override void _Ready()
	{
		OwnerUnit = FindOwnerUnit();
		Muzzle ??= GetNodeOrNull<Node3D>("Muzzle");
		_muzzleVfx = Muzzle?.GetNodeOrNull<Node>("VFX");
		Ammo = EffectiveMagazineSize;
		AddToGroup("weapons");
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (_cooldown > 0f)
		{
			_cooldown -= dt;
		}

		if (_reloadRemaining > 0f)
		{
			_reloadRemaining -= dt;
			if (_reloadRemaining <= 0f)
			{
				IsReloading = false;
				Ammo = EffectiveMagazineSize;
				AmmoChanged?.Invoke(Ammo);
				ReloadCompleted?.Invoke();
			}
		}

		if (_infiniteFireRemaining > 0f)
		{
			_infiniteFireRemaining -= dt;
			if (_infiniteFireRemaining <= 0f)
			{
				FireIntervalOverride = null;
			}
		}

		// 火力倾泻期间自动连射（不需要玩家按住开火键）。
		if (InfiniteAmmo && _cooldown <= 0f && OwnerUnit is { IsDead: false })
		{
			TryFire(AimPoint);
		}

		// 被动装填：一段时间没开火且弹匣不满就自动补满（打空的情况在 TryFire 里已经立即触发）。
		_sinceLastFire += dt;
		if (!IsReloading && Ammo < EffectiveMagazineSize && _sinceLastFire >= AutoReloadDelay)
		{
			TryReload();
		}
	}

	public void SetAiming(bool aiming) => IsAiming = aiming;

	public bool TryFire(Vector3 aimPoint)
	{
		if (!Enabled || ProjectileScene == null)
		{
			return false;
		}

		if (IsReloading || _cooldown > 0f)
		{
			return false;
		}

		if (Ammo <= 0 && !InfiniteAmmo)
		{
			TryReload();
			return false;
		}

		Vector3 origin = (Muzzle ?? this).GlobalPosition;
		Vector3 direction = aimPoint - origin;
		direction = direction.LengthSquared() < 0.0001f ? -GlobalTransform.Basis.Z : direction.Normalized();

		float spread = IsAiming ? SpreadDegrees * AimSpreadScale : SpreadDegrees;

		// 攻击产生时快照攻击侧属性（战斗规则说明 §4）。
		StatSheet? stats = OwnerUnit?.Stats;
		int projectileCount = Mathf.Max(1, ProjectilesPerShot + (int)(stats?.Get("ProjectileCountAdd") ?? 0f));
		var attack = new AttackInstance
		{
			Attacker = OwnerUnit,
			Ability = "WeaponFire",
			BaseDamage = Damage * (stats?.Get("DamageMultiplier") ?? 1f),
			CanCrit = true,
			CritChance = Mathf.Clamp(CritChance + (stats?.Get("CritChanceAdd") ?? 0f), 0f, 1f),
			CritMultiplier = CritMultiplier + (stats?.Get("CritMultiplierAdd") ?? 0f),
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

		if (!InfiniteAmmo)
		{
			Ammo--;
		}

		_cooldown = EffectiveFireInterval;
		_sinceLastFire = 0f;
		AmmoChanged?.Invoke(Ammo);
		_muzzleVfx?.Call("play");
		Fired?.Invoke();

		if (Ammo <= 0)
		{
			TryReload();
		}

		return true;
	}

	/// <summary>火力倾泻（步枪）：一段时间内持续开火且不消耗子弹（弹匣为空也能用）。</summary>
	public void BeginInfiniteFire(float duration, float interval)
	{
		_infiniteFireRemaining = Mathf.Max(0.1f, duration);
		FireIntervalOverride = interval;
		IsReloading = false;
		_reloadRemaining = 0f;
	}

	/// <summary>火力倾泻（手枪 / 激光狙击枪）：无视当前弹药，直接打出一整个弹匣。</summary>
	public void FireFullMagazine()
	{
		int total = EffectiveMagazineSize;
		Ammo = total;
		for (int i = 0; i < total; i++)
		{
			_cooldown = 0f;
			if (!TryFire(AimPoint))
			{
				break;
			}
		}

		Ammo = 0;
		AmmoChanged?.Invoke(Ammo);
		TryReload();
	}

	/// <summary>立即补满弹匣（战地补给等效果用）。</summary>
	public void RefillMagazine()
	{
		IsReloading = false;
		_reloadRemaining = 0f;
		Ammo = EffectiveMagazineSize;
		AmmoChanged?.Invoke(Ammo);
		ReloadCompleted?.Invoke();
	}

	public void TryReload()
	{
		if (IsReloading || Ammo >= EffectiveMagazineSize)
		{
			return;
		}

		IsReloading = true;
		_reloadRemaining = EffectiveReloadTime;
		ReloadStarted?.Invoke();
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

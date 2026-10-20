using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// **能量波**（角色 A 武器 1「能量掌」的武技 1 / 武技 2 共用）：
/// 朝准星方向发射一枚直线、无下坠的投射物，命中后按统一结算流程造成伤害。
///
/// <b>本技能完全由动画事件帧驱动</b>（用户裁定 + 文档「子弹在事件帧到 ATK 时发射」）：
/// <list type="number">
/// <item><see cref="TryActivate"/> 先查动画是否存在（**全路径查**）——不存在就**不执行**并写明确日志。</item>
/// <item>激活时播技能动画，并按 <see cref="Ability.AnimSpeedScale"/> 设播放器速度
///   （逻辑帧跟着动画走，"加速动画释放"不会对不齐）。</item>
/// <item>只有动画播到 <c>ATK</c> 那一帧（<see cref="OnAnimEvent"/>）才真的发射 ——
///   里面**没有**任何 <c>StateTime</c> 计时兜底。</item>
/// </list>
///
/// <b>两种模式共用一套参数</b>：普通发射与"完美时机"发射的差别只有伤害倍率 / 弹速倍率 /
/// 击退三列（全部来自技能行的表，改数值不改代码）。这样"蓄力"是逻辑，
/// "子弹"只有一种实现。
///
/// ⚠️ 继承 <see cref="ProjectileAttackAbility"/> 是为了复用它的 <c>MinRange/MaxRange</c> 语义
/// （敌人远程兵那一套），但**覆写了全部行为**：不走 <c>Weapon</c>（自己发投射物），也不看 <c>Windup</c> 计时。
/// </summary>
public partial class EnergyWaveAbility : ProjectileAttackAbility
{
	[ExportGroup("子弹（直线 / 无下坠 / 穿透格挡）")]
	/// <summary>
	/// 投射物预制体（`res://Scenes/Combat/EnergyWave.tscn`）。
	/// 用 <c>PackedScene</c> 引用交给 Godot 管理依赖；同时提供字符串列
	/// <see cref="ProjectileScenePath"/> 让**配置表**能改它（表是权威）。
	/// </summary>
	[Export] public PackedScene? ProjectileScene { get; set; }

	/// <summary>投射物预制体的**路径**（表列；填了就覆盖 <see cref="ProjectileScene"/>，留空则用场景里的引用）。</summary>
	[Export(PropertyHint.File, "*.tscn")] public string ProjectileScenePath { get; set; } = string.Empty;

	/// <summary>弹速（m/s）。文档：武技1 <c>150</c>、武技2 蓄力满 <c>250</c>、完美时机 <c>400</c>。</summary>
	[Export] public float Speed { get; set; } = 150f;

	/// <summary>基础伤害（不做 <c>DamageMultiplier</c> 之外的任何加工）。</summary>
	[Export] public float Damage { get; set; } = 10f;

	/// <summary>存活时间（秒）。</summary>
	[Export] public float MaxLifetime { get; set; } = 1.6f;

	/// <summary>
	/// 「求准星世界点」时射线的**最大长度**（准星什么都没打到时，就用这个距离上的点定方向）。
	/// 与枪械的 <c>PlayerWeaponInput.MaxAimDistance</c> 同一个默认值 200 —— 口径一致；
	/// 表也能改它（技能行 <c>Params</c> 里的 <c>MaxAimDistance</c>）。
	/// </summary>
	[Export] public float MaxAimDistance { get; set; } = 200f;

	/// <summary>可穿透的目标数量（0 = 命中即消失；「穿透」类增幅经 StatSheet 的 PierceAdd 叠加）。</summary>
	[Export] public int Pierce { get; set; }

	/// <summary>击退力。</summary>
	[Export] public float Knockback { get; set; }

	/// <summary>暴击率（角色基础 5% 由角色/武器表给，这里只是本技能自己的兜底）。</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float CritChance { get; set; }

	/// <summary>暴击倍率。</summary>
	[Export] public float CritMultiplier { get; set; } = 2f;

	/// <summary>攻击标签（供增幅的"武器标签匹配"用）。</summary>
	[Export] public string[] AttackTags { get; set; } = { "Projectile", "Energy", "Physical" };

	/// <summary>
	/// **无视格挡**（文档：能量波"能穿透格挡（无视格挡效果）"）。
	/// 传进 <c>HitInstance.BypassBlock</c>，由 <c>Unit.ResolveBlock</c> 判定 —— 这样"某某子弹不可格挡"
	/// 是一条**数据**，不是给能量波写死的特例。
	/// </summary>
	[Export] public bool BypassBlock { get; set; } = true;

	[ExportGroup("完美时机（蓄力类技能的另一档参数：**绝对值进表**）")]
	/// <summary>完美时机那一发的**伤害绝对值**（文档 武技2：40）。≤0 = 用普通 <see cref="Damage"/>。</summary>
	[Export] public float PerfectDamage { get; set; }

	/// <summary>完美时机那一发的**弹速绝对值**（文档 武技2：400）。≤0 = 用普通 <see cref="Speed"/>。</summary>
	[Export] public float PerfectSpeed { get; set; }

	/// <summary>完美时机那一发的额外击退（文档："带略微的击退效果"）。</summary>
	[Export] public float PerfectKnockback { get; set; } = 2f;

	[ExportGroup("运行期可读状态（UI / 自测）")]
	/// <summary>最后一次发射的伤害（含完美时机 / 后置蓄力档位的加工结果）。</summary>
	public float LastShotDamage { get; private set; }

	/// <summary>最后一次发射的弹速。</summary>
	public float LastShotSpeed { get; private set; }

	/// <summary>最后一次发射是不是完美时机那一发。</summary>
	public bool LastShotPerfect { get; private set; }

	private bool _fired;
	private bool _perfect;
	private bool _releasePending;
	private float _releasedPower = 1;

	/// <summary>
	/// **不需要目标**：能量波朝准星发射（`Aim = Crosshair`），有没有敌人都能放。
	///
	/// ⚠️ 必须覆写基类 <see cref="ProjectileAttackAbility.CanActivate"/> —— 那一份是给
	/// **敌人远程兵**写的（要求"目标在 MinRange~MaxRange 之间"），拿它当玩家技能的闸门会让
	/// "没锁定目标就放不出来"（实测：自测里 TryActivate 一直 False，原因就是 Target=null）。
	/// </summary>
	protected override bool CanActivate(Unit? target) => true;

	protected override void OnActivate(Unit? target)
	{
		_fired = false;
		_perfect = false;
		_releasePending = false;
		_releasedPower = 1;

		if (!ResolveProjectileScene())
		{
			GD.PushWarning($"[EnergyWave] {AbilityId} 没有可用的投射物预制体，技能不执行。");
			GameLog.Write("Combat", $"⚠ 技能 {AbilityId} 未执行：投射物预制体读不到（ProjectileScenePath 检查一下）。");
			Cancel();
			return;
		}

		// 蓄力期的ATK不出弹；松手后重播释放段并等待那一轮真实ATK。
		if (PlaySkillAnimation().Length == 0) { Cancel(); return; }
		if (ActRoguelike.Debug.SelfTest.DebugSkipSkillAnimation)
		{
			// 排障开关：跳过"等动画事件帧"这一段，直接在激活时发射。
			// 用来把「动画 → 事件帧」与「子弹 → 命中」两段分开定位（自测里用）。
			GameLog.Write("Combat", $"⚠ 技能 {AbilityId} 按排障开关跳过了动画事件帧，直接发射。");
			FireShot();
		}
	}

	/// <summary>先按表里的路径解析预制体（表是权威），没有路径就用场景里的引用。读不到返回 false。</summary>
	private bool ResolveProjectileScene()
	{
		if (!string.IsNullOrEmpty(ProjectileScenePath))
		{
			if (ResourceLoader.Exists(ProjectileScenePath) && RuntimeResources.Load<PackedScene>(ProjectileScenePath) is { } scene)
			{
				ProjectileScene = scene;
			}
			else
			{
				return false;
			}
		}

		return ProjectileScene != null;
	}

	/// <summary>
	/// 松手冻结完美结果与威力，释放段真实ATK才生成投射物。
	/// </summary>
	protected override void OnChargeReleased(bool perfect, int steps, float powerScale)
	{
		if (!IsBusy || _fired)
		{
			return;
		}

		_perfect = perfect;
		_releasedPower = powerScale;
		_releasePending = true;
		if (!PlayChargeReleaseAnimation()) Cancel();
	}

	/// <summary>
	/// **基础蓄力没完成就松手 → 什么都不发生**（设计文档第 14 行）。
	/// 这里只收尾（停动画、退出施法态）并留一条日志；**不消耗冷却**（没打出去就没有冷却）。
	/// </summary>
	protected override void OnChargeAborted()
	{
		GameLog.Write(
			"Combat",
			$"能量波：{AbilityId} 基础蓄力未完成（{ChargeTime:0.##}s / {ChargeBaseSeconds:0.##}s）就松手，本次不释放。");
		Cancel();
	}

	/// <summary>
	/// 非蓄力直接响应ATK；蓄力只响应松手后重播的释放段ATK。
	/// </summary>
	protected override void OnAnimEvent(string name)
	{
		if (name != AnimEvent || (HoldToCharge && !_releasePending))
		{
			return;
		}

		_releasePending = false;
		FireShot();
	}

	/// <summary>事件帧没来时的兜底：**不发射**，只让技能超时结束（避免一直占着 IsBusy）。</summary>
	protected override void Tick(float delta)
	{
		if (!IsBusy)
		{
			return;
		}

		if (_fired)
		{
			Finish();
			return;
		}

		float eventSpeed = HoldToCharge ? ChargeReleaseAnimSpeedScale : AnimSpeedScale;
		if ((!HoldToCharge || _releasePending) && StateTime > AnimationEventTimeout / Mathf.Max(0.01f, eventSpeed))
		{
			GameLog.Write("Combat", $"⚠ 技能 {AbilityId} 等不到动画事件帧「{AnimEvent}」，本次施法放弃（检查 SkillAnimations 表的事件帧名）。");
			Cancel();
		}
	}
	protected override void OnFinishOrCancel() => _releasePending = false;

	/// <summary>
	/// **口径与既有枪械完全一致**（`docs/anim-pipeline.md` §6.4-4）：先求「准星打到的世界点」
	/// （<see cref="AimUtil.GetCrosshairPoint"/> —— **与 <c>PlayerWeaponInput</c> 同一个函数**），
	/// 再从**子弹出生点**朝那个世界点发射（枪械的 `Weapon.TryFire` 也是"枪口 → 准星世界点"）。
	///
	/// ⚠️ 以前这里走 `ResolveAimDirection()`（相机屏幕中心射线的**方向**）：斜向下 20° 从胸口射出，
	/// 约 3m 就落地 —— 近身能中、远距离打不到（§6.4-4 的实测）。改成"求世界点"之后，
	/// 子弹朝着**准星真正指着的那个点**飞。
	///
	/// ⚠️ 这不与「直线子弹无下坠」矛盾：变的是**方向**（朝着准星世界点），**飞行中**仍然是
	/// `GravityScale = 0`（见 <see cref="FireShot"/> 里那一行）。文档说的"直线子弹"是弹道不下坠。
	/// </summary>
	private Vector3 ResolveShotDirection()
	{
		if (OwnerUnit == null)
		{
			return ResolveAimDirection();
		}

		// 从「胸口」起算 —— 与 FireShot 的出生点在同一条线上：出生点 = 胸口 + 方向 × 0.6，
		// 所以"从出生点朝世界点"与"从胸口朝世界点"是同一个方向（不再有相机↔角色的视差）。
		Vector3 chest = OwnerUnit.GlobalPosition + (Vector3.Up * 0.9f);
		Vector3 point = AimUtil.GetCrosshairPoint(
			GetViewport(), OwnerUnit, MaxAimDistance, CombatLayers.World | CombatLayers.HostileTo(OwnerUnit.Faction));

		Vector3 direction = point - chest;
		// 退化（准星点正好压在胸口）时退回原来的解析：没有相机时它给的是"角色前方"。
		return direction.LengthSquared() > 0.0001f ? direction.Normalized() : ResolveAimDirection();
	}

	private void FireShot()
	{
		if (_fired || OwnerUnit == null || ProjectileScene == null)
		{
			return;
		}

		// 排障开关（自测里用来把"事件帧有没有到"与"发射有没有问题"两件事分开）。
		if (ActRoguelike.Debug.SelfTest.DebugSkipFireShot)
		{
			GameLog.Write("Combat", $"能量波·排障：事件帧已到但按开关跳过发射（{AbilityId}）。");
			return;
		}

		_fired = true;
		Vector3 direction = ResolveShotDirection();
		Vector3 origin = OwnerUnit.GlobalPosition + (Vector3.Up * 0.9f) + (direction * 0.6f);

		StatSheet stats = OwnerUnit.Stats;
		// 表里的伤害是**绝对数值**（文档：武技1 伤害 10；武技2 蓄满 30 / 完美 40）。角色/增幅的攻击力倍率默认为 1，
		// 所以这里照乘就行 —— 不改数值时"伤害 10"就是 10（自测硬指标可逐位核对）。
		AttackOffense offense = AttackOffense.Capture(OwnerUnit, CritChance, CritMultiplier);
		float damageMultiplier = offense.DamageMultiplier;

		// 完美时机：**绝对值优先**（表里的 PerfectDamage / PerfectSpeed）；没配才回落到普通值。
		// 后置蓄力的档位倍率（弓 / 短弩 / 长枪那一段）与完美时机是**两件正交的事**，所以两者相乘
		// （武技2 没有后置蓄力 → ChargePowerScale 恒为 1，于是 30 → 40、250 → 400 逐位精确）。
		float power = HoldToCharge ? _releasedPower : 1;
		float shotDamage = (_perfect && PerfectDamage > 0f ? PerfectDamage : Damage) * power * (HoldToCharge ? LastReleaseDamageScale : 1f);
		float shotSpeed = (_perfect && PerfectSpeed > 0f ? PerfectSpeed : Speed) * power;

		var attack = new AttackInstance
		{
			Attacker = OwnerUnit,
			Ability = AbilityId,
			BaseDamage = shotDamage * damageMultiplier,
			CanCrit = true,
			CritChance = offense.CritChance,
			CritMultiplier = offense.CritMultiplier,
			Knockback = Knockback + (_perfect ? PerfectKnockback : 0f),
			Policy = HitPolicy.OncePerTarget,
			Lifetime = MaxLifetime,
			Tags = AttackTags,
			Pierce = Pierce + (int)stats.Get("PierceAdd"),
			Ricochet = (int)stats.Get("RicochetAdd"),
			// 命中爆炸（P5 `<A1限定>`「爆裂火球」：能量波命中时产生范围伤害）。
			// 与 `Weapon.cs` 同一套键：半径 = `ExplosionRadiusAdd`（0 = 不爆炸），伤害 = 本次伤害 × `ExplosionDamageScale`。
			// 没配置时两个键都是 0 → **逐位不变**（不发散、不加行）。
			ExplosionRadius = stats.Get("ExplosionRadiusAdd"),
			ExplosionDamage = shotDamage * damageMultiplier * stats.Get("ExplosionDamageScale"),
			BypassBlock = BypassBlock,
			AbilityKind = AbilityId,
		};

		Node parent = GetTree().CurrentScene ?? GetTree().Root;
		var projectile = ProjectileScene.Instantiate<Projectile>();
		parent.AddChild(projectile);
		projectile.GlobalPosition = origin;
		projectile.Speed = shotSpeed;
		projectile.GravityScale = 0f; // 直线子弹，无下坠（方向朝着准星世界点，见 ResolveShotDirection）
		projectile.Launch(attack, direction);

		LastShotDamage = attack.BaseDamage;
		LastShotSpeed = projectile.Speed;
		LastShotPerfect = _perfect;

		GameLog.Write(
			"Combat",
			$"能量波发射：{AbilityId} 伤害 {attack.BaseDamage:0.##} 弹速 {projectile.Speed:0.##}"
			+ $"（{(_perfect ? "完美时机" : "普通")}，蓄力 {ChargeTime:0.##}s/{ChargeBaseSeconds:0.##}s 档 {ChargeSteps} 威力 ×{power:0.###}，"
			+ $"无视格挡={BypassBlock}，穿透 {attack.Pierce}，方向 {direction}）");
	}
}


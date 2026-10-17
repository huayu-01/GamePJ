using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// **箭矢齐射**：角色 A 武器 2「短弩」与武器 3「弓」的武技共用这一个类。
///
/// <para>文档里的四件事都在这一个类里，差别**只有表**（`Config/Tables/Skills/skl_a2_*.tres` /
/// `skl_a3_*.tres` 的列与 Params）—— 代码里没有任何一个技能专属的数值：</para>
/// <list type="bullet">
/// <item><b>散射</b>（武技1「射射射」3 发 / 20°）：<see cref="Count"/> × <see cref="SpreadStepDegrees"/>。</item>
/// <item><b>基础蓄力 + 后置蓄力逐档 + 每档加发数</b>（武技2「大射天下」0.4s 基础、每 0.2s +2 支、最多 9）：
///   档位由 <see cref="Ability"/> 基类算（`ChargeSteps`），本类按 <see cref="CountPerChargeStep"/>
///   换算成发数 —— 这就是 `docs/abilities.md` §1.3 第 4 条说的"非倍率维度：自己子类加一列"。</item>
/// <item><b>蓄力增伤 + 增弹速</b>（弓 武技1「箭在弦上」每 0.1s +30% 伤害、+30 弹速）：
///   伤害走基类的 <c>ChargePowerScale</c>（表列 <c>ChargeStepScale = 1.3</c>），
///   弹速走本类的 <see cref="SpeedPerChargeStep"/>（+30 是**绝对值**，不是倍率）。</item>
/// <item><b>箭的子弹特性</b>（短弩）：飞行一段距离后下坠（<see cref="GravityStartDistance"/>，
///   "射速决定什么时候开始下坠"）+ 弹速够高才击退（<see cref="KnockbackMinSpeed"/>）+ 追踪被打标记的目标
///   （<see cref="HomingStatusId"/>，弓 武技2「刹那之眼」那条）。</item>
/// </list>
///
/// <para><b>动画驱动</b>（用户裁定"没有对应动画 → 技能不执行"）：
/// 非蓄力技（短弩武技1）在事件帧（<c>ATK</c>）里一次把整排箭发完；
	/// 蓄力技松手冻结档位，重播释放段，只有该轮真实ATK才发射；变奏从ATK开始逐箭连发。</para>
/// </summary>
public partial class ArrowVolleyAbility : Ability
{
	[ExportGroup("子弹（箭）")]
	/// <summary>箭的预制体（`res://Scenes/Combat/Arrow.tscn`）。</summary>
	[Export] public PackedScene? ProjectileScene { get; set; }

	/// <summary>箭预制体的**路径**（表列；填了就覆盖 <see cref="ProjectileScene"/>）。</summary>
	[Export(PropertyHint.File, "*.tscn")] public string ProjectileScenePath { get; set; } = string.Empty;

	/// <summary>单发伤害（**绝对数值**，来自表；不做 <c>DamageMultiplier</c> 之外的加工）。</summary>
	[Export] public float Damage { get; set; } = 10f;

	/// <summary>弹速（m/s）。</summary>
	[Export] public float Speed { get; set; } = 150f;

	/// <summary>存活时间（秒）。</summary>
	[Export] public float MaxLifetime { get; set; } = 1.6f;

	[ExportGroup("散射（多角度多弹）")]
	/// <summary>**基础发数**（文档：射射射 3 发 / 大射天下 基础 3 支）。</summary>
	[Export] public int Count { get; set; } = 1;

	/// <summary>
	/// **相邻两发之间的夹角**（度；文档写「角度 20」= 每一档散开 20°）。
	/// 发数在准星轴上**对称展开**：3 发 → -20° / 0° / +20°。
	/// </summary>
	[Export] public float SpreadStepDegrees { get; set; }

	[ExportGroup("蓄力档位 → 发数 / 弹速（非倍率维度：位阶由基类给，单位由本类定）")]
	/// <summary>**后置蓄力每升一档多发几支**（文档：大射天下 每 0.2s +2 支）。0 = 不增加。</summary>
	[Export] public int CountPerChargeStep { get; set; }

	/// <summary>**后置蓄力每升一档加多少弹速**（绝对值；文档：箭在弦上 每档 +30 射速）。</summary>
	[Export] public float SpeedPerChargeStep { get; set; }

	[ExportGroup("子弹行为（短弩的子弹特性）")]
	/// <summary>重力缩放（0 = 直线无下坠）。短弩的箭 = 表里给一个 &gt; 0 的值。</summary>
	[Export] public float GravityScale { get; set; }

	/// <summary>**下坠起点**（米；0 = 一发射就下坠）。文档：「飞行一段距离后会下坠，射速决定了什么时候开始下坠」。</summary>
	[Export] public float GravityStartDistance { get; set; }

	/// <summary>击退力（**弹速够高时**才生效，见 <see cref="KnockbackMinSpeed"/>）。</summary>
	[Export] public float Knockback { get; set; }

	/// <summary>**弹速门槛**：只有实际弹速 ≥ 它才吃 <see cref="Knockback"/>（文档：「射速较高的时候造成击退」）。</summary>
	[Export] public float KnockbackMinSpeed { get; set; }

	[ExportGroup("追踪（弓 武技2「刹那之眼」：射出的箭追踪被打标记的最近目标）")]
	/// <summary>要追踪的**标记状态 Id**（空 = 不追踪）。表列 <c>HomingStatusId</c>。</summary>
	[Export] public string HomingStatusId { get; set; } = string.Empty;

	/// <summary>追踪转向速度（度/秒）。</summary>
	[Export] public float HomingTurnRateDegrees { get; set; }

	/// <summary>追踪搜索半径（米；0 = 不限）。</summary>
	[Export] public float HomingRange { get; set; }

	[ExportGroup("其余命中参数")]
	/// <summary>可穿透的目标数量（0 = 命中即消失）。</summary>
	[Export] public int Pierce { get; set; }

	/// <summary>暴击率（角色/武器表给基准，这里只是本技能自己的兜底）。</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float CritChance { get; set; }

	/// <summary>暴击倍率。</summary>
	[Export] public float CritMultiplier { get; set; } = 2f;

	/// <summary>攻击标签（供增幅的武器标签匹配）。</summary>
	[Export] public string[] AttackTags { get; set; } = { "Projectile", "Arrow", "Physical" };

	/// <summary>无视格挡（箭默认**可以**被格挡 —— 文档只给能量波写了"穿透格挡"）。</summary>
	[Export] public bool BypassBlock { get; set; }

	/// <summary>求"准星世界点"时射线的最大长度。</summary>
	[Export] public float MaxAimDistance { get; set; } = 200f;

	/// <summary>
	/// **投射物的整体缩放**（P4 新增，**只加不删**；≤0 = 不缩放）。
	///
	/// <para>文档（角色 B 灵剑武技2）：「剑气大小会**等比攻击盒大小**」—— 剑气就是一个被缩放的
	/// 箭（同一个 <c>Projectile</c> 组件、同一套弹道/穿透/格挡语义），只是外观尺寸跟攻击盒走。
	/// 默认 ≤0 → 这一行代码不进，**既有四把武器的箭/弹行为逐位不变**。</para>
	/// </summary>
	[Export] public float ProjectileSizeScale { get; set; }

	[ExportGroup("完美时机（预留：本类与能量掌同一套表列，武技2 的两行都留空）")]
	/// <summary>完美时机那一发的伤害绝对值（≤0 = 用普通 <see cref="Damage"/>）。</summary>
	[Export] public float PerfectDamage { get; set; }

	/// <summary>完美时机那一发的弹速绝对值（≤0 = 用普通 <see cref="Speed"/>）。</summary>
	[Export] public float PerfectSpeed { get; set; }

	/// <summary>完美时机的额外击退。</summary>
	[Export] public float PerfectKnockback { get; set; }

	[ExportGroup("运行期可读状态（UI / 自测）")]
	/// <summary>最后一次齐射的单发伤害（含档位加工）。</summary>
	public float LastShotDamage { get; private set; }

	/// <summary>最后一次齐射的弹速。</summary>
	public float LastShotSpeed { get; private set; }

	/// <summary>最后一次齐射的**发数**（散射 / 档位加发数之后）。</summary>
	public int LastShotCount { get; private set; }

	/// <summary>最后一次齐射实际用的夹角（度）。</summary>
	public float LastSpreadDegrees { get; private set; }

	/// <summary>最后一次齐射是不是完美时机。</summary>
	public bool LastShotPerfect { get; private set; }

	[ExportGroup("连续射击增强（<A3限定>「变奏」，P5b 新增：不填 = 一行都不进）")]
	/// <summary>
	/// 连续射击时**发数的下标上限**（0 = 不启用，= 改动前的行为）。
	/// 文档第 92 行「变奏：武技1 的非蓄力射击在连续射击时会变得更强，第二箭变为二连发，第三箭变为三连发」
	/// → 这一列填 3：第 1 箭 1 发、第 2 箭 2 发、第 3 箭 3 发（发数 = max(表里的发数, 连射序号)）。
	/// </summary>
	[Export] public int RampMaxCount { get; set; }

	/// <summary>两次射击间隔超过它，连射序号归零（秒；≤0 = 不归零）。</summary>
	[Export] public float RampWindowSeconds { get; set; }
	/// <summary>变奏逐箭发射的间隔；第一箭必须由真实ATK启动，后续可被动作交接取消。</summary>
	[Export] public float RampShotIntervalSeconds { get; set; } = 0.08f;
	public void ConfigureRamp(int maxCount, float window, float interval)
	{
		RampMaxCount = Mathf.Max(1, maxCount);
		RampWindowSeconds = Mathf.Max(0.01f, window);
		RampShotIntervalSeconds = Mathf.Max(0.01f, interval);
		_rampCount = 0; _rampTimer = 0;
	}
	protected override bool CanReleaseBeforeBase => RampMaxCount > 0;

	private int _rampCount;
	private float _rampTimer;

	private bool _fired;
	private bool _perfect;
	private bool _releasePending, _quickRelease;
	private float _releasedPower = 1;
	private int _releasedSteps;
	private AttackInstance? _burstAttack;
	private int _burstRemaining;
	private float _burstTimer, _burstSpeed, _burstSize, _burstInterval;
	private ulong _burstFrame;

	/// <summary>不需要目标（朝准星/朝角色朝向都能放）。</summary>
	protected override bool CanActivate(Unit? target) => true;

	protected override void OnActivate(Unit? target)
	{
		_fired = false;
		_perfect = false;
		_releasePending = false; _quickRelease = false;
		_releasedPower = 1; _releasedSteps = 0;
		_burstRemaining = 0; _burstAttack = null;

		if (!ResolveProjectileScene())
		{
			GD.PushWarning($"[ArrowVolley] {AbilityId} 没有可用的箭预制体，技能不执行。");
			GameLog.Write("Combat", $"⚠ 技能 {AbilityId} 未执行：箭的投射物预制体读不到（ProjectileScenePath 检查一下）。");
			Cancel();
			return;
		}

		if (PlaySkillAnimation().Length == 0) { Cancel(); return; }
		if (ActRoguelike.Debug.SelfTest.DebugSkipSkillAnimation)
		{
			GameLog.Write("Combat", $"⚠ 技能 {AbilityId} 按排障开关跳过了动画事件帧，直接发射。");
			FireVolley();
		}
	}

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

	/// <summary>松手冻结档位；变奏允许基础蓄力前快射，二者均等待释放段真实ATK。</summary>
	protected override void OnChargeReleased(bool perfect, int steps, float powerScale)
	{
		if (!IsBusy || _fired)
		{
			return;
		}

		_perfect = perfect;
		_releasedPower = powerScale; _releasedSteps = steps;
		_quickRelease = !ChargeBaseComplete;
		_releasePending = true;
		if (!PlayChargeReleaseAnimation()) Cancel();
	}

	/// <summary>基础蓄力没完成就松手 → 不释放（设计文档第 14 行）。</summary>
	protected override void OnChargeAborted()
	{
		GameLog.Write(
			"Combat",
			$"箭矢：{AbilityId} 基础蓄力未完成（{ChargeTime:0.##}s / {ChargeBaseSeconds:0.##}s）就松手，本次不释放。");
		Cancel();
	}

	/// <summary>仅释放阶段的真实ATK发射；蓄力期间越过ATK不能提前出弹。</summary>
	protected override void OnAnimEvent(string name)
	{
		if (name != AnimEvent || (HoldToCharge && !_releasePending))
		{
			return;
		}

		_releasePending = false;
		FireVolley();
	}

	/// <summary>事件帧没来时的兜底：**不发射**，只让技能超时结束（不做时间驱动兜底）。</summary>
	protected override void Tick(float delta)
	{
		// 连续射击窗口：**不施法时也要走**（归零之后下一箭从第 1 发重新数）。
		// RampWindowSeconds ≤ 0 → 这一段一行都不进（既有四把武器逐位不变）。
		if (RampWindowSeconds > 0f)
		{
			_rampTimer -= delta;
			if (_rampTimer <= 0f)
			{
				_rampCount = 0;
			}
		}

		if (!IsBusy)
		{
			return;
		}

		if (_fired)
		{
			if (_burstRemaining > 0 && _burstAttack != null)
			{
				if (Engine.GetPhysicsFrames() == _burstFrame) return;
				_burstTimer -= delta;
				if (_burstTimer > 0.000001f) return;
				FireProjectile(_burstAttack, ResolveShotDirection(), _burstSpeed, _burstSize, 0);
				_burstRemaining--;
				_burstTimer += _burstInterval;
				if (_burstRemaining > 0) return;
			}
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
	protected override void OnFinishOrCancel()
	{
		_releasePending = false; _burstRemaining = 0; _burstAttack = null;
	}
	protected override void OnLoadoutReset() { _rampCount = 0; _rampTimer = 0; }
	protected virtual void PrepareShot() { }

	/// <summary>
	/// 释放方向。
	/// <list type="bullet">
	/// <item><c>Aim = Crosshair</c>：**与枪械/能量掌同一个口径** —— 先求"准星打到的世界点"，
	///   再从箭的出生点朝那个点飞（<see cref="AimUtil.GetCrosshairPoint"/>）。</item>
	/// <item>其余（文档：大射天下的「朝角色方向」= 角色的朝向）：走基类 <see cref="Ability.ResolveAimDirection"/>。</item>
	/// </list>
	/// </summary>
	private Vector3 ResolveShotDirection()
	{
		if (Aim != AimSource.Crosshair || OwnerUnit == null)
		{
			return ResolveAimDirection();
		}

		Vector3 chest = OwnerUnit.GlobalPosition + (Vector3.Up * 0.9f);
		Vector3 point = AimUtil.GetCrosshairPoint(
			GetViewport(), OwnerUnit, MaxAimDistance, CombatLayers.World | CombatLayers.HostileTo(OwnerUnit.Faction));
		Vector3 direction = point - chest;
		return direction.LengthSquared() > 0.0001f ? direction.Normalized() : ResolveAimDirection();
	}

	private void FireVolley()
	{
		if (_fired || OwnerUnit == null || ProjectileScene == null)
		{
			return;
		}

		if (ActRoguelike.Debug.SelfTest.DebugSkipFireShot)
		{
			GameLog.Write("Combat", $"箭矢·排障：事件帧已到但按开关跳过发射（{AbilityId}）。");
			return;
		}

		_fired = true;
		PrepareShot();

		Vector3 direction = ResolveShotDirection();
		StatSheet stats = OwnerUnit.Stats;
		float damageMultiplier = stats.Get("DamageMultiplier");
		float power = HoldToCharge ? _releasedPower : 1;
		int steps = HoldToCharge ? _releasedSteps : 0;

		// 发数 = 基础 + 档数 × 每档（文档：大射天下 3 + 2×档，最多 9）。
		int count = Mathf.Max(1, Count + (steps * CountPerChargeStep));

		// **连续射击增强**（P5b `<A3限定>`「变奏」）：第 N 发连射时发数至少 N（文档：第二箭二连发、第三箭三连发）。
		// RampMaxCount ≤ 0 → 一行都不进（既有四把武器逐位不变）。
		bool burst = RampMaxCount > 0 && (!HoldToCharge || _quickRelease);
		if (burst)
		{
			_rampCount = (_rampCount % RampMaxCount) + 1;
			_rampTimer = Mathf.Max(0.01f, RampWindowSeconds);
			count = Mathf.Max(count, _rampCount);
		}
		else if (RampMaxCount > 0) { _rampCount = 0; _rampTimer = 0; }

		// **发数覆盖**（P5 `<A2限定>`「专一」：只会射出一箭，但原先每多出一箭就 +80% 伤害）。
		// 覆盖值 ≤ 0 → 一行都不进（既有四把武器逐位不变）；被"砍掉"的那几发按**每一发**乘一个倍率
		// （`VolleyDamagePerLostArrow`，默认 0 → 不乘）。
		float lostArrowDamageMultiplier = 1f;
		float countOverride = stats.Get("VolleyCountOverride");
		if (countOverride > 0f && countOverride < count)
		{
			int lost = count - Mathf.RoundToInt(countOverride);
			lostArrowDamageMultiplier = 1f + (lost * Mathf.Max(0f, stats.Get("VolleyDamagePerLostArrow")));
			count = Mathf.Max(1, Mathf.RoundToInt(countOverride));
		}

		// 伤害：绝对值 × 档位倍率（A2 的 ChargeStepScale = 1 → 不变；A3 = 1.3^档）。
		float shotDamage = (_perfect && PerfectDamage > 0f ? PerfectDamage : Damage) * power;
		// 弹速：绝对值 + 档数 × 每档（文档写的是**加** 30 射速，不是乘 1.3）。
		float shotSpeed = (_perfect && PerfectSpeed > 0f ? PerfectSpeed : Speed) + (steps * SpeedPerChargeStep);
		// 击退：只有"弹速较高"时才给（文档：射速较高的时候造成击退）。
		float knockback = KnockbackMinSpeed > 0f && shotSpeed < KnockbackMinSpeed
			? 0f
			: Knockback + (_perfect ? PerfectKnockback : 0f);

		var attack = new AttackInstance
		{
			Attacker = OwnerUnit,
			Ability = AbilityId,
			BaseDamage = shotDamage * damageMultiplier * lostArrowDamageMultiplier,
			CanCrit = true,
			CritChance = Mathf.Clamp(CritChance + stats.Get("CritChanceAdd"), 0f, 1f),
			CritMultiplier = CritMultiplier + stats.Get("CritMultiplierAdd"),
			Knockback = knockback,
			Policy = HitPolicy.OncePerTarget,
			Lifetime = MaxLifetime,
			Tags = AttackTags,
			Pierce = Pierce + (int)stats.Get("PierceAdd"),
			Ricochet = (int)stats.Get("RicochetAdd"),
			BypassBlock = BypassBlock,
			AbilityKind = AbilityId,
		};

		// 散射：发数在准星轴上对称展开（3 发 / 20° → -20° / 0° / +20°）。
		for (int i = 0; i < (burst ? 1 : count); i++)
		{
			float offset = burst ? 0 : (i - ((count - 1) * 0.5f)) * SpreadStepDegrees;
			FireProjectile(attack, direction, shotSpeed, ProjectileSizeScale, offset);
		}
		if (burst && count > 1)
		{
			_burstAttack = attack; _burstRemaining = count - 1;
			_burstSpeed = shotSpeed; _burstSize = ProjectileSizeScale;
			_burstInterval = Mathf.Max(0.01f, RampShotIntervalSeconds); _burstTimer = _burstInterval;
			_burstFrame = Engine.GetPhysicsFrames();
		}

		LastShotDamage = attack.BaseDamage;
		LastShotSpeed = shotSpeed;
		LastShotCount = count;
		LastSpreadDegrees = SpreadStepDegrees;
		LastShotPerfect = _perfect;

		GameLog.Write(
			"Combat",
			$"箭矢齐射：{AbilityId} 发数 {count}（基础 {Count} + 档 {ChargeSteps}×{CountPerChargeStep}）"
			+ $" 单发伤害 {attack.BaseDamage:0.##} 弹速 {shotSpeed:0.##} 夹角 {SpreadStepDegrees:0.##}°"
			+ $"（{(_perfect ? "完美时机" : (HoldToCharge ? "蓄力松手" : "事件帧"))}，蓄力 {ChargeTime:0.##}s 档 {ChargeSteps} 威力 ×{power:0.###}，"
			+ $"下坠起点 {GravityStartDistance:0.##}m 重力 {GravityScale:0.##} 击退 {knockback:0.##} 追踪 [{HomingStatusId}]，方向 {direction}）");
	}
	private void FireProjectile(AttackInstance attack, Vector3 direction, float speed, float size, float offset)
	{
		if (OwnerUnit == null || ProjectileScene == null) return;
		Vector3 origin = OwnerUnit.GlobalPosition + Vector3.Up * 0.9f + direction * 0.6f;
		Vector3 dir = Mathf.Abs(offset) < 0.0001f ? direction : direction.Rotated(Vector3.Up, Mathf.DegToRad(offset));
		var arrow = ProjectileScene.Instantiate<Projectile>();
		(GetTree().CurrentScene ?? GetTree().Root).AddChild(arrow);
		arrow.GlobalPosition = origin;
		if (size > 0) arrow.Scale = Vector3.One * size;
		arrow.Speed = speed; arrow.GravityScale = GravityScale; arrow.GravityStartDistance = GravityStartDistance;
		arrow.HomingStatusId = HomingStatusId; arrow.HomingTurnRateDegrees = HomingTurnRateDegrees; arrow.HomingRange = HomingRange;
		arrow.Pierce = attack.Pierce; arrow.MaxLifetime = MaxLifetime;
		arrow.Launch(attack, dir);
	}
}

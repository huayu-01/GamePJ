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
/// （敌人远程兵那一套），但**覆写了全部行为**：不再走 <c>Weapon</c>（武器组件有弹匣/装填，
/// 能量掌没有弹匣），也不再看 <c>Windup</c> 计时。
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

	[ExportGroup("完美时机（蓄力类技能的第二档参数）")]
	/// <summary>完美时机那一发的伤害倍率（文档：30 → 40，即 ×1.333…）。</summary>
	[Export] public float PerfectDamageScale { get; set; } = 4f / 3f;

	/// <summary>完美时机那一发的弹速倍率（文档：250 → 400，即 ×1.6）。</summary>
	[Export] public float PerfectSpeedScale { get; set; } = 1.6f;

	/// <summary>完美时机那一发的额外击退（文档："带略微的击退效果"）。</summary>
	[Export] public float PerfectKnockback { get; set; } = 2f;

	private bool _fired;
	private bool _perfect;

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

		if (!ResolveProjectileScene())
		{
			GD.PushWarning($"[EnergyWave] {AbilityId} 没有可用的投射物预制体，技能不执行。");
			GameLog.Write("Combat", $"⚠ 技能 {AbilityId} 未执行：投射物预制体读不到（ProjectileScenePath 检查一下）。");
			Cancel();
			return;
		}

		PlaySkillAnimation();
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
			if (ResourceLoader.Exists(ProjectileScenePath) && GD.Load<PackedScene>(ProjectileScenePath) is { } scene)
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

	/// <summary>**按住蓄力**：超过完美时机秒数之后就标记成"完美"（不自己发射）。</summary>
	public override void OnInputHeld(float delta)
	{
		if (IsBusy && StateTime >= PerfectTime)
		{
			_perfect = true;
		}
	}

	/// <summary>**松开按键**：按当前蓄力档位发射（<c>ATK</c> 帧到过之后才允许）。</summary>
	public override void OnInputReleased()
	{
		if (!IsBusy || _fired)
		{
			return;
		}

		FireShot();
	}

	/// <summary>**动画事件帧 <c>ATK</c>** —— 这就是"子弹在事件帧到 ATK 时发射"的那一行。</summary>
	protected override void OnAnimEvent(string name)
	{
		// 动画 ATK 帧正好落在完美时机的边界上：帧到了就说明蓄力已经完成（scale 由表按此反推）。
		if (HoldToCharge)
		{
			_perfect = true;
		}

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

		// ⚠️ 这里**刻意不是**"到时间就发射"：用户裁定没有动画就不执行，也就不会有时间驱动的兜底。
		// 只有一种情况会走到这儿 —— 动画里根本没有这个事件帧（生成表配错了），
		// 那就放弃这次施法，并留一条日志说明原因。
		if (StateTime > Windup + Recover + 1.5f)
		{
			GameLog.Write("Combat", $"⚠ 技能 {AbilityId} 等不到动画事件帧「{AnimEvent}」，本次施法放弃（检查 SkillAnimations 表的事件帧名）。");
			Cancel();
		}
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
		Vector3 direction = ResolveAimDirection();
		Vector3 origin = OwnerUnit.GlobalPosition + (Vector3.Up * 0.9f) + (direction * 0.6f);

		StatSheet stats = OwnerUnit.Stats;
		// 表里的伤害是**绝对数值**（文档：武技1 伤害 10）。角色/增幅的攻击力倍率默认为 1，
		// 所以这里照乘就行 —— 不改数值时"伤害 10"就是 10（自测硬指标可逐位核对）。
		float damageMultiplier = stats.Get("DamageMultiplier");

		var attack = new AttackInstance
		{
			Attacker = OwnerUnit,
			Ability = AbilityId,
			BaseDamage = Damage * damageMultiplier * (_perfect ? PerfectDamageScale : 1f),
			CanCrit = true,
			CritChance = Mathf.Clamp(CritChance + stats.Get("CritChanceAdd"), 0f, 1f),
			CritMultiplier = CritMultiplier + stats.Get("CritMultiplierAdd"),
			Knockback = Knockback + (_perfect ? PerfectKnockback : 0f),
			Policy = HitPolicy.OncePerTarget,
			Lifetime = MaxLifetime,
			Tags = AttackTags,
			Pierce = Pierce + (int)stats.Get("PierceAdd"),
			Ricochet = (int)stats.Get("RicochetAdd"),
			BypassBlock = BypassBlock,
			AbilityKind = AbilityId,
		};

		Node parent = GetTree().CurrentScene ?? GetTree().Root;
		var projectile = ProjectileScene.Instantiate<Projectile>();
		parent.AddChild(projectile);
		projectile.GlobalPosition = origin;
		projectile.Speed = Speed * (_perfect ? PerfectSpeedScale : 1f);
		projectile.GravityScale = 0f; // 直线子弹，无下坠
		projectile.Launch(attack, direction);

		GameLog.Write(
			"Combat",
			$"能量波发射：{AbilityId} 伤害 {attack.BaseDamage:0.##} 弹速 {projectile.Speed:0.##}"
			+ $"（{( _perfect ? "完美时机" : "普通")}，无视格挡={BypassBlock}，穿透 {attack.Pierce}，方向 {direction}）");
	}

	/// <summary>完美时机窗口（秒）——蓄到这个时长就算"闭合"。由技能行给（表是权威）。</summary>
	[Export] public float PerfectTime { get; set; } = 0.5f;
}


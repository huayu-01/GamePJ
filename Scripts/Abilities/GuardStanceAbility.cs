using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// **技能 2「架」**（角色 B，设计文档第 103 行）：
///
/// <code>
/// 技能2[架]{cd:3}:角色短暂的进入架势{0.75}秒,如果这期间被敌人命中则规避这次攻击并退出架势;
/// </code>
///
/// <list type="bullet">
/// <item><b>进入架势 0.75s</b>：<c>Windup</c> 之后（动画事件帧 <c>GuardOn</c> 也算，两条路都指向同一个状态）。</item>
/// <item><b>被命中则规避这次攻击并退出架势</b>：挨打时把这次伤害**整个规避掉**
///   （<c>Unit.IsInvulnerable</c> 是既有的"这次命中不算"通路，见 <c>Unit.ReceiveHit</c> 的
///   无敌分支 → 返回 <c>Invulnerable = true</c>、一点血都不掉），然后立刻收招。</item>
/// <item><b>逐帧复位</b>：规避只对"打进来的那一次"生效 —— 每帧开始时把上一帧设的无敌清掉，
///   这样架势的剩余时间里**下一次命中仍会被规避**（而不是"无敌到架势结束"）。
///   这就是"架势 = 挡一下"与"无敌帧 = 一段时间免伤"的区别。</item>
/// </list>
///
/// <para>为什么不动 <c>BlockComponent</c>：既有格挡是**部分格挡**（保留
/// <c>BlockDamageMultiplier</c> 比例的伤害），而文档这一条要求的是"**规避这次攻击**"
/// （0 伤害）。两者语义不同，所以这一条走"无效化"而不是把既有的格挡改成 0 伤害
/// （改既有语义会连带影响守卫类敌人与"护卫"那条异化）。</para>
/// </summary>
public partial class GuardStanceAbility : Ability
{
	[ExportGroup("架势")]
	/// <summary>架势总时长（秒）。文档：0.75。</summary>
	[Export] public float StanceSeconds { get; set; } = 0.75f;

	/// <summary>架势从哪一帧开始算（秒）—— 通常 = <see cref="Ability.Windup"/>（举刀那一刻）。</summary>
	[Export] public float GuardStartSeconds { get; set; } = 0.15f;

	/// <summary>开架势的那个动画事件帧名（表列 <c>AnimEvent</c>；留空 = 纯时间轴）。</summary>
	[Export] public string GuardOnEvent { get; set; } = "GuardOn";

	/// <summary>收架势的那个动画事件帧名（表列 <c>AnimEventEnd</c>；留空 = 不用）。</summary>
	[Export] public string GuardOffEvent { get; set; } = "GuardOff";

	[ExportGroup("运行期可读状态（自测 / UI）")]
	/// <summary>本局累计成功"架"住几次（"招架反击"这条增幅要读它）。</summary>
	public int ParryCount { get; private set; }

	/// <summary>现在还在架势里吗（自测 / HUD）。</summary>
	public bool StanceActive { get; private set; }

	/// <summary>架势里最近一次挡到的伤害值（自测取证）。</summary>
	public float LastParriedDamage { get; private set; }

	/// <summary>架势开始/结束（自测取证）。</summary>
	public int StanceCount { get; private set; }

	private bool _subscribed;
	private bool _parryInvulnerable;

	protected override void OnReady()
	{
		// "被命中"的订阅点：Unit.ReceiveHit 里 HitReceived 在**无敌判定之前**派发，
		// 所以在这里把这次命中的伤害规避掉（把 IsInvulnerable 置位）就能挡住它。
		// ⚠️ 只在 <see cref="Enabled"/> 时生效（Enabled 来自技能行：能力开关矩阵/角色行决定谁有「架」）。
	}

	protected override void OnActivate(Unit? target)
	{
		if (OwnerUnit == null)
		{
			Cancel();
			return;
		}

		StanceActive = false;
		EnsureSubscribed();
		PlaySkillAnimation();

		GameLog.Write(
			"Combat",
			$"架：{AbilityId} 进入架势（{GuardStartSeconds:0.##}s 后开始，持续 {StanceSeconds:0.##}s，事件帧 {GuardOnEvent}/{GuardOffEvent}）");
	}

	private void EnsureSubscribed()
	{
		if (_subscribed || OwnerUnit == null)
		{
			return;
		}

		OwnerUnit.HitReceived += OnOwnerHit;
		_subscribed = true;
	}

	/// <summary>
	/// **每帧把上一帧"架住"那次设的规避清掉** —— 这样架势的剩余时间里下一次命中仍会被规避
	/// （这才是"架势档一下"，而不是"无敌到架势结束"）。
	///
	/// <para>⚠️ 只清**本技能自己设的**那一个（<see cref="_parryInvulnerable"/>）：
	/// 别人的无敌帧（后撤 / 冲刺）**一点都不碰**。</para>
	/// </summary>
	public override void _PhysicsProcess(double delta)
	{
		if (_parryInvulnerable)
		{
			_parryInvulnerable = false;
			if (OwnerUnit != null && IsInstanceValid(OwnerUnit))
			{
				OwnerUnit.IsInvulnerable = false;
			}
		}

		base._PhysicsProcess(delta);
	}

	/// <summary>被命中：架势内 → 规避这次 + 退出架势。</summary>
	private void OnOwnerHit(HitInstance hit)
	{
		if (!Enabled || !StanceActive || OwnerUnit == null)
		{
			return;
		}

		// ⚠️ 已经在无敌里（别的技能给的无敌帧）就**不要**算成"架"住了 —— 那不是这次架势挡的。
		if (OwnerUnit.IsInvulnerable)
		{
			return;
		}

		OwnerUnit.IsInvulnerable = true;
		_parryInvulnerable = true;
		ParryCount++;
		LastParriedDamage = hit.Damage;

		GameLog.Write(
			"Combat",
			$"架：{AbilityId} **架住了**（挡下 {hit.Damage:0.##} 点，来源 {hit.Attacker?.Name ?? "（无）"}），规避这次攻击并退出架势（累计 {ParryCount} 次）");

		// P5 第二批（只加）：把"架住了"这件事抛到事件总线上。
		// 订阅方是两条**表驱动**的增幅：「招架反击」（刷新技能 2 冷却）、「能量护盾」（对周围敌人造成伤害）。
		// 没有任何订阅者时这一句什么也不做（Raise 自己判 handlers == null）。
		GameplayEventBus.Raise(new GameplayEvent
		{
			Kind = GameplayEventKind.Parried,
			Source = OwnerUnit,
			Target = hit.Attacker,
			Value = hit.Damage,
			Tag = AbilityId,
			Position = OwnerUnit.GlobalPosition,
		});

		EndStance(exitStance: true);
	}

	/// <summary>动画事件帧：GuardOn 进架势 / GuardOff 出架势。</summary>
	protected override void OnAnimEvent(string name)
	{
		if (!string.IsNullOrEmpty(GuardOnEvent) && name == GuardOnEvent)
		{
			BeginStance();
			return;
		}

		if (!string.IsNullOrEmpty(GuardOffEvent) && name == GuardOffEvent)
		{
			EndStance(exitStance: true);
		}
	}

	private void BeginStance()
	{
		if (StanceActive)
		{
			return;
		}

		StanceActive = true;
		StanceCount++;
		GameLog.Write("Combat", $"架：{AbilityId} 架势开始（{StanceSeconds:0.##}s，期间被命中则规避并退出）");
	}

	private void EndStance(bool exitStance)
	{
		if (!StanceActive)
		{
			if (exitStance && IsBusy)
			{
				Finish();
			}

			return;
		}

		StanceActive = false;
		GameLog.Write("Combat", $"架：{AbilityId} 架势结束（累计架住 {ParryCount} 次）");
		if (IsBusy)
		{
			Finish();
		}
	}

	protected override void Tick(float delta)
	{
		if (!IsBusy)
		{
			return;
		}

		if (!StanceActive)
		{
			if (StateTime >= GuardStartSeconds)
			{
				BeginStance();
			}

			if (StateTime > GuardStartSeconds + StanceSeconds + 1.5f)
			{
				// 事件帧一直没来（表配错）→ 明确收招，不静默卡住。
				GameLog.Write("Combat", $"⚠ 技能 {AbilityId} 的架势超过 {GuardStartSeconds + StanceSeconds + 1.5f:0.##}s 仍没收招（检查事件帧「{GuardOffEvent}」）。");
				Finish();
			}

			return;
		}

		if (StateTime >= GuardStartSeconds + StanceSeconds)
		{
			EndStance(exitStance: true);
		}
	}

	protected override void OnFinishOrCancel()
	{
		StanceActive = false;
		Unsubscribe();
	}

	private void Unsubscribe()
	{
		if (!_subscribed || OwnerUnit == null || !IsInstanceValid(OwnerUnit))
		{
			_subscribed = false;
			return;
		}

		OwnerUnit.HitReceived -= OnOwnerHit;
		_subscribed = false;
	}

	public override void _ExitTree()
	{
		Unsubscribe();
		base._ExitTree();
	}
}

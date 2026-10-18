using ActRoguelike.Combat;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>
/// 技能2「架」：只有真实GuardOn开启0.75游戏秒的窗口；缺事件只取消。
/// 第一次敌方命中仅标记该HitInstance无效并退出，不能扩散成同帧无敌。
/// 用户已确认可以规避能量波；普通BlockComponent仍遵守穿透格挡规则。
/// 收招后派发Parried，招架反击的冷却刷新不再被收招写回覆盖。
/// </summary>
public partial class GuardStanceAbility : Ability
{
	[ExportGroup("架势")]
	/// <summary>架势总时长（秒）。文档：0.75。</summary>
	[Export] public float StanceSeconds { get; set; } = 0.75f;

	/// <summary>开架势的真实动画事件名；留空拒绝，不以时间代替。</summary>
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
	private float _stanceElapsed;
	private ulong _stanceOpenedFrame;

	protected override void OnActivate(Unit? target)
	{
		if (OwnerUnit == null)
		{
			Cancel();
			return;
		}

		StanceActive = false;
		_stanceElapsed = 0;
		EnsureSubscribed();
		if (string.IsNullOrEmpty(AnimName) || string.IsNullOrEmpty(GuardOnEvent) || PlaySkillAnimation().Length == 0) { Cancel(); return; }

		GameLog.Write(
			"Combat",
			$"架：{AbilityId} 等待真实{GuardOnEvent}，随后持续{StanceSeconds:0.##}游戏秒。");
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

	/// <summary>被命中：架势内 → 规避这次 + 退出架势。</summary>
	private void OnOwnerHit(HitInstance hit)
	{
		if (!Enabled || !IsBusy || !StanceActive || OwnerUnit == null || hit.Attacker == null || !OwnerUnit.IsHostileTo(hit.Attacker)
			|| hit.Attack?.IsDamageOverTime == true)
		{
			return;
		}

		// ⚠️ 已经在无敌里（别的技能给的无敌帧）就**不要**算成"架"住了 —— 那不是这次架势挡的。
		if (OwnerUnit.IsInvulnerable || hit.Negated)
		{
			return;
		}

		hit.Negated = true;
		ParryCount++;
		LastParriedDamage = hit.Damage;

		GameLog.Write(
			"Combat",
			$"架：{AbilityId} **架住了**（挡下 {hit.Damage:0.##} 点，来源 {hit.Attacker?.Name ?? "（无）"}），规避这次攻击并退出架势（累计 {ParryCount} 次）");

		// P5 第二批（只加）：把"架住了"这件事抛到事件总线上。
		// 订阅方是两条**表驱动**的增幅：「招架反击」（刷新技能 2 冷却）、「能量护盾」（对周围敌人造成伤害）。
		// 没有任何订阅者时这一句什么也不做（Raise 自己判 handlers == null）。
		EndStance(exitStance: true);
		GameplayEventBus.Raise(new GameplayEvent
		{
			Kind = GameplayEventKind.Parried,
			Source = OwnerUnit,
			Target = hit.Attacker,
			Value = hit.Damage,
			Tag = AbilityId,
			Position = OwnerUnit.GlobalPosition,
			Context = hit.Context,
		});
	}

	/// <summary>GuardOn开窗口；GuardOff不得提前截短表内0.75游戏秒。</summary>
	protected override void OnAnimEvent(string name)
	{
		if (!string.IsNullOrEmpty(GuardOnEvent) && name == GuardOnEvent)
		{
			BeginStance();
			return;
		}

		if (!string.IsNullOrEmpty(GuardOffEvent) && name == GuardOffEvent && _stanceElapsed + 0.000001f >= StanceSeconds)
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
		_stanceElapsed = 0;
		_stanceOpenedFrame = Engine.GetPhysicsFrames();
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
			if (StateTime > AnimationEventTimeout / Mathf.Max(0.01f, AnimSpeedScale))
			{
				// 事件帧一直没来（表配错）→ 明确收招，不静默卡住。
				GameLog.Write("Combat", $"架：{AbilityId} 缺少{GuardOnEvent}，取消且不开架。");
				Cancel();
			}

			return;
		}

		if (Engine.GetPhysicsFrames() != _stanceOpenedFrame) _stanceElapsed += delta;
		if (_stanceElapsed + 0.000001f >= StanceSeconds)
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

	protected override void OnLoadoutReset()
	{
		StanceActive = false;
		Unsubscribe();
	}

	public override void _ExitTree()
	{
		Unsubscribe();
		base._ExitTree();
	}
}

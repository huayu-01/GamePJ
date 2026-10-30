using System;

namespace ActRoguelike.Abilities;

public abstract partial class Ability
{
	/// <summary>阶段0为基础完成，后续为真实强化档位；由蓄力状态机派发，不由表现层猜时间。</summary>
	public event Action<Ability, int>? ChargeMilestoneReached;
	public event Action<Ability, bool, bool>? ChargePresentationChanged;
	private int _presentedChargeStage = -1;
	private bool _chargePresentationActive;
	private void BeginChargePresentation()
	{
		if (_chargePresentationActive) return;
		_chargePresentationActive = true; ChargePresentationChanged?.Invoke(this, true, false);
	}
	private void PublishChargeMilestone()
	{
		if (!ChargeBaseComplete || ChargeSteps <= _presentedChargeStage) return;
		// 跨档长帧只强调本次到达的最高阶段，不在同一帧堆叠多次声光。
		_presentedChargeStage = ChargeSteps; ChargeMilestoneReached?.Invoke(this, ChargeSteps);
	}
	private void EndChargePresentation(bool canceled, bool force = false)
	{
		if (!_chargePresentationActive && !force) return;
		_chargePresentationActive = false; ChargePresentationChanged?.Invoke(this, false, canceled);
	}
}

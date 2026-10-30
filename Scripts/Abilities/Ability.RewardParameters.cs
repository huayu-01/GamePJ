using System;
using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Abilities;

public abstract partial class Ability
{
	public event Action? RewardParametersChanged;
	private readonly List<(object Source, Action<Ability> Apply)> _rewardParameters = new();
	private Action? _rewardBaseline;
	/// <summary>只冻结增强参数，不重置动作、冷却、弹道和输入状态。</summary>
	public virtual Action CaptureRewardParameters()
	{
		var value = (Cooldown, CooldownMultiplier, ChargeBaseSeconds, ChargeStepSeconds, ChargeMaxSeconds, ChargeMaxSteps,
			MaxCharges, RechargeSeconds, AlwaysPerfectRelease, AutoReleaseOnFullCharge,
			_chargeBaseOverride, _chargeStepOverride, _chargeStepTimeScale, _chargeCooldownBaseline);
		return () => (Cooldown, CooldownMultiplier, ChargeBaseSeconds, ChargeStepSeconds, ChargeMaxSeconds, ChargeMaxSteps,
			MaxCharges, RechargeSeconds, AlwaysPerfectRelease, AutoReleaseOnFullCharge,
			_chargeBaseOverride, _chargeStepOverride, _chargeStepTimeScale, _chargeCooldownBaseline) = value;
	}
	public IDisposable AcquireRewardParameters(Action<Ability> apply)
	{
		_rewardBaseline ??= CaptureRewardParameters();
		var source = new object();
		_rewardParameters.Add((source, apply));
		try { apply(this); RewardParametersChanged?.Invoke(); }
		catch { ReleaseRewardParameters(source); throw; }
		return new ParameterLease(this, source);
	}
	private void ReleaseRewardParameters(object source)
	{
		if (!IsInstanceValid(this) || _rewardParameters.RemoveAll(e => ReferenceEquals(e.Source, source)) == 0) return;
		_rewardBaseline?.Invoke();
		foreach (var entry in _rewardParameters) entry.Apply(this);
		if (_rewardParameters.Count == 0) _rewardBaseline = null;
		RewardParametersChanged?.Invoke();
	}
	private void ClearRewardParameters()
	{
		_rewardBaseline?.Invoke(); _rewardBaseline = null; _rewardParameters.Clear();
	}
	private sealed class ParameterLease : IDisposable
	{
		private Ability? _ability; private readonly object _source;
		public ParameterLease(Ability ability, object source) { _ability = ability; _source = source; }
		public void Dispose() { var ability = _ability; _ability = null; ability?.ReleaseRewardParameters(_source); }
	}
}

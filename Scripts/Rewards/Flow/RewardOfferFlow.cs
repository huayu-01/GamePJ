using System;
using System.Collections.Generic;
using System.Linq;

namespace ActRoguelike.Rewards;

/// <summary>一次奖励的不可变候选与单次完成上下文；不依赖 UI、节点或关卡控制器。</summary>
public sealed class RewardOfferSession
{
	private enum State { Offering, Applying, Applied, Completed, Cancelled }
	private State _state;
	private Action? _onCompleted;
	public IReadOnlyList<RewardDefinition> Options { get; }
	public RewardDefinition? Selected { get; private set; }
	public bool IsPending => _state is State.Offering or State.Applying or State.Applied;
	internal RewardOfferSession(IReadOnlyList<RewardDefinition> options, Action? completed) { Options = Array.AsReadOnly(options.ToArray()); _onCompleted = completed; }
	public bool TryApply(int index, Action<RewardDefinition> apply)
	{
		if (_state != State.Offering || index < 0 || index >= Options.Count) return false;
		_state = State.Applying;
		try { apply(Options[index]); }
		catch { if (_state == State.Applying) _state = State.Offering; throw; }
		if (_state != State.Applying) return false;
		Selected = Options[index]; _state = State.Applied; return true;
	}
	public bool Complete()
	{
		if (_state != State.Applied) return false;
		_state = State.Completed;
		Action? callback = _onCompleted; _onCompleted = null; callback?.Invoke(); return true;
	}
	public void Cancel() { _state = State.Cancelled; _onCompleted = null; }
}

/// <summary>只管理当前一次选择；领取、关闭界面和完成节点是三个明确步骤。</summary>
public sealed class RewardOfferFlow
{
	public RewardOfferSession? Current { get; private set; }
	public bool TryBegin(IReadOnlyList<RewardDefinition> options, Action? completed = null)
	{
		if (options.Count == 0 || Current?.IsPending == true) return false;
		Current = new RewardOfferSession(options, completed); return true;
	}
	public void Cancel() { Current?.Cancel(); Current = null; }
}

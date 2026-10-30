using System;
using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Interaction;

namespace ActRoguelike.Rewards;

/// <summary>只暴露收益策略需要的服务，不传整个RunController。</summary>
public sealed class NodeRewardContext
{
	public required Unit Player { get; init; }
	public required NodeConfig Config { get; init; }
	public required Func<bool> IsActive { get; init; }
	public required Func<NodeConfig, Action?, bool> Offer { get; init; }
	public required Func<string, IReadOnlyList<string>, Action<int>, bool> Choice { get; init; }
	public Func<int, bool> Spend { get; init; } = Run.MetaProgress.TrySpend;
	public Action<int> Refund { get; init; } = Run.MetaProgress.AddShards;
	public Func<int> Balance { get; init; } = () => Run.MetaProgress.Shards;
	public required Action OnCompleted { get; init; }
	private Action? _cancel;
	public bool IsPending { get; private set; } = true;
	public bool IsValid => IsPending && IsActive();
	public void Complete() { if (!IsValid) return; IsPending = false; _cancel = null; OnCompleted(); }
	public void Cancel() { if (!IsPending) return; IsPending = false; var cancel = _cancel; _cancel = null; cancel?.Invoke(); }
	public void RefundOnCancel(Action? action) => _cancel = action;
}

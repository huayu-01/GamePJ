using System;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>一个持有者的一次效果应用；定义资源不保存冷却、持有者或撤销状态。</summary>
public abstract class RewardEffectInstance : IDisposable
{
	protected RewardContext Context { get; }
	public string RewardId { get; }
	public bool IsDisposed { get; private set; }
	protected bool IsTreeExitCleanup { get; private set; }
	private bool _activated;
	protected RewardEffectInstance(RewardContext context, string rewardId) { Context = context; RewardId = rewardId; }
	public void Activate()
	{
		if (_activated || IsDisposed) throw new InvalidOperationException("效果实例不能重复应用。");
		_activated = true;
		try { ApplyCore(); } catch { Rollback(); throw; }
	}
	internal void Commit() { if (!IsDisposed) CommitCore(); }
	internal void Rollback() { if (IsDisposed) return; Dispose(); RollbackCore(); }
	protected virtual void CommitCore() { }
	protected virtual void RollbackCore() { }
	protected abstract void ApplyCore();
	protected abstract void RemoveCore();
	public void Dispose()
	{
		if (IsDisposed) return;
		IsDisposed = true;
		if (_activated) RemoveCore();
	}
	internal void DisposeForTreeExit() { IsTreeExitCleanup = true; Dispose(); }
}

/// <summary>复用既有触发规则；移除时立即停止事件，再释放节点。</summary>
internal sealed class TriggerEffectInstance : RewardEffectInstance
{
	private readonly TriggerRule _rule;
	public TriggerEffectInstance(RewardContext context, string rewardId, TriggerRule rule) : base(context, rewardId) { _rule = rule; _rule.RewardId = rewardId; }
	protected override void ApplyCore()
	{
		Context.Player.AddChild(_rule); Context.Build.RegisterTrigger(_rule);
	}
	protected override void RemoveCore()
	{
		Context.Build.UnregisterTrigger(_rule);
		if (!GodotObject.IsInstanceValid(_rule)) return;
		_rule.Stop();
		if (!IsTreeExitCleanup && _rule.IsInsideTree() && GodotObject.IsInstanceValid(Context.Player) && Context.Player.IsInsideTree()) _rule.GetParent()?.RemoveChild(_rule);
		_rule.QueueFree();
	}
}

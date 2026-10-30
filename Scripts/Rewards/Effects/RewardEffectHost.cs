using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>统一持有应用实例；换装、死亡与退树时撤销，不让共享配置保存本局状态。</summary>
public partial class RewardEffectHost : Node
{
	public const string NodeName = "RewardEffects";
	private sealed record Entry(RunBuild Build, RewardEffectInstance Instance, bool Rune);
	private readonly List<Entry> _entries = new();
	private Unit _owner = null!;
	private bool _exiting;
	public int InstanceCount => _entries.Count;
	public static RewardEffectHost Ensure(Unit owner)
	{
		if (owner.GetNodeOrNull<RewardEffectHost>(NodeName) is { } host) return host;
		var created = new RewardEffectHost { Name = NodeName }; owner.AddChild(created); return created;
	}
	public override void _Ready() { _owner = GetParent<Unit>(); _owner.Died += OnDied; _owner.Stats.SourceRemoved += OnSourceRemoved; }
	public void Own(RunBuild build, RewardEffectInstance instance, bool rune) => _entries.Add(new Entry(build, instance, rune));
	public void ReleaseBuild(RunBuild build) => Release(_entries.Where(e => ReferenceEquals(e.Build, build)).ToArray());
	public void ReleaseReward(RunBuild build, string id) => Release(_entries.Where(e => ReferenceEquals(e.Build, build) && e.Instance.RewardId == id).ToArray());
	public void ReleaseForLoadout() => Release(_entries.Where(e => !e.Rune).ToArray());
	private void OnDied(HitInstance hit) => Release(_entries.ToArray());
	private void OnSourceRemoved(object source) { if (source is RunBuild build) ReleaseBuild(build); }
	private void Release(Entry[] entries)
	{
		foreach (Entry entry in entries.Reverse()) { _entries.Remove(entry); if (_exiting) entry.Instance.DisposeForTreeExit(); else entry.Instance.Dispose(); }
	}
	public override void _ExitTree()
	{
		_exiting = true;
		if (IsInstanceValid(_owner)) { _owner.Died -= OnDied; _owner.Stats.SourceRemoved -= OnSourceRemoved; }
		Release(_entries.ToArray());
	}
}

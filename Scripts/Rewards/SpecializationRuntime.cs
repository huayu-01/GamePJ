using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;
using K = ActRoguelike.Config.SpecializationKind;

namespace ActRoguelike.Rewards;

/// <summary>只协调定义实例、事件顺序与来源生命周期；具体特化由自己的行为处理。</summary>
public partial class SpecializationRuntime : Node, IOutgoingDamageScale, IDeathAvoid, IStatusVisualSource
{
	public const string NodeName = "Specializations";
	private Unit _owner = null!;
	private readonly Dictionary<K, RewardConfig> _rows = new();
	private readonly Dictionary<K, SpecializationBehavior> _behaviors = new();
	private readonly Dictionary<K, HashSet<object>> _sources = new();
	private SpecializationBehavior[] _behaviorOrder = Array.Empty<SpecializationBehavior>();
	private List<GameplayEvent> _pendingHits = new(), _dispatchHits = new();
	public SpecializationServices Services { get; private set; } = null!;
	public int ChainCount => Services.ChainCount;
	public int ThunderCount => Services.ThunderCount;
	public int SpearCount => Services.SpearCount;
	public int AfterimageCount => Services.AfterimageCount;
	public int FieldCount => Services.FieldCount;
	public bool Has(K kind) => _behaviors.TryGetValue(kind, out var b) && !b.IsDisposed;
	public T? Definition<T>(K kind) where T : SpecializationBehaviorDefinition => Has(kind) ? _behaviors[kind].Definition as T : null;
	public T? Behavior<T>(K kind) where T : SpecializationBehavior => Has(kind) ? _behaviors[kind] as T : null;
	public string? RewardId(K kind) => _rows.TryGetValue(kind, out var row) ? row.Id : null;
	public float Value(K kind, string key, float fallback = 0) => Has(kind) ? _behaviors[kind].Parameter(key, fallback) : fallback;
	public static SpecializationRuntime Ensure(Unit owner)
	{
		if (owner.GetNodeOrNull<SpecializationRuntime>(NodeName) is { } runtime) return runtime;
		var created = new SpecializationRuntime { Name = NodeName }; owner.AddChild(created); return created;
	}
	public override void _Ready()
	{
		_owner = GetParent<Unit>(); Services = new(this, _owner);
		_owner.AddOutgoingDamageScale(this); _owner.AddDeathAvoid(this);
		_owner.Damaged += OnDamaged; _owner.Died += OnOwnerDied; GameplayEventBus.Raised += OnFact;
	}
	public void Apply(RewardConfig row)
	{
		if (row.Specialization == K.None || Has(row.Specialization)) return;
		Add(row, SpecializationBehaviorCompatibility.Definition(row) ?? throw new InvalidOperationException($"特化 {row.Id} 缺少行为定义。"), new object());
	}
	internal IDisposable Acquire(RewardConfig row, SpecializationBehaviorDefinition definition)
	{
		object source = new(); Add(row, definition, source); return new Lease(this, definition.Kind, source);
	}
	private void Add(RewardConfig row, SpecializationBehaviorDefinition definition, object source)
	{
		if (definition.Kind != row.Specialization || definition.Validate().Any()) throw new InvalidOperationException($"特化 {row.Id} 的行为定义无效或类型不匹配。");
		if (Has(definition.Kind)) { _sources[definition.Kind].Add(source); return; }
		var behavior = definition.Create(new SpecializationContext(_owner, this, row.Id));
		_rows[definition.Kind] = row; _behaviors[definition.Kind] = behavior; _sources[definition.Kind] = new() { source };
		Reorder();
		try { behavior.Attach(); foreach (var other in _behaviorOrder) if (!other.IsDisposed) other.DefinitionsChanged(); }
		catch { Release(definition.Kind, source); throw; }
	}
	private void Reorder() => _behaviorOrder = _behaviors.Values.OrderBy(b => b.Priority).ToArray();
	private void Release(K kind, object source)
	{
		if (!_sources.TryGetValue(kind, out var sources) || !sources.Remove(source) || sources.Count != 0) return;
		_sources.Remove(kind); _rows.Remove(kind); _buffVisuals.Remove(kind);
		if (_behaviors.Remove(kind, out var behavior)) behavior.Dispose();
		Reorder(); foreach (var other in _behaviorOrder) if (!other.IsDisposed) other.DefinitionsChanged();
	}
	private sealed class Lease : IDisposable
	{
		private SpecializationRuntime? _runtime; private readonly K _kind; private readonly object _source;
		public Lease(SpecializationRuntime runtime, K kind, object source) { _runtime = runtime; _kind = kind; _source = source; }
		public void Dispose() { var runtime = _runtime; _runtime = null; if (GodotObject.IsInstanceValid(runtime)) runtime!.Release(_kind, _source); }
	}
	public override void _PhysicsProcess(double delta)
	{
		using var profile=ActRoguelike.Core.RuntimeFrameProfiler.Measure("Specializations");
		if (_owner.IsDead) return;
		// 分离本帧快照与新入队事件；回调重入的命中留给下一帧，不被收尾Clear吞掉。
		(_pendingHits, _dispatchHits) = (_dispatchHits, _pendingHits);
		try { foreach (var fact in _dispatchHits) foreach (var behavior in _behaviorOrder) if (!behavior.IsDisposed) behavior.OnPrimaryHit(fact); }
		finally { _dispatchHits.Clear(); }
		foreach (var behavior in _behaviorOrder) { if (!behavior.IsDisposed) behavior.Tick((float)delta); if (_owner.IsDead) break; }
	}
	private void OnFact(GameplayEvent fact)
	{
		if (_owner.IsDead) return;
		foreach (var behavior in _behaviorOrder) if (!behavior.IsDisposed) behavior.OnFact(fact);
		if (fact.Source == _owner && !fact.IsDamageOverTime && !fact.IsSecondary && fact.Kind == GameplayEventKind.Hit
			&& fact.Target is { } target && IsInstanceValid(target) && _owner.IsHostileTo(target)) _pendingHits.Add(fact);
	}
	public float ScaleAgainst(Unit target, AttackInstance attack)
	{
		float scale = 1; foreach (var behavior in _behaviorOrder) if (!behavior.IsDisposed) scale *= behavior.ScaleAgainst(target, attack); return scale;
	}
	private void OnDamaged(DamageResult result, HitInstance hit) { foreach (var behavior in _behaviorOrder) if (!behavior.IsDisposed) behavior.OnDamaged(result, hit); }
	private void OnOwnerDied(HitInstance hit) { foreach (var behavior in _behaviorOrder) behavior.Dispose(); }
	public IEnumerable<Unit> Enemies(Vector3 center, float radius) => Services.Enemies(center, radius);
	public override void _ExitTree()
	{
		GameplayEventBus.Raised -= OnFact;
		foreach (var behavior in _behaviorOrder) behavior.Dispose(); _behaviors.Clear(); _sources.Clear(); _behaviorOrder = Array.Empty<SpecializationBehavior>();
		if (!IsInstanceValid(_owner)) return;
		_owner.Damaged -= OnDamaged; _owner.Died -= OnOwnerDied; _owner.RemoveOutgoingDamageScale(this); _owner.RemoveDeathAvoid(this);
		_owner.Stats.RemoveBySource(this);
	}
}

using System;
using System.Collections.Generic;
using ActRoguelike.Config;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>单个持有者的特化实例；只由协调器调度，定义资源不保存计时或持有者。</summary>
public abstract class SpecializationBehavior : IDisposable
{
	protected Unit Owner { get; }
	protected SpecializationContext Context { get; private set; } = null!;
	public SpecializationBehaviorDefinition Definition { get; private set; } = null!;
	private readonly List<Node> _nodes = new();
	internal void Bind(SpecializationContext context, SpecializationBehaviorDefinition definition) { Context = context; Definition = definition; }
	public virtual void Attach() { }
	protected T Own<T>(T node) where T : Node { _nodes.Add(node); return node; }
	public bool IsDisposed { get; private set; }
	public virtual int Priority => 0;
	public virtual Color? StatusColor => null;
	protected SpecializationBehavior(Unit owner) => Owner = owner;
	public virtual void Tick(float delta) { }
	public virtual void OnKill() { }
	public virtual void OnFact(GameplayEvent fact) { if (fact.Kind == GameplayEventKind.Kill && fact.Source == Owner) OnKill(); }
	public virtual void OnPrimaryHit(GameplayEvent fact) { }
	public virtual void OnDamaged(DamageResult result, HitInstance hit) { }
	public virtual void DefinitionsChanged() { }
	public virtual float ScaleAgainst(Unit target, AttackInstance attack) => 1;
	public virtual bool TryAvoidDeath(Unit unit) => false;
	/// <summary>兼容既有 Value 查询；新逻辑直接使用实例冻结的强类型参数。</summary>
	public virtual float Parameter(string key, float fallback) => Definition.Parameter(key, fallback);
	protected virtual void Remove() { }
	public void Dispose()
	{
		if (IsDisposed) return; IsDisposed = true; Remove();
		foreach (var node in _nodes) if (GodotObject.IsInstanceValid(node)) { node.ProcessMode = Node.ProcessModeEnum.Disabled; if (node is Node3D visual) visual.Visible = false; node.QueueFree(); }
		_nodes.Clear();
		Definition?.Dispose();
	}
}

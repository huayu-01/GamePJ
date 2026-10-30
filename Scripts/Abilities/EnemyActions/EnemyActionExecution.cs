using System;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Abilities;

/// <summary>动作只获得所属单位、当前目标与朝向服务；不持有计划分支或全局状态。</summary>
public sealed class EnemyActionContext
{
	public Unit Owner { get; }
	public Func<Unit?> Target { get; }
	public Action<float> FaceTarget { get; }
	public EnemyActionState? Step { get; }
	public string AbilityId { get; } = string.Empty;
	private readonly Func<Vector3>? _readHeading;
	private readonly Action<Vector3>? _writeHeading;
	private Vector3 _heading = Vector3.Forward;
	public Vector3 Heading { get => _readHeading?.Invoke() ?? _heading; set { _heading = value; _writeHeading?.Invoke(value); } }
	public EnemyActionContext(Unit owner, Func<Unit?> target, Action<float> faceTarget) { Owner = owner; Target = target; FaceTarget = faceTarget; }
	public EnemyActionContext(Unit owner, Func<Unit?> target, Action<float> faceTarget, EnemyActionState step, string abilityId, Func<Vector3> heading, Action<Vector3> setHeading)
		: this(owner, target, faceTarget) { Step = step; AbilityId = abilityId; _readHeading = heading; _writeHeading = setHeading; }
}

/// <summary>每次进入动作创建独立实例；统一由计划协调器在结束、中断和退树时释放。</summary>
public abstract class EnemyActionExecution : IDisposable
{
	protected EnemyActionContext Context { get; }
	public bool IsDisposed { get; private set; }
	public virtual bool IsComplete => false;
	/// <summary>带内部阶段的动作自行控制时长，例如俯冲落地后重启伤害窗口。</summary>
	public virtual bool ControlsDuration => false;
	public virtual bool SuppressWarning => false;
	public virtual Vector3? WarningVelocity => null;
	protected EnemyActionExecution(EnemyActionContext context) => Context = context;
	public virtual void Begin() { }
	public virtual void Tick(float delta) { }
	protected virtual void End() { }
	public void Dispose() { if (IsDisposed) return; IsDisposed = true; End(); }
}

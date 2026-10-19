using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Rewards;

/// <summary>快乐小子：奔跑接触轻推普通敌人；脱离受伤后奔跑回血，不赋予无敌或穿墙。</summary>
public partial class HappyRunnerRule : Node
{
	public float DamageFreeSeconds { get; set; } = 10f;
	public float HealPerSecond { get; set; } = 1f;
	public float PushSpeed { get; set; } = 5f;
	public float PushCooldown { get; set; } = 0.5f;
	private Unit _owner = null!;
	private SprintController? _sprint;
	private float _safeSeconds;
	private readonly Dictionary<ulong, float> _cooldowns = new();
	public int PushCount { get; private set; }
	public override void _Ready()
	{
		_owner = GetParent<Unit>(); _sprint = _owner.GetNodeOrNull<SprintController>("Sprint");
		_owner.Damaged += OnDamaged;
	}
	private void OnDamaged(DamageResult result, HitInstance hit)
	{
		if (result.ArmorLost + result.ShieldLost + result.HealthLost > 0f) _safeSeconds = 0f;
	}
	public override void _PhysicsProcess(double delta)
	{
		if (_owner.IsDead) return;
		float dt = (float)delta;
		float previousSafe = _safeSeconds; _safeSeconds += dt;
		foreach (ulong id in _cooldowns.Keys.ToArray()) { _cooldowns[id] -= dt; if (_cooldowns[id] <= 0f) _cooldowns.Remove(id); }
		if (_sprint?.IsSprinting != true) return;
		// 跨过10秒门槛的那一帧只治疗超出门槛的部分。
		_owner.Heal(HealPerSecond * Mathf.Clamp(_safeSeconds - Mathf.Max(previousSafe, DamageFreeSeconds), 0f, dt));
		for (int i = 0; i < _owner.GetSlideCollisionCount(); i++)
		{
			if (_owner.GetSlideCollision(i).GetCollider() is not Unit target || !target.IsValidTarget
				|| !_owner.IsHostileTo(target) || target.HasTag("Boss") || _cooldowns.ContainsKey(target.GetInstanceId())) continue;
			Vector3 direction = target.GlobalPosition - _owner.GlobalPosition; direction.Y = 0f;
			if (direction.LengthSquared() < 0.0001f) continue;
			if (!target.ReceiveKnockback(direction.Normalized() * PushSpeed)) continue;
			_cooldowns[target.GetInstanceId()] = Mathf.Max(0.05f, PushCooldown); PushCount++;
		}
	}
	public override void _ExitTree() { if (IsInstanceValid(_owner)) _owner.Damaged -= OnDamaged; }
}

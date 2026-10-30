using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Rewards;

public sealed class DeathDuelBehavior : SpecializationBehavior
{
	private readonly float _duration, _cooldown, _healFraction;
	private readonly int _requiredKills;
	private float _remaining;
	public bool IsActive => !IsDisposed && _remaining > 0;
	public int Kills { get; private set; }
	public float Cooldown { get; private set; }
	// 先判死斗超时，再结算嗜血恢复，避免同帧恢复绕过死亡。
	public override int Priority => -100;
	public override Color? StatusColor => IsActive ? new Color(.85f, .4f, 1f, .8f) : null;
	public DeathDuelBehavior(Unit owner, float duration, float cooldown, int requiredKills, float healFraction) : base(owner)
	{ _duration = duration; _cooldown = cooldown; _requiredKills = requiredKills; _healFraction = healFraction; }
	public override float Parameter(string key, float fallback) => key switch { "Duration" => _duration, "Cooldown" => _cooldown, "RequiredKills" => _requiredKills, "HealFraction" => _healFraction, _ => fallback };
	public override bool TryAvoidDeath(Unit unit)
	{
		if (IsDisposed || unit != Owner) return false;
		if (IsActive) return true;
		if (Cooldown > 0) return false;
		_remaining = _duration; Cooldown = _cooldown; Kills = 0; return IsActive;
	}
	public override void OnKill()
	{
		if (IsActive && ++Kills >= _requiredKills) { _remaining = 0; Owner.RestoreHealthForDeathAvoid(Owner.MaxHealth * _healFraction); }
	}
	public override void Tick(float delta)
	{
		if (IsDisposed || Owner.IsDead) return;
		Cooldown = Mathf.Max(0, Cooldown - delta);
		if (!IsActive) return;
		_remaining -= delta; if (_remaining <= 0) Owner.ForceKill();
	}
	protected override void Remove() { _remaining = 0; Cooldown = 0; }
}

using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Abilities;

public partial class ArrowVolleyAbility
{
	private float _heldFireInterval, _heldFireDamageScale = 1, _heldFireTimer;
	private int _heldFireCount;
	public float HeldFireInterval => _heldFireInterval;
	public void ConfigureHeldFire(HeldFireTuning tuning)
	{
		_heldFireInterval = tuning.IntervalSeconds; _heldFireDamageScale = tuning.DamageScale;
		_heldFireCount = tuning.ProjectileCount; _volleyCountOverride = tuning.ProjectileCount;
		HoldToCharge = false; HoldToMaintain = true;
	}
	private string PlayVolleyAnimation() => _heldFireInterval > 0
		? PlaySkillAnimationNamed(AnimName, Mathf.Max(.01f, ChargeReleaseAnimSpeedScale)) : PlaySkillAnimation();
	private void TickHeldFire(float delta)
	{
		_heldFireTimer -= delta;
		if (!_fired)
		{
			if (StateTime > AnimationEventTimeout / Mathf.Max(.01f, ChargeReleaseAnimSpeedScale)) Cancel();
			return;
		}
		if (_heldFireTimer > .000001f) return;
		// 每轮重播真实动作，仍只由该轮ATK发射；卡顿不补射一串积压飞刀。
		_heldFireTimer = _heldFireInterval;
		_fired = false; StateTime = 0;
		if (PlayVolleyAnimation().Length == 0) Cancel();
	}
	protected override void OnMaintainReleased()
	{
		if (_heldFireInterval > 0) Finish();
	}
}

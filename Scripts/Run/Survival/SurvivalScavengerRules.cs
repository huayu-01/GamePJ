using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Run;

public partial class ScavengerCompassRule : SurvivalAugmentRule
{
	private float _flashCooldown;
	public float RadiusScale { get; }
	public float SpeedScale { get; }
	public ScavengerCompassRule(SurvivalDirector d, Unit p, float radius, float speed) : base(d,p) { RadiusScale = radius; SpeedScale = speed; }
	protected override void Start() => Director.ExperienceCollected += Collected;
	protected override void Removed() => Director.ExperienceCollected -= Collected;
	private void Collected(SurvivalExperienceReceipt receipt)
	{
		if (!Stopped && receipt.Source == SurvivalExperienceSource.Enemy && _flashCooldown<=0) { _flashCooldown=.12f; SurvivalAugmentFx.Ring(this, Player.GlobalPosition + Vector3.Up * .08f, .8f, new Color(.2f, 1, .8f, .55f), .25f); }
	}
	protected override void Tick(float delta) => _flashCooldown=Mathf.Max(0,_flashCooldown-delta);
}

public partial class EnergyEchoRule : SurvivalAugmentRule
{
	private readonly float _threshold, _radius, _scale;
	public float Charge { get; private set; }
	public int TriggerCount { get; private set; }
	private SurvivalAugmentFx? _ready;
	public EnergyEchoRule(SurvivalDirector d, Unit p, float threshold, float radius, float scale) : base(d,p) { _threshold = threshold; _radius = radius; _scale = scale; }
	protected override void Start() => Director.ExperienceCollected += Collected;
	protected override void Removed() { Director.ExperienceCollected -= Collected; if (IsInstanceValid(_ready)) _ready!.QueueFree(); Charge = 0; }
	private void Collected(SurvivalExperienceReceipt receipt)
	{
		if (Stopped || receipt.Source != SurvivalExperienceSource.Enemy) return;
		Charge = Mathf.Min(_threshold, Charge + receipt.RawValue);
		if (Charge >= _threshold && !IsInstanceValid(_ready))
		{
			_ready = SurvivalAugmentFx.Ring(this, Player.GlobalPosition, .24f, new Color(.15f, 1, .8f, .8f), follow: Player); _ready.FollowOffset = new Vector3(.55f, .8f, 0);
		}
	}
	public override void PrimaryHit(GameplayEvent fact)
	{
		if (Charge < _threshold || !Once(fact.Attack!)) return;
		Charge = 0; TriggerCount++; if (IsInstanceValid(_ready)) _ready!.QueueFree();
		SurvivalAugmentDamage.Pulse(this, Director, fact, _radius, _scale, "SurvivalEnergyEcho", new Color(.1f, 1, .8f, .6f));
	}
}

public partial class ScavengerWardRule : SurvivalAugmentRule
{
	private readonly float _threshold, _ratio, _cap, _duration;
	private float _charge;
	private SurvivalWardVisual? _visual;
	public ScavengerWardRule(SurvivalDirector d, Unit p, float threshold, float ratio, float cap, float duration) : base(d,p) { _threshold = threshold; _ratio = ratio; _cap = cap; _duration = duration; }
	protected override void Start() => Director.ExperienceCollected += Collected;
	protected override void Removed() { Director.ExperienceCollected -= Collected; if (IsInstanceValid(_visual)) _visual!.QueueFree(); }
	private void Collected(SurvivalExperienceReceipt receipt)
	{
		if (Stopped || receipt.Source != SurvivalExperienceSource.Enemy) return;
		_charge += receipt.RawValue;
		if (_charge < _threshold) return;
		int count = Mathf.FloorToInt(_charge / _threshold); _charge %= _threshold;
		Player.GrantTemporaryShield(this, Player.MaxHealth * _ratio * count, Player.MaxHealth * _cap, _duration);
		if (IsInstanceValid(_visual)) _visual!.QueueFree();
		_visual = new SurvivalWardVisual { Player=Player, ShieldSource=this }; AddChild(_visual); _visual.GlobalPosition=Player.GlobalPosition+Vector3.Up*.9f;
	}
	protected override void Tick(float delta) { if (Player.TemporaryShieldFrom(this) <= 0 && IsInstanceValid(_visual)) _visual!.QueueFree(); }
}

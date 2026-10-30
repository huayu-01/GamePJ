using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Run;

public partial class AnchorCoreRule : SurvivalAugmentRule
{
	private readonly float _wait, _baseRadius, _bonus, _expiry;
	private Vector3 _origin, _position;
	private float _still, _outside, _linger;
	private SurvivalAugmentFx? _circle;
	public bool Established { get; private set; }
	public bool Inside { get; private set; }
	public float LingerRemaining => _linger;
	public float Radius => _baseRadius + (Runtime.Find<AnchorAfterglowRule>()?.RadiusBonus ?? 0);
	public AnchorCoreRule(SurvivalDirector d, Unit p, float wait, float radius, float bonus, float expiry) : base(d,p) { _wait = wait; _baseRadius = radius; _bonus = bonus; _expiry = expiry; }
	protected override void Start() => _origin = Player.GlobalPosition;
	protected override void Tick(float delta)
	{
		bool grounded = Player.Movement?.IsGrounded ?? Player.IsOnFloor();
		if (!Established)
		{
			if (!grounded || Player.Reactions?.IsStaggered == true || Player.Velocity.LengthSquared() > .04f || Player.GlobalPosition.DistanceSquaredTo(_origin) > .64f) { _still = 0; _origin = Player.GlobalPosition; }
			else _still += delta;
			if (_still >= _wait)
			{
				Established = true; _position = Player.GlobalPosition; _outside = 0;
				_circle = SurvivalAugmentFx.Ring(this, _position + Vector3.Up * .07f, _baseRadius, new Color(1, .7f, .25f, .5f));
			}
		}
		Inside = Established && grounded && new Vector2(Player.GlobalPosition.X-_position.X, Player.GlobalPosition.Z-_position.Z).LengthSquared() <= Radius*Radius;
		if (Inside) { _outside = 0; _linger = Runtime.Find<AnchorAfterglowRule>()?.LingerSeconds ?? 0; }
		else { _outside += delta; _linger = Mathf.Max(0, _linger-delta); }
		if (IsInstanceValid(_circle)) _circle!.Scale = new Vector3(Radius/_baseRadius, .12f, Radius/_baseRadius);
		if (Established && _outside >= _expiry) { Established = false; Inside = false; _still = 0; _origin = Player.GlobalPosition; if (IsInstanceValid(_circle)) _circle!.QueueFree(); }
	}
	protected override float DirectScale(Unit target) => Inside || (_linger > 0 && Runtime.Find<AnchorAfterglowRule>() != null) ? 1+_bonus : 1;
	protected override void Removed() { if (IsInstanceValid(_circle)) _circle!.QueueFree(); Inside = Established = false; _linger = 0; }
}

public partial class AnchorPulseRule : SurvivalAugmentRule
{
	private readonly float _interval, _radius, _scale, _push;
	private float _remaining;
	public int TriggerCount { get; private set; }
	public AnchorPulseRule(SurvivalDirector d, Unit p, float interval, float radius, float scale, float push) : base(d,p) { _interval=interval; _radius=radius; _scale=scale; _push=push; _remaining=interval; }
	protected override void Tick(float delta) => _remaining = Mathf.Max(0, _remaining-delta);
	public override void PrimaryHit(GameplayEvent fact)
	{
		if (_remaining > 0 || Runtime.Find<AnchorCoreRule>()?.Inside != true || !Once(fact.Attack!)) return;
		_remaining = _interval; TriggerCount++;
		SurvivalAugmentDamage.Pulse(this, Director, fact, _radius, _scale, "SurvivalAnchorPulse", new Color(1, .7f, .2f, .55f), _push);
	}
}

public partial class AnchorAfterglowRule : SurvivalAugmentRule
{
	private bool _wasInside;
	private SurvivalAugmentFx? _afterglow;
	public float RadiusBonus { get; }
	public float LingerSeconds { get; }
	public AnchorAfterglowRule(SurvivalDirector d, Unit p, float radius, float linger) : base(d,p) { RadiusBonus=radius; LingerSeconds=linger; }
	protected override void Tick(float delta)
	{
		var anchor=Runtime.Find<AnchorCoreRule>();
		if (anchor?.Inside==true) { if(IsInstanceValid(_afterglow)) _afterglow!.QueueFree(); _wasInside=true; }
		else if (_wasInside) { _wasInside=false; if(anchor?.LingerRemaining>0) _afterglow=SurvivalAugmentFx.Ring(this,Player.GlobalPosition,.9f,new Color(1,.65f,.2f,.5f),LingerSeconds,Player); }
	}
	protected override void Removed() { if(IsInstanceValid(_afterglow)) _afterglow!.QueueFree(); }
}

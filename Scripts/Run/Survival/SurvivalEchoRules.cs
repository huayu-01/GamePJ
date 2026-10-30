using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Run;

public partial class EchoFamiliarRule : SurvivalAugmentRule
{
	private readonly float _interval, _scale, _speed;
	private float _cooldown, _delay;
	private int _pending;
	private GameplayEvent _fact;
	private readonly List<MeshInstance3D> _orbs = new();
	public int ShotCount { get; private set; }
	public EchoFamiliarRule(SurvivalDirector d, Unit p, float interval, float scale, float speed) : base(d,p) { _interval=interval; _scale=scale; _speed=speed; }
	protected override void Start()
	{
		for (int i=0; i<2; i++) { var orb = new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(.16f,.36f,.16f) }, MaterialOverride = SurvivalAugmentFx.Material(new Color(.6f,.75f,1,.65f)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off }; AddChild(orb); _orbs.Add(orb); }
	}
	protected override void Tick(float delta)
	{
		_cooldown = Mathf.Max(0, _cooldown-delta);
		for (int i=0; i<_orbs.Count; i++)
		{
			Vector3 resting = Player.GlobalPosition + new Vector3(i==0 ? -.7f : .7f, 1.65f, .15f);
			_orbs[i].GlobalPosition = Runtime.Find<EchoLanceRule>() != null && _pending > 0
				? _orbs[i].GlobalPosition.MoveToward(Player.GlobalPosition+Vector3.Up*.9f,delta*12) : resting;
		}
		if (_pending <= 0) return;
		if (!IsInstanceValid(_fact.Target) || _fact.Target!.IsDead || _fact.Target.Health <= 0) { _pending=0; return; }
		_delay -= delta; if (_delay > 0) return;
		var lance = Runtime.Find<EchoLanceRule>();
		var shot = new SurvivalEchoShot { Player=Player, Target=_fact.Target!, Damage=_fact.Attack!.BaseDamage * (lance?.DamageScale ?? _scale), Speed=_speed, ExtraPierce=lance?.ExtraPierce ?? 0, Context=_fact.Context, Runtime=Runtime };
		AddChild(shot); shot.GlobalPosition = lance == null ? _orbs[_pending%2].GlobalPosition : Player.GlobalPosition+Vector3.Up*.9f;
		_pending--; _delay=.08f; ShotCount++;
	}
	public override void PrimaryHit(GameplayEvent fact)
	{
		if (_cooldown > 0 || _pending > 0 || !Once(fact.Attack!)) return;
		_fact=fact; var lance=Runtime.Find<EchoLanceRule>(); _cooldown=lance?.Interval ?? _interval; _pending=lance==null ? 2 : 1; _delay=lance==null ? 0 : .12f;
	}
	protected override void Removed() { _pending=0; foreach(var node in GetChildren()) if(node is Node child) child.QueueFree(); _orbs.Clear(); }
}

public partial class EchoMarkRule : SurvivalAugmentRule
{
	private readonly float _duration, _bonus;
	private readonly Dictionary<Unit,(float Remaining, SurvivalAugmentFx Fx)> _marks = new();
	public int MarkCount => _marks.Count;
	public EchoMarkRule(SurvivalDirector d, Unit p, float duration, float bonus) : base(d,p) { _duration=duration; _bonus=bonus; }
	public void Mark(Unit target)
	{
		if (Stopped || target.IsDead) return;
		if (_marks.Remove(target, out var previous) && IsInstanceValid(previous.Fx)) previous.Fx.QueueFree();
		var visual=SurvivalAugmentFx.Ring(this, target.GlobalPosition, .42f, new Color(.6f,.75f,1,.7f), follow:target); visual.RotationDegrees=new Vector3(90,0,0); visual.FollowOffset=Vector3.Up*1.4f;
		_marks[target]=(_duration,visual);
	}
	protected override void Tick(float delta)
	{
		foreach(var pair in _marks.ToArray())
		{
			float remaining=pair.Value.Remaining-delta;
			if(!IsInstanceValid(pair.Key) || pair.Key.IsDead || remaining<=0) { if(IsInstanceValid(pair.Value.Fx)) pair.Value.Fx.QueueFree(); _marks.Remove(pair.Key); }
			else _marks[pair.Key]=(remaining,pair.Value.Fx);
		}
	}
	protected override float DirectScale(Unit target) => _marks.ContainsKey(target) ? 1+_bonus : 1;
	protected override void Removed() { foreach(var mark in _marks.Values) if(IsInstanceValid(mark.Fx)) mark.Fx.QueueFree(); _marks.Clear(); }
}

public partial class EchoLanceRule : SurvivalAugmentRule
{
	public float DamageScale { get; }
	public float Interval { get; }
	public int ExtraPierce { get; }
	public EchoLanceRule(SurvivalDirector d, Unit p, float scale, float interval, int pierce) : base(d,p) { DamageScale=scale; Interval=interval; ExtraPierce=pierce; }
}

/// <summary>协击使用真实场景射线碰撞；先跟踪选中目标，命中后穿透段沿既定方向前进。</summary>
public partial class SurvivalEchoShot : Node3D
{
	public Unit Player { get; init; } = null!;
	public Unit Target { get; init; } = null!;
	public SurvivalBuildRuntime Runtime { get; init; } = null!;
	public EventContext? Context { get; init; }
	public float Damage { get; init; }
	public float Speed { get; init; }
	public int ExtraPierce { get; init; }
	private Vector3 _direction;
	private float _age, _travel;
	private bool _struck;
	private readonly Godot.Collections.Array<Rid> _excluded = new();
	public override void _Ready()
	{
		_excluded.Add(Player.GetRid());
		AddChild(new MeshInstance3D { Mesh=new PrismMesh { Size=new Vector3(.09f,.09f,ExtraPierce>0 ? 1.6f : .8f) }, MaterialOverride=SurvivalAugmentFx.Material(new Color(.6f,.8f,1,.8f)), CastShadow=GeometryInstance3D.ShadowCastingSetting.Off });
	}
	public override void _PhysicsProcess(double delta)
	{
		if (Runtime.Stopped || Player.IsDead || (! _struck && (!IsInstanceValid(Target) || Target.IsDead || Target.Health<=0))) { QueueFree(); return; }
		_age+=(float)delta; if (_age>2 || _travel>45) { QueueFree(); return; }
		if (!_struck) _direction=(Projectile.GetTargetCenter(Target)-GlobalPosition).Normalized();
		if (_direction.LengthSquared()<.001f) { QueueFree(); return; }
		Vector3 end=GlobalPosition+_direction*Speed*(float)delta;
		for(int i=0; i<4; i++)
		{
			var query=PhysicsRayQueryParameters3D.Create(GlobalPosition,end,CombatLayers.World|CombatLayers.HostileTo(Player.Faction),_excluded);
			var hit=GetWorld3D().DirectSpaceState.IntersectRay(query); if(hit.Count==0) break;
			if(hit["collider"].AsGodotObject() is not Unit target || !Player.IsHostileTo(target)) { QueueFree(); return; }
			_excluded.Add(target.GetRid());
			var result=SurvivalAugmentDamage.Hit(Player,target,Damage,GlobalPosition,"SurvivalEchoFamiliar",Context);
			if(result.ArmorLost+result.ShieldLost+result.HealthLost>0) Runtime.Find<EchoMarkRule>()?.Mark(target);
			_struck=true;
			if(_excluded.Count-1>=ExtraPierce+1) { QueueFree(); return; }
		}
		_travel+=GlobalPosition.DistanceTo(end); GlobalPosition=end;
		LookAt(end+_direction,Mathf.Abs(_direction.Dot(Vector3.Up))>.99f ? Vector3.Right : Vector3.Up);
	}
}

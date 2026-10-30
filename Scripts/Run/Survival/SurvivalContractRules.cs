using System.Linq;
using ActRoguelike.Combat;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Run;

public partial class RiskContractRule : SurvivalAugmentRule
{
	public float ExperienceScale { get; }
	private readonly float _healthDamageScale;
	private SurvivalWardVisual? _rim;
	public RiskContractRule(SurvivalDirector d, Unit p, float experience, float healthDamage) : base(d,p) { ExperienceScale=experience; _healthDamageScale=healthDamage; }
	protected override void Start()
	{
		Player.Stats.AddModifier(new StatModifier { Stat="IncomingHealthDamageMultiplier", Operation=ModifierOperation.Multiply, Value=_healthDamageScale, Source=this });
		_rim=new SurvivalWardVisual { Player=Player,ShieldSource=this,RequiresShield=false,RimColor=new Color(1,.45f,.18f,.3f),Strength=.3f }; AddChild(_rim);
	}
	protected override void Removed() { if(IsInstanceValid(_rim)) _rim!.QueueFree(); }
}

public partial class TimedHuntRule : SurvivalAugmentRule
{
	private readonly float _interval, _duration, _speed;
	private readonly int _reward;
	private float _next;
	public float Remaining { get; private set; }
	public Unit? Target { get; private set; }
	public int CompletedCount { get; private set; }
	private SurvivalAugmentFx? _mark;
	private Label3D? _clock;
	public TimedHuntRule(SurvivalDirector d, Unit p, float interval, float duration, float speed, int reward) : base(d,p) { _interval=interval; _duration=duration; _speed=speed; _reward=reward; _next=interval; }
	protected override void Start() => Director.EnemyDefeated+=Defeated;
	protected override void Tick(float delta)
	{
		if(Director.State!=SurvivalDirector.PhaseState.Surviving) { Clear(); return; }
		_next=Mathf.Max(0,_next-delta);
		if(Target!=null)
		{
			Remaining-=delta;
			if(!IsInstanceValid(Target) || Target.IsDead || Target.IsQueuedForDeletion() || !Director.OwnsEnemy(Target) || Remaining<=0) { Clear(); return; }
			_clock!.Text=$"猎令 {Mathf.CeilToInt(Remaining)}s"; _clock.GlobalPosition=Target.GlobalPosition+Vector3.Up*2.7f; return;
		}
		if(_next>0) return;
		var eligible=Director.LivingEnemies.Where(u=>!u.HasTag("Boss") && !u.HasMeta("EliteSummoned") && u.Armor<=0 && !u.GetChildren().OfType<EliteModifier>().Any()
			&& MapManager.Instance?.TryPath(Player.GlobalPosition,u.GlobalPosition,out _,Player)==true).ToArray();
		if(eligible.Length==0) { _next=1; return; }
		Target=eligible[GD.RandRange(0,eligible.Length-1)]; Remaining=_duration; _next=_interval;
		Target.Stats.AddModifier(new StatModifier { Stat="MoveSpeedMultiplier", Operation=ModifierOperation.Multiply, Value=_speed, Source=this }); Target.RefreshStats();
		_mark=SurvivalAugmentFx.Ring(this,Target.GlobalPosition,.65f,new Color(1,.45f,.15f,.7f),follow:Target); _mark.RotationDegrees=new Vector3(90,0,45); _mark.FollowOffset=Vector3.Up*2.1f;
		_clock=new Label3D { Font=ActRoguelike.Core.RuntimeResources.Load<Font>("res://Assets/UI/Themes/ui_font.tres"), FontSize=40, PixelSize=.008f, Billboard=BaseMaterial3D.BillboardModeEnum.Enabled }; AddChild(_clock); _clock.GlobalPosition=Target.GlobalPosition+Vector3.Up*2.7f;
	}
	private void Defeated(Unit unit,int pressure)
	{
		if(Stopped || unit!=Target || Remaining<=0 || Director.State!=SurvivalDirector.PhaseState.Surviving) return;
		Clear(); CompletedCount++; Director.GrantExperience(_reward,SurvivalExperienceSource.Objective);
		SurvivalAugmentFx.Ring(this,Player.GlobalPosition+Vector3.Up*.1f,1.1f,new Color(1,.8f,.3f,.7f),.4f);
	}
	private void Clear()
	{
		if(IsInstanceValid(Target)) { Target!.Stats.RemoveBySource(this); Target.RefreshStats(); }
		Target=null; Remaining=0; if(IsInstanceValid(_mark)) _mark!.QueueFree(); if(IsInstanceValid(_clock)) _clock!.QueueFree(); _clock=null;
	}
	protected override void Removed() { Director.EnemyDefeated-=Defeated; Clear(); }
}

public partial class ContractPayoffRule : SurvivalAugmentRule
{
	private readonly float _perCompletion;
	private readonly int _cap;
	public ContractPayoffRule(SurvivalDirector d, Unit p, float bonus, int cap) : base(d,p) { _perCompletion=bonus; _cap=cap; }
	public int Completed => Mathf.Min(_cap,Runtime.Find<TimedHuntRule>()?.CompletedCount??0);
	protected override float DirectScale(Unit target) => 1+Completed*_perCompletion;
}

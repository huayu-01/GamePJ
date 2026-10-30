using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>死战专属退用与046回归；旧机制用例保留追溯，正式入口运行退用验证。</summary>
public partial class SurvivalAugmentsProbe : Node
{
	private int _pass, _fail;
	private readonly List<object> _checks = new();
	private SurvivalDirector _director = null!;
	private RunController _run = null!;
	private Unit Player => _director.Player!;
	private SurvivalBuildRuntime Runtime => _director.BuildRuntime!;
	private void Check(bool ok,string message) { if(ok) _pass++; else _fail++; _checks.Add(new {passed=ok,message}); GD.Print($"[SurvivalBuilds] {(ok?"PASS":"FAIL")} {message}"); }
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds,true),SceneTreeTimer.SignalName.Timeout);
	private async Task Until(Func<bool> predicate,double seconds,string message)
	{
		for(int i=0;i<seconds*50;i++) { if(predicate()) return; await Wait(.02); }
		throw new InvalidOperationException("等待超时："+message);
	}
	private void Grant(string id) => Check(_run.TryAcquireReward("surv_"+id),"正式资格校验并取得 "+id);
	private void Release(string id) => RewardEffectHost.Ensure(Player).ReleaseReward(_run.Build,"surv_"+id);
	private AttackInstance Attack(float damage=10) => new() { Attacker=Player,Ability="ProbePrimary",AbilityKind="ProbePrimary",BaseDamage=damage,IsManualWeaponAttack=true };
	private void Hit(Unit target,AttackInstance? attack=null) { attack??=Attack(); target.ReceiveHit(attack.RollHit(target,Vector3.Forward,Projectile.GetTargetCenter(target))); }
	private void FreezeEnemies()
	{
		int i=0;
		foreach(var unit in _director.LivingEnemies)
		{
			foreach(Node child in unit.GetChildren()) if(child is Ability ability) ability.Enabled=false; else if(child.GetType().Name=="EnemyBrain") child.ProcessMode=ProcessModeEnum.Disabled;
			unit.Movement?.SetPhysicsProcess(false); unit.Velocity=Vector3.Zero;
			unit.ApplyBaseStats(50000,0,0,0); unit.FillVitals(); unit.GlobalPosition=Player.GlobalPosition+new Vector3(0,0,-(3+i++*2)); unit.ResetPhysicsInterpolation();
		}
	}
	private async Task Refill()
	{
		_director.SetPhysicsProcess(true); await Until(()=>_director.LivingEnemies.Length>=3,6,"正常预算补充敌人"); _director.SetPhysicsProcess(false); FreezeEnemies(); await Wait(.1);
	}
	private async Task Shot(string name)
	{
		if(DisplayServer.GetName()=="headless") return;
		await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
		using var image=GetViewport().GetTexture().GetImage(); Check(image.SavePng(ValidationArtifacts.Path($"res://Logs/evidence/survival-builds/{name}.png"))==Error.Ok,"真实渲染 "+name);
	}
	public override async void _Ready()
	{
		ProcessMode=ProcessModeEnum.Always;
		try { await RunRetired(); } catch(Exception error) { Check(false,error.ToString()); }
		GetTree().Paused=false;
		System.IO.File.WriteAllText(ValidationArtifacts.Path("res://Logs/evidence/survival-builds/"+(DisplayServer.GetName()=="headless"?"retired-headless":"retired-render")+".json"),JsonSerializer.Serialize(new {passed=_fail==0,pass=_pass,fail=_fail,checks=_checks},new JsonSerializerOptions {WriteIndented=true}));
		GD.Print($"[SurvivalBuilds] SUMMARY pass={_pass} fail={_fail}"); GameExit.Request(GetTree(),_fail==0?0:1);
	}
	private async Task Run()
	{
#if DEBUG
		MetaProgress.UseInMemoryStoreForSelfTest(); RuneCarry.UseInMemoryStoreForSelfTest();
#endif
		RunSetup.Character=PlayerCharacterKind.Hunter; RunSetup.Weapon=WeaponPresetKind.EnergyPalm; RunSetup.HasSelection=true;
		_director=GetParent().GetNode<SurvivalDirector>("Mode/Director"); _run=GetParent().GetNode<RunController>("Mode/RunController");
		var settings=(SurvivalDefinition)RuntimeResources.Load<SurvivalDefinition>("res://Config/Modes/twilight_survival.tres").Duplicate(true);
		settings.DurationSeconds=1800; settings.BaseLevelExperience=100000; settings.SpawnWarningSeconds=.1f; settings.SpawnInterval=.1f; settings.MilestoneFractions=Array.Empty<float>();
		var chaser=settings.Phases[0].Enemies.First();
		foreach(var phase in settings.Phases) { phase.Enemies=new[]{chaser}; phase.MaxAlive=3; phase.MaxAlivePressure=40; phase.BudgetPerSecond=100; phase.EliteChance=0; }
		_director.Definition=settings;
		await Until(()=>_run.GameplayReady && _director.State==SurvivalDirector.PhaseState.Surviving,10,"正式模式就绪"); Player.IsInvulnerable=true;
		await Refill();
		var rows=RewardCatalog.All.Where(r=>r.RequiredModeIds.Contains(settings.Id)).ToArray();
		Check(rows.Length==12 && rows.All(r=>r.MaxStacks==1 && UiIcons.ForReward(r)!=null),"十二项唯一增幅均已编译，有独立图标");
		foreach(var item in ConfigIndex.Settings.Weapons) Check(rows.All(r=>r.AllowsWeapon(item) && r.AllowsCharacter(item.OwnerCharacter!.Id)),item.Id+"可使用全部十二项专属构筑，仍有各自前置与互斥");
		Check(rows.All(r=>!r.Description.Contains('{') && RewardPresentation.Details(r).Contains("模式限定")),"说明读取实际资源参数，详情包含模式限定");
		var ordinary=new RewardPool(); ordinary.Initialize(Player.GetNode<ActRoguelike.Weapons.Weapon>("WeaponMount/Pistol"),"chr_hunter",string.Empty);
		Check(ordinary.Available(new RunBuild()).All(r=>r.RequiredModeIds.Length==0),"原冒险候选不泄漏生存卡");
		var roots=_run.ModeRewardOptions(null,true); Check(roots.Count==3 && roots.All(r=>r.RequiredModeIds.Length>0 && r.Requires.Length==0),"第一次专属三选一仅来自四个入门件");
		var screen=UiService.Instance!.Screen<RewardScreen>()!; screen.Open(roots,"生存专属 · 选择路线"); await Wait(.35); await Shot("exclusive-choice"); screen.Close(); await Wait(.1);
		Check(!_run.TryAcquireReward("surv_energy_echo"),"缺前置时GM也不能取得关联件");
		Grant("scavenger_compass"); Check(!_run.TryAcquireReward("surv_scavenger_compass"),"唯一卡不能重复领取");
		Check(Mathf.IsEqualApprox(_director.ExperienceField!.PickupRadius,6) && Mathf.IsEqualApprox(_director.ExperienceField.PickupSpeedScale,1.25f),"罗盘真实改变拾取范围与速度");
		Grant("energy_echo"); Grant("scavenger_ward");
		var echo=Runtime.Find<EnergyEchoRule>()!; var ward=Runtime.Find<ScavengerWardRule>()!;
		_director.GrantExperience(19); _director.GrantExperience(14,SurvivalExperienceSource.Objective); Check(echo.Charge==19 && Player.TemporaryShieldFrom(ward)==0,"原始经验19未充满，目标奖励不充能");
		_director.GrantExperience(1); _director.GrantExperience(40);
		Check(echo.Charge==20 && Mathf.IsEqualApprox(Player.TemporaryShieldFrom(ward),Player.MaxHealth*.1f),"合并数值一次跨多门槛，回响储备一次、护盾遵守来源上限");
		Unit victim=_director.LivingEnemies.First(); var derived=new List<float>(); victim.HitReceived+=hit=>{if(hit.Attack?.Ability=="SurvivalEnergyEcho") derived.Add(hit.Damage);};
		var primary=new AttackInstance {Attacker=Player,Ability="ProbePrimary",BaseDamage=10,IsManualWeaponAttack=true,CanCrit=true,CritChance=1,CritMultiplier=3};
		Hit(victim,primary); Check(echo.TriggerCount==1 && derived.Count==1 && Mathf.IsEqualApprox(derived[0],4),"暴击30点仍只按基础10点产生4点回响，衍生不递归");
		_director.GrantExperience(20); Hit(victim,primary); Check(echo.TriggerCount==1 && echo.Charge==20,"同次出手多目标或重复命中不能再次消耗回响");
		Grant("risk_contract"); int before=_director.Experience; _director.GrantExperience(5);
		Check(_director.Experience-before==6 && echo.Charge==20,"契约实际提高经验，不放大原始充能计数");
		Player.ApplyBaseStats(Player.MaxHealth,0,0,Player.MoveSpeed); Player.FillVitals(); Player.IsInvulnerable=false;
		var enemyAttack=new AttackInstance {Attacker=victim,BaseDamage=10}; var hitShield=Player.ReceiveHit(enemyAttack.RollHit(Player,Vector3.Back,Player.GlobalPosition));
		Check(Mathf.IsEqualApprox(hitShield.ShieldLost,10) && hitShield.HealthLost==0,"契约不放大护膜损耗");
		Player.RemoveTemporaryShield(ward); float health=Player.Health; Player.ReceiveHit(enemyAttack.RollHit(Player,Vector3.Back,Player.GlobalPosition));
		Check(Mathf.IsEqualApprox(health-Player.Health,11) && Player.Reactions?.BlockStunCount==0,"穿透后生命伤害×1.1，临时护盾不产生格挡硬直"); Player.IsInvulnerable=true;
		Grant("anchor_core"); Grant("anchor_pulse"); Grant("anchor_afterglow");
		var anchor=Runtime.Find<AnchorCoreRule>()!; var pulse=Runtime.Find<AnchorPulseRule>()!;
		await Until(()=>anchor.Established && anchor.Inside,3,"真实地面停留建立锚圈");
		Check(Mathf.IsEqualApprox(anchor.Radius,6),"关联件真实扩大锚圈");
		var utility=new AttackInstance {Attacker=Player,BaseDamage=10,IsDirectAbilityAttack=true};
		Check(Mathf.IsEqualApprox(utility.RollHit(victim,Vector3.Forward,victim.GlobalPosition).Damage,11.5f),"主动技能本体享受直接攻击增伤");
		int oldPulse=pulse.TriggerCount,oldEcho=echo.TriggerCount; Hit(victim,utility);
		Check(pulse.TriggerCount==oldPulse && echo.TriggerCount==oldEcho,"技能本体不冒充主武技触发回响和脉冲");
		var secondary=new AttackInstance {Attacker=Player,BaseDamage=10,IsDirectAbilityAttack=true,IsSecondary=true};
		Check(Mathf.IsEqualApprox(secondary.RollHit(victim,Vector3.Forward,victim.GlobalPosition).Damage,10),"衍生伤害即使沿用来源标识也不再次套直接增伤");
		await Wait(2.05); var next=Attack(); Hit(victim,next); Check(pulse.TriggerCount==1,"净域脉冲由实际主武技命中释放");
		await Wait(2.05); Hit(victim,next); Check(pulse.TriggerCount==1,"同次出手不因间隔恢复再次触发脉冲");
		Vector3 origin=Player.GlobalPosition; Player.GlobalPosition+=Vector3.Right*7; Player.ResetPhysicsInterpolation(); await Wait(.12);
		Check(!anchor.Inside && Attack().RollHit(victim,Vector3.Forward,victim.GlobalPosition).Damage>10,"离圈后余火短暂保留直接增伤");
		GetTree().Paused=true; await Wait(2.2); Check(anchor.Established,"暂停冻结锚圈过期时间"); GetTree().Paused=false;
		await Wait(2.2); Check(Mathf.IsEqualApprox(Attack().RollHit(victim,Vector3.Forward,victim.GlobalPosition).Damage,10),"余火到期撤销增伤");
		Check(Player.TemporaryShieldFrom(ward)==0,"临时护膜按真实时间到期，不变成永久护盾");
		Release("anchor_core"); Release("anchor_pulse"); Release("anchor_afterglow"); Player.GlobalPosition=origin; Player.ResetPhysicsInterpolation(); await Wait(.1); FreezeEnemies();
		Grant("echo_familiar"); Grant("echo_mark"); Check(!_run.TryAcquireReward("surv_echo_lance"),"协击两个变形分支互斥");
		var familiar=Runtime.Find<EchoFamiliarRule>()!; victim=_director.LivingEnemies.First(); Hit(victim);
		await Until(()=>familiar.ShotCount==2 && Runtime.Find<EchoMarkRule>()!.MarkCount>0,3,"两枚协击真实飞行碰撞并加刻印");
		Check(Mathf.IsEqualApprox(Attack().RollHit(victim,Vector3.Forward,victim.GlobalPosition).Damage,11.5f),"刻印从后续玩家直接命中加伤"); await Shot("echo-and-ward");
		Release("echo_mark"); Release("echo_familiar");
		var lanceRow=ConfigRows.ById<RewardConfig>("surv_echo_lance")!;
		// 新构筑夹具独立于先前互斥选择；实际应用仍由同一个资源编译入口完成。
		var fresh=new RunBuild(); var weapon=Player.GetNode<ActRoguelike.Weapons.Weapon>("WeaponMount/Pistol"); var context=new RewardContext { Player=Player,Weapon=weapon,Build=fresh };
		fresh.Add(RewardCatalog.Find("surv_echo_familiar")!,context); fresh.Add(RewardEffectRegistry.Compile(lanceRow)!,context); await Wait(.1);
		var damage=_director.LivingEnemies.ToDictionary(u=>u,u=>u.Health); Hit(_director.LivingEnemies.First());
		await Until(()=>Runtime.Find<EchoFamiliarRule>()!.ShotCount==1 && damage.Count(p=>p.Key.Health<p.Value)>=3,3,"集束冲针真实穿过三个敌人");
		Check(Runtime.Find<EchoFamiliarRule>()!.ShotCount==1,"集束只发射一枚，不重复原双弹");
		await Wait(2.25); victim=_director.LivingEnemies.First(); victim.GlobalPosition=Player.GlobalPosition+Vector3.Up*4; victim.ResetPhysicsInterpolation(); await Wait(.1); float aerialHealth=victim.Health; Hit(victim);
		await Until(()=>victim.Health<aerialHealth-20,3,"正上方目标的真实冲针命中"); Check(true,"正上方目标使用有效朝向轴，协击仍实际碰撞命中");
		RewardEffectHost.Ensure(Player).ReleaseBuild(fresh); FreezeEnemies();
		Grant("timed_hunt"); Release("timed_hunt");
		var huntRow=(RewardConfig)ConfigRows.ById<RewardConfig>("surv_timed_hunt")!.Duplicate(true); var tuning=(TimedHuntRewardEffect)huntRow.Effects[0]; tuning.Interval=1.1f; tuning.Duration=.4f;
		var huntBuild=new RunBuild(); var huntContext=new RewardContext {Player=Player,Weapon=weapon,Build=huntBuild}; huntBuild.Add(RewardEffectRegistry.Compile(huntRow)!,huntContext);
		Grant("contract_payoff"); var hunt=Runtime.Find<TimedHuntRule>()!;
		await Until(()=>hunt.Target!=null,3,"导航选出真实普通猎令目标"); Unit marked=hunt.Target!;
		await Shot("hunt-marker");
		Check(Mathf.IsEqualApprox(marked.Stats.Get("MoveSpeedMultiplier"),1.2f) && !marked.HasTag("Boss"),"猎令加速来源与普通目标限制生效");
		float remaining=hunt.Remaining; GetTree().Paused=true; await Wait(.3); Check(Mathf.IsEqualApprox(hunt.Remaining,remaining),"暂停冻结猎令倒计时"); GetTree().Paused=false;
		await Wait(.7); Check(hunt.CompletedCount==0 && Mathf.IsEqualApprox(marked.Stats.Get("MoveSpeedMultiplier"),1),"猎令超时无奖励并撤销目标加速");
		for(int i=0;i<6;i++)
		{
			await Until(()=>hunt.Target!=null,3,"发布下一份猎令"); int count=hunt.CompletedCount; float charge=echo.Charge; var target=hunt.Target!; target.ForceKill(Player); target.ForceKill(Player);
			Check(hunt.CompletedCount==count+1 && echo.Charge==charge,"真实击杀仅结算一次合约，合约经验不充能");
			if(i<5) await Refill();
		}
		Check(Runtime.Find<ContractPayoffRule>()!.Completed==5 && Mathf.IsEqualApprox(Attack().RollHit(_director.LivingEnemies.First(),Vector3.Forward,Vector3.Zero).Damage,11.5f),"破晓清算五次封顶，多次合约不会无限增长");
		Check(RewardPresentation.Details(RewardCatalog.Find("surv_contract_payoff")!).Contains("5／5"),"详情通过行为接口显示本局合约进度"); await Shot("contracts");
		object otherSource=new(); Player.GrantTemporaryShield(otherSource,7,7,10);
		RewardEffectHost.Ensure(Player).ReleaseBuild(huntBuild); RewardEffectHost.Ensure(Player).ReleaseBuild(_run.Build);
		Check(Runtime.RuleCount==0 && Mathf.IsEqualApprox(Player.TemporaryShield,7) && Mathf.IsEqualApprox(Player.Stats.Get("IncomingHealthDamageMultiplier"),1),"撤销来源保留其他护盾，撤销额外承伤、订阅和全部规则");
		Check(Mathf.IsEqualApprox(_director.ExperienceField!.PickupRadius,4),"拾取参数恢复模式基础值");
		Player.RemoveTemporaryShield(otherSource);
		int completions=0; Check(_run.OfferModeReward(null,"生存专属 · 资格更新验证",()=>completions++,true),"专属池耗尽时实际回退普通候选");
		await Until(()=>screen.IsOpen && !screen.InputBlocked,3,"奖励保护期结束");
		var stale=screen.Options[0]; int oldStacks=_run.Build.StacksOf(stale.Id);
		_run.Build.DebugGrantWithoutApplying(new RewardDefinition {Id="probe_late_conflict",Name="资格变更夹具",Description="",Excludes=new[]{stale.Id},Apply=_=>{}});
		var button=screen.GetNode<Container>("Root/Panel/VBox/ChoicesScroll/Cards").GetChildren().OfType<Button>().First(b=>b.Visible); button.EmitSignal(Button.SignalName.Pressed);
		Check(_run.Build.StacksOf(stale.Id)==oldStacks && completions==0 && screen.IsOpen && screen.Options.All(r=>r.Id!=stale.Id),"点击时重新检查资格，过期卡不领取并刷新当前选择");
		await Until(()=>!screen.InputBlocked,3,"刷新后保护期结束");
		button=screen.GetNode<Container>("Root/Panel/VBox/ChoicesScroll/Cards").GetChildren().OfType<Button>().First(b=>b.Visible); button.EmitSignal(Button.SignalName.Pressed); button.EmitSignal(Button.SignalName.Pressed);
		Check(completions==1 && !screen.IsOpen,"刷新后的选择只完成一次，不留下奖励队列死锁");
		var finalBuild=new RunBuild(); var finalContext=new RewardContext {Player=Player,Weapon=weapon,Build=finalBuild};
		finalBuild.Add(RewardCatalog.Find("surv_echo_familiar")!,finalContext);
		var cancelledShot=Runtime.Find<EchoFamiliarRule>()!; var dying=_director.LivingEnemies.First(); Hit(dying); dying.ForceKill(Player); await Wait(.15);
		Check(cancelledShot.ShotCount==0,"目标提前死亡会取消尚未发出的协击");
		finalBuild.Add(RewardCatalog.Find("surv_risk_contract")!,finalContext); Player.ForceKill(_director.LivingEnemies.First());
		Check(Runtime.RuleCount==0 && Mathf.IsEqualApprox(Player.Stats.Get("IncomingHealthDamageMultiplier"),1),"真实死亡即时撤销全部效果来源");
		await Until(()=>_run.RunEnded,3,"死亡进入共享结算"); Check(_director.State==SurvivalDirector.PhaseState.Stopped,"死亡结算停止模式和增幅协调器");
		GC.Collect(); GC.WaitForPendingFinalizers(); Check(true,"模式停止后完整GC正常结束");
	}
}

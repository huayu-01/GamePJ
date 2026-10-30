using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;
using Kind = ActRoguelike.Config.EnemyActionStep.ActionKind;

namespace ActRoguelike.Testing;

/// <summary>攻击定义与预警共用冻结快照；验证实例命中去重、归属、中断和旧动作兼容。</summary>
public partial class EnemyActionExecutionProbe : Node3D
{
	private int _pass, _fail;
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[EnemyActions] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Frame(int count = 1) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
	private Unit Actor(Faction faction, Vector3 position)
	{
		var actor = new Unit { Faction = faction, MaxHealth = 1000 };
		actor.AddChild(new CollisionShape3D { Name = "Body", Position = Vector3.Up * .9f, Shape = new CapsuleShape3D { Radius = .45f, Height = 1.8f } });
		actor.AddChild(new CharacterMovement { Name = "Movement" }); AddChild(actor); actor.GlobalPosition = position;
		actor.SetPhysicsProcess(false); actor.Movement!.SetPhysicsProcess(false); return actor;
	}
	private static EnemyActionContext Context(Unit owner, Unit target, EnemyActionState state) => new(owner, () => target, _ => { }, state, "probe_action", () => Vector3.Forward, _ => { });
	public override async void _Ready()
	{
		try { await Run(); } catch (Exception error) { Check(false, error.ToString()); }
		GD.Print($"[EnemyActions] SUMMARY pass={_pass} fail={_fail}"); GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private async Task Run()
	{
		var floor = new StaticBody3D { Position = new Vector3(10, -.5f, 0), CollisionLayer = CombatLayers.World };
		floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(60, 1, 60) } }); AddChild(floor);
		await Frame(3);
		var definition = new MeleeEnemyAction { Damage = 10, Radius = 1.5f, Speed = 0 };
		var source = new EnemyActionStep { Action = definition, WindupSeconds = .5f, ActiveSeconds = .3f };
		var owner = Actor(Faction.Enemy, Vector3.Zero); var victim = Actor(Faction.Player, Vector3.Forward);
		var movement = owner.Movement!;
		var state = EnemyActionState.From(source, owner.GlobalPosition);
		definition.Damage = 90; definition.Spin = true;
		Check(state.Damage == 10 && state.Kind == Kind.Melee && source.Kind == Kind.Spin, "已进入的动作冻结参数；资源改动只影响下一次进入");
		Check(state.Action!.RequiresAttackEvent, "攻击定义继续要求真实动画事件");
		await Frame(3); int hits = 0; victim.Damaged += (_, _) => hits++;
		float health = victim.Health; var execution = state.Action!.Create(Context(owner, victim, state)); execution.Begin();
		for (int i = 0; i < 4; i++) { execution.Tick(.02f); await Frame(); }
		Check(Mathf.IsEqualApprox(health - victim.Health, 10) && hits == 1, "真实命中体使用冻结伤害，同段多次扫掠只命中一次");
		execution.Dispose(); execution.Dispose();
		Check(execution.IsDisposed && !movement.HasVelocityOverride, "结束或重复取消归还本次资源");
		definition.Spin = false; definition.Damage = 10; var second = definition.Create(Context(owner, victim, EnemyActionState.From(source, owner.GlobalPosition))); second.Begin();
		Check(hits == 2, "同一定义的新实例有独立命中名单"); second.Dispose();
		var fresh = Actor(Faction.Player, Vector3.Forward * .8f); float freshHealth = fresh.Health; await Frame(4);
		Check(fresh.Health == freshHealth, "取消后新进入范围的目标不会受到残留命中体伤害"); fresh.QueueFree();
		owner.GlobalPosition = new Vector3(20, 0, 0); victim.GlobalPosition = new Vector3(20, 0, -10); await Frame(3);
		var charge = new ChargeEnemyAction { Speed = 5, TravelDistance = 1, Radius = .5f, Damage = 10 };
		var chargeState = EnemyActionState.From(new EnemyActionStep { Action = charge }, owner.GlobalPosition);
		var dash = charge.Create(Context(owner, victim, chargeState)); dash.Begin(); dash.Tick(.02f);
		Check(movement.HasVelocityOverride && !dash.IsComplete, "冲锋实例取得自己的移动所有权");
		owner.GlobalPosition += Vector3.Forward * 2; dash.Tick(.02f); Check(dash.IsComplete, "冲锋按本次进入位置与固定距离结束"); dash.Dispose();
		Check(!movement.HasVelocityOverride, "冲锋结束释放自己的速度覆盖");
		object other = new(); movement.BeginVelocityOverride(other, Vector3.Right); dash = charge.Create(Context(owner, victim, chargeState)); dash.Begin(); dash.Tick(.02f); dash.Dispose();
		Check(movement.HasVelocityOverride, "攻击实例无法抢占或释放其他来源的速度覆盖"); movement.EndVelocityOverride(other);
		var legacy = EnemyActionState.From(new EnemyActionStep { Kind = Kind.Melee, Damage = 13, Radius = 2, Speed = 3 }, Vector3.Zero);
		Check(legacy.Action == null && legacy.Kind == Kind.Melee && legacy.Damage == 13 && legacy.Radius == 2, "未迁移的旧动作参数仍能生成等价快照");
		var stored = new EnemyActionStep { Action = new MeleeEnemyAction { Damage = 17, Speed = 0 }, Kind = Kind.Spin, Damage = 999, Radius = 99 };
		const string path = "res://Logs/validation/enemy-action-roundtrip.tres";
		Error saved = ResourceSaver.Save(stored, path);
		var restored = ResourceLoader.Load<EnemyActionStep>(path, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
		string text = Godot.FileAccess.GetFileAsString(path);
		Check(saved == Error.Ok && EnemyActionState.From(restored, Vector3.Zero) is { Kind: Kind.Melee, Damage: 17 }
			&& text.Contains("[resource]") && !System.Text.RegularExpressions.Regex.IsMatch(text.Split("[resource]")[^1], @"(?m)^(Kind|Damage|Radius|Speed|Centered) ="), "动作资源保存往返只保留定义参数，不写入动作段的旧字段");
		Check(new MeleeEnemyAction { Damage = float.NaN }.Validate().Any() && new ChargeEnemyAction { TravelDistance = 0 }.Validate().Any(), "攻击参数错误在制作期拒绝");
		foreach (string name in new[] { "blade_spinner", "combo_fighter", "tracking_charger", "spear_guard" })
		{
			var plan = RuntimeResources.Load<EnemyActionPlan>($"res://Config/EnemyPlans/{name}.tres");
			Check(plan.Opening.Concat(plan.Near).Concat(plan.Far).Concat(plan.Closing).Where(step => step.Kind is Kind.Melee or Kind.Spin or Kind.Charge or Kind.HomingCharge).All(step => step.Action is EnemyHitboxAction), $"{name} 的近战／冲锋步骤已接独立执行器");
		}
		var row = ConfigRows.ById<EnemyConfig>("enemy_tracking_charger")!;
		var enemy = RuntimeResources.Load<PackedScene>(row.ScenePath).Instantiate<Unit>(); row.ApplyTo(enemy); AddChild(enemy); enemy.GetNode("Brain").SetPhysicsProcess(false); enemy.GlobalPosition = new Vector3(20, 0, 0);
		await Frame(2); var ability = enemy.GetNode<EnemyPlanAbility>("AbilityPlan"); float before = victim.Health;
		Check(ability.TryActivate(victim), "生产攻击计划通过真实动画存在性闸门"); ability.ResetForLoadout(); await Frame(3);
		Check(!ability.IsBusy && ability.ExecutedAttackCount == 0 && victim.Health == before && !enemy.Movement!.HasVelocityOverride, "事件帧前取消不会开始攻击或遗留速度覆盖");
		// 用独立计划测试范围近战的切换时机，不修改共享生产资源。
		enemy.Movement!.SetPhysicsProcess(false); victim.GlobalPosition = enemy.GlobalPosition + Vector3.Forward;
		ability.Plan = new EnemyActionPlan { Opening = new[] { new EnemyActionStep { Action = new MeleeEnemyAction { Centered = true, Speed = 0, Damage = 10, Radius = 2 }, WindupSeconds = .3f, ActiveSeconds = .3f } } };
		var warning = ability.GetNode<EnemyAttackWarning>("AttackWarning").Indicator;
		bool warningHiddenAtHit = false; victim.Damaged += (_, _) => warningHiddenAtHit = !warning.IsShowing;
		await Frame(2); before = victim.Health;
		Check(ability.TryActivate(victim) && warning.IsShowing, "中心范围近战在真实动画前摇显示预警");
		for (int i = 0; i < 120 && victim.Health == before; i++) await Frame();
		Check(victim.Health < before && warningHiddenAtHit, "真实ATK开始命中的同一帧已撤去近战前摇预警");
		ability.ResetForLoadout(); Check(!warning.IsShowing && !enemy.Movement.HasVelocityOverride, "取消同时清理预警与攻击执行实例");
		await Advanced();
	}
}

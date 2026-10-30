using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>039～044新增规则的真实移动、落地和导航验证；交互键回归在SurvivalModeProbe。</summary>
public partial class Content039044Probe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "Content039044";
	protected override PlayerCharacterKind TestCharacter => PlayerCharacterKind.WeaponMaster;
	protected override WeaponPresetKind TestWeapon => WeaponPresetKind.Spear;
	private async Task<bool> Until(Func<bool> test, double seconds = 3)
	{
		for (int i = 0; i < seconds * 50 && !test(); i++) await Wait(.02);
		return test();
	}
	protected override async Task RunChecks()
	{
		Player.IsInvulnerable = true; Player.SetPhysicsProcess(false);
		Player.MoveSpeed = RunController.CharacterRow(TestCharacter)!.MoveSpeed;
		await Leap(); await HoverMovement(); await Landing(); await Burrow();
		var run = Mode.GetNode<RunController>("RunController");
		Check(ConfigRows.ById<RewardConfig>("upg_insight")!.Weight == 0 && !run.AvailableRewards().Any(r => r.Id == "upg_insight"), "043洞悉保留内容身份但退出正式奖励候选");
	}
	private async Task Leap()
	{
		var run = Mode.GetNode<RunController>("RunController");
		var movement = Player.Movement!; var hover = Player.GetNode<AirHoverController>("AirHover");
		var fall = Player.GetNode<FallAttackAbility>("AbilityFallAttack");
		var other = Player.AbilitySlots.Resolve("character_skill_2") ?? Player.GetNode<Ability>("AbilityBSkill2");
		float fallCooldown = fall.EffectiveCooldown, otherCooldown = other.EffectiveCooldown;
		Check(run.GrantReward("evo_light_leap"), "039从正式进化池获得浮光跃金");
		Check(Mathf.IsEqualApprox(fall.EffectiveCooldown, fallCooldown * .5f) && other.EffectiveCooldown == otherCooldown, "039只将落的冷却减半，其他槽位不受影响");
		movement.SetPhysicsProcess(true); await Wait(.2);
		Check(movement.TryJump() && Mathf.Abs(movement.LastTeleportJumpHeight - movement.StandardJumpHeight * 3) < .05f, "039真实瞬移跳高为基础3倍");
		Check(hover.Maintaining && Mathf.Abs(hover.HoverRemaining - .5f) < .05f, "039授予0.5秒自动浮空");
		await Wait(.65); Check(!hover.IsHovering && !hover.Maintaining, "039浮空自然到期，不残留旧2秒时长");
		Player.GetNode<RewardEffectHost>(RewardEffectHost.NodeName).ReleaseReward(run.Build, "evo_light_leap");
		Check(!movement.TeleportJumpEnabled && Mathf.IsEqualApprox(fall.EffectiveCooldown, fallCooldown), "039撤销后还原跳跃和落的冷却");
		movement.SetPhysicsProcess(false); movement.Stop();
	}
	private async Task HoverMovement()
	{
		var movement = Player.Movement!; var hover = Player.GetNode<AirHoverController>("AirHover");
		var source = new object(); Player.Stats.AddModifier(new StatModifier { Stat = "MoveSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = 4, Source = source });
		Vector3 origin = Player.GlobalPosition; Player.GlobalPosition = origin with { Y = 15 }; Player.Velocity = Vector3.Zero;
		Player.ResetPhysicsInterpolation(); movement.Stop(); hover.Grant(4);
		movement.SetIntent(Vector3.Right, Player.MoveSpeed * 3); movement.SetPhysicsProcess(true); await Wait(.45);
		Check(Mathf.Abs(movement.PlanarVelocity.Length() - Player.MoveSpeed * .5f) < .08f, "040真实空中移速固定为基础一半，奔跑意图及4倍移速增益不放大");
		movement.Stop(); await Wait(.25); Check(movement.PlanarVelocity.Length() < .02f, "040无移动意图时停止，不以固定速度自行漂移");
		movement.SetIntent(Vector3.Right, Player.MoveSpeed * 3); movement.AddImpulse(Vector3.Forward * 6); await Wait(.03);
		Check(movement.PlanarVelocity.Z < -2, "040浮空限速不抵消外力击退");
		var dash = new object(); Check(movement.BeginVelocityOverride(dash, Vector3.Forward * 19), "040技能仍可持有速度覆盖");
		await Wait(.04); Check(Mathf.Abs(movement.PlanarVelocity.Z + 19) < .05f, "040技能突进速度不被浮空减半"); movement.EndVelocityOverride(dash);
		hover.ResetForLoadout(); movement.SetIntent(Vector3.Right, Player.MoveSpeed); await Wait(.65);
		Check(movement.PlanarVelocity.X > Player.MoveSpeed * .7f, $"040取消浮空后恢复原有加成移动：实际{movement.PlanarVelocity.X:0.##}，基础{Player.MoveSpeed:0.##}");
		movement.SetPhysicsProcess(false); movement.Stop(); Player.Stats.RemoveBySource(source);
		Player.GlobalPosition = origin; Player.Velocity = Vector3.Zero; Player.ResetPhysicsInterpolation();
	}
	private async Task Landing()
	{
		var movement = Player.Movement!; var fall = Player.GetNode<FallAttackAbility>("AbilityFallAttack");
		var camera = new Camera3D(); Mode.AddChild(camera); camera.MakeCurrent();
		Vector3 ground = Player.GlobalPosition with { Y = .08f };
		foreach (float distance in new[] { 2f, 20f })
		{
			fall.ResetForLoadout(); fall.RefreshCooldown(); movement.Stop();
			Player.GlobalPosition = ground + Vector3.Up * 8; Player.Velocity = Vector3.Zero; Player.ResetPhysicsInterpolation();
			movement.SetPhysicsProcess(true); await Wait(.02); movement.SetPhysicsProcess(false);
			camera.GlobalPosition = Player.GlobalPosition + new Vector3(0, 3, 6);
			camera.LookAt(ground + Vector3.Right * distance); await Wait(.05);
			float height = Player.GlobalPosition.Y - ground.Y + .08f;
			var preview = fall.GetNodeOrNull<SkillIndicator>("LandingPreview");
			var range = fall.GetNodeOrNull<SkillIndicator>("LandingRange");
			Check(fall.LandingPreviewVisible && preview?.IsShowing == true && range?.IsShowing == true, "039空中准备落时显示落点圈和可选范围圈");
			Check(Mathf.Abs(fall.PreviewAimRadius - height) < .15f && Mathf.Abs(range!.LastRadius - fall.PreviewAimRadius) < .01f, "039选择半径随真实离地高度变化");
			Vector3 destination = fall.PreviewAimPoint;
			float horizontal = new Vector2(destination.X - Player.GlobalPosition.X, destination.Z - Player.GlobalPosition.Z).Length();
			Check(Mathf.Abs(horizontal - Mathf.Min(distance, fall.PreviewAimRadius)) < .15f, "039近点保留、超范围目标限制到圆周");
			await Shot(distance < 3 ? "039-landing-near" : "039-landing-range");
			Check(fall.TryPress() && await Until(() => fall.IsDiving), "039真实动画Action启动下落");
			Check(fall.LastAimPoint.DistanceTo(destination) < .05f && !fall.LandingPreviewVisible, "039Action与预览共用落点，下冲后收起预览");
			Check(Mathf.Abs(fall.EffectiveDiveSpeed - 34 * 1.8f) < .01f && Mathf.Abs(fall.LastDownwardSpeed - fall.EffectiveDiveSpeed * -fall.LastDiveDirection.Y) < .01f, "039总下冲速提高80%，伤害仍使用实际垂直分量");
			movement.SetPhysicsProcess(true);
			bool landed = await Until(() => fall.HasLanded);
			Check(landed && new Vector2(Player.GlobalPosition.X - destination.X, Player.GlobalPosition.Z - destination.Z).Length() < .5f, $"039真实物理落点与预览一致：目标{destination}，实际{Player.GlobalPosition}");
			await Wait(.15); movement.SetPhysicsProcess(false);
			Check(!fall.LandingPreviewVisible, "039落地后不残留范围圈");
		}
		new PlayerLoadout().Apply(Player, this, TestCharacter, WeaponPresetKind.Scythe); await Wait(.05);
		Check(!fall.ShowLandingIndicator && fall.AimRadiusHeightRatio == 0 && fall.DiveSpeedScale == 1 && !fall.LandingPreviewVisible, "039换镰刀还原直落、基础速度与预览配置");
		camera.QueueFree();
	}
	private async Task Burrow()
	{
		var row = ConfigRows.ById<EnemyConfig>("1339")!;
		var shroom = RuntimeResources.Load<PackedScene>(row.ScenePath).Instantiate<Unit>(); row.ApplyTo(shroom); Mode.AddChild(shroom);
		shroom.GetNode("Brain").ProcessMode = ProcessModeEnum.Disabled; shroom.GetNode<EnemyPlanAbility>("AbilityPlan").Enabled = false;
		shroom.Movement!.SetPhysicsProcess(false); shroom.GlobalPosition = Player.GlobalPosition + Vector3.Forward * 6;
		var model = shroom.GetNode<Node3D>("Model"); Vector3 home = model.Position, start = shroom.GlobalPosition;
		var step = EnemyActionState.From(shroom.GetNode<EnemyPlanAbility>("AbilityPlan").Plan.Closing[1], start);
		EnemyActionContext Context(Func<Unit?> target) => new(shroom, target, _ => { }, step, row.Id, () => Vector3.Forward, _ => { });
		using (var execution = new EnemyBurrowExecution(Context(() => Player)))
		{
			execution.Tick(.24f); Check(shroom.GlobalPosition == start && model.Position.Y < home.Y - 1.9f, "041接近完全下沉前不提前换位");
			execution.Tick(.01f); Check(shroom.GlobalPosition.DistanceTo(start) > 1 && model.Position.Y <= home.Y - 2 + .001f, "041完全下沉时在有效导航位置换位");
			var meshes = new List<MeshInstance3D>(); NodeSearch.CollectInSubtree(model, meshes);
			float top = meshes.Where(m => m.Mesh != null).Select(m => { var bounds = m.GlobalTransform * m.GetAabb(); return bounds.End.Y; }).DefaultIfEmpty(float.MaxValue).Max();
			Check(top < shroom.GlobalPosition.Y + .02f, "041真实胆小菇网格在换位帧完全低于地面");
			Vector3 exit = shroom.GlobalPosition; execution.Tick(.25f);
			Check(shroom.GlobalPosition == exit && model.Position.DistanceTo(home) < .001f, "041只在新位置出土，结束时不再次瞬移");
		}
		using (var cancel = new EnemyBurrowExecution(Context(() => Player))) { cancel.Tick(.1f); }
		Check(model.Position == home, "041中断遁地恢复模型本地位置");
		start = shroom.GlobalPosition;
		using (var noTarget = new EnemyBurrowExecution(Context(() => null))) { noTarget.Tick(.25f); noTarget.Tick(.25f); }
		Check(shroom.GlobalPosition == start && model.Position == home, "041目标失效时不传送，正常复位");
		await Shot("041-shroom-emerged"); shroom.QueueFree();
	}
}

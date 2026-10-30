using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Testing;

public partial class Content049054Probe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "Content049054";
	protected override async Task RunChecks()
	{
		Player.IsInvulnerable = true;
		await Hud(); await Mage(); await Assist(); await Scythe();
	}
	private async Task Hud()
	{
		var run = Mode.GetNode<RunController>("RunController");
		for (int i = 0; i < 12; i++)
		{
			var row = run.AvailableRewards().FirstOrDefault(item => !run.Build.Has(item.Id));
			if (row == null) break; run.TryAcquireReward(row.Id);
		}
		await Wait(.12);
		var hud = Mode.GetNode<PlayerHud>("Hud"); var screen = GetViewport().GetVisibleRect().Size;
		Check(hud.GetNode<Control>("Root/PlayerBar/BarBg").GetGlobalRect().GetCenter().Y > screen.Y * .75f, "049实际血条位于左下方");
		Check(hud.GetNode<Control>("Root/BuildIcons").GetGlobalRect().GetCenter().Y < screen.Y * .25f, "049构筑图标位于左上方");
		Check(run.Build.Acquired.Count > 6 && hud.HiddenBuildCount == run.Build.Acquired.Count - 6 && hud.BuildIconCount == 7, "049最多六个图标与一个剩余数量提示，不无限铺满屏幕");
		Check(hud.GetNode<Control>("Root/CarriedRune").GetGlobalRect().End.Y < hud.GetNode<Control>("Root/BuildIcons").GetGlobalRect().Position.Y, "049携带符文与构筑图标互不覆盖");
		await Shot("049-hud-layout");
	}
	private async Task Mage()
	{
		var row = ConfigRows.ById<EnemyConfig>("enemy_meteor_mage")!;
		Check(row.VisualProfile?.ModelScene?.ResourcePath == "res://Assets/Enemies/GhostMage/ghost_mage.glb", "053正式落弹法师引用已定稿的小黑法师档案");
		var mage = RuntimeResources.Load<PackedScene>(row.ScenePath).Instantiate<Unit>(); row.ApplyTo(mage); Mode.AddChild(mage);
		mage.GlobalPosition = Player.GlobalPosition + new Vector3(3, 0, -6); mage.Movement?.SetPhysicsProcess(false); mage.GetNode("Brain").ProcessMode = ProcessModeEnum.Disabled;
		await Wait(.15);
		var model = mage.GetNode<Node3D>("Model"); var skeletons = new System.Collections.Generic.List<Skeleton3D>(); NodeSearch.CollectInSubtree(model, skeletons);
		Check(skeletons.Count > 0 && Mathf.IsEqualApprox(model.Scale.X, .82f), "053真实骨架及0.82外观比例已载入");
		Check(model.GetScript().VariantType == Variant.Type.Nil && !model.FindChildren("*", "EnemyVisualPreview", true, false).Any(), "053未把样板的预览控制脚本带入战斗");
		Check(row.PressureBehaviors.SequenceEqual(new[] { "MeteorCast" }) && mage.GetNode<EnemyPlanAbility>("AbilityPlan").Plan.Far.Length > 0 && !mage.Movement!.CanFly, "053沿用落弹预警与地面导航，不新增重复敌种或飞行逻辑");
		Check(mage.GetNode<UnitArtStyle>("ArtStyle").StyledSurfaceCount >= 4, "052/053小黑法师在正式单位中接入样板配色和细描边");
		await Shot("053-ghost-mage-runtime"); mage.QueueFree();
	}
	private async Task Assist()
	{
		var rig = Player.GetNode<Node3D>("CameraRig"); rig.SetPhysicsProcess(false); rig.SetProcessInput(false);
		// 空中隔离目标区，避免正式场景南门/树木恰好挡住索敌夹具；遮挡另用真实墙单独验证。
		Player.GlobalPosition += Vector3.Up * 15; Player.ResetPhysicsInterpolation();
		var camera = new Camera3D { Current = true }; Mode.AddChild(camera); var origin = Player.GlobalPosition;
		camera.GlobalPosition = origin + new Vector3(0, 3, 8); camera.LookAt(origin + new Vector3(0, .9f, -8)); camera.ResetPhysicsInterpolation();
		var target = MakeEnemy(origin + new Vector3(.5f, 0, -9)); var nearby = MakeEnemy(origin + new Vector3(7, 0, -1)); await Wait(.08);
		var assist = Player.GetNode<AimAssistController>("AimAssist");
		Check(!assist.Enabled, "054默认保留自由瞄准");
		Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = true });
		Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = false }); await Wait(.12);
		Check(assist.Enabled && assist.Selected == target && target.GlobalPosition.DistanceTo(origin) > nearby.GlobalPosition.DistanceTo(origin), $"054真实中键开启，按准星邻近程度选目标而非角色最近敌人：enabled={assist.Enabled}, selected={assist.Selected?.Name}, target={target.Name}, pixel={camera.UnprojectPosition(Targeting.AimPoint(target))}, view={GetViewport().GetVisibleRect()}");
		Vector3 point = AimUtil.GetCrosshairPoint(GetViewport(), Player, 60, CombatLayers.World | CombatLayers.HostileTo(Player.Faction));
		Check(point.DistanceTo(Targeting.AimPoint(target)) < .001f && point.Y > target.GlobalPosition.Y + .5f, "054技能统一瞄准目标身体中心");
		Vector3 direction = AimUtil.GetCrosshairDirection(GetViewport(), Vector3.Forward, Player);
		Check(direction.Dot((point - Targeting.AimPoint(Player)).Normalized()) > .999f, "054朝向技能与投射物瞄准使用相同目标");
		await Shot("054-aim-assist-target");
		var wall = new StaticBody3D { CollisionLayer = CombatLayers.World, Position = origin + new Vector3(0, 1.5f, -4) };
		wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(4, 4, .4f) } }); Mode.AddChild(wall); await Wait(.12); assist.RefreshTarget();
		Check(assist.Selected == null && !assist.TryGetAim(60, out _), "054地形遮挡时不能隔墙辅助选中或射击");
		wall.QueueFree(); await Wait(.08); assist.RefreshTarget(); Check(assist.Selected == target, "054遮挡移除后正常重选");
		target.ApplyDirectDamage(999999, Player); assist.RefreshTarget(); Check(assist.Selected != target, "054死亡目标立即失去选中资格");
		Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = true });
		Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = false }); await Wait(.08);
		Check(!assist.Enabled && assist.Selected == null && !assist.IsPhysicsProcessing(), "054中键关闭后清标记并停止索敌更新");
		nearby.QueueFree();
	}
	private async Task Scythe()
	{
		new PlayerLoadout().Apply(Player, this, PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Scythe); await Wait(.05);
		var scythe = (ScytheThrowAbility)Player.AbilitySlots.Resolve("weapon_skill_2")!;
		float initial = scythe.FlightRange; var restore = scythe.CaptureThunderRecall();
		var config = ConfigRows.ById<RewardConfig>("evo_thunder_recall")!;
		var data = (ThunderRecallBehaviorDefinition)config.SpecializationBehavior!;
		scythe.ConfigureThunderRecall(data.SpeedScale, Player.GetNode<FallAttackAbility>("AbilityFallAttack"), data.LandingDamageScale, data.DistanceScale);
		Check(Mathf.IsEqualApprox(scythe.FlightRange, initial * 1.5f) && data.SpeedScale == 2, "050落雷术增加50%射程，保留已有2倍飞速");
		scythe.ConfigureThunderRecall(data.SpeedScale, Player.GetNode<FallAttackAbility>("AbilityFallAttack"), data.LandingDamageScale, data.DistanceScale);
		Check(Mathf.IsEqualApprox(scythe.FlightRange, initial * 1.5f), "050重复配置不累乘射程"); restore();
		Check(Mathf.IsEqualApprox(scythe.FlightRange, initial), "050撤销来源还原原始射程");
		Check(config.LocalizedDescription.Contains("1.5"), "050说明从同一资源参数显示射程倍率");
	}
}

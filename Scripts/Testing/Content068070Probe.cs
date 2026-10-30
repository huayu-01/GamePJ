using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.Weapons;
using ActRoguelike.World;
using Godot;
using K = ActRoguelike.Config.SpecializationKind;

namespace ActRoguelike.Testing;

public partial class Content068070Probe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "Content068070";
	protected override async Task RunChecks()
	{
		var row = RewardCatalog.Find("evo_wind_domain")!;
		foreach (var weaponRow in ConfigIndex.Settings.Weapons)
		{
			var weapon = new Weapon(); weaponRow.ApplyTo(weapon); using var pool = new RewardPool();
			pool.Initialize(weapon, weaponRow.OwnerCharacter!.Id, "survival_twilight"); var build = new RunBuild();
			Check(!pool.Available(build).Any(r => r.Id == row.Id), "068没有风印时不出现：" + weaponRow.Id);
			build.SetRuneUnlocks(new[] { "rune_wind_seal" });
			Check(pool.Available(build).Any(r => r.Id == row.Id), "068携带风印后六武器均可获取：" + weaponRow.Id); weapon.Free();
		}
		var run = Mode.GetNode<RunController>("RunController"); run.Build.SetRuneUnlocks(new[] { "rune_wind_seal" });
		Check(run.TryAcquireReward(row.Id), "068通过正式可获取入口授予能量武器的浮空领域");
		var runtime = Player.GetNode<SpecializationRuntime>(SpecializationRuntime.NodeName);
		var behavior = runtime.Behavior<WindDomainBehavior>(K.WindDomain)!;
		var hover = NodeSearch.FindInSubtree<AirHoverController>(Player)!;
		await Wait(.08);
		Check(!runtime.GetChildren().OfType<SpecializationField>().Any(f => f.Kind == K.WindDomain && f.Active), "068未获浮空时不出现旋风");
		MapManager.Instance!.TryNavigationAnchor(Player.GlobalPosition, out var initialGround, Player);
		Player.GlobalPosition += Vector3.Up * 2; Player.Velocity = Vector3.Down; Player.ResetPhysicsInterpolation();
		hover.Grant(3, .9f); Player.Movement!.SetPhysicsProcess(true); await Wait(.15);
		var domain = runtime.GetChildren().OfType<SpecializationField>().Single(f => f.Kind == K.WindDomain);
		Check(domain.Active && Mathf.Abs(domain.GlobalPosition.Y - initialGround.Y) < .2f && Player.GlobalPosition.Y - domain.GlobalPosition.Y > 1,
			$"068真实浮空时旋风贴导航地面{initialGround.Y:0.00}，领域Y={domain.GlobalPosition.Y:0.00}、玩家Y={Player.GlobalPosition.Y:0.00}");
		Check(behavior.ExtensionCount == 1 && hover.HoverRemaining > 4.5f, "068接触旋风立即增加2秒浮空");
		await Wait(.35); Check(behavior.ExtensionCount == 1, "068一秒内不逐帧延长浮空");
		await Wait(.85); Check(behavior.ExtensionCount == 2, "068间隔一秒后可再次延长");
		Player.GlobalPosition += Vector3.Up * 9; Player.Velocity = Vector3.Zero; Player.ResetPhysicsInterpolation(); await Wait(1.05);
		Check(behavior.ExtensionCount == 2, "068超过旋风接触高度时不远程延长浮空");
		hover.ResetForLoadout(); await Wait(.08); Check(!domain.Active, "068浮空取消后旋风立即停止伤害与减速");
		Player.Movement.SetPhysicsProcess(false);
		Vector3 spot = Player.GlobalPosition + Vector3.Up * 10;
		var field = runtime.Services.Field(K.WindHeart, spot, 0, () => 3, 10, _ => { });
		var second = runtime.Services.Field(K.WindHeart, spot, 0, () => 3, 10, _ => { }); second.ProjectileSpeedMultiplier = .5f;
		var shot = new Projectile { Speed = 8, GravityScale = 0 }; Mode.AddChild(shot); shot.SetPhysicsProcess(false);
		shot.GlobalPosition = spot + Vector3.Left * 5; shot.Launch(new AttackInstance { Attacker = Player, BaseDamage = 1, Lifetime = 10 }, Vector3.Right);
		Vector3 start = shot.GlobalPosition; shot._PhysicsProcess(.1);
		Check(Mathf.Abs(shot.GlobalPosition.DistanceTo(start) - .8f) < .001f, "068场外投掷物保持原速度");
		shot.GlobalPosition = spot; start = shot.GlobalPosition; shot._PhysicsProcess(.1);
		Check(Mathf.Abs(shot.GlobalPosition.DistanceTo(start) - .2f) < .001f && Mathf.Abs(shot.ActualSpeed - 2) < .001f,
			"068进入旋风后实际位移减速75%，两场重叠取最强值");
		shot.GlobalPosition = spot + Vector3.Right * 5; start = shot.GlobalPosition; shot._PhysicsProcess(.1);
		Check(Mathf.Abs(shot.GlobalPosition.DistanceTo(start) - .8f) < .001f && shot.Speed == 8, "068离开旋风恢复原速度，不永久修改弹速");
		field.QueueFree(); second.QueueFree(); shot.QueueFree(); await Wait(.05);
		Check(ProjectileInfluenceFields.SpeedMultiplier(spot) == 1, "068场退树后不残留减速注册");
		var map = MapManager.Instance!;
		Check(!map.TryValidPoint(new Vector3(float.NaN, 0, 0), Player.GlobalPosition, out _) && !map.TryRingPoint(Vector3.Zero, 3, 2, out _),
			"070严格有效位置接口拒绝无效坐标与倒置半径");
		Check(map.TryNavigationAnchor(Player.GlobalPosition, out var anchor, Player) && map.TryRingPoint(anchor, 3, 8, out var point, Player)
			&& map.TryPath(anchor, point, out var path, Player) && path.Length > 0 && map.HasBodyClearance(point, Player),
			"070现有导航锚点、环形有效点、完整路径与体型净空能串成同一选点链路");
		RewardEffectHost.Ensure(Player).ReleaseReward(run.Build, row.Id); await Wait(.08);
		Check(!IsInstanceValid(domain), "068撤销奖励回收旋风，没有旧镰刀回调残留");
	}
}

using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.Weapons;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>两项通用构筑的正常资格、真实获取、体型撤销及逐杀成长。</summary>
public partial class GrowthBuildsProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "GrowthBuilds";
	protected override async Task RunChecks()
	{
		var giant = ConfigIndex.Settings.Upgrades.Single(row => row.NumericId == 1341);
		var steel = ConfigIndex.Settings.Upgrades.Single(row => row.NumericId == 1342);
		Check(giant.RewardKind == RewardKind.Mutation && steel.RewardKind == RewardKind.Evolution && giant.MaxStacks == 1 && steel.MaxStacks == 1,
			"巨化异化/心之钢进化均唯一，具有正式索引与正权重");
		Check(giant.LocalizedDescription.Contains("20%") && giant.LocalizedDescription.Contains("100") && !giant.LocalizedDescription.Contains('{')
			&& steel.LocalizedDescription.Contains("5") && steel.LocalizedDescription.Contains("20") && !steel.LocalizedDescription.Contains('{'), "两项本地化描述从真实参数生成，没有未解析占位符");
		foreach (var weaponRow in ConfigIndex.Settings.Weapons)
		{
			var weapon = new Weapon(); weaponRow.ApplyTo(weapon);
			foreach (string mode in new[] { "adventure", "twilight_survival" })
			{
				using var pool = new RewardPool(); pool.Initialize(weapon, weaponRow.OwnerCharacter!.Id, mode);
				var offered = pool.Available(new RunBuild());
				Check(offered.Any(r => r.Id == giant.Id) && offered.Any(r => r.Id == steel.Id), $"通用两构筑正常进入{weaponRow.Id}/{mode}候选，不需符文前置");
			}
			weapon.Free();
		}
		var run = Mode.GetNode<RunController>("RunController"); var host = RewardEffectHost.Ensure(Player);
		Vector3 scale = Player.Scale, cameraScale = Player.GetNode<Node3D>("CameraRig").GlobalBasis.Scale;
		float health = Player.MaxHealth, radius = MapManager.BodySize(Player).Radius;
		Check(run.TryAcquireReward(giant.Id), "巨化通过正式可获取列表真实授予");
		Check(Player.Scale.IsEqualApprox(scale * 1.2f) && Mathf.IsEqualApprox(Player.MaxHealth, health + 100), "巨化实际体型增加20%且最大生命固定增加100");
		Check(Mathf.IsEqualApprox(MapManager.BodySize(Player).Radius, radius * 1.2f) && Player.GetNode<Node3D>("CameraRig").GlobalBasis.Scale.IsEqualApprox(cameraScale),
			"身体净空同步增大，TopLevel镜头保持原世界缩放");
		Check(!run.TryAcquireReward(giant.Id), "巨化获得后不能重复领取");
		Check(run.TryAcquireReward(steel.Id) && !run.TryAcquireReward(steel.Id), "心之钢正式授予且仅可获取一次");
		var growth = Player.GetChildren().OfType<KillHealthGrowthRule>().Single();
		Check(!growth.IsProcessing() && !growth.IsPhysicsProcessing(), "击杀成长通过事件触发，无逐帧监听");
		Unit Kill(bool boss = false, Unit? source = null)
		{
			var enemy = MakeEnemy(Player.GlobalPosition + new Vector3(5, 0, -9));
			if (boss) enemy.Tags = enemy.Tags.Append("Boss").ToArray();
			enemy.ApplyDirectDamage(1000000, source ?? Player); return enemy;
		}
		health = Player.MaxHealth; float current = Player.Health;
		for (int i = 0; i < 4; i++) Kill();
		Check(Player.MaxHealth == health && growth.KillTally == 4, "前四次真实击杀只累计，不提前增加生命");
		var fifth = Kill();
		Check(Player.MaxHealth == health + 1 && Player.Health == current + 1 && growth.KillTally == 5, "同帧第五杀立即获得1点本局最大生命，当前生命同步新增1点");
		fifth.ApplyDirectDamage(1000000, Player);
		Check(growth.KillTally == 5 && Player.MaxHealth == health + 1, "同一死者再次受伤不会重复计数");
		Kill(true); Check(Player.MaxHealth == health + 21 && growth.BossTally == 1 && growth.KillTally == 6, "首领属于击杀并额外增加20点最大生命");
		for (int i = 0; i < 3; i++) Kill(); Kill(true);
		Check(Player.MaxHealth == health + 42 && growth.KillTally == 10, "首领恰为第五杀时两项收益同时结算为21，不丢计数");
		var other = new Unit { Faction = Faction.Player }; Mode.AddChild(other); Kill(source: other);
		Check(growth.KillTally == 10 && Player.MaxHealth == health + 42, "其他单位击杀不计入本人心之钢"); other.QueueFree();
		var cap = new object(); Player.Stats.AddModifier(new StatModifier { Stat = "MaxHealthCap", Value = 30, Operation = ModifierOperation.Override, Source = cap }); Player.RefreshStats();
		Player.ApplyDirectDamage(5); current = Player.Health; Player.GrantMaximumHealthGrowth(2);
		Check(Player.MaxHealth == 30 && Player.Health == current, "硬生命上限仍有效，被夹住的成长不借机回血");
		Player.Stats.RemoveBySource(cap); Player.RefreshStats(); Player.FillVitals();
		var secondBuild = new RunBuild(); var secondContext = new RewardContext { Player = Player, Weapon = NodeSearch.FindInSubtree<Weapon>(Player)!, Build = secondBuild };
		secondBuild.Add(RewardEffectRegistry.Compile(new RewardConfig { Id = "probe_scale", Effects = new RewardEffectDefinition[] { new UnitScaleRewardEffect { SizeIncrease = .1f } } })!, secondContext);
		host.ReleaseReward(run.Build, giant.Id);
		Check(Player.Scale.IsEqualApprox(scale * 1.1f) && Player.MaxHealth == health - 100 + 44, "撤销巨化只移除自己的体型/100生命，保留其它体型来源及已挣得成长");
		host.ReleaseBuild(secondBuild); Check(Player.Scale.IsEqualApprox(scale), "最后体型来源移除后恢复原比例，不残留缩放");
		health = Player.MaxHealth; host.ReleaseReward(run.Build, steel.Id);
		for (int i = 0; i < 5; i++) Kill();
		Check(Player.MaxHealth == health && growth.KillTally == 10, "撤销心之钢立即停订阅，保留已挣得成长，不继续加血");
		await Wait(.1);
		Check(!IsInstanceValid(growth), "撤销后的成长组件已回收");
		var fresh = new Unit { MaxHealth = 100 }; Mode.AddChild(fresh);
		Check(fresh.MaxHealth == 100, "新单位/新局不继承旧局生命成长"); fresh.QueueFree();
		var camera = new Camera3D { Current = true }; Mode.AddChild(camera); camera.GlobalPosition = Player.GlobalPosition + new Vector3(3, 2.5f, 6); camera.LookAt(Targeting.AimPoint(Player));
		await Shot("growth-builds-normal");
		var preview = new RunBuild(); preview.Add(RewardEffectRegistry.Compile(giant)!, new RewardContext { Player = Player, Weapon = secondContext.Weapon, Build = preview });
		await Wait(.06); await Shot("growth-builds-giant"); host.ReleaseBuild(preview);
	}
}

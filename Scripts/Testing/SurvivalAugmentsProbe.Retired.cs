using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.AI.Behaviors;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Testing;

public partial class SurvivalAugmentsProbe
{
	private RewardScreen CommonScreen => GetParent().GetNode<RewardScreen>("Mode/RunController/RewardScreen");
	private async Task ChooseCommonReward()
	{
		await Until(() => CommonScreen.IsOpen && !CommonScreen.InputBlocked, 3, "通用奖励保护期结束");
		Check(CommonScreen.Options.Count > 0 && CommonScreen.Options.All(r => r.RequiredModeIds.Length == 0), "真实奖励界面仅显示通用增幅");
		var chosen = CommonScreen.Options[0]; int before = _run.Build.StacksOf(chosen.Id);
		var button = CommonScreen.GetNode<Container>("Root/Panel/VBox/ChoicesScroll/Cards").GetChildren().OfType<Button>().First(b => b.Visible);
		button.EmitSignal(Button.SignalName.Pressed); button.EmitSignal(Button.SignalName.Pressed);
		Check(_run.Build.StacksOf(chosen.Id) == before + 1 && !CommonScreen.IsOpen, "通用奖励实际领取一次，重复点击不重复发奖");
		await Wait(.05);
	}
	private async Task RunRetired()
	{
#if DEBUG
		MetaProgress.UseInMemoryStoreForSelfTest(); RuneCarry.UseInMemoryStoreForSelfTest();
#endif
		RunSetup.Character = PlayerCharacterKind.Hunter; RunSetup.Weapon = WeaponPresetKind.EnergyPalm; RunSetup.HasSelection = true;
		_director = GetParent().GetNode<SurvivalDirector>("Mode/Director"); _run = GetParent().GetNode<RunController>("Mode/RunController");
		var settings = (SurvivalDefinition)RuntimeResources.Load<SurvivalDefinition>("res://Config/Modes/twilight_survival.tres").Duplicate(true);
		settings.DurationSeconds = 1800; settings.BaseLevelExperience = 100000; settings.MilestoneFractions = Array.Empty<float>();
		_director.Definition = settings;
		await Until(() => _run.GameplayReady && _director.State == SurvivalDirector.PhaseState.Surviving, 10, "正式模式就绪");
		Player.IsInvulnerable = true;
		Check(_director.BuildRuntime == null && !_director.GetChildren().OfType<SurvivalBuildRuntime>().Any(), "不安装旧四路线命中与经验协调器");
		var rows = ConfigIndex.Settings.Upgrades.Where(r => r.Id.StartsWith("surv_", StringComparison.Ordinal)).ToArray();
		Check(rows.Length == 12 && rows.All(r => r.Weight == 0), "十二项旧专属全部停用，包含契约路线");
		foreach (var row in rows) Check(!_run.TryAcquireReward(row.Id) && _run.Build.StacksOf(row.Id) == 0, "真实GM获取也拒绝 " + row.Id);
		var console = GetParent().GetNode<ActRoguelike.Debug.GmConsole>("Mode/GmConsole");
		Check(console.Available, "实际开发控制台可用，命令测试不靠关闭GM伪通过");
		foreach (var row in rows)
		{
			console.ExecuteCommand("give " + row.Id);
			Check(!console.LastCommandSucceeded && console.LastResult.Contains("已停用") && _run.Build.StacksOf(row.Id) == 0, "命令行give也拒绝退用项 " + row.Id);
		}
		var retired = rows.Select(r => r.Id).ToHashSet();
		Check(CodexCatalog.Build().All(e => !retired.Contains(e.Key.Split('/').Last())), "正式图鉴不显示十二项旧专属");
		var weapon = NodeSearch.FindInSubtree<ActRoguelike.Weapons.Weapon>(Player)!;
		foreach (var item in ConfigIndex.Settings.Weapons)
		{
			var pool = new RewardPool(); item.ApplyTo(weapon);
			pool.Initialize(weapon, item.OwnerCharacter!.Id, settings.Id);
			Check(pool.Available(new RunBuild()).All(r => !retired.Contains(r.Id)), item.Id + "死战候选不含旧专属");
			pool.Initialize(weapon, item.OwnerCharacter.Id, "");
			Check(pool.Available(new RunBuild()).All(r => !retired.Contains(r.Id)), item.Id + "原冒险候选不含旧专属");
		}
		ConfigRows.ById<WeaponConfig>("wpn_energy_palm")!.ApplyTo(weapon);
		_director.GrantExperience(settings.ExperienceForLevel(_director.Level));
		await Until(() => CommonScreen.IsOpen, 3, "原专属等级2打开正常奖励");
		Check(_director.Level == 2 && _director.Experience == 0 && GetTree().Paused, "经验不带契约倍率，升级仍暂停战场");
		await ChooseCommonReward();
		for (int level = 3; level <= 5; level++)
		{
			_director.GrantExperience(settings.ExperienceForLevel(_director.Level)); await ChooseCommonReward();
		}
		Check(_director.Level == 5 && _director.PendingRewards == 0, "经过原专属等级5仍有正常奖励，无空队列");
		// 复用真实信标对象作为已登记遗物，保留导演目标归属校验，正式F流程由模式专项覆盖。
		var relic = _director.Objectives.First(); relic.Kind = SurvivalObjective.ObjectiveKind.Relic;
		Check(_director.ClaimRelic(relic) && !_director.ClaimRelic(relic), "强敌遗物只发一份奖励");
		await ChooseCommonReward();
		Check(_director.PendingRewards == 0, "遗物通用奖励后队列清空");
		var mage = RuntimeResources.Load<PackedScene>("res://Scenes/Enemies/MeteorMage.tscn").Instantiate<Unit>();
		ConfigRows.ById<EnemyConfig>("enemy_meteor_mage")!.ApplyTo(mage);
		mage.ProcessMode = ProcessModeEnum.Disabled; _director.AddChild(mage);
		var keep = mage.GetNode<KeepDistanceBehavior>("Brain/KeepDistance");
		Check(Mathf.IsEqualApprox(keep.PreferredDistance, 11) && Mathf.IsEqualApprox(keep.Tolerance, 1), "B1正式场景实例加载11±1米驻位");
		Check(Mathf.IsEqualApprox(mage.MoveSpeed, 3) && !keep.Strafe, "B1不改基础速度或启用横移");
		var plan = mage.GetNode<ActRoguelike.Abilities.EnemyPlanAbility>("AbilityPlan").Plan;
		Check(Mathf.IsEqualApprox(plan.TriggerDistance, 16) && Mathf.IsEqualApprox(plan.Far[0].WindupSeconds, 1.2f), "法师触发范围与准备时序不变");
		mage.QueueFree();
		await Shot("retired-production");
		Player.ForceKill(); await Until(() => _run.RunEnded, 3, "死亡结算");
		Check(_director.State == SurvivalDirector.PhaseState.Stopped && _director.PendingRewards == 0, "死亡停止模式并清空奖励队列");
		GC.Collect(); GC.WaitForPendingFinalizers(); Check(true, "模式停止后完整GC正常结束");
	}
}

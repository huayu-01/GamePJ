using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.UI;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>用真实候选池和入构筑验证重复获取、单次过滤与已有等级前置。</summary>
public partial class RewardRulesProbe : Node3D
{
	private int _pass, _fail;
	private Unit _player = null!;
	private readonly PlayerLoadout _loadout = new();
	private RunBuild _build = new();
	private Weapon Weapon => NodeSearch.FindInSubtree<Weapon>(_player)!;
	private string _characterId = "chr_hunter";
	private static readonly string[] RepeatIds = { "upg_power", "upg_armor", "upg_swift" };
	private static readonly string[] SingleIds = { "upg_life", "upg_crit", "upg_critdmg", "upg_hard", "upg_climb", "upg_insight" };
	private static readonly (string Id, string Parent)[] Levels =
	{
		("mut_burn_plus", "mut_burn"), ("mut_freeze_plus", "mut_freeze"),
		("mut_cleave_plus", "mut_cleave"), ("mut_cleave_plus2", "mut_cleave_plus"),
		("mut_growth_plus", "mut_growth"), ("mut_growth_plus2", "mut_growth_plus"),
		("mut_haste_plus", "mut_haste"), ("mut_haste_plus2", "mut_haste_plus"),
		("mut_lunge_plus", "mut_lunge"), ("mut_lunge_plus2", "mut_lunge_plus"),
		("mut_pierce_plus", "mut_pierce"), ("mut_pierce_plus2", "mut_pierce_plus"),
		("mut_ricochet_plus", "mut_ricochet"), ("mut_ricochet_plus2", "mut_ricochet_plus"),
		("upg_life_plus", "upg_life"), ("upg_life_plus2", "upg_life_plus"),
		("upg_crit_plus", "upg_crit"), ("upg_crit_plus2", "upg_crit_plus"),
		("upg_critdmg_plus", "upg_critdmg"), ("upg_critdmg_plus2", "upg_critdmg_plus"),
		("upg_hard_plus", "upg_hard"), ("upg_hard_plus2", "upg_hard_plus"),
	};
	private void Check(bool ok, string text)
	{
		if (ok) _pass++; else _fail++;
		GD.Print($"[RewardRules] {(ok ? "PASS" : "FAIL")} {text}");
	}
	private void Reset(PlayerCharacterKind character = PlayerCharacterKind.Hunter, WeaponPresetKind weapon = WeaponPresetKind.EnergyPalm)
	{
		foreach (var rule in _build.Triggers)
			if (IsInstanceValid(rule)) { rule.GetParent()?.RemoveChild(rule); rule.QueueFree(); }
		_player.Stats.RemoveBySource(_build);
		_build = new RunBuild();
		_loadout.Apply(_player, this, character, weapon);
		_characterId = RunController.CharacterRow(character)!.Id;
	}
	private RewardDefinition[] Available()
	{
		using var pool = new RewardPool();
		pool.Initialize(Weapon, _characterId);
		// 数量大于目录，返回全部合格候选；不依赖某个随机种子碰巧抽到目标。
		return pool.Roll(_build, RewardCatalog.All.Length + 1).ToArray();
	}
	private bool Acquire(string id)
	{
		var definition = Available().FirstOrDefault(row => row.Id == id);
		if (definition == null) return false;
		_build.Add(definition, new RewardContext { Player = _player, Weapon = Weapon, Build = _build });
		return true;
	}
	private bool AcquireParents(string id)
	{
		var row = RewardCatalog.Find(id);
		return row != null && row.Requires.All(parent => _build.Has(parent) || AcquireParents(parent)) && Acquire(id);
	}
	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		try
		{
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.Hunter; RunSetup.Weapon = WeaponPresetKind.EnergyPalm;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GlobalPosition = new Vector3(20, 0.05f, 20);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Check(RewardCatalog.All.Length == 70 && RewardCatalog.All.Count(row => row.Kind == RewardKind.Enhancement) == 17
				&& RewardCatalog.All.Count(row => row.Kind == RewardKind.Evolution) == 23 && RewardCatalog.All.Count(row => row.Kind == RewardKind.Mutation) == 30,
				"真实ConfigIndex奖励目录70=17强化／23进化／30异化（新增八条，符文不混入目录）");
			Check(RewardCatalog.All.All(row => row.MaxStacks == (RepeatIds.Contains(row.Id) ? 0 : 1)), "全目录只有三项星号强化可重复，其他已建项均一次");
			foreach (string id in RepeatIds)
			{
				Reset();
				bool acquired = Enumerable.Range(0, 5).All(_ => Acquire(id));
				Check(acquired && _build.StacksOf(id) == 5 && Available().Any(row => row.Id == id), $"{id}真实连续获得五次后仍在池中，越过旧三次上限");
			}
			foreach (string id in SingleIds)
			{
				Reset();
				Check(Acquire(id) && _build.StacksOf(id) == 1 && !Acquire(id), $"{id}真实获得一次后退出候选池");
			}
			foreach (var level in Levels)
			{
				var row = RewardCatalog.Find(level.Id)!;
				bool compatible = false;
				foreach (var character in new[] { PlayerCharacterKind.Hunter, PlayerCharacterKind.WeaponMaster })
				{
					foreach (var weapon in Enum.GetValues<WeaponPresetKind>())
					{
						Reset(character, weapon);
						if (row.AllowsCharacter(_characterId) && row.AllowsWeapon(Weapon)) { compatible = true; break; }
					}
					if (compatible) break;
				}
				Check(compatible && row.Requires.SequenceEqual(new[] { level.Parent }) && !Available().Any(candidate => candidate.Id == level.Id), $"{level.Id}未拥有上一级时不出现，前置指向{level.Parent}");
				Check(compatible && AcquireParents(level.Parent) && Acquire(level.Id) && !Acquire(level.Id), $"{level.Id}真实获取上一级后可获得，获得后不重复");
			}
			TestTierValues();
			TestHandlerDescription();
			await TestRewardCards();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[RewardRules] === 获取规则验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private void TestTierValues()
	{
		foreach (var family in new[] { (Id: "upg_life", Stat: "MaxHealthAdd"), (Id: "upg_crit", Stat: "CritChanceAdd"),
			(Id: "upg_critdmg", Stat: "CritMultiplierAdd"), (Id: "upg_hard", Stat: "DamageReductionAdd") })
		{
			Reset();
			float baseline = _player.Stats.Get(family.Stat), total = 0f;
			float healthBaseline = _player.MaxHealth, reductionBaseline = _player.CombinedDamageReduction();
			foreach (string suffix in new[] { "", "_plus", "_plus2" })
			{
				string id = family.Id + suffix;
				var row = ConfigRows.ById<RewardConfig>(id)!;
				total += row.Modifiers.Single(modifier => modifier.Stat == family.Stat).Value;
				bool acquired = Acquire(id);
				bool consumed = family.Stat switch
				{
					"MaxHealthAdd" => Mathf.IsEqualApprox(_player.MaxHealth, healthBaseline + total),
					"DamageReductionAdd" => _player.CombinedDamageReduction() > reductionBaseline || Mathf.IsEqualApprox(_player.CombinedDamageReduction(), 0.9f),
					_ => true,
				};
				Check(acquired && consumed && Mathf.IsEqualApprox(_player.Stats.Get(family.Stat), baseline + total), $"{id}按真实候选取得后，实际属性={_player.Stats.Get(family.Stat):0.###}，基线+表内累计={baseline + total:0.###}，生命／减伤消费者={consumed}");
			}
			Reset();
			using var copy = (RewardConfig)ConfigRows.ById<RewardConfig>(family.Id + "_plus")!.Duplicate(true);
			float value = family.Stat == "MaxHealthAdd" ? 7f : 0.07f;
			copy.Modifiers.Single(modifier => modifier.Stat == family.Stat).Value = value;
			var compiled = RewardEffectRegistry.Compile(copy)!;
			string expectedText = value.ToString(family.Stat == "MaxHealthAdd" ? "0" : "0.#%", CultureInfo.InvariantCulture);
			Check(!compiled.Description.Contains('{') && compiled.Description.Contains(expectedText), $"{copy.Id}仅改修饰器Value后，编译说明同步为「{compiled.Description}」");
			baseline = _player.Stats.Get(family.Stat);
			// 前置由上面的真实池验证；此处独立编译副本只验证参数消费，不替换全局目录。
			_build.Add(compiled, new RewardContext { Player = _player, Weapon = Weapon, Build = _build });
			Check(Mathf.IsEqualApprox(_player.Stats.Get(family.Stat), baseline + value), $"{copy.Id}副本实际属性增量同步读取新Value={value:0.###}");
		}
	}
	private void TestHandlerDescription()
	{
		Check(RewardCatalog.Find("mut_stomp")!.Description == "成功起跳时对周围 3 米内的敌人造成 15 点伤害，空中跳跃同样生效", "既有杀戮践踏HandlerParams占位符在奖励编译时解析，不把原始模板交给卡片");
		using var copy = (RewardConfig)ConfigRows.ById<RewardConfig>("mut_stomp")!.Duplicate(true);
		foreach (var parameter in copy.HandlerParams)
		{
			if (parameter.Key == "Radius") parameter.Value = 1.5f;
			if (parameter.Key == "Damage") parameter.Value = 7f;
		}
		Check(RewardEffectRegistry.Compile(copy)!.Description == "成功起跳时对周围 1.5 米内的敌人造成 7 点伤害，空中跳跃同样生效", "仅改既有处理器半径／伤害参数，编译说明同步变化");
	}
	private async Task TestRewardCards()
	{
		var screen = RuntimeResources.Load<PackedScene>("res://Scenes/UI/RewardScreen.tscn").Instantiate<RewardScreen>(); AddChild(screen);
		var options = new[] { RewardCatalog.Find("upg_life_plus")!, RewardCatalog.Find("upg_crit_plus")!, RewardCatalog.Find("mut_stomp")! };
		try
		{
			screen.Open(options, "强化等级与表内参数");
			await ToSignal(GetTree().CreateTimer(0.35, true), SceneTreeTimer.SignalName.Timeout);
			var buttons = screen.GetNode<VBoxContainer>("Root/Panel/VBox/Cards").GetChildren().OfType<Button>().Where(button => button.Visible).ToArray();
			Check(buttons.Length == 3 && options.Select((option, index) => buttons[index].Text.Contains(option.Description) && !buttons[index].Text.Contains('{')).All(ok => ok), "真实奖励界面三张卡片显示解析后的数值，无模板占位符");
			if (DisplayServer.GetName() != "headless")
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				using var screenshot = GetViewport().GetTexture().GetImage();
				Check(screenshot.SavePng("res://docs/screenshots/j24_reward_tiers.png") == Error.Ok, "真实奖励卡片截图j24_reward_tiers");
			}
		}
		finally { screen.Close(); screen.QueueFree(); }
	}
}

using System;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
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
		try
		{
			RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.Hunter; RunSetup.Weapon = WeaponPresetKind.EnergyPalm;
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GlobalPosition = new Vector3(20, 0.05f, 20);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Check(RewardCatalog.All.Length > 0, "读取真实ConfigIndex奖励目录");
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
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[RewardRules] === 获取规则验收结束：通过 {_pass}，失败 {_fail} ===");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
}

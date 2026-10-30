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
using ActRoguelike.Weapons;
using Godot;
using K = ActRoguelike.Config.SpecializationKind;

namespace ActRoguelike.Testing;

/// <summary>从真实候选规则生成两两特化组合；覆盖六武器、符文前置、派生隔离、状态视觉和生命倍率。</summary>
public partial class SynergyMatrixProbe : Node3D
{
	private int _pass, _fail, _combinations;
	private readonly PlayerLoadout _loadout = new();
	private Unit _player = null!, _enemy = null!;
	private RunBuild? _previousBuild, _previousRune;
	private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"[SynergyMatrix] {(ok ? "PASS" : "FAIL")} {text}"); }
	private async Task Wait(double seconds = .06) => await ToSignal(GetTree().CreateTimer(seconds, true), SceneTreeTimer.SignalName.Timeout);
	public override async void _Ready()
	{
		try
		{
#if DEBUG
			MetaProgress.UseInMemoryStoreForSelfTest(); RuneCarry.UseInMemoryStoreForSelfTest();
#endif
			await Wait(.2);
			_player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<Unit>(); AddChild(_player);
			_player.GetNode<Node3D>("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
			_player.GetNode<PlayerAbilityInput>("AbilityInput").ProcessMode = ProcessModeEnum.Disabled;
			_player.GlobalPosition = new Vector3(0, .05f, 0); _player.Movement!.SetPhysicsProcess(false);
			_enemy = new Unit { Name = "MatrixTarget", Faction = Faction.Enemy, MaxHealth = 1000000 };
			_enemy.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = .5f }, Position = Vector3.Up });
			AddChild(_enemy); _enemy.GlobalPosition = new Vector3(2, .05f, 0); _enemy.SetPhysicsProcess(false);
			await Matrix(); await Visuals(); await RuneHud(); HealthScales();
		}
		catch (Exception ex) { Check(false, ex.ToString()); }
		GD.Print($"[SynergyMatrix] SUMMARY combinations={_combinations} pass={_pass} fail={_fail}");
		GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private RewardContext Equip(CharacterConfig character, WeaponConfig weapon, RuneConfig? rune)
	{
		if (_previousBuild != null) _player.Stats.RemoveBySource(_previousBuild);
		if (_previousRune != null) _player.Stats.RemoveBySource(_previousRune);
		_loadout.Apply(_player, this, (PlayerCharacterKind)(character.Slot - 1), (WeaponPresetKind)(weapon.Slot - 1));
		var build = new RunBuild(); _previousBuild = build;
		var equipped = NodeSearch.FindInSubtree<Weapon>(_player)!;
		if (rune != null)
		{
			_previousRune = new RunBuild();
			RewardEffectRegistry.Compile(rune)!.Apply(new RewardContext { Player = _player, Weapon = equipped, Build = _previousRune });
			build.SetRuneUnlocks(rune.UnlockList().Append(rune.Id));
		}
		_player.FillVitals();
		return new RewardContext { Player = _player, Weapon = equipped, Build = build };
	}
	private async Task Matrix()
	{
		foreach (WeaponConfig weapon in ConfigIndex.Settings.Weapons)
		foreach (string runeId in new[] { "", "rune_lei_seal", "rune_war_seal", "rune_wind_seal" })
		{
			CharacterConfig character = weapon.OwnerCharacter!;
			RuneConfig? rune = ConfigRows.ById<RuneConfig>(runeId);
			var baseline = Equip(character, weapon, rune);
			using var pool = new RewardPool(); pool.Initialize(baseline.Weapon, character.Id);
			var rows = pool.Available(baseline.Build).Where(d => ConfigRows.ById<RewardConfig>(d.Id)?.Specialization != K.None).ToArray();
			for (int a = 0; a < rows.Length; a++)
			for (int b = a + 1; b < rows.Length; b++)
			{
				foreach (bool reverse in new[] { false, true })
				{
					var context = Equip(character, weapon, rune);
					var first = rows[reverse ? b : a]; var second = rows[reverse ? a : b];
					context.Build.Add(first, context);
					if (!pool.Available(context.Build).Any(d => d.Id == second.Id)) continue;
					context.Build.Add(second, context); _combinations++;
					var runtime = _player.GetNode<SpecializationRuntime>(SpecializationRuntime.NodeName); runtime.SetPhysicsProcess(false);
					_enemy.FillVitals(); foreach (string id in _enemy.GetStatusIds()) _enemy.RemoveStatus(id);
					float before = _enemy.Health;
					Hit(false, false, true); runtime._PhysicsProcess(.01);
					int chains = runtime.ChainCount, marks = runtime.MarkCount, spears = runtime.SpearCount;
					Hit(true, false, false); Hit(false, true, false); runtime._PhysicsProcess(.01);
					Check(context.Build.Has(first.Id) && context.Build.Has(second.Id) && _enemy.Health < before
						&& _player.Health > 0 && float.IsFinite(_enemy.Health)
						&& runtime.ChainCount == chains && runtime.MarkCount == marks && runtime.SpearCount == spears,
						$"{weapon.Id}/{runeId} {first.Id}+{second.Id}：真实授予、主击与暴击有效，派生／DoT不递归");
					if (_combinations % 12 == 0) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				}
			}
		}
		Check(_combinations >= 400, $"覆盖合法组合及相反获取顺序，共{_combinations}例");
		if (_player.GetNodeOrNull<SpecializationRuntime>(SpecializationRuntime.NodeName) is { } last) { _player.RemoveChild(last); last.QueueFree(); }
	}
	private void Hit(bool secondary, bool dot, bool crit)
	{
		var attack = new AttackInstance { Attacker = _player, Ability = "MatrixPrimary", AbilityKind = "MatrixPrimary", BaseDamage = 3,
			CanCrit = crit, CritChance = crit ? 1f : 0f, CritMultiplier = 2, IsSecondary = secondary, IsDamageOverTime = dot };
		_enemy.ReceiveHit(attack.RollHit(_enemy, Vector3.Zero, _enemy.GlobalPosition));
	}
	private async Task Visuals()
	{
		foreach (string id in _enemy.GetStatusIds()) _enemy.RemoveStatus(id);
		var fx = RuntimeResources.Load<PackedScene>("res://Scenes/Combat/StatusEffectFx.tscn").Instantiate<StatusEffectFx>(); _enemy.AddChild(fx);
		var statuses = ConfigIndex.Settings.Effects.Where(row => row.IsStatus).ToArray();
		foreach (EffectConfig row in statuses)
		{
			Check(row.HideStatusVisual || row.VisualStyle != StatusVisualStyle.None && row.VisualIcon != null, $"附加状态{row.Id}有专属视觉，内部信标显式隐藏");
			_enemy.ApplyStatus(row.ToStatus(20), _player, "VisualAudit");
		}
		_enemy.ApplyStatus(new StatusEffect { Id = "FutureStatusProbe", Duration = 20 });
		await Wait(.2);
		Check(fx.VisualCount == statuses.Count(row => !row.HideStatusVisual) + 1, "全部外显状态与未知状态通用兜底都有实际视觉节点");
		Check(fx.GetNode<Sprite3D>("ThunderMark/Icon").Texture != null && fx.GetNode<Sprite3D>("ThunderMark/Icon").NoDepthTest, "引雷符持续符文图标可见且不被模型遮挡");
		Check(fx.FindChildren("*", "CollisionObject3D", true, false).Count == 0, "状态表现不增加碰撞和伤害");
		foreach (string id in _enemy.GetStatusIds()) _enemy.RemoveStatus(id);
		await Wait(.2); Check(fx.VisualCount == 0, "状态清除后表现同步回收");
	}
	private async Task RuneHud()
	{
		var hud = RuntimeResources.Load<PackedScene>("res://Scenes/UI/Hud.tscn").Instantiate<PlayerHud>(); AddChild(hud);
		foreach (RuneConfig rune in ConfigIndex.Settings.Runes)
		{
			RuneCarry.Set(rune.Id); await Wait(.07);
			Check(hud.DisplayedRuneId == rune.Id && hud.GetNode<TextureRect>("Root/CarriedRune/Layout/Icon").Texture != null,
				$"携带{rune.DisplayName}时常驻HUD显示对应图标");
		}
		RuneCarry.Set(string.Empty); await Wait(.07);
		Check(hud.DisplayedRuneId.Length == 0 && !hud.GetNode<TextureRect>("Root/CarriedRune/Layout/Icon").Visible, "不携带时无残留旧符文图标");
		hud.QueueFree();
	}
	private void HealthScales()
	{
		foreach (EnemyConfig row in ConfigIndex.Settings.Enemies.Where(row => !row.IsGlobal))
		{
			var unit = new Unit { Faction = Faction.Enemy }; unit.ApplyBaseStats(row.MaxHealth, 0, 0, row.MoveSpeed);
			AddChild(unit); ConfigIndex.ScaleEnemy(unit, this, false);
			Check(Mathf.IsEqualApprox(unit.MaxHealth, row.MaxHealth * .75f * 2.5f), $"敌种{row.Id}生命为原倍率的2.5倍：{unit.MaxHealth}"); unit.QueueFree();
		}
		var boss = new Unit { Faction = Faction.Enemy }; boss.ApplyBaseStats(ConfigIndex.Settings.Bosses[0].MaxHealth, 0, 0, 1); AddChild(boss);
		ConfigIndex.ScaleEnemy(boss, this, true); Check(Mathf.IsEqualApprox(boss.MaxHealth, ConfigIndex.Settings.Bosses[0].MaxHealth * 2.5f), "完整首领生命同样提高2.5倍"); boss.QueueFree();
	}
}

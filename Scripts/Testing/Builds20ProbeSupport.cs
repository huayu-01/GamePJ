using System;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Player;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Testing;

public abstract partial class Builds20ProbeSupport : EliteTraitProbeSupport
{
	protected override string? TestRune => "";
	protected RunBuild Build = new();
	protected readonly PlayerLoadout Loadout = new();
	protected Camera3D Camera = null!;
	private readonly object _crit = new();
	protected RunController Run => Mode.GetNode<RunController>("RunController");
	protected RewardContext Context => new() { Player = Player, Weapon = NodeSearch.FindInSubtree<Weapon>(Player)!, Build = Build };
	protected async Task<bool> Until(Func<bool> predicate, double seconds = 3)
	{
		for (int i = 0; i < seconds * 100; i++) { if (predicate()) return true; await Wait(.01); } return predicate();
	}
	protected void Press(string action, bool value) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = value });
	protected void Prepare()
	{
		Player.Stats.RemoveBySource(_crit); Player.Stats.AddModifier(new StatModifier { Stat = "CritChanceAdd", Operation = ModifierOperation.Override, Value = -1, Source = _crit });
		Player.ApplyBaseStats(5000, 0, 0, 6.5f); Player.FillVitals(); Player.Movement?.SetPhysicsProcess(false);
		Player.GetNode("CameraRig").ProcessMode = ProcessModeEnum.Disabled;
		if (!IsInstanceValid(Camera)) { Camera = new Camera3D { Current = true }; Mode.AddChild(Camera); }
		Aim(Player.GlobalPosition + Vector3.Forward * 12 + Vector3.Up * .9f);
	}
	protected void Aim(Vector3 target)
	{
		Camera.MakeCurrent(); Camera.GlobalPosition = Player.GlobalPosition + new Vector3(0, .95f, 6); Camera.LookAt(target); Camera.ResetPhysicsInterpolation();
	}
	protected async Task Equip(WeaponPresetKind weapon)
	{
		foreach (string action in new[] { "weapon_skill_1", "weapon_skill_2", "skill_1", "skill_2" }) Press(action, false);
		RewardEffectHost.Ensure(Player).ReleaseBuild(Build); Build = new RunBuild();
		Loadout.Apply(Player, Mode, TestCharacter, weapon); Prepare(); await Wait(.08);
	}
	protected bool Give(string id)
	{
		using var pool = new RewardPool(); pool.Initialize(Context.Weapon, RunController.CharacterRow(TestCharacter)!.Id, Director.ModeId);
		var definition = pool.Available(Build).Find(r => r.Id == id);
		Check(definition != null, "069正常候选资格允许：" + id);
		if (definition == null) return false; Build.Add(definition, Context); return true;
	}
	protected void Release(string id) => RewardEffectHost.Ensure(Player).ReleaseReward(Build, id);
	protected T Skill<T>(string action = "weapon_skill_2") where T : Ability => (T)Player.AbilitySlots.Resolve(action)!;
	protected async Task Tap(string action)
	{
		Press(action, true); await Wait(.04); Press(action, false);
	}
}

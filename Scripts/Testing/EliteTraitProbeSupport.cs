using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Combat.Elites;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>精英专项共享真实模式、导航和独立报告；不写玩家存档。</summary>
public abstract partial class EliteTraitProbeSupport : Node
{
	protected abstract string ProbeTag { get; }
	protected abstract Task RunChecks();
	protected virtual PlayerCharacterKind TestCharacter => PlayerCharacterKind.Hunter;
	protected virtual WeaponPresetKind TestWeapon => WeaponPresetKind.EnergyPalm;
	protected virtual string? TestRune => null;
	protected Node3D Mode { get; private set; } = null!;
	protected Unit Player { get; private set; } = null!;
	protected SurvivalDirector Director { get; private set; } = null!;
	private int _pass, _fail;
	private readonly List<object> _checks = new();
	protected void Check(bool condition, string message)
	{
		if (condition) _pass++; else _fail++;
		_checks.Add(new { passed = condition, message }); GD.Print($"[{ProbeTag}] {(condition ? "PASS" : "FAIL")} {message}");
	}
	protected async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds, true), SceneTreeTimer.SignalName.Timeout);
	protected EliteTraitDefinition Definition(string key) => (EliteTraitDefinition)RuntimeResources.Load<EliteTraitDefinition>($"res://Config/EliteTraits/elite_{key}.tres").Duplicate(true);
	protected Unit MakeEnemy(Vector3 position)
	{
		var scene = RuntimeResources.Load<PackedScene>("res://Scenes/Enemies/Chaser.tscn");
		var unit = scene.Instantiate<Unit>(); ConfigRows.ByScenePath<EnemyConfig>(scene.ResourcePath)?.ApplyTo(unit);
		Mode.AddChild(unit); unit.GlobalPosition = position;
		unit.ApplyBaseStats(5000, 0, 0, 0); unit.FillVitals(); unit.ResetPhysicsInterpolation();
		foreach (Node child in unit.GetChildren())
			if (child is Ability ability) ability.Enabled = false;
			else if (child.GetType().Name == "EnemyBrain") child.ProcessMode = ProcessModeEnum.Disabled;
		unit.Movement?.SetPhysicsProcess(false); unit.Velocity = Vector3.Zero;
		return unit;
	}
	protected EliteTraitWave MakeWave(EliteTraitDefinition definition, params Unit[] members)
	{
		var world = new EliteHazardWorld(); Mode.AddChild(world);
		var wave = new EliteTraitWave { Definition = definition, Player = Player, World = world, MaxAlive = 64 };
		Mode.AddChild(wave); foreach (var member in members) wave.Register(member);
		if (members.Length > 0)
		{
			using var reservation = wave.Reserve(members[0], true, float.MaxValue);
			Check(reservation?.Commit(members[0]) == true, $"安装 {definition.Id} 单特性真实行为");
		}
		wave.Seal(); return wave;
	}
	protected async Task Shot(string name)
	{
		if (DisplayServer.GetName() == "headless") return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		using var image = GetViewport().GetTexture().GetImage();
		Check(image.SavePng(ValidationArtifacts.Path($"res://Logs/evidence/elites/{name}.png")) == Error.Ok, "真实渲染 " + name);
	}
	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		try
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
#if DEBUG
			MetaProgress.UseInMemoryStoreForSelfTest(); RuneCarry.UseInMemoryStoreForSelfTest();
#endif
			if (TestRune != null) RuneCarry.Set(TestRune);
			RunSetup.Character = TestCharacter; RunSetup.Weapon = TestWeapon; RunSetup.HasSelection = true;
			Mode = RuntimeResources.Load<PackedScene>("res://Scenes/Survival.tscn").Instantiate<Node3D>();
			Director = Mode.GetNode<SurvivalDirector>("Director"); Director.EnableEliteTraits = false; Director.EnableSceneRunes = false;
			var definition = (SurvivalDefinition)RuntimeResources.Load<SurvivalDefinition>("res://Config/Modes/twilight_survival.tres").Duplicate(true);
			definition.DurationSeconds = 1800; definition.BaseLevelExperience = 100000; definition.MilestoneFractions = Array.Empty<float>();
			foreach (var phase in definition.Phases) { phase.BudgetPerSecond = .01f; phase.EliteChance = 0; }
			Director.Definition = definition; GetParent().AddChild(Mode);
			for (int i = 0; i < 600 && Director.State != SurvivalDirector.PhaseState.Surviving; i++) await Wait(.02);
			if (Director.Player == null || Director.State != SurvivalDirector.PhaseState.Surviving) throw new InvalidOperationException("真实模式／导航准备超时");
			Player = Director.Player; Player.ApplyBaseStats(50000, 0, 0, 0); Player.FillVitals(); Player.IsInvulnerable = false;
			Director.SetPhysicsProcess(false); Player.Movement?.SetPhysicsProcess(false);
			Check(true, "真实生存模式、玩家和导航已准备");
			await RunChecks();
		}
		catch (Exception exception) { Check(false, exception.ToString()); }
		GetTree().Paused = false;
		if (Mode != null && IsInstanceValid(Mode)) { Director.Stop(); Mode.QueueFree(); await Wait(.15); }
		GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); await Wait(.1);
		System.IO.File.WriteAllText(ValidationArtifacts.Path($"res://Logs/evidence/elites/{ProbeTag}-{(DisplayServer.GetName() == "headless" ? "headless" : "render")}.json"), JsonSerializer.Serialize(new { passed = _fail == 0, pass = _pass, fail = _fail, checks = _checks }, new JsonSerializerOptions { WriteIndented = true }));
		GD.Print($"[{ProbeTag}] SUMMARY pass={_pass} fail={_fail}"); GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
}

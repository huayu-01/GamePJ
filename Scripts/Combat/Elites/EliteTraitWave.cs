using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Encounter;
using Godot;

namespace ActRoguelike.Combat.Elites;

/// <summary>同波冻结一种定义；预留与成功承载分别记账，延迟生成失败不占用承载名额。</summary>
public partial class EliteTraitWave : Node
{
	public Unit? Player { get; set; }
	public EliteHazardWorld World { get; set; } = null!;
	private EliteTraitDefinition? _definition;
	public EliteTraitDefinition? Definition
	{
		get => _definition;
		set { if (IsInsideTree()) throw new InvalidOperationException("本波已开始，不能中途替换特性。"); _definition = value; }
	}
	public Action<Unit, int>? RegisterSummoned { get; set; }
	public Func<int>? CurrentAlive { get; set; }
	public Func<float>? AvailablePressure { get; set; }
	public int MaxAlive { get; set; } = 48;
	public float SummonHealthScale { get; set; } = 1;
	public float SummonDamageScale { get; set; } = 1;
	public IReadOnlyList<Unit> Members => _members;
	public bool IsSealed { get; private set; }
	public bool IsStopped { get; private set; }
	public float Time { get; private set; }
	public int CarriersCommitted { get; private set; }
	public int CarriersReserved { get; private set; }
	public int SummonedCount { get; private set; }
	public event Action<Unit, HitInstance>? MemberDied;
	private readonly List<Unit> _members = new();
	private readonly Dictionary<Unit, Action<HitInstance>> _deaths = new();
	private readonly List<Unit> _summoned = new();
	public Unit[] LiveMembers() => _members.Where(u => IsInstanceValid(u) && !u.IsDead && !u.IsQueuedForDeletion()).ToArray();
	public override void _PhysicsProcess(double delta) { if (!IsStopped) Time += (float)delta; }
	public void Seal() => IsSealed = true;
	public void Register(Unit unit)
	{
		if (IsStopped || IsSealed || _members.Contains(unit) || unit.HasMeta("EliteSummoned")) return;
		_members.Add(unit);
		Action<HitInstance> callback = hit => MemberDied?.Invoke(unit, hit);
		_deaths.Add(unit, callback); unit.Died += callback;
	}
	public EliteTraitReservation? Reserve(Unit unit, bool elite, float availableCost, float availablePressure = float.MaxValue)
	{
		var definition = Definition;
		if (!CanAssign(unit, elite) || definition == null || definition.ExtraCost > availableCost || definition.ExtraCost > availablePressure) return null;
		CarriersReserved++;
		// 必须在选点前按最终体型检查；精英Ready会使用这份相同的最终缩放。
		foreach (var modifier in unit.GetChildren().OfType<EliteModifier>())
		{
			unit.SetMeta("EliteTraitScaleBefore", unit.Scale);
			modifier.SetMeta("EliteTraitScaleBefore", modifier.ModelScale);
			modifier.ModelScale *= definition.PreSpawnScale;
			unit.Scale = Vector3.One * modifier.ModelScale;
		}
		return new EliteTraitReservation(this, definition);
	}
	public bool CanAssign(Unit unit, bool elite)
		=> !IsStopped && elite && Definition is { } definition && definition.Supports(unit)
			&& EliteTraitRuntime.Find(unit) == null && (!IsSealed || _members.Contains(unit))
			&& CarriersCommitted + CarriersReserved < definition.MaxCarriers;
	internal void ReleaseReservation() => CarriersReserved = Math.Max(0, CarriersReserved - 1);
	internal bool Commit(Unit unit, EliteTraitReservation reservation)
	{
		if (IsStopped || !IsInstanceValid(unit) || unit.IsDead || EliteTraitRuntime.Find(unit) != null) return false;
		Register(unit); ReleaseReservation(); CarriersCommitted++;
		unit.AddChild(new EliteTraitRuntime { Name = EliteTraitRuntime.NodeName, Definition = reservation.Definition, Wave = this });
		return true;
	}
	/// <summary>随从生成费为零；真实数量及清场仍登记，成功名额由调用行为一生限额。</summary>
	public int TrySummon(Unit owner, int count)
	{
		if (IsStopped || owner.IsDead || Player == null || !IsInstanceValid(Player) || count <= 0 || Definition?.SummonScene == null) return 0;
		int spawned = 0;
		var reserved = new List<Vector3>();
		for (int i = 0; i < Math.Min(8, count); i++)
		{
			if ((CurrentAlive?.Invoke() ?? LiveMembers().Length + _summoned.Count(u => IsInstanceValid(u) && !u.IsDead)) >= MaxAlive) break;
			var unit = Definition.SummonScene.Instantiate<Unit>();
			var row = ConfigRows.ByScenePath<EnemyConfig>(Definition.SummonScene.ResourcePath);
			row?.ApplyTo(unit);
			if ((row?.SpawnResourceCost ?? 1) > (AvailablePressure?.Invoke() ?? float.MaxValue)) { unit.Free(); break; }
			unit.SetMeta("EliteSummoned", true);
			if (!SpawnPlanner.TryFindSpawnPoint(owner.GlobalPosition, 1.8f, 5, out Vector3 point, unit, reserved)) { unit.Free(); continue; }
			(GetParent() ?? this).AddChild(unit); unit.GlobalPosition = point + Vector3.Up * .04f;
			ConfigIndex.ScaleEnemy(unit, this, false);
			unit.Stats.AddModifier(new StatModifier { Stat = "MaxHealthMultiplier", Operation = ModifierOperation.Multiply, Value = SummonHealthScale, Source = this });
			unit.Stats.AddModifier(new StatModifier { Stat = "DamageMultiplier", Operation = ModifierOperation.Multiply, Value = SummonDamageScale, Source = this });
			unit.RefreshStats(); unit.FillVitals();
			_summoned.Add(unit); reserved.Add(point); spawned++; SummonedCount++;
			RegisterSummoned?.Invoke(unit, row?.SpawnResourceCost ?? 1);
		}
		return spawned;
	}
	public void Stop()
	{
		if (IsStopped) return; IsStopped = true;
		foreach (var unit in _members) if (IsInstanceValid(unit)) EliteTraitRuntime.Find(unit)?.Stop();
		foreach (var pair in _deaths) if (IsInstanceValid(pair.Key)) pair.Key.Died -= pair.Value;
		_deaths.Clear(); MemberDied = null; _members.Clear(); _summoned.Clear();
	}
	public override void _ExitTree() => Stop();
}

public sealed class EliteTraitReservation : IDisposable
{
	private EliteTraitWave? _wave;
	public EliteTraitDefinition Definition { get; }
	public int Cost => Definition.ExtraCost;
	internal EliteTraitReservation(EliteTraitWave wave, EliteTraitDefinition definition) { _wave = wave; Definition = definition; }
	public bool Commit(Unit unit)
	{
		if (_wave == null) return false;
		if (!_wave.Commit(unit, this)) return false;
		_wave = null; return true;
	}
	public void Dispose() { _wave?.ReleaseReservation(); _wave = null; }
}

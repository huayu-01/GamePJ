using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Combat;
using ActRoguelike.Combat.Elites;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Run;

public partial class SurvivalDirector
{
	[Export] public bool EnableEliteTraits { get; set; } = true;
	[Export] public float EliteTraitBatchSeconds { get; set; } = 20;
	[Export] public int EliteTraitBatchSize { get; set; } = 4;
	[Export] public EliteTraitCatalog? TraitCatalog { get; set; }
	public EliteTraitWave? CurrentTraitWave => _currentTraitWave;
	public EliteHazardWorld? TraitWorld => _traitWorld;
	private EliteHazardWorld? _traitWorld;
	private EliteTraitWave? _currentTraitWave;
	private readonly List<EliteTraitWave> _traitWaves = new();
	private float _traitBatchEnd;
	private int _traitPhase = -1;
	private EliteTraitWave? EnsureTraitWave(SurvivalPhase phase)
	{
		if (!EnableEliteTraits || _player == null) return null;
		if (_currentTraitWave != null && !_currentTraitWave.IsSealed && ElapsedSeconds < _traitBatchEnd && _traitPhase == _phaseIndex
			&& _currentTraitWave.Members.Count + _pending.Count(p => p.TraitWave == _currentTraitWave) < Mathf.Max(2, EliteTraitBatchSize)) return _currentTraitWave;
		TraitCatalog ??= RuntimeResources.Load<EliteTraitCatalog>("res://Config/EliteTraits/catalog.tres");
		if (_traitWorld == null || !IsInstanceValid(_traitWorld)) { _traitWorld = new EliteHazardWorld { Name = "EliteHazards" }; AddChild(_traitWorld); }
		var prototypes = new List<Unit>();
		foreach (var row in phase.Enemies)
		{
			var scene = RuntimeResources.Load<PackedScene>(row.ScenePath); if (scene == null) continue;
			var unit = scene.Instantiate<Unit>(); row.ApplyTo(unit); prototypes.Add(unit);
		}
		var selected = TraitCatalog?.Pick(prototypes);
		foreach (var unit in prototypes) unit.Free();
		_traitBatchEnd = ElapsedSeconds + Mathf.Max(3, EliteTraitBatchSeconds); _traitPhase = _phaseIndex;
		_currentTraitWave = new EliteTraitWave
		{
			Name = $"EliteBatch{_traitWaves.Count + 1}", Definition = selected, Player = _player, World = _traitWorld,
			CurrentAlive = () => _owned.Count + _pending.Count, MaxAlive = phase.MaxAlive,
			AvailablePressure = () => phase.MaxAlivePressure - AlivePressure - _pending.Sum(p => p.Cost),
			SummonHealthScale = phase.HealthScale, SummonDamageScale = phase.DamageScale,
			RegisterSummoned = (unit, pressure) => RegisterUnit(unit, pressure, false, false)
		};
		AddChild(_currentTraitWave); _traitWaves.Add(_currentTraitWave);
		SealFinishedTraitWaves(); return _currentTraitWave;
	}
	private void SealFinishedTraitWaves()
	{
		foreach (var wave in _traitWaves)
			if (!wave.IsSealed && !_pending.Any(p => p.TraitWave == wave)
				&& (wave != _currentTraitWave || ElapsedSeconds >= _traitBatchEnd
					|| wave.Members.Count >= Mathf.Max(2, EliteTraitBatchSize)
					|| _owned.Count >= wave.MaxAlive)) wave.Seal();
		for (int i = _traitWaves.Count - 1; i >= 0; i--)
		{
			var wave = _traitWaves[i];
			if (wave == _currentTraitWave || !wave.IsSealed || wave.LiveMembers().Length > 0) continue;
			wave.Stop(); wave.QueueFree(); _traitWaves.RemoveAt(i);
		}
	}
	private void StopEliteTraits()
	{
		foreach (var wave in _traitWaves) if (IsInstanceValid(wave)) { wave.Stop(); wave.QueueFree(); }
		_traitWaves.Clear(); _currentTraitWave = null; _traitPhase = -1;
		if (_traitWorld != null && IsInstanceValid(_traitWorld)) _traitWorld.Clear();
	}
}

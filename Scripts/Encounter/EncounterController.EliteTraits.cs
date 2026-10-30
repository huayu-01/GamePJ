using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Combat;
using ActRoguelike.Combat.Elites;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Encounter;

public partial class EncounterController
{
	[Export] public bool EnableEliteTraits { get; set; } = true;
	[Export] public EliteTraitCatalog? TraitCatalog { get; set; }
	public EliteTraitWave? CurrentTraitWave => _currentTraitWave;
	public EliteHazardWorld? TraitWorld => _traitWorld;
	private EliteHazardWorld? _traitWorld;
	private EliteTraitWave? _currentTraitWave;
	private readonly List<EliteTraitWave> _traitWaves = new();
	private void BeginTraitWave()
	{
		if (!EnableEliteTraits || _player == null) return;
		_currentTraitWave?.Seal();
		TraitCatalog ??= RuntimeResources.Load<EliteTraitCatalog>("res://Config/EliteTraits/catalog.tres");
		if (_traitWorld == null || !IsInstanceValid(_traitWorld)) { _traitWorld = new EliteHazardWorld { Name = "EliteHazards" }; AddChild(_traitWorld); }
		var prototypes = new List<Unit>();
		foreach (var entry in _waveEntries.Count > 0 ? _waveEntries : _entries)
			if (entry.Scene?.Instantiate() is Unit unit) prototypes.Add(unit);
		var selected = TraitCatalog?.Pick(prototypes);
		foreach (var unit in prototypes) unit.Free();
		_currentTraitWave = new EliteTraitWave
		{
			Name = $"EliteWave{_waveIndex}", Player = _player, World = _traitWorld, Definition = selected,
			CurrentAlive = () => AliveCount, MaxAlive = 48,
			SummonHealthScale = EnemyHealthScale, SummonDamageScale = EnemyDamageScale,
			RegisterSummoned = (unit, pressure) => { _spawned.Add(unit); _spawnedPressure[unit] = pressure; AliveCount++; AlivePressure += pressure; }
		};
		AddChild(_currentTraitWave); _traitWaves.Add(_currentTraitWave);
	}
	private void StopEliteTraits()
	{
		foreach (var wave in _traitWaves) if (IsInstanceValid(wave)) { wave.Stop(); wave.QueueFree(); }
		_traitWaves.Clear(); _currentTraitWave = null;
		if (_traitWorld != null && IsInstanceValid(_traitWorld)) _traitWorld.Clear();
	}
}

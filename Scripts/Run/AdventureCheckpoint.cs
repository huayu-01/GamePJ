using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.AbilityCards;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using Godot;
using Disk = System.IO.File;

namespace ActRoguelike.Run;

/// <summary>一次出击的回退点；只回退成长与配装，设置独立保留。不跨局复用。</summary>
public sealed class AdventureCheckpoint
{
	private static AdventureCheckpoint? _active, _returning;
	private readonly Dictionary<string, byte[]?> _files = new();
	private readonly Action _restoreMeta, _restoreCards, _restoreRune, _restoreLoadout;
	private readonly PlayerCharacterKind _character;
	private readonly WeaponPresetKind _weapon;
	private readonly Transform3D? _baseTransform;
	private readonly float _health, _armor, _shield, _armorPeak;
	public static bool IsActive => _active != null;
	private AdventureCheckpoint(Unit? basePlayer)
	{
		_restoreMeta = MetaProgress.CaptureRollback(); _restoreCards = AbilityStore.CaptureRollback();
		_restoreRune = RuneCarry.CaptureRollback(); _restoreLoadout = LoadoutMemory.CaptureRollback();
		_character = RunSetup.Character; _weapon = RunSetup.Weapon;
		if (basePlayer != null && GodotObject.IsInstanceValid(basePlayer))
		{
			_baseTransform = basePlayer.GlobalTransform;
			_health = basePlayer.Health; _armor = basePlayer.Armor; _shield = basePlayer.Shield; _armorPeak = basePlayer.ArmorPeak;
		}
		foreach (string path in new[] { "user://meta.cfg", AbilityStore.SavePath, "user://rune.cfg", LoadoutMemory.SavePath })
		{
			string absolute = ProjectSettings.GlobalizePath(path);
			_files[absolute] = Disk.Exists(absolute) ? Disk.ReadAllBytes(absolute) : null;
		}
	}
	public static void Capture(Unit? basePlayer = null) { _active = new AdventureCheckpoint(basePlayer); _returning = null; }
	public static void EnsureCaptured() { if (_active == null) Capture(); }
	public static void Complete() { _active = null; _returning = null; }
	public static bool TryRestore(out string error)
	{
		error = string.Empty;
		if (_active == null) { error = "没有可恢复的出击记录。"; return false; }
		Dictionary<string, byte[]?>? current = null;
		try
		{
			current = _active._files.Keys.ToDictionary(path => path, path => Disk.Exists(path) ? Disk.ReadAllBytes(path) : null);
			foreach (var pair in _active._files) RestoreFile(pair.Key, pair.Value);
		}
		catch (Exception ex)
		{
			if (current != null)
				foreach (var pair in current)
					try { RestoreFile(pair.Key, pair.Value); }
					catch (Exception rollback) { GameLog.Write("Flow", $"取消冒险恢复当前存档失败：{pair.Key}：{rollback.Message}"); }
			error = "未能恢复出击前进度，请稍后重试。"; GameLog.Write("Flow", $"取消冒险回退失败：{ex.Message}"); return false;
		}
		_active._restoreMeta(); _active._restoreCards(); _active._restoreRune(); _active._restoreLoadout();
		RunSetup.Character = _active._character; RunSetup.Weapon = _active._weapon; RunSetup.Clear();
		_returning = _active; _active = null;
		return true;
	}
	private static void RestoreFile(string path, byte[]? bytes)
	{
		if (bytes == null) { if (Disk.Exists(path)) Disk.Delete(path); return; }
		if (Disk.Exists(path) && Disk.ReadAllBytes(path).SequenceEqual(bytes)) return;
		// 临时文件与目标同目录，避免写到一半留下损坏存档。
		string pending = path + ".adventure-restore";
		Disk.WriteAllBytes(pending, bytes); Disk.Move(pending, path, true);
	}
	public static void RestoreBasePlayer(Unit player)
	{
		if (_returning == null) return;
		if (_returning._baseTransform is { } transform)
		{
			player.GlobalTransform = transform; player.Velocity = Vector3.Zero; player.ResetPhysicsInterpolation();
			player.RestoreAdventureVitals(_returning._health, _returning._armor, _returning._shield, _returning._armorPeak);
		}
		_returning = null;
	}
}

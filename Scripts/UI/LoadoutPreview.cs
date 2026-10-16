using ActRoguelike.AbilityCards;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Player;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.Visual;
using Godot;

namespace ActRoguelike.UI;

/// <summary>基地装备控制器；沿用旧节点名，真正的战斗配置与关卡共用PlayerLoadout。</summary>
public partial class LoadoutPreview : Node
{
	private readonly PlayerLoadout _loadout = new();
	private Unit _player = null!;
	private PlayerCharacterKind _character = (PlayerCharacterKind)(-1);
	private WeaponPresetKind _weapon = (WeaponPresetKind)(-1);
	private string _cards = string.Empty;
	public string PreviewModelPath => _player.GetNode<CharacterDisplay>("Appearance").CurrentModelPath;
	public int PreviewModelCount => _player.GetNode<CharacterDisplay>("Appearance").ModelCount;
	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		_player = GetParent<Unit>();
	}
	public override void _Process(double delta)
	{
		string cards = string.Empty;
		foreach (var row in AbilityStore.Rows) if (AbilityStore.IsActive(row.Id)) cards += $"{row.Id}:{AbilityStore.LevelOf(row.Id)};";
		if (!_player.IsDead && (RunSetup.Character != _character || RunSetup.Weapon != _weapon || cards != _cards))
		{
			Apply(RunSetup.Character, RunSetup.Weapon);
			_cards = cards;
		}
	}
	public void Apply(PlayerCharacterKind character, WeaponPresetKind weapon)
	{
		if (RunController.CharacterRow(character) is not { } row) return;
		if (!LoadoutSelection.Allows(row, RunController.WeaponRow(weapon)))
		{
			if (LoadoutSelection.FirstWeapon(row) is not { } fallback) return;
			weapon = (WeaponPresetKind)(fallback.Slot - 1);
			RunSetup.Weapon = weapon;
		}
		_character = character;
		_weapon = weapon;
		_loadout.Apply(_player, this, character, weapon);
	}
}

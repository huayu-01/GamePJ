using ActRoguelike.Config;
using ActRoguelike.Run;
using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Interaction;

/// <summary>一个实例代表一个表内角色，直接切换并只打开该角色的武器列表。</summary>
public partial class CharacterStand : Interactable
{
	[Export] public CharacterConfig Character { get; set; } = null!;
	private string _weaponId = string.Empty;
	public override void _Ready()
	{
		SingleUse = false;
		Prompt = Character.DisplayName;
		base._Ready();
		GetNode<Label3D>("Name").Text = Character.DisplayName;
		GetNode<Label3D>("Name").Visible = true;
	}
	public override void _Process(double delta)
	{
		if (LoadoutMemory.WeaponFor(Character) is not { } weapon || weapon.Id == _weaponId) return;
		_weaponId = weapon.Id;
		GetNode<ActRoguelike.Visual.CharacterDisplay>("Visual").Apply(Character, weapon);
		GetNode<Label3D>("Name").Text = $"{Character.DisplayName}\n{weapon.DisplayName}";
	}
	protected override bool OnInteract()
	{
		if (!LoadoutSelection.SelectCharacter(Character)) return false;
		return UiService.Instance?.OpenLoadout(kind => LoadoutSelection.SelectWeapon(RunSetup.Character, kind)) == true;
	}
}

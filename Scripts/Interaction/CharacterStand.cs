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
		Prompt = Character?.DisplayName ?? "未配置角色";
		Enabled = Character != null;
		base._Ready();
		if (Nameplate is { } label)
		{
			label.Text = Prompt;
			label.Visible = true;
		}
	}
	public override void _Process(double delta)
	{
		if (Character == null || LoadoutMemory.WeaponFor(Character) is not { } weapon || weapon.Id == _weaponId) return;
		_weaponId = weapon.Id;
		(GetNodeOrNull<ActRoguelike.Visual.CharacterDisplay>("TypeModel/Visual") ?? GetNodeOrNull<ActRoguelike.Visual.CharacterDisplay>("Visual"))?.Apply(Character, weapon);
		if (Nameplate is { } label) label.Text = $"{Character.DisplayName}\n{weapon.DisplayName}";
	}
	private Label3D? Nameplate => GetNodeOrNull<Label3D>("TypeModel/Name") ?? GetNodeOrNull<Label3D>("Name");
	protected override bool OnInteract()
	{
		if (!LoadoutSelection.SelectCharacter(Character)) return false;
		return UiService.Instance?.OpenLoadout(kind => LoadoutSelection.SelectWeapon(RunSetup.Character, kind)) == true;
	}
}

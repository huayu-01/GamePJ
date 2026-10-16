using ActRoguelike.Config;
using ActRoguelike.Interaction;
using Godot;

namespace ActRoguelike.Run;

/// <summary>按角色表实例化交互预制体，新增角色无需改场景或代码。</summary>
public partial class CharacterRoster : Node3D
{
	[Export] public PackedScene StandScene { get; set; } = null!;
	[Export] public float Spacing { get; set; } = 6;
	public override void _Ready()
	{
		int count = ConfigIndex.Settings.Characters.Length;
		for (int i = 0; i < count; i++)
		{
			CharacterConfig row = ConfigIndex.Settings.Characters[i];
			if (row == null) continue;
			var stand = StandScene.Instantiate<CharacterStand>();
			stand.Character = row;
			stand.GetNode<ActRoguelike.Visual.CharacterDisplay>("Visual").FixedCharacter = row;
			stand.Name = row.Id;
			stand.Position = Vector3.Right * ((i - (count - 1) * 0.5f) * Spacing);
			AddChild(stand);
		}
	}
}

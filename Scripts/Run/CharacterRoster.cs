using ActRoguelike.Config;
using ActRoguelike.Interaction;
using Godot;

namespace ActRoguelike.Run;

/// <summary>按角色表创建展台宿主与交互组件，统一使用当前类型模型，不再依赖旧展台预制体。</summary>
public partial class CharacterRoster : Node3D
{
	[Export] public float Spacing { get; set; } = 6;
	public override void _Ready()
	{
		int count = ConfigIndex.Settings.Characters.Length;
		for (int i = 0; i < count; i++)
		{
			CharacterConfig row = ConfigIndex.Settings.Characters[i];
			if (row == null) continue;
			var stand = new Node3D
			{
				Name = row.Id,
				Position = Vector3.Right * ((i - (count - 1) * 0.5f) * Spacing),
			};
			stand.AddChild(new InteractionComponent { Name = "InteractionComponent", Kind = InteractionKind.Character, Character = row });
			AddChild(stand);
		}
	}
}

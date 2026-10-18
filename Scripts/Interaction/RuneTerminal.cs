using ActRoguelike.UI;
using Godot;

namespace ActRoguelike.Interaction;

/// <summary>基地独立符文台，复用靠近后按交互键的流程。</summary>
public partial class RuneTerminal : Interactable
{
	public override void _Ready()
	{
		SingleUse = false; Prompt = "符文台";
		var material = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.8f, 0.95f), EmissionEnabled = true, Emission = new Color(0.12f, 0.35f, 0.5f) };
		AddChild(new MeshInstance3D { Name = "Mesh", Position = Vector3.Up, Mesh = new PrismMesh { Size = new Vector3(1.4f, 2, 1.4f), Material = material } });
		base._Ready();
	}
	protected override bool OnInteract() => UiService.Instance?.OpenRunes() == true;
}

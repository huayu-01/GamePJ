using System.Collections.Generic;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Visual;
using Godot;

namespace ActRoguelike.Interaction;

/// <summary>交互类型的统一造型；各类型有独立几何，编辑器预览和运行时共用。</summary>
[Tool]
public partial class InteractionModel : Node3D
{
	private static readonly Dictionary<InteractionKind, ArrayMesh> Shapes = new();
	private static readonly Dictionary<Color, StandardMaterial3D> Materials = new();
	private MeshInstance3D _core = null!, _ring = null!;
	private float _phase, _height;
	private InteractionKind _kind;
	private Color? _ringColor;
	public static InteractionModel Create(InteractionKind kind, CharacterConfig? character = null, bool preview = false)
	{
		var model = new InteractionModel { Name = "TypeModel", _kind = kind };
		Color color = ColorFor(kind);
		model.AddChild(new MeshInstance3D
		{
			Name = "Plinth", Position = Vector3.Up * .14f,
			Mesh = new CylinderMesh { TopRadius = .82f, BottomRadius = 1.02f, Height = .28f, RadialSegments = 8 },
			MaterialOverride = MaterialFor(new Color(.16f, .21f, .27f)),
		});
		model._ring = new MeshInstance3D
		{
			Name = "Ring", Position = Vector3.Up * .3f,
			Mesh = new TorusMesh { InnerRadius = .72f, OuterRadius = .81f, Rings = 24, RingSegments = 6 },
			MaterialOverride = MaterialFor(color), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		model.AddChild(model._ring);
		model._height = kind is InteractionKind.BossDoor or InteractionKind.Expedition ? 1.6f : kind == InteractionKind.Character ? .35f : 1.3f;
		if (!Shapes.TryGetValue(kind, out ArrayMesh? shape)) Shapes[kind] = shape = BuildShape(kind);
		model._core = new MeshInstance3D { Name = "Mesh", Position = Vector3.Up * model._height, Mesh = shape, MaterialOverride = MaterialFor(color) };
		model.AddChild(model._core);
		if (kind == InteractionKind.Character && character != null)
		{
			var label = new Label3D { Name = "Name", Position = Vector3.Up * 3.1f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 38, OutlineSize = 8, Text = character.DisplayName };
			model.AddChild(label);
			// 编辑器仅展示台座，避免为预览运行角色动画与装备业务。
			if (!preview && !string.IsNullOrEmpty(character.ModelPath))
			{
				var display = new CharacterDisplay
				{
					Name = "Visual", FixedCharacter = character, Rotation = new Vector3(0, Mathf.Pi, 0),
					GripScene = RuntimeResources.Load<PackedScene>("res://Scenes/Visual/WeaponGrip.tscn"),
				};
				Node3D body = RuntimeResources.Load<PackedScene>(character.ModelPath).Instantiate<Node3D>();
				body.Name = "Model"; body.Transform = character.ModelTransform; body.Position += Vector3.Up * .3f;
				display.AddChild(body); model.AddChild(display);
			}
		}
		return model;
	}
	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() || GetParent() is not Interactable owner) return;
		if (owner is RewardNode reward) SetRingColor(reward.PresentationColor);
		if (_kind is InteractionKind.BossDoor or InteractionKind.Expedition or InteractionKind.Character) return;
		_phase += (float)delta * (owner.CanInteract ? 2.6f : 1.4f);
		Vector3 position = _core.Position;
		position.Y = _height + Mathf.Sin(_phase) * .08f;
		_core.Position = position;
	}
	private void SetRingColor(Color color)
	{
		if (_ringColor == color) return;
		_ringColor = color; _ring.MaterialOverride = MaterialFor(color);
	}
	private static StandardMaterial3D MaterialFor(Color color)
	{
		if (Materials.TryGetValue(color, out StandardMaterial3D? material)) return material;
		return Materials[color] = new StandardMaterial3D
		{
			AlbedoColor = color, Roughness = .65f, Metallic = .2f,
			EmissionEnabled = true, Emission = color, EmissionEnergyMultiplier = .35f,
		};
	}
	private static Color ColorFor(InteractionKind kind) => kind switch
	{
		InteractionKind.Enhancement => new(.3f, .7f, 1f), InteractionKind.Evolution => new(.65f, .4f, 1f),
		InteractionKind.Mutation => new(1f, .3f, .55f), InteractionKind.Resource => new(1f, .75f, .3f),
		InteractionKind.Heal => new(.3f, .95f, .6f), InteractionKind.Special => new(1f, .9f, .55f),
		InteractionKind.Shop => new(1f, .55f, .25f), InteractionKind.BossDoor => new(.95f, .22f, .25f),
		InteractionKind.Exit => new(.3f, 1f, .8f), InteractionKind.Expedition => new(.4f, .75f, 1f),
		InteractionKind.AbilityCards => new(.55f, .9f, .5f), InteractionKind.Loadout => new(.65f, .7f, 1f),
		InteractionKind.Runes => new(.45f, .9f, 1f), InteractionKind.Help => new(1f, .85f, .4f),
		InteractionKind.Character => new(.7f, .85f, .95f), _ => new(.35f, 1f, .65f),
	};
	private static ArrayMesh BuildShape(InteractionKind kind)
	{
		using var tool = new SurfaceTool(); tool.Begin(Mesh.PrimitiveType.Triangles);
		void Part(Mesh mesh, Vector3 position, Vector3 rotation = default)
			=> tool.AppendFrom(mesh, 0, new Transform3D(Basis.FromEuler(rotation), position));
		void Box(Vector3 size, Vector3 position, float roll = 0)
			=> Part(new BoxMesh { Size = size }, position, new Vector3(0, 0, roll));
		void Ring(float inner, float outer, Vector3 position, Vector3 rotation)
			=> Part(new TorusMesh { InnerRadius = inner, OuterRadius = outer, Rings = 20, RingSegments = 6 }, position, rotation);
		void Arrow(Vector3 offset)
		{
			Box(new Vector3(.22f, .85f, .25f), offset);
			Part(new PrismMesh { Size = new Vector3(.9f, .5f, .28f) }, offset + Vector3.Up * .5f);
		}
		switch (kind)
		{
			case InteractionKind.Enhancement:
				for (int i = 0; i < 3; i++) Box(new Vector3(1.2f - i * .3f, .22f, .45f), new Vector3(0, i * .38f - .4f, 0));
				break;
			case InteractionKind.Evolution: Arrow(Vector3.Zero); Box(new Vector3(1.2f, .16f, .4f), Vector3.Down * .65f); break;
			case InteractionKind.Mutation:
				for (int i = 0; i < 3; i++) Part(new PrismMesh { Size = new Vector3(.45f, 1.4f, .4f) }, new Vector3((i - 1) * .45f, i == 1 ? .2f : -.15f, 0), new Vector3(0, 0, (1 - i) * .45f));
				break;
			case InteractionKind.Resource:
				Box(Vector3.One * .6f, new Vector3(-.35f, -.3f, 0), .1f); Box(Vector3.One * .6f, new Vector3(.35f, -.3f, 0), -.1f); Box(Vector3.One * .6f, Vector3.Up * .35f, .1f); break;
			case InteractionKind.Heal: Box(new Vector3(.35f, 1.45f, .35f), Vector3.Zero); Box(new Vector3(1.25f, .35f, .35f), Vector3.Zero); break;
			case InteractionKind.Special:
				for (int i = 0; i < 3; i++) Box(new Vector3(.23f, 1.6f, .3f), Vector3.Zero, i * Mathf.Pi / 3); break;
			case InteractionKind.Shop:
				Box(new Vector3(1.5f, .75f, .8f), Vector3.Down * .2f); Part(new CylinderMesh { TopRadius = .45f, BottomRadius = .45f, Height = 1.5f, RadialSegments = 8 }, Vector3.Up * .3f, new Vector3(0, 0, Mathf.Pi / 2)); Box(new Vector3(.2f, .3f, .15f), new Vector3(0, .05f, .45f)); break;
			case InteractionKind.BossDoor:
				Box(new Vector3(.35f, 2.5f, .5f), Vector3.Left * 1.05f); Box(new Vector3(.35f, 2.5f, .5f), Vector3.Right * 1.05f); Box(new Vector3(2.4f, .35f, .5f), Vector3.Up * 1.2f);
				Part(new PrismMesh { Size = new Vector3(.6f, .75f, .45f) }, new Vector3(-1, 1.65f, 0)); Part(new PrismMesh { Size = new Vector3(.6f, .75f, .45f) }, new Vector3(1, 1.65f, 0)); break;
			case InteractionKind.Exit: Ring(.7f, .95f, Vector3.Zero, new Vector3(Mathf.Pi / 2, 0, 0)); Arrow(Vector3.Zero); break;
			case InteractionKind.Expedition:
				Box(new Vector3(.22f, 2.55f, .45f), Vector3.Left * 1.15f, -.12f); Box(new Vector3(.22f, 2.55f, .45f), Vector3.Right * 1.15f, .12f); Arrow(Vector3.Up * .1f); break;
			case InteractionKind.AbilityCards:
				for (int i = 0; i < 3; i++) Box(new Vector3(.65f, 1.2f, .12f), new Vector3((i - 1) * .48f, -.1f * Mathf.Abs(i - 1), -.1f * i), (1 - i) * .3f); break;
			case InteractionKind.Loadout:
				for (int i = -1; i <= 1; i += 2) { Box(new Vector3(.18f, 1.9f, .2f), Vector3.Zero, i * .6f); Box(new Vector3(.75f, .16f, .25f), new Vector3(i * .42f, -.55f, 0), i * .6f); } break;
			case InteractionKind.Runes: Ring(.42f, .57f, Vector3.Left * .28f, new Vector3(Mathf.Pi / 2, 0, .4f)); Ring(.42f, .57f, Vector3.Right * .28f, new Vector3(Mathf.Pi / 2, .7f, -.4f)); break;
			case InteractionKind.Help:
				Ring(.32f, .5f, Vector3.Up * .3f, new Vector3(Mathf.Pi / 2, 0, 0)); Box(new Vector3(.2f, .45f, .22f), new Vector3(.2f, -.1f, 0), -.4f); Part(new SphereMesh { Radius = .13f, Height = .26f, RadialSegments = 8, Rings = 4 }, Vector3.Down * .65f); break;
			case InteractionKind.Character: Part(new CylinderMesh { TopRadius = .55f, BottomRadius = .65f, Height = .12f, RadialSegments = 12 }, Vector3.Zero); break;
			default:
				Part(new CylinderMesh { TopRadius = .55f, BottomRadius = .55f, Height = .25f, RadialSegments = 10 }, Vector3.Down * .3f); Box(new Vector3(.18f, 1.2f, .18f), Vector3.Up * .15f, -.5f); Part(new SphereMesh { Radius = .26f, Height = .52f, RadialSegments = 8, Rings = 4 }, new Vector3(.3f, .7f, 0)); break;
		}
		return tool.Commit();
	}
}

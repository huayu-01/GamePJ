using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Visual;

/// <summary>角色与握持武器的纯表现缓存。玩家与角色交互物复用，不创建单位或碰撞。</summary>
public partial class CharacterDisplay : Node3D
{
	[Export] public NodePath OriginalModelPath { get; set; } = "Model";
	[Export] public PackedScene GripScene { get; set; } = null!;
	[Export] public CharacterConfig? FixedCharacter { get; set; }
	private readonly Dictionary<string, Node3D> _models = new();
	private readonly Dictionary<Node3D, Node3D> _hands = new();
	private readonly Dictionary<(Node3D, string), Node3D> _weapons = new();
	private Node3D _original = null!;
	public string CurrentModelPath { get; private set; } = string.Empty;
	public string CurrentWeaponPath { get; private set; } = string.Empty;
	public Node3D? ActiveModel { get; private set; }
	public Node3D? EquippedWeapon { get; private set; }
	private readonly List<WeaponThrowAbility> _throws = new();
	public int ModelCount => _models.Count;
	public override void _Ready()
	{
		_original = GetNode<Node3D>(OriginalModelPath);
		if (GetParent() is Unit unit) NodeSearch.CollectInSubtree(unit, _throws);
		_models[_original.SceneFilePath] = _original;
		foreach (CharacterConfig row in FixedCharacter != null ? new[] { FixedCharacter } : ConfigIndex.Settings.Characters)
		{
			if (row == null) continue;
			Node3D model = _original;
			if (!string.IsNullOrEmpty(row.ModelPath) && !_models.TryGetValue(row.ModelPath, out model!))
			{
				model = RuntimeResources.Load<PackedScene>(row.ModelPath).Instantiate<Node3D>();
				model.Transform = row.ModelTransform;
				model.Position += _original.Position;
				model.Visible = false;
				// 提前预热技能库；不在第一次出招时向已经运行的播放器临时加库。
				if (NodeSearch.FindInSubtree<AnimationPlayer>(model) is { } player)
				{
					player.Autoplay = string.Empty;
				}
				AddChild(model);
				if (NodeSearch.FindInSubtree<AnimationPlayer>(model) is { } prepared)
					SkillAnimationLibrary.LoadInto(prepared, row.AnimLibraryName, row.AnimLibraryDir);
				_models[row.ModelPath] = model;
			}
			EnsureHand(model, row.WeaponGripScene);
		}
	}
	private Node3D EnsureHand(Node3D model, PackedScene? configuredGrip = null)
	{
		if (_hands.TryGetValue(model, out var hand)) return hand;
		var grip = (configuredGrip ?? GripScene).Instantiate<BoneAttachment3D>();
		if (NodeSearch.FindInSubtree<Skeleton3D>(model) is { } skeleton && skeleton.FindBone(grip.BoneName) >= 0)
			skeleton.AddChild(grip);
		else { grip.Free(); throw new System.InvalidOperationException($"角色模型缺少武器挂点：{model.SceneFilePath}"); }
		hand = grip.GetNode<Node3D>("Socket");
		_hands[model] = hand;
		return hand;
	}
	public void Apply(CharacterConfig character, WeaponConfig weapon)
	{
		CurrentModelPath = string.IsNullOrEmpty(character.ModelPath) ? _original.SceneFilePath : character.ModelPath;
		if (!_models.TryGetValue(CurrentModelPath, out var model)) return;
		foreach (var cached in _models.Values) cached.Visible = cached == model;
		ActiveModel = model;
		CurrentWeaponPath = weapon.ModelPath;
		EquippedWeapon = null;
		foreach (var cached in _weapons.Values) cached.Visible = false;
		if (!string.IsNullOrEmpty(weapon.ModelPath))
		{
			var key = (model, weapon.ModelPath);
			if (!_weapons.TryGetValue(key, out var visual))
			{
				visual = RuntimeResources.Load<PackedScene>(weapon.ModelPath).Instantiate<Node3D>();
				EnsureHand(model).AddChild(visual);
				_weapons[key] = visual;
			}
			visual.Visible = true;
			EquippedWeapon = visual;
		}
		if (GetParent() is Unit unit)
		{
			unit.GetNode<UnitAnimator>(UnitAnimator.NodeName).BindVisual(model);
			unit.GetNodeOrNull<UnitPresentation>("Presentation")?.BindVisual(model);
			unit.GetNodeOrNull<ChargeBodyFx>("ChargeBodyFx")?.BindVisual(model);
			if (unit.GetNodeOrNull<MeshInstance3D>("WeaponMount/Pistol/Body") is { } placeholder)
				placeholder.Visible = string.IsNullOrEmpty(weapon.ModelPath);
		}
		else if (NodeSearch.FindInSubtree<AnimationPlayer>(model) is { } player && player.HasAnimation("idle"))
		{
			player.GetAnimation("idle").LoopMode = Animation.LoopModeEnum.Linear;
			player.Play("idle");
		}
	}
	public override void _PhysicsProcess(double delta)
	{
		if (EquippedWeapon == null) return;
		bool away = false;
		foreach (var ability in _throws)
			if (IsInstanceValid(ability) && ability.Enabled && ability.InFlight) { away = true; break; }
		EquippedWeapon.Visible = !away;
	}
}

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
	private FallAttackAbility? _fall;
	private Transform3D _weaponRestTransform;
	public bool DivePoseActive { get; private set; }
	[Export] public Vector3 DiveGripOffset { get; set; } = new(0.25f, 0.9f, -0.2f);
	public int ModelCount => _models.Count;
	public override void _Ready()
	{
		_original = GetNode<Node3D>(OriginalModelPath);
		if (GetParent() is Unit unit)
		{
			NodeSearch.CollectInSubtree(unit, _throws);
			_fall = NodeSearch.FindInSubtree<FallAttackAbility>(unit);
		}
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
				_models[row.ModelPath] = model;
			}

			// 技能库**对"解析到的那个模型"加载**，不限于"新实例化"的那条分支：
			// 角色行 ModelPath 与场景里 `Model` 节点同一个资源时（例如 chr_hunter 直接指向 Player.tscn 的 Model），
			// 走的是去重分支、不会新建模型 —— 那时也必须把技能库装进这个播放器，否则技能动画查不到（`LoadInto` 幂等）。
			if (NodeSearch.FindInSubtree<AnimationPlayer>(model) is { } prepared)
			{
				SkillAnimationLibrary.LoadInto(prepared, row.AnimLibraryName, row.AnimLibraryDir);
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
		if (EquippedWeapon != null && IsInstanceValid(EquippedWeapon))
		{
			EquippedWeapon.TopLevel = false;
			EquippedWeapon.Transform = _weaponRestTransform;
		}
		DivePoseActive = false;
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
			_weaponRestTransform = visual.Transform;
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
		DivePoseActive = !away && _fall is { Enabled: true, IsDiving: true, HomingMode: FallAttackAbility.DiveMode.Crosshair };
		Vector3 worldScale = EquippedWeapon.GlobalBasis.Scale.Abs();
		EquippedWeapon.TopLevel = DivePoseActive;
		if (DivePoseActive)
		{
			// 只读真实下冲方向，把占位长枪的+Y枪尖朝向落点，不改骨架、速度或命中体。
			// 临时解除骨骼父变换，避免同帧稍后的骨骼更新再次转偏枪尖。
			Unit unit = _fall!.OwnerUnit!;
			EquippedWeapon.GlobalTransform = new Transform3D(new Basis(new Quaternion(Vector3.Up, _fall.LastDiveDirection)) * Basis.FromScale(worldScale),
				unit.GlobalPosition + unit.GlobalBasis * DiveGripOffset);
		}
		else EquippedWeapon.Transform = _weaponRestTransform;
	}
}

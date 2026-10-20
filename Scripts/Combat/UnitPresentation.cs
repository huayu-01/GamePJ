using System.Collections.Generic;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 受击表现：只监听 Unit 的 Gameplay 事件，不参与任何战斗结算（执行文档 §5.4）。
/// 命中闪色、护甲/护盾破碎提示、死亡后延迟销毁。
/// 另外保证单位身上有 <see cref="UnitAnimator"/>（动画切换本身在那个组件里，本文件只负责"它在位"）。
/// </summary>
public partial class UnitPresentation : Node
{
	[Export] public float FlashDuration { get; set; } = 0.09f;
	[Export] public Color FlashColor { get; set; } = new(1f, 0.55f, 0.45f);
	[Export] public float DeathLinger { get; set; } = 1.1f;
	[Export] public bool RemoveOnDeath { get; set; } = true;

	private readonly List<MeshInstance3D> _meshes = new();
	private Unit _unit = null!;
	private StandardMaterial3D _flashMaterial = null!;
	private double _flashTimer;
	private bool _flashing;
	private readonly Dictionary<MeshInstance3D, Material?> _baseMaterials = new();
	private readonly StandardMaterial3D _attackMaterial = new() { AlbedoColor = new Color(1, 0.85f, 0.25f), EmissionEnabled = true, Emission = new Color(1, 0.65f, 0.1f), EmissionEnergyMultiplier = 2 };
	private float _attackTimer;
	private MeshInstance3D? _armorAura;
	public int AttackWarningCount { get; private set; }
	public void FlashAttack() { AttackWarningCount++; _attackTimer = 0.12f; ApplyMaterial(); }
	public void SetBaseMaterial(Material material)
	{
		foreach (var mesh in _meshes) if (IsInstanceValid(mesh)) _baseMaterials[mesh] = material;
		ApplyMaterial();
	}
	private void ApplyMaterial()
	{
		foreach (var mesh in _meshes)
			if (IsInstanceValid(mesh)) mesh.MaterialOverride = _attackTimer > 0 ? _attackMaterial : _flashing ? _flashMaterial : _baseMaterials.GetValueOrDefault(mesh);
	}
	private void EnsureArmorAura()
	{
		if (!IsInstanceValid(_unit) || !_unit.IsInsideTree() || _unit.GetNodeOrNull<CollisionShape3D>("Body") == null) return;
		var size = ActRoguelike.World.MapManager.BodySize(_unit);
		Vector3 scale = _unit.GlobalTransform.Basis.Scale.Abs();
		float radius = size.Radius / Mathf.Max(0.01f, Mathf.Max(scale.X, scale.Z)) + 0.035f;
		float height = size.Height / Mathf.Max(0.01f, scale.Y) + 0.07f;
		_armorAura = new MeshInstance3D
		{
			Name = "ArmorAura", Position = Vector3.Up * (height * 0.5f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Mesh = new CapsuleMesh { Radius = radius, Height = Mathf.Max(radius * 2, height), Material = new StandardMaterial3D
			{
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(0.35f, 0.65f, 1, 0.18f),
				EmissionEnabled = true, Emission = new Color(0.25f, 0.5f, 1), EmissionEnergyMultiplier = 0.6f,
			} },
		};
		_unit.AddChild(_armorAura); _armorAura.Visible = _unit.Armor > 0;
	}

	public override void _Ready()
	{
		_unit = GetParent<Unit>();
		_unit.Damaged += OnDamaged;
		_unit.Died += OnDied;

		_flashMaterial = new StandardMaterial3D
		{
			AlbedoColor = FlashColor,
			EmissionEnabled = true,
			Emission = FlashColor,
			EmissionEnergyMultiplier = 1.6f,
		};

		CollectMeshes(_unit);

		// 动画组件：场景里挂了就用那个，**没挂就补一个** —— 这样"新增一个敌人"不需要手动挂节点，
		// 只要有 `Model/…/AnimationPlayer` 就自动按移速切 idle/walk/跑（读不到配置表也成立）。
		UnitAnimator.Ensure(_unit);
		// 收集模型之后再挂状态特效，避免受击闪色覆盖纯视觉节点。
		StatusEffectFx.Ensure(_unit);
		CallDeferred(nameof(EnsureArmorAura));
	}

	public override void _ExitTree()
	{
		if (IsInstanceValid(_unit))
		{
			_unit.Damaged -= OnDamaged;
			_unit.Died -= OnDied;
		}
	}

	public override void _Process(double delta)
	{
		if (IsInstanceValid(_armorAura)) _armorAura!.Visible = !_unit.IsDead && _unit.Armor > 0;
		bool wasFlashing = _flashing; bool wasAttack = _attackTimer > 0;
		_flashTimer -= delta; _attackTimer = Mathf.Max(0, _attackTimer - (float)delta);
		_flashing = _flashTimer > 0;
		if (wasFlashing != _flashing || wasAttack != (_attackTimer > 0)) ApplyMaterial();
	}

	/// <summary>换模型后只收集该模型及其握持武器，排除单位状态特效。</summary>
	public void BindVisual(Node model)
	{
		foreach (var mesh in _meshes) if (IsInstanceValid(mesh)) mesh.MaterialOverride = _baseMaterials.GetValueOrDefault(mesh);
		_flashing = false;
		_meshes.Clear();
		_baseMaterials.Clear();
		CollectMeshes(model);
	}

	private void CollectMeshes(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is MeshInstance3D mesh)
			{
				_meshes.Add(mesh);
				_baseMaterials[mesh] = mesh.MaterialOverride;
			}

			CollectMeshes(child);
		}
	}

	private void OnDamaged(DamageResult result, HitInstance hit)
	{
		if (result.FinalDamage <= 0f || _meshes.Count == 0)
		{
			return;
		}

		_flashing = true;
		_flashTimer = FlashDuration;
		ApplyMaterial();
	}

	private void OnDied(HitInstance hit)
	{
		if (!RemoveOnDeath)
		{
			return;
		}

		Tween tween = CreateTween();
		tween.TweenInterval(DeathLinger);
		tween.TweenCallback(Callable.From(_unit.QueueFree));
	}
}

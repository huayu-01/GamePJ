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
	private ShaderMaterial _flashMaterial = null!;
	private double _flashTimer;
	private bool _flashing;
	private readonly ShaderMaterial _attackMaterial = UnitRimOverlay.Create(new Color(1, .8f, .2f), .7f, 1.8f);
	private float _attackTimer;
	private MeshInstance3D? _armorAura;
	public int AttackWarningCount { get; private set; }
	public void FlashAttack() { AttackWarningCount++; _attackTimer = 0.12f; ApplyMaterial(); SetProcess(true); }
	private void SyncArmorAura() { if (IsInstanceValid(_armorAura)) _armorAura!.Visible = !_unit.IsDead && _unit.Armor > 0; }
	private void ApplyMaterial()
	{
		foreach (var mesh in _meshes)
			if (IsInstanceValid(mesh)) UnitRimOverlay.Set(mesh, this,
				_attackTimer > 0 ? _attackMaterial : _flashing ? _flashMaterial : null, _attackTimer > 0 ? 60 : 50);
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
			Mesh = new CapsuleMesh { Radius = radius, Height = Mathf.Max(radius * 2, height),
				Material = UnitRimOverlay.Create(new Color(.35f, .65f, 1), .38f, .8f) },
		};
		_unit.AddChild(_armorAura); SyncArmorAura();
	}

	public override void _Ready()
	{
		_unit = GetParent<Unit>();
		_unit.Died += OnDied;
		_unit.ArmorChanged += SyncArmorAura;
		SetProcess(false);

		_flashMaterial = UnitRimOverlay.Create(FlashColor, .65f, 1.6f);

		CollectMeshes(_unit);

		// 动画组件：场景里挂了就用那个，**没挂就补一个** —— 这样"新增一个敌人"不需要手动挂节点，
		// 只要有 `Model/…/AnimationPlayer` 就自动按移速切 idle/walk/跑（读不到配置表也成立）。
		UnitAnimator.Ensure(_unit);
		// 收集模型之后再挂状态特效，避免受击闪色覆盖纯视觉节点。
		StatusEffectFx.Ensure(_unit);
		CallDeferred(nameof(EnsureArmorAura));
		CallDeferred(nameof(ApplyArtStyle));
	}
	private void ApplyArtStyle()
	{
		if (!IsInstanceValid(_unit) || !_unit.IsInsideTree()) return;
		Node? model = _unit.GetNodeOrNull<ActRoguelike.Visual.CharacterDisplay>("Appearance")?.ActiveModel ?? _unit.GetNodeOrNull<Node>("Model");
		if (model != null) UnitArtStyle.Ensure(_unit).Bind(model);
	}

	public override void _ExitTree()
	{
		UnitRimOverlay.RemoveSource(this);
		if (IsInstanceValid(_unit))
		{
			_unit.Died -= OnDied;
			_unit.ArmorChanged -= SyncArmorAura;
		}
	}

	public override void _Process(double delta)
	{
		using var frameProfile=ActRoguelike.Core.RuntimeFrameProfiler.Measure("Presentation");
		bool wasFlashing = _flashing; bool wasAttack = _attackTimer > 0;
		_flashTimer -= delta; _attackTimer = Mathf.Max(0, _attackTimer - (float)delta);
		_flashing = _flashTimer > 0;
		if (wasFlashing != _flashing || wasAttack != (_attackTimer > 0)) ApplyMaterial();
		if (!_flashing && _attackTimer <= 0) SetProcess(false);
	}

	/// <summary>换模型后只收集该模型及其握持武器，排除单位状态特效。</summary>
	public void BindVisual(Node model)
	{
		if (_unit != null && _unit.IsInsideTree()) UnitArtStyle.Ensure(_unit).Bind(model);
		UnitRimOverlay.RemoveSource(this);
		_flashing = false;
		_flashTimer = 0; _attackTimer = 0;
		SetProcess(false);
		_meshes.Clear();
		CollectMeshes(model);
	}

	private void CollectMeshes(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			// 攻击预警有独立阵营配色，不能被身体闪色或精英底材染色。
			if (child is SkillIndicator or EnemyAttackWarning or StatusEffectFx or ChargeBodyFx or ActRoguelike.Visual.UnitMotionFx || child.Name == "ArmorAura") continue;
			if (child is MeshInstance3D mesh)
			{
				_meshes.Add(mesh);
			}

			CollectMeshes(child);
		}
	}

	public void ShowImpact(ImpactKind kind, float duration)
	{
		if (_meshes.Count == 0)
		{
			return;
		}

		_flashing = true;
		_flashTimer = Mathf.Max(0, duration);
		Color color = CombatFeedback.ColorFor(kind);
		_flashMaterial.SetShaderParameter("rim_color", color);
		ApplyMaterial();
		SetProcess(true);
	}

	private void OnDied(HitInstance hit)
	{
		SyncArmorAura();
		if (!RemoveOnDeath)
		{
			return;
		}

		Tween tween = CreateTween();
		tween.TweenInterval(DeathLinger);
		tween.TweenCallback(Callable.From(_unit.QueueFree));
	}
}

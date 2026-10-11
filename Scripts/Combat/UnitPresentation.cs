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
		if (!_flashing)
		{
			return;
		}

		_flashTimer -= delta;
		if (_flashTimer <= 0d)
		{
			_flashing = false;
			foreach (MeshInstance3D mesh in _meshes)
			{
				if (IsInstanceValid(mesh))
				{
					mesh.MaterialOverride = null;
				}
			}
		}
	}

	private void CollectMeshes(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is MeshInstance3D mesh)
			{
				_meshes.Add(mesh);
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

		foreach (MeshInstance3D mesh in _meshes)
		{
			mesh.MaterialOverride = _flashMaterial;
		}

		_flashing = true;
		_flashTimer = FlashDuration;
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

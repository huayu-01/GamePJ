using System;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 统一攻击碰撞体：近战挥砍、AOE、持续区域、旋转攻击共用（战斗规则说明 §7）。
/// 激活期间每个物理帧检查重叠单位，按 Hit Policy 过滤后进入统一 Hit Resolution。
/// 形状、位置、伤害与命中策略都在预制体/Inspector 里配置。
/// </summary>
public partial class AttackHitbox : Area3D
{
	[Export] public float Damage { get; set; } = 10f;
	[Export] public float Knockback { get; set; } = 3f;
	[Export] public HitPolicy Policy { get; set; } = HitPolicy.OncePerTarget;
	[Export] public float HitInterval { get; set; } = 0.2f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float CritChance { get; set; }
	[Export] public float CritMultiplier { get; set; } = 2f;
	[Export] public string[] AttackTags { get; set; } = System.Array.Empty<string>();

	/// <summary>命中后附加的状态 Id（见 StatusPresets），留空则不附加。</summary>
	[Export] public string StatusId { get; set; } = string.Empty;

	[Export] public float StatusDuration { get; set; } = 3f;

	private readonly HitTracker _tracker = new();
	private AttackInstance? _attack;
	private bool _active;
	private int _phase;

	public Unit? OwnerUnit { get; private set; }

	/// <summary>
	/// 命中体向前能够到的距离 = 形状半长 + 命中体中心相对拥有者前方的偏移。
	/// 行为用它在“真的够得着”的距离才发动攻击，否则会停在射程边缘空挥。
	/// </summary>
	public float Reach
	{
		get
		{
			if (_reach < 0f)
			{
				_reach = ComputeReach();
			}

			return _reach;
		}
	}

	private float _reach = -1f;

	/// <summary>本次激活中命中到目标时触发（冲锋用来“命中即停止”）。</summary>
	public event Action<Unit>? HitLanded;

	public override void _Ready()
	{
		// 常开监测：反复切换 monitoring 会让“一直静止在范围内”的单位不进重叠列表。
		Monitoring = true;
	}

	private float ComputeReach()
	{
		float halfLength = 0f;
		foreach (Node child in GetChildren())
		{
			if (child is CollisionShape3D shapeNode && shapeNode.Shape != null)
			{
				halfLength = shapeNode.Shape switch
				{
					BoxShape3D box => box.Size.Z * 0.5f,
					SphereShape3D sphere => sphere.Radius,
					CapsuleShape3D capsule => (capsule.Height * 0.5f) + capsule.Radius,
					CylinderShape3D cylinder => cylinder.Height * 0.5f,
					_ => 0f,
				};
				break;
			}
		}

		// 命中体通常挂在单位前方，用与拥有者的水平距离近似这段前伸量。
		float offset = 0f;
		Node? node = GetParent();
		while (node != null && node is not Unit)
		{
			node = node.GetParent();
		}

		if (node is Unit owner)
		{
			Vector3 delta = GlobalPosition - owner.GlobalPosition;
			delta.Y = 0f;
			offset = delta.Length();
		}

		return Mathf.Max(0.2f, offset + halfLength);
	}

	/// <summary>owner 为攻击来源；attack 为空时按本节点上的导出参数生成攻击快照。</summary>
	public void Activate(Unit? owner, AttackInstance? attack = null, int phase = 0)
	{
		OwnerUnit = owner;
		_phase = phase;
		_attack = attack ?? new AttackInstance
		{
			Attacker = owner,
			Ability = "Hitbox",
			BaseDamage = Damage,
			CanCrit = CritChance > 0f,
			CritChance = CritChance,
			CritMultiplier = CritMultiplier,
			Knockback = Knockback,
			Policy = Policy,
			HitInterval = HitInterval,
			Tags = AttackTags,
			Status = StatusPresets.Create(StatusId, StatusDuration),
		};
		_tracker.Reset();
		_active = true;
	}

	public void Deactivate()
	{
		_active = false;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_active || _attack == null)
		{
			return;
		}

		float now = Time.GetTicksMsec() / 1000f;
		foreach (Node3D node in GetOverlappingBodies())
		{
			if (node is not Unit target || !target.IsValidTarget)
			{
				continue;
			}

			if (OwnerUnit != null && (target == OwnerUnit || !OwnerUnit.IsHostileTo(target)))
			{
				continue;
			}

			if (!_tracker.TryRegister(target, _attack.Policy, _attack.HitInterval, now, _phase))
			{
				continue;
			}

			Vector3 direction = target.GlobalPosition - GlobalPosition;
			direction.Y = 0f;
			target.ReceiveHit(_attack.RollHit(target, direction.Normalized(), target.GlobalPosition));

			// 击中特效（Binbun Hit FX）：命中体判定的是"重叠"、手上没有表面命中点，
			// 而单位原点在**脚底** —— 直接用目标原点会把火花打在地上，所以抬到胸口高度。
			// 抬升量读 `Balance.tres` 的「表现 · 命中特效 · 高度偏移」，不写死在这里。
			HitEffect.Spawn(this, target.GlobalPosition + (Vector3.Up * Balance.Settings.HitEffectHeightOffset));

			HitLanded?.Invoke(target);
		}
	}
}

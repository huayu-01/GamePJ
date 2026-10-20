using System;
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
	[Export] public float FeedbackWeight { get; set; } = 1f;
	/// <summary>脱手投掷即使仍挂在单位子树，也不能让远程命中暂停出手者。</summary>
	[Export] public bool ContactFeedbackEnabled { get; set; } = true;
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
	/// <summary>最近激活的攻击快照，只读供战斗验收与诊断。</summary>
	public AttackInstance? CurrentAttack => _attack;
	private bool _active;
	private int _phase;
	private bool _contact;

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
		Node? ancestor = GetParent();
		while (ancestor != null && ancestor is not Unit) ancestor = ancestor.GetParent();
		ConfigureCollision(ancestor as Unit);
		Monitorable = false;
		// 常开监测：反复切换 monitoring 会让“一直静止在范围内”的单位不进重叠列表。
		Monitoring = true;
	}

	private void ConfigureCollision(Unit? owner)
	{
		CollisionLayer = owner == null ? 0 : CombatLayers.HitboxOf(owner.Faction);
		CollisionMask = owner == null ? CombatLayers.UnitMask : CombatLayers.HostileTo(owner.Faction);
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
		_contact = ContactFeedbackEnabled && this is not Explosion && owner != null && owner.IsAncestorOf(this);
		ConfigureCollision(owner);
		_phase = phase;
		AttackOffense offense = AttackOffense.Capture(owner, CritChance, CritMultiplier);
		bool player = owner?.Faction == Faction.Player;
		_attack = attack ?? new AttackInstance
		{
			Attacker = owner,
			Ability = "Hitbox",
			FeedbackWeight = FeedbackWeight,
			BaseDamage = Damage * (player ? offense.DamageMultiplier : 1f),
			CanCrit = player || CritChance > 0f,
			CritChance = player ? offense.CritChance : CritChance,
			CritMultiplier = player ? offense.CritMultiplier : CritMultiplier,
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

	private ConvexPolygonShape3D? _sweepShape;
	/// <summary>平移Box的凸包扫掠，覆盖高速往返两帧之间的整个攻击体积；共享命中追踪。</summary>
	public void SweepBox(Vector3 from, Vector3 to)
	{
		if (!_active || _attack == null) return;
		foreach (Node child in GetChildren())
		{
			if (child is not CollisionShape3D { Shape: BoxShape3D box, Disabled: false } shape) continue;
			var points = new Vector3[16];
			Vector3 half = box.Size * 0.5f;
			for (int i = 0; i < 8; i++)
			{
				Vector3 corner = shape.GlobalBasis * new Vector3((i & 1) == 0 ? -half.X : half.X, (i & 2) == 0 ? -half.Y : half.Y, (i & 4) == 0 ? -half.Z : half.Z);
				points[i] = corner;
				points[i + 8] = corner + to - from;
			}
			_sweepShape ??= new ConvexPolygonShape3D();
			_sweepShape.Points = points;
			using var query = new PhysicsShapeQueryParameters3D
			{
				Shape = _sweepShape, Transform = new Transform3D(Basis.Identity, shape.GlobalPosition + from - to),
				CollisionMask = CollisionMask, CollideWithAreas = false, CollideWithBodies = true
			};
			foreach (var hit in GetWorld3D().DirectSpaceState.IntersectShape(query, 128))
				if (hit["collider"].AsGodotObject() is Unit target) HitTarget(target);
		}
	}

	private CapsuleShape3D? _sphereSweep;
	/// <summary>球体平移的精确胶囊扫掠，覆盖高速下冲的两帧间隙；与Area共用命中间隔。</summary>
	public void SweepSphere(Vector3 from, Vector3 to)
	{
		if (!_active || _attack == null) return;
		Vector3 travel = to - from;
		float length = travel.Length();
		foreach (Node child in GetChildren())
		{
			if (child is not CollisionShape3D { Shape: SphereShape3D sphere, Disabled: false } shape) continue;
			Vector3 scale = shape.GlobalBasis.Scale.Abs();
			float radius = sphere.Radius * Mathf.Max(scale.X, Mathf.Max(scale.Y, scale.Z));
			_sphereSweep ??= new CapsuleShape3D();
			_sphereSweep.Radius = radius;
			_sphereSweep.Height = length + 2 * radius;
			Basis basis = length > 0.00001f ? new Basis(new Quaternion(Vector3.Up, travel / length)) : Basis.Identity;
			using var query = new PhysicsShapeQueryParameters3D
			{
				Shape = _sphereSweep, Transform = new Transform3D(basis, shape.GlobalPosition - travel * 0.5f),
				CollisionMask = CollisionMask, CollideWithAreas = false, CollideWithBodies = true
			};
			foreach (var hit in GetWorld3D().DirectSpaceState.IntersectShape(query, 128))
				if (hit["collider"].AsGodotObject() is Unit target) HitTarget(target);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_active || _attack == null) return;
		if (_contact && OwnerUnit?.ImpactPause?.IsHolding == true) return;
		foreach (Node3D node in GetOverlappingBodies())
			if (node is Unit target) HitTarget(target);
	}

	private void HitTarget(Unit target)
	{
		if (_attack == null) return;
		float now = Time.GetTicksMsec() / 1000f;
		if (!target.IsValidTarget)
		{
			return;
		}

		if (OwnerUnit != null && (target == OwnerUnit || !OwnerUnit.IsHostileTo(target)))
		{
			return;
		}

		if (!_tracker.TryRegister(target, _attack.Policy, _attack.HitInterval, now, _phase))
		{
			return;
		}

		// 命中点先算、再结算：一击致死时 `Unit.Die()` 会把 `CollisionLayer` 归零，顺序反了容易踩到
		// "目标已经进入死亡态"这类边界（本方法不读层，但先算后打更符合直觉）。
		Vector3 hitPoint = ResolveHitPoint(target);

		Vector3 direction = target.GlobalPosition - GlobalPosition;
		direction.Y = 0f;
		var hit = _attack.RollHit(target, direction.Normalized(), hitPoint);
		hit.ContactFeedback = _contact;
		target.ReceiveHit(hit);

		HitLanded?.Invoke(target);
	}
	/// <summary>
	/// **实际命中点** = 目标碰撞体表面上**离命中体最近**的那一点
	/// （用户要求：「实际应该在命中的位置发生特效」）。
	///
	/// <para><b>为什么是几何最近点，而不是打一条射线</b>：重叠判定本身不给接触点
	/// （`GetOverlappingBodies()` 与 `Area3D` 的信号都只有"撞上了谁"，没有 position），最初这里补的是
	/// 一条朝目标身体的射线 —— 但射线读的是**物理空间在查询那一瞬间**的状态，实测不稳：目标被脚本挪动、
	/// 刚被冻住、或刚被打死（<c>Unit.Die()</c> 把 <c>CollisionLayer</c> 归零）时，脚本位置与物理位置不一致，
	/// 射线会**静默打空**、特效退回身体中心（自测里 7 次跑出过 3 次打空）。几何最近点只依赖两边的
	/// **碰撞体本身**，与物理空间的时序无关；语义也更贴切 —— "这一刀落在身上的哪一面"本来就是
	/// "离刀最近的那一面"。</para>
	///
	/// <para><b>位置怎么算</b>：把命中体的世界坐标投到目标那个碰撞体上（单位预制体里的直接子节点
	/// `Body`）—— 胶囊：先投到中轴线段、再沿半径推到表面；球：沿半径推；盒：先转进局部空间、再夹到盒面上。
	/// <b>认不出的形状 / 没有碰撞体</b> → 退回**碰撞体中心**（同样是几何量，不是配置里的魔数）。</para>
	///
	/// <para>⚠️ 调用点在 <see cref="Unit.ReceiveHit"/> **之前**：一击致死时 <c>Unit.Die()</c> 会把
	/// <c>CollisionLayer</c> 归零（本方法不读层，但"先算命中点、再结算"这个顺序本身也更符合直觉）。</para>
	///
	/// <para>因此 `ConfigIndex.tres` 里那列 <c>HitEffectHeightOffset</c> 没有消费者了，已随本次改动删除：
	/// 高矮不同的单位各自把特效落在自己身上，不需要再调一个全局高度。</para>
	/// </summary>
	private Vector3 ResolveHitPoint(Unit target)
	{
		Vector3 from = GlobalPosition;
		foreach (Node child in target.GetChildren())
		{
			if (child is not CollisionShape3D { Disabled: false } shapeNode || shapeNode.Shape == null)
			{
				continue;
			}

			Transform3D shape = shapeNode.GlobalTransform;

			// 单位被整体缩放时（精英怪 `EliteModifier.ModelScale` 1.35）碰撞体也跟着缩放，
			// 形状自己的 radius/height 是不缩放的原始值 —— 所以半径要乘上变换的缩放量。
			float scale = shape.Basis.Y.Length();

			switch (shapeNode.Shape)
			{
				case CapsuleShape3D capsule:
				{
					Vector3 axis = shape.Basis.Y.Normalized();
					float radius = capsule.Radius * scale;
					float half = Mathf.Max(0f, (capsule.Height * 0.5f) - capsule.Radius) * scale;
					Vector3 center = shape.Origin;
					Vector3 projected = center + (axis * Mathf.Clamp((from - center).Dot(axis), -half, half));
					Vector3 offset = from - projected;
					return offset.LengthSquared() < 0.0001f
						? projected + (Vector3.Up * radius) // 几乎贴在中轴上：往上顶一个半径，别把特效塞进身体里
						: projected + (offset.Normalized() * radius);
				}

				case SphereShape3D sphere:
				{
					Vector3 offset = from - shape.Origin;
					float radius = sphere.Radius * scale;
					return offset.LengthSquared() < 0.0001f
						? shape.Origin + (Vector3.Up * radius)
						: shape.Origin + (offset.Normalized() * radius);
				}

				case BoxShape3D box:
				{
					// 转进盒的局部空间再夹到盒面上：夹完的那一点就是"离命中体最近的那一面"。
					Vector3 local = shape.AffineInverse() * from;
					Vector3 half = box.Size * 0.5f;
					var clamped = new Vector3(
						Mathf.Clamp(local.X, -half.X, half.X),
						Mathf.Clamp(local.Y, -half.Y, half.Y),
						Mathf.Clamp(local.Z, -half.Z, half.Z));
					return shape * clamped;
				}
			}
		}

		return BodyCenter(target);
	}

	/// <summary>
	/// 目标的身体中心 = 它**直接子节点**里的第一个碰撞体位置（单位预制体里那个 `Body`：1.8m 单位在 0.9 处 = 胸口）。
	/// 只看直接子节点是有意的：能力节点下的那些 `Shape` 是**攻击命中体**（Area3D 的子节点），不是身体。
	/// </summary>
	private static Vector3 BodyCenter(Unit target)
	{
		foreach (Node child in target.GetChildren())
		{
			if (child is CollisionShape3D { Disabled: false } shape)
			{
				return shape.GlobalPosition;
			}
		}

		return target.GlobalPosition;
	}
}

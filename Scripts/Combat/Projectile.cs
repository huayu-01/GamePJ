using ActRoguelike.Audio;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 统一投射物（子弹、箭矢、气刃、投掷物）。
/// 每个物理帧沿本帧位移做一次射线检测（战斗规则说明 §7），避免高速穿透；
/// 命中后建立 HitInstance 交给目标 Unit 结算，并按 Hit Policy 决定是否继续飞行（穿透）。
/// 弹道下坠只需把 <see cref="GravityScale"/> 设为非 0。
/// </summary>
public partial class Projectile : Node3D
{
	[Export] public float Speed { get; set; } = 70f;
	[Export] public float GravityScale { get; set; }
	[Export] public float MaxLifetime { get; set; } = 2.5f;

	/// <summary>可穿透的目标数量，0 = 命中即消失。</summary>
	[Export] public int Pierce { get; set; }

	[Export] public uint HitMask { get; set; } = CombatLayers.World | CombatLayers.UnitMask;

	/// <summary>弹射时搜索下一个目标的半径。</summary>
	[Export] public float RicochetRange { get; set; } = 9f;

	private readonly HitTracker _tracker = new();
	private readonly System.Collections.Generic.HashSet<ulong> _hitTargets = new();
	private AttackInstance? _attack;
	private Vector3 _velocity;
	private float _lifetime;
	private int _pierceLeft;
	private int _ricochetLeft;
	private bool _exploded;
	private Rid _ownerRid;

	public override void _Ready() => AddToGroup("projectiles");

	public void Launch(AttackInstance attack, Vector3 direction)
	{
		_attack = attack;
		_velocity = direction.Normalized() * Speed;
		_pierceLeft = Pierce + attack.Pierce;
		_ricochetLeft = attack.Ricochet;
		_ownerRid = attack.Attacker?.GetRid() ?? default;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_attack == null)
		{
			return;
		}

		float dt = (float)delta;
		_lifetime += dt;
		if (_lifetime > _attack.Lifetime)
		{
			QueueFree();
			return;
		}

		if (GravityScale != 0f)
		{
			float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
			_velocity.Y -= gravity * GravityScale * dt;
		}

		Vector3 from = GlobalPosition;
		Vector3 to = from + _velocity * dt;

		var query = PhysicsRayQueryParameters3D.Create(from, to, HitMask);
		if (_ownerRid.IsValid)
		{
			query.Exclude = new Godot.Collections.Array<Rid> { _ownerRid };
		}

		Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count > 0)
		{
			ResolveImpact(hit, to);
			return;
		}

		GlobalPosition = to;
		Orient();
	}

	private void ResolveImpact(Godot.Collections.Dictionary hit, Vector3 to)
	{
		var collider = hit["collider"].As<GodotObject>();
		Vector3 position = hit["position"].AsVector3();

		if (_attack!.ExplosionRadius > 0f && !_exploded)
		{
			_exploded = true;
			Detonate(position);
		}

		if (collider is Unit target && _attack.Attacker != null && _attack.Attacker.IsHostileTo(target))
		{
			float now = Time.GetTicksMsec() / 1000f;
			if (target.IsValidTarget && _tracker.TryRegister(target, _attack.Policy, _attack.HitInterval, now, 0))
			{
				_hitTargets.Add(target.GetInstanceId());
				target.ReceiveHit(_attack.RollHit(target, _velocity.Normalized(), position));

				// 命中特效（Binbun Hit FX）：用**射线命中点**，子弹打在身上哪就炸在哪。
				// 开关 / 生命周期 / 预制体路径都在 `Config/ConfigIndex.tres` 的「表现 · 命中特效」组里（不写死）。
				HitEffect.Spawn(this, position);

				// 命中音效：与命中特效**同一个位置、同一个判定**，按 id 播（表里说了算）。
				AudioManager.PlaySfx(AudioIds.CombatHit);
			}

			if (_pierceLeft > 0)
			{
				_pierceLeft--;
				GlobalPosition = to;
				Orient();
				return;
			}

			if (_ricochetLeft > 0 && TryRicochet(target))
			{
				return;
			}
		}

		QueueFree();
	}

	/// <summary>弹射：转向附近还没命中过的敌人继续飞。</summary>
	private bool TryRicochet(Unit justHit)
	{
		Unit? next = null;
		float bestDistance = RicochetRange * RicochetRange;

		foreach (Node node in GetTree().GetNodesInGroup("enemy_units"))
		{
			if (node is not Unit candidate || candidate == justHit || !candidate.IsValidTarget)
			{
				continue;
			}

			if (_hitTargets.Contains(candidate.GetInstanceId()))
			{
				continue;
			}

			float distance = GlobalPosition.DistanceSquaredTo(candidate.GlobalPosition);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				next = candidate;
			}
		}

		if (next == null)
		{
			return false;
		}

		_hitTargets.Add(next.GetInstanceId());
		_ricochetLeft--;
		Vector3 direction = (next.GlobalPosition - GlobalPosition).Normalized();
		_velocity = direction * Speed;
		GlobalPosition = GlobalPosition + (direction * 0.6f);
		Orient();
		return true;
	}

	private void Detonate(Vector3 position)
	{
		var scene = GD.Load<PackedScene>("res://Scenes/Combat/Explosion.tscn");
		if (scene?.Instantiate<Explosion>() is not { } explosion)
		{
			return;
		}

		Node parent = GetTree().CurrentScene ?? GetTree().Root;
		parent.AddChild(explosion);
		explosion.GlobalPosition = position;
		explosion.Detonate(_attack!.Attacker, _attack.ExplosionDamage, _attack.ExplosionRadius, _attack.Tags);
	}

	private void Orient()
	{
		if (_velocity.LengthSquared() < 0.0001f)
		{
			return;
		}

		Vector3 direction = _velocity.Normalized();
		Vector3 up = Mathf.Abs(direction.Dot(Vector3.Up)) > 0.99f ? Vector3.Forward : Vector3.Up;
		LookAt(GlobalPosition + direction, up);
	}
}

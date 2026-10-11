using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Combat;

/// <summary>
/// 一次性爆炸：本身就是一个 AttackHitbox，按半径缩放后判定一次再自毁。
/// 命中爆炸、自爆、异化效果都复用这一个预制体。
/// </summary>
public partial class Explosion : AttackHitbox
{
	[Export] public float Lifetime { get; set; } = 0.12f;

	private float _remaining;

	public override void _Ready()
	{
		base._Ready();
		_remaining = Lifetime;
	}

	/// <summary>引爆：radius 为世界单位半径（预制体球形碰撞体基准半径 1）。</summary>
	public void Detonate(Unit? attacker, float damage, float radius, string[] tags)
	{
		Scale = Vector3.One * Mathf.Max(0.1f, radius);
		Activate(attacker, new AttackInstance
		{
			Attacker = attacker,
			Ability = "Explosion",
			BaseDamage = damage,
			Policy = HitPolicy.OncePerTarget,
			Tags = tags,
		});
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);

		_remaining -= (float)delta;
		if (_remaining <= 0f)
		{
			QueueFree();
		}
	}
}

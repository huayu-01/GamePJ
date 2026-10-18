using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using Godot;
using K = ActRoguelike.Config.SpecializationKind;

namespace ActRoguelike.Rewards;

public partial class SpecializationRuntime
{
	private ScytheThrowAbility? _scythe;
	private SpecializationField? _heart, _domain;
	private float _windSwordTimer, _trailTimer, _fieldTimer;
	private readonly object _sprintArmorSource = new();
	private bool _hadSprintArmor;
	public int WindShotCount { get; private set; }
	public float WindSizeScale => Has(K.WildWind) ? Value(K.WildWind, "SizeScale", 1f) : 1f;
	private bool Sprinting => _owner.GetNodeOrNull<SprintController>("Sprint")?.IsSprinting == true;
	private void TickWind(float dt)
	{
		bool sprint = Sprinting;
		bool armor = sprint && Has(K.WindArmor);
		if (armor != _hadSprintArmor)
		{
			_hadSprintArmor = armor; _owner.Stats.RemoveBySource(_sprintArmorSource);
			if (armor) _owner.Stats.AddModifier(new StatModifier { Stat = "IncomingDamageMultiplier", Operation = ModifierOperation.Multiply, Value = 1f - Value(K.WindArmor, "Reduction"), Source = _sprintArmorSource });
		}
		if (Has(K.WindHeart) && (_heart == null || !IsInstanceValid(_heart))) _heart = SpawnField(K.WindHeart, _owner.GlobalPosition);
		if (_heart != null && IsInstanceValid(_heart)) { _heart.GlobalPosition = _owner.GlobalPosition; _heart.Active = sprint; }
		if (_domain != null && IsInstanceValid(_domain))
		{
			_domain.Active = _scythe is { InFlight: true, HasSettled: true, IsReturning: false };
			if (_domain.Active) _domain.GlobalPosition = _scythe!.WeaponPosition;
		}
		if (Has(K.WindSword) && sprint)
		{
			_windSwordTimer += dt;
			if (_windSwordTimer >= Value(K.WindSword, "Interval")) { _windSwordTimer = 0f; FireProjectile(K.WindSword, false); }
		}
		else _windSwordTimer = 0f;
		if (Has(K.WindTrail) && sprint)
		{
			_trailTimer += dt;
			if (_trailTimer >= Value(K.WindTrail, "Interval")) { _trailTimer = 0f; SpawnField(K.WindTrail, _owner.GlobalPosition); }
		}
		else _trailTimer = 0f;
		if (Has(K.WindField))
		{
			_fieldTimer += dt;
			if (_fieldTimer >= Value(K.WindField, "Interval")) { _fieldTimer = 0f; SpawnRandomField(); }
		}
	}
	private void OnWeaponSettled(Vector3 position)
	{
		if (_domain != null && IsInstanceValid(_domain)) _domain.QueueFree();
		_domain = SpawnField(K.WindDomain, position);
	}
	private void SpawnRandomField()
	{
		for (int i = 0; i < 12; i++)
		{
			float angle = GD.Randf() * Mathf.Tau, distance = Mathf.Sqrt(GD.Randf()) * Value(K.WindField, "SpawnRange");
			Vector3 candidate = _owner.GlobalPosition + new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
			var query = PhysicsRayQueryParameters3D.Create(candidate + Vector3.Up * 5, candidate + Vector3.Down * 10, CombatLayers.World);
			var hit = _owner.GetWorld3D().DirectSpaceState.IntersectRay(query);
			if (hit.Count == 0 || hit["normal"].AsVector3().Y < 0.7f) continue;
			Vector3 position = hit["position"].AsVector3();
			using var clearance = new PhysicsShapeQueryParameters3D { Shape = new SphereShape3D { Radius = 0.45f }, Transform = new Transform3D(Basis.Identity, position + Vector3.Up * 0.6f), CollisionMask = CombatLayers.World };
			if (_owner.GetWorld3D().DirectSpaceState.IntersectShape(clearance, 1).Count > 0) continue;
			SpawnField(K.WindField, position); return;
		}
	}
	private SpecializationField SpawnField(K kind, Vector3 position)
	{
		var field = new SpecializationField { Runtime = this, Kind = kind, SourceUnit = _owner, Lifetime = Value(kind, "Duration") };
		// 字段挂在规则下并设TopLevel：固定区域不会随玩家移动，规则销毁时又能统一回收。
		AddChild(field); field.GlobalPosition = position; FieldCount++; return field;
	}
}

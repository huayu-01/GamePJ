using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Rewards;

public partial class DefenseReactionRule : Node, IIncomingHitRule, IStoppableRewardRule
{
	public DefenseReactionRewardEffect Definition { get; set; } = null!;
	private Unit _owner = null!;
	private MeshInstance3D _ward = null!;
	public float Remaining { get; private set; }
	public int BlockCount { get; private set; }
	public int BurstCount { get; private set; }
	private bool _stopped;
	public override void _Ready()
	{
		_owner = GetParent<Unit>(); _owner.ArmorBroke += Arm; _owner.AddIncomingHitRule(this);
		GameplayEventBus.Raised += OnEvent; SetProcess(false);
		_ward = new MeshInstance3D { Visible = false, Position = Vector3.Up * .9f,
			Mesh = new SphereMesh { Radius = .7f, Height = 1.5f }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(.4f, .8f, 1, .12f),
				RimEnabled = true, Rim = 1, RimTint = 1 } }; AddChild(_ward);
	}
	private void Arm()
	{
		if (Definition.Kind != DefenseReactionRewardEffect.ReactionKind.ArmorBreakWard || _owner.IsDead) return;
		Remaining = Definition.Duration; _ward.Visible = true; SetProcess(true);
	}
	public bool Blocks(HitInstance hit) => Remaining > 0 && !_owner.IsDead && hit.Damage > 0 && !hit.Negated
		&& hit.Attack?.IsDamageOverTime != true && !hit.BypassBlock;
	public void OnBlocked(HitInstance hit)
	{
		Remaining = 0; _ward.Visible = false; SetProcess(false); BlockCount++;
		hit.Negated = true; _owner.RaiseBlocked(hit);
	}
	private void OnEvent(GameplayEvent value)
	{
		if (Definition.Kind != DefenseReactionRewardEffect.ReactionKind.BlockBurst || value.Kind != GameplayEventKind.Blocked
			|| value.Source != _owner || _owner.IsDead) return;
		BurstCount++;
		float radius = Definition.Radius * _owner.Stats.Get("AttackRangeMultiplier");
		using var shape = new SphereShape3D { Radius = radius };
		using var query = new PhysicsShapeQueryParameters3D { Shape = shape,
			Transform = new Transform3D(Basis.Identity, _owner.GlobalPosition + Vector3.Up * .8f), CollisionMask = CombatLayers.HostileTo(_owner.Faction) };
		var hits = _owner.GetWorld3D().DirectSpaceState.IntersectShape(query, 256);
		var seen = new HashSet<ulong>();
		var attack = new AttackInstance { Attacker = _owner, Ability = "BlockBurst", IsSecondary = true,
			BaseDamage = Definition.Damage * AttackOffense.Capture(_owner).DamageMultiplier, Knockback = Definition.Knockback };
		foreach (var item in hits)
			if (item["collider"].AsGodotObject() is Unit unit && _owner.IsHostileTo(unit) && seen.Add(unit.GetInstanceId()))
				unit.ReceiveHit(attack.RollHit(unit, (unit.GlobalPosition - _owner.GlobalPosition).Normalized(), unit.GlobalPosition));
		CombatImpactFx.Spawn(_owner, _owner.GlobalPosition + Vector3.Up, Vector3.Up, ImpactKind.Block, 1);
	}
	public override void _Process(double delta)
	{
		Remaining = Mathf.Max(0, Remaining - (float)delta);
		if (Remaining <= 0) { _ward.Visible = false; SetProcess(false); }
	}
	public override void _ExitTree() => Stop();
	public void Stop()
	{
		if (_stopped) return; _stopped = true; Remaining = 0; SetProcess(false);
		GameplayEventBus.Raised -= OnEvent;
		if (IsInstanceValid(_owner)) { _owner.ArmorBroke -= Arm; _owner.RemoveIncomingHitRule(this); }
	}
}

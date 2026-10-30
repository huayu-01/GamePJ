using System;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Combat;

public partial class SupplyPickup : Node3D
{
	public Unit OwnerUnit { get; set; } = null!;
	public SupplyOptionDefinition Option { get; set; } = null!;
	public Vector3 GroundPoint { get; set; }
	public float Lifetime { get; set; } = 30;
	public bool Landed { get; private set; }
	public bool Consumed { get; private set; }
	public event Action<SupplyPickup>? Collected;
	private float _age, _landedAge;
	private MeshInstance3D _crate = null!, _warning = null!;
	public override void _Ready()
	{
		TopLevel = true; SetProcess(false); GlobalPosition = GroundPoint;
		var material = new StandardMaterial3D { AlbedoColor = Option.Color, Roughness = .35f, Metallic = .3f, EmissionEnabled = true, Emission = Option.Color * .25f };
		_crate = new MeshInstance3D { Mesh = new BoxMesh { Size = Vector3.One * .55f }, MaterialOverride = material, Position = Vector3.Up * 6, Visible = false }; AddChild(_crate);
		_warning = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = .7f, OuterRadius = .8f, Rings = 32, RingSegments = 6 }, MaterialOverride = material,
			Position = Vector3.Up * .05f, Scale = new Vector3(1, .08f, 1), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off }; AddChild(_warning);
		var label = new Label3D { Text = Option.LocalizedName, Position = Vector3.Up * 1.1f, FontSize = 28, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Modulate = Option.Color };
		AddChild(label);
	}
	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta; _age += dt;
		if (!IsInstanceValid(OwnerUnit) || OwnerUnit.IsDead || RunController.Instance?.RunEnded == true) { Stop(); return; }
		if (_age < .6f) return;
		_crate.Visible = true;
		if (!Landed)
		{
			_crate.Position = _crate.Position.MoveToward(Vector3.Up * .35f, 18 * dt);
			if (_crate.Position.Y > .36f) return;
			Landed = true; _warning.Hide();
		}
		_landedAge += dt; if (_landedAge >= Lifetime) { Stop(); return; }
		_crate.RotateY(dt); Vector3 offset = OwnerUnit.GlobalPosition - GroundPoint;
		if (Mathf.Abs(offset.Y) <= 2 && new Vector2(offset.X, offset.Z).LengthSquared() <= 1.4f * 1.4f) TryCollect();
	}
	public bool TryCollect()
	{
		if (Consumed || IsQueuedForDeletion() || !Landed || _landedAge >= Lifetime || !IsInstanceValid(OwnerUnit) || OwnerUnit.IsDead) return false;
		var run = RunController.Instance; if (run?.GameplayReady != true) return false;
		switch (Option.Kind)
		{
			case SupplyKind.Heal: OwnerUnit.Heal(Option.Amount); break;
			case SupplyKind.Armor: OwnerUnit.GrantArmor(Option.Amount); break;
			case SupplyKind.Status:
				if (Option.Status == null || StatusPresets.Create(Option.Status.Id, Option.Duration) is not { } status) return false;
				OwnerUnit.ApplyStatus(status, OwnerUnit, "Supply"); break;
			case SupplyKind.Experience:
				if (run.Mode is not SurvivalDirector director || !director.Active) return false;
				director.GrantExperience(Mathf.RoundToInt(Option.Amount), SurvivalExperienceSource.Objective); break;
			case SupplyKind.Shards: if (!run.CollectBonusShards(OwnerUnit, Mathf.RoundToInt(Option.Amount))) return false; break;
		}
		Consumed = true; GameLog.Write("Reward", "拾取空投补给：" + Option.LocalizedName); Collected?.Invoke(this); Collected = null; Stop(); return true;
	}
	public void Stop() { Hide(); SetPhysicsProcess(false); QueueFree(); }
}

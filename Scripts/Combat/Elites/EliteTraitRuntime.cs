using System.Linq;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Combat.Elites;

public partial class EliteTraitRuntime : Node
{
	public const string NodeName = "EliteTrait";
	public EliteTraitDefinition Definition { get; set; } = null!;
	public EliteTraitWave Wave { get; set; } = null!;
	public EliteTraitBehavior Behavior { get; private set; } = null!;
	public new Unit Owner { get; private set; } = null!;
	public bool Stopped { get; private set; }
	private Label3D? _label;
	public static EliteTraitRuntime? Find(Unit owner) => owner.GetNodeOrNull<EliteTraitRuntime>(NodeName);
	public override void _Ready()
	{
		Owner = GetParent<Unit>();
		Behavior = Definition.Create(Owner, Wave);
		Owner.Damaged += OnDamaged; Owner.Died += OnDeath; GameplayEventBus.Raised += OnFact;
		Behavior.Attach();
		_label = new Label3D { Text = ContentText.Get(Definition.NameKey, Definition.Title), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 28, PixelSize = .007f, Position = Vector3.Up * 2.25f, NoDepthTest = true, Modulate = new Color(1, .72f, .25f) };
		Owner.AddChild(_label);
	}
	public override void _PhysicsProcess(double delta)
	{
		if (Stopped || Wave.IsStopped || Owner.IsDead || Owner.ImpactPause?.IsHolding == true) return;
		Behavior.Tick((float)delta);
	}
	private void OnDamaged(DamageResult result, HitInstance hit) { if (!Stopped && !Wave.IsStopped) Behavior.OnDamaged(result, hit); }
	private void OnFact(GameplayEvent fact) { if (!Stopped && !Wave.IsStopped) Behavior.OnFact(fact); }
	private void OnDeath(HitInstance hit) { if (Stopped) return; if (!Wave.IsStopped) Behavior.OnDeath(hit); Stop(); }
	public void Stop()
	{
		if (Stopped) return; Stopped = true;
		if (IsInstanceValid(Owner)) { Owner.Damaged -= OnDamaged; Owner.Died -= OnDeath; }
		GameplayEventBus.Raised -= OnFact; Behavior?.Dispose();
		if (IsInstanceValid(Owner) && Owner.HasMeta("EliteTraitScaleBefore"))
		{
			Owner.Scale = Owner.GetMeta("EliteTraitScaleBefore").AsVector3(); Owner.RemoveMeta("EliteTraitScaleBefore");
			foreach (var modifier in Owner.GetChildren().OfType<EliteModifier>())
				if (modifier.HasMeta("EliteTraitScaleBefore")) { modifier.ModelScale = modifier.GetMeta("EliteTraitScaleBefore").AsSingle(); modifier.RemoveMeta("EliteTraitScaleBefore"); }
		}
		if (_label != null && IsInstanceValid(_label)) _label.QueueFree();
	}
	public override void _ExitTree() => Stop();
}

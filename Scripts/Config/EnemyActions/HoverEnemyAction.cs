using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class HoverEnemyAction : EnemyActionDefinition
{
	public override EnemyActionStep.ActionKind? SemanticKind => EnemyActionStep.ActionKind.Hover;
	public override EnemyActionExecution Create(EnemyActionContext context) => new Execution(context);
	private sealed class Execution : EnemyActionExecution
	{
		public Execution(EnemyActionContext context) : base(context) { }
		public override void Begin() => Tick(0);
		public override void Tick(float delta) => Context.Owner.Movement?.BeginVelocityOverride(this, Vector3.Zero);
		protected override void End() { if (GodotObject.IsInstanceValid(Context.Owner.Movement)) Context.Owner.Movement!.EndVelocityOverride(this); }
	}
}

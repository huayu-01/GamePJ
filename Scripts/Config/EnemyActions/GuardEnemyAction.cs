using ActRoguelike.Abilities;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class GuardEnemyAction : EnemyActionDefinition
{
	public override EnemyActionStep.ActionKind? SemanticKind => EnemyActionStep.ActionKind.Guard;
	[Export] public bool TrackTarget { get; set; } = true;
	public override bool BlocksDuringStep => true;
	public override EnemyActionExecution Create(EnemyActionContext context) => new Execution(context, TrackTarget);
	private sealed class Execution : EnemyActionExecution
	{
		private readonly bool _track;
		public Execution(EnemyActionContext context, bool track) : base(context) => _track = track;
		public override void Tick(float delta) { if (_track) Context.FaceTarget(delta); }
	}
}

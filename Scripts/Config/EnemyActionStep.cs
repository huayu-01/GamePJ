using Godot;

namespace ActRoguelike.Config;

/// <summary>可复用动作段；不同敌人通过准备、远近分支和收尾序列组合。</summary>
[Tool, GlobalClass]
public partial class EnemyActionStep : Resource
{
	public enum ActionKind { Wait, Guard, Shot, ArcShot, Bomb, ArcBomb, Meteor, Melee, Spin, Charge, HomingCharge, Jump, Hover, Dive, Burrow, BackHop }
	private ActionKind _kind;
	[Export] public ActionKind Kind { get => Action?.SemanticKind ?? _kind; set => _kind = value; }
	/// <summary>新动作由定义创建执行器；留空兼容旧 Kind。时序仍由本段配置，Kind 保留预警分类。</summary>
	private EnemyActionDefinition? _action;
	[Export] public EnemyActionDefinition? Action { get => _action; set { _action = value; if (Engine.IsEditorHint()) NotifyPropertyListChanged(); } }
	public override void _ValidateProperty(Godot.Collections.Dictionary property)
	{
		if (Action == null || System.Array.IndexOf(Action.OwnedStepProperties, property["name"].AsString()) < 0) return;
		property["usage"] = (long)((PropertyUsageFlags)property["usage"].AsInt64() & ~(PropertyUsageFlags.Storage | PropertyUsageFlags.Editor));
	}
	[Export] public float WindupSeconds { get; set; } = 0.5f;
	[Export] public float ActiveSeconds { get; set; } = 0.2f;
	[Export] public float RecoverSeconds { get; set; } = 0.2f;
	[Export] public float Damage { get; set; } = 12;
	[Export] public float Radius { get; set; } = 1.5f;
	[Export] public float Speed { get; set; } = 8;
	[Export] public float TravelDistance { get; set; } = 14;
	[Export] public float FlightSeconds { get; set; } = 1;
	[Export] public float TurnDegreesPerSecond { get; set; } = 100;
	[Export] public bool Blocking { get; set; }
	[Export] public bool Centered { get; set; }
}

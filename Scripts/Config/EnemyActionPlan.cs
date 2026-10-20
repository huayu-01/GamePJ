using System;
using Godot;

namespace ActRoguelike.Config;

[GlobalClass]
public partial class EnemyActionPlan : Resource
{
	[Export] public EnemyActionStep[] Opening { get; set; } = Array.Empty<EnemyActionStep>();
	[Export] public EnemyActionStep[] Near { get; set; } = Array.Empty<EnemyActionStep>();
	[Export] public EnemyActionStep[] Far { get; set; } = Array.Empty<EnemyActionStep>();
	[Export] public EnemyActionStep[] Closing { get; set; } = Array.Empty<EnemyActionStep>();
	[Export] public float NearDistance { get; set; } = 3;
	[Export] public float CooldownSeconds { get; set; } = 2;
	[Export] public float TriggerDistance { get; set; } = 14;
}

using Godot;

namespace ActRoguelike.Combat;

/// <summary>命中反馈的唯一调参入口；时长为本项目预设，不是参考游戏的内部参数。</summary>
[GlobalClass]
public partial class CombatFeedbackProfile : Resource
{
	[Export] public bool Enabled { get; set; } = true;
	[Export] public bool LocalPauseEnabled { get; set; } = true;
	[Export] public float LightPauseSeconds { get; set; } = .035f;
	[Export] public float HeavyPauseSeconds { get; set; } = .06f;
	[Export] public float MaxPauseSeconds { get; set; } = .075f;
	[Export] public float PauseCooldownSeconds { get; set; } = .12f;
	[Export] public float FinisherWeight { get; set; } = 1.45f;
	[Export] public float CriticalWeight { get; set; } = 1.25f;
	[Export] public float BreakWeight { get; set; } = 1.6f;
	[Export] public float KillWeight { get; set; } = 1.2f;
	[Export] public float CameraStrength { get; set; } = 1f;
	[Export] public float CameraBaseOffset { get; set; } = .035f;
	[Export] public float CameraMaxOffset { get; set; } = .085f;
	[Export] public float CameraSeconds { get; set; } = .14f;
	[Export] public float ContactCoreSeconds { get; set; } = .07f;
	[Export] public float ContactCoreSize { get; set; } = .16f;
	[Export] public float CameraCooldownSeconds { get; set; } = .07f;
	[Export] public float SoundCooldownSeconds { get; set; } = .045f;
	[Export] public float TargetCooldownSeconds { get; set; } = .035f;
	[Export] public float FlashSeconds { get; set; } = .075f;
	[Export] public float BurstSeconds { get; set; } = .18f;
	[Export] public int MaxEffectsPerFrame { get; set; } = 12;
	[Export] public string FleshSound { get; set; } = "sfx_combat_hit";
	[Export] public string ArmorSound { get; set; } = "sfx_combat_hit";
	[Export] public string BlockSound { get; set; } = "sfx_combat_hit";
	[Export] public string BreakSound { get; set; } = "sfx_combat_hit";
	[Export] public float ArmorPitch { get; set; } = 1.55f;
	[Export] public float BlockPitch { get; set; } = .7f;
	[Export] public float BreakPitch { get; set; } = .55f;
}

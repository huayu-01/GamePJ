namespace ActRoguelike.Abilities;

/// <summary>长枪的路径穿透语义；平抛与法线刺落可共用气贯长虹，不按显示名判断。</summary>
public interface IPiercingSpearAbility
{
	bool LanceUnlimitedTargets { get; set; }
}

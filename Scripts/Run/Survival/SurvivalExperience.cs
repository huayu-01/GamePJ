namespace ActRoguelike.Run;

public enum SurvivalExperienceSource { Enemy, Objective }
/// <summary>原始经验与来源独立于绘制合并和收益倍率。</summary>
public readonly record struct SurvivalExperienceReceipt(int RawValue, int GrantedValue, SurvivalExperienceSource Source);

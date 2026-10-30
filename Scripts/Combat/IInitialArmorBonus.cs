namespace ActRoguelike.Combat;

/// <summary>出生或显式回满时按最终属性计算的额外护甲；普通属性刷新不会恢复已损失的护甲。</summary>
public interface IInitialArmorBonus
{
	float ArmorFor(Unit unit);
}

using Godot;

namespace ActRoguelike.Combat;

public partial class Unit
{
	private readonly object _earnedHealthSource = new();
	private StatModifier? _earnedHealthModifier;
	private float _earnedHealth;
	/// <summary>本局已经挣得的生命成长；只保留一个修正器，不随击杀无限增加属性条目。</summary>
	public void GrantMaximumHealthGrowth(float amount, bool fillNewHealth = true)
	{
		if (IsDead || !float.IsFinite(amount) || amount <= 0) return;
		float before = MaxHealth;
		_earnedHealth += amount;
		if (_earnedHealthModifier != null) Stats.RemoveModifier(_earnedHealthModifier);
		_earnedHealthModifier = new StatModifier { Stat = "MaxHealthAdd", Value = _earnedHealth, Source = _earnedHealthSource };
		Stats.AddModifier(_earnedHealthModifier); RefreshStats();
		// 只补足实际新增上限；上限被符文夹住时不偷回血，死斗的零血状态也不提前复活。
		if (fillNewHealth && Health > 0) Health = Mathf.Min(MaxHealth, Health + Mathf.Max(0, MaxHealth - before));
	}
}

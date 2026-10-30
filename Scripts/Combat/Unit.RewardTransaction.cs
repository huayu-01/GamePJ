using System;
using Godot;

namespace ActRoguelike.Combat;

public partial class Unit
{
	/// <summary>只用于同步奖励事务失败回滚；普通撤销不退款已取得或消耗的战斗资源。</summary>
	internal Action CaptureRewardTransaction()
	{
		var values = (Health, Armor, ArmorPeak, Shield, _armorBonus, _armorHealthRatio);
		var conversion = Stats.ModifiersFrom(this);
		return () =>
		{
			if (!IsInstanceValid(this)) return;
			Stats.RestoreModifiersFrom(this, conversion);
			_armorHealthRatio = values._armorHealthRatio; RefreshStats();
			(Health, Armor, ArmorPeak, Shield, _armorBonus, _armorHealthRatio) = values;
		};
	}
}

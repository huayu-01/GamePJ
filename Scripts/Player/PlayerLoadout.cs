using System;
using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.AbilityCards;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Player;

/// <summary>基地与关卡共用的装备应用器；不创建关卡、奖励或结算上下文。</summary>
public sealed class PlayerLoadout
{
	private readonly Dictionary<Ability, (bool Enabled, string Action, string Id)> _baseline = new();
	private int? _baseAirJumps;

	public void Apply(Unit player, Node owner, PlayerCharacterKind characterKind, WeaponPresetKind weaponKind)
	{
		CharacterConfig? character = RunController.CharacterRow(characterKind);
		WeaponConfig? weaponRow = RunController.WeaponRow(weaponKind);
		var abilities = new List<Ability>();
		if (player.Movement is { } jump)
		{
			_baseAirJumps ??= jump.ExtraAirJumps;
			jump.SetExtraAirJumps(_baseAirJumps.Value, Mathf.Max(_baseAirJumps.Value, jump.ExtraAirJumps));
		}
		player.GetNodeOrNull<AirHoverController>("AirHover")?.ResetForLoadout();
		NodeSearch.CollectInSubtree(player, abilities);
		foreach (Ability ability in abilities)
		{
			if (!_baseline.TryGetValue(ability, out var baseline))
			{
				baseline = (ability.Enabled, ability.InputAction, ability.AbilityId);
				_baseline.Add(ability, baseline);
			}
			ability.ResetForLoadout();
			ability.Enabled = baseline.Enabled;
			ability.InputAction = baseline.Action;
			ability.AbilityId = baseline.Id;
		}

		MetaProgress.Load();
		player.Stats.RemoveBySource(owner);
		player.ApplyBaseStats((character?.MaxHealth ?? player.MaxHealth) + MetaProgress.BonusMaxHealth,
			character?.StartingArmor ?? player.StartingArmor, character?.MaxShield ?? player.MaxShield, character?.MoveSpeed ?? player.MoveSpeed);
		player.Stats.AddModifier(new StatModifier { Stat = "DamageMultiplier", Operation = ModifierOperation.Multiply, Value = MetaProgress.BonusDamageMultiplier, Source = owner });
		player.Stats.AddModifier(new StatModifier { Stat = "MoveSpeedMultiplier", Operation = ModifierOperation.Multiply, Value = MetaProgress.BonusMoveSpeedMultiplier, Source = owner });
		player.Stats.AddModifier(new StatModifier { Stat = "CritChanceAdd", Operation = ModifierOperation.Add, Value = MetaProgress.BonusCritChance, Source = owner });
		player.RefreshStats();
		player.DamageReduction = character?.DamageReduction ?? player.DamageReduction;
		if (NodeSearch.FindInSubtree<Weapon>(player) is { } weapon) weaponRow?.ApplyTo(weapon);

		if (character != null)
		{
			character.ApplySkillsTo(player);
			character.ApplyAttackTo(player);
			character.ApplyAnimationTo(player);
			if (player.Movement is { } movement)
			{
				movement.JumpVelocity = character.JumpVelocity;
				if (character.JumpGravityScale > 0) movement.GravityScale = character.JumpGravityScale;
			}
			if (player.GetNodeOrNull<SprintController>("Sprint") is { } sprint)
			{
				if (!string.IsNullOrEmpty(character.SprintInputAction)) sprint.InputAction = character.SprintInputAction;
				if (character.SprintSpeedMultiplier > 0) sprint.SpeedMultiplier = character.SprintSpeedMultiplier;
			}
		}
		if (weaponRow != null) UnitConfig.ApplySkills(player, weaponRow.Skills, weaponRow.Id);

		// 先恢复绑定、再套技能行、最后执行矩阵；不能把新技能的按键再次清空。
		foreach (Ability ability in abilities)
		{
			if (character != null && Matches(character.DisabledAbilityIds, ability)) ability.Enabled = false;
			if (character != null && Matches(character.EnabledAbilityIds, ability)) ability.Enabled = true;
			ability.RefreshCooldown();
		}
		AbilityEffects.ApplyToPlayer(player, owner);
		if (character != null && weaponRow != null)
			player.GetNodeOrNull<ActRoguelike.Visual.CharacterDisplay>("Appearance")?.Apply(character, weaponRow);
		GameLog.Write("Flow", $"装备应用：{character?.Id}/{weaponRow?.Id} · 已套角色/武技/能力卡，来源 {owner.Name}");
	}

	private static bool Matches(string[] names, Ability ability)
		=> Array.IndexOf(names, ability.AbilityId) >= 0 || Array.IndexOf(names, ability.GetType().Name) >= 0;
}

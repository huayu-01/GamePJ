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
	public void Apply(Unit player, Node owner, PlayerCharacterKind characterKind, WeaponPresetKind weaponKind)
	{
		var slots = player.AbilitySlots; slots.CaptureDefaults(player); slots.Clear();
		if (player.GetNodeOrNull<SpecializationRuntime>(SpecializationRuntime.NodeName) is { } specialization)
		{
			player.RemoveChild(specialization); specialization.QueueFree();
		}
		player.ResetArmorHealthConversion();
		player.Reactions?.Reset();
		foreach (Node child in player.GetChildren())
			if (child is HappyRunnerRule) { player.RemoveChild(child); child.QueueFree(); }
		CharacterConfig? character = RunController.CharacterRow(characterKind);
		WeaponConfig? weaponRow = RunController.WeaponRow(weaponKind);
		var abilities = new List<Ability>();
		if (player.Movement is { } jump)
		{
			jump.ConfigurePhysics(character?.PhysicsProfile ?? slots.BasePhysicsProfile);
			jump.ConfigureTeleportJump(false);
			jump.SetExtraAirJumps(slots.BaseAirJumps, Mathf.Max(slots.BaseAirJumps, jump.ExtraAirJumps));
		}
		player.GetNodeOrNull<AirHoverController>("AirHover")?.ResetForLoadout();
		NodeSearch.CollectInSubtree(player, abilities);
		foreach (Ability ability in abilities)
		{
			ability.ResetForLoadout();
			ability.Enabled = false;
			ability.InputAction = string.Empty;
		}

		MetaProgress.Load();
		player.Stats.RemoveBySource(owner);
		player.Stats.SetBase("BaseCritChance", weaponRow?.CritChance ?? 0.05f);
		player.Stats.SetBase("BaseCritMultiplier", weaponRow?.CritMultiplier ?? 2f);
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
				character.ApplyJumpTo(movement);
			}
			if (player.GetNodeOrNull<SprintController>("Sprint") is { } sprint)
			{
				if (!string.IsNullOrEmpty(character.SprintInputAction)) sprint.InputAction = character.SprintInputAction;
				if (character.SprintSpeedMultiplier > 0) sprint.SpeedMultiplier = character.SprintSpeedMultiplier;
			}
		}
		if (weaponRow != null) UnitConfig.ApplySkills(player, weaponRow.Skills, weaponRow.Id);
		if (weaponRow?.FallAttackAim is { TargetAbility: { } fallRow } aim
			&& Ability.FindByAbilityId(player, fallRow.Id) is FallAttackAbility fall) aim.ApplyAimTo(fall);

		// 只有当前角色／武器声明的技能能占槽；其它节点只是仓库，不参与输入。
		if (character != null) slots.Assign(player, character.Skills);
		if (weaponRow != null) slots.Assign(player, weaponRow.Skills);
		foreach (Ability ability in abilities)
		{
			if (slots.Owns(ability))
			{
				if (character != null && Matches(character.DisabledAbilityIds, ability)) ability.Enabled = false;
				if (character != null && Matches(character.EnabledAbilityIds, ability)) ability.Enabled = true;
			}
			ability.RefreshCooldown();
		}
		AbilityEffects.ApplyToPlayer(player, owner);
		player.GetNodeOrNull<PlayerAbilityInput>("AbilityInput")?.ResetForLoadout();
		if (character != null && weaponRow != null)
			player.GetNodeOrNull<ActRoguelike.Visual.CharacterDisplay>("Appearance")?.Apply(character, weaponRow);
		GameLog.Write("Flow", $"装备应用：{character?.Id}/{weaponRow?.Id} · 已套角色/武技/能力卡，来源 {owner.Name}");
	}

	private static bool Matches(string[] names, Ability ability)
		=> Array.IndexOf(names, ability.AbilityId) >= 0 || Array.IndexOf(names, ability.GetType().Name) >= 0;
}

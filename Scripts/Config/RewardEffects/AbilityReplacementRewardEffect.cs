using System;
using System.Collections.Generic;
using System.Linq;
using ActRoguelike.Abilities;
using ActRoguelike.Rewards;
using Godot;

namespace ActRoguelike.Config;

/// <summary>按资源替换当前武技槽，复用原稳定身份；新动作和参数由独立场景维护。</summary>
[Tool, GlobalClass]
public partial class AbilityReplacementRewardEffect : RewardEffectDefinition
{
	[Export] public AbilityConfig? TargetAbility { get; set; }
	[Export] public PackedScene? ReplacementScene { get; set; }
	[Export] public string SkillName { get; set; } = "";
	[Export] public string SkillNameKey { get; set; } = "";
	[Export(PropertyHint.MultilineText)] public string SkillDescription { get; set; } = "";
	[Export] public string SkillDescriptionKey { get; set; } = "";
	public override IEnumerable<string> Validate()
	{
		if (TargetAbility == null || ReplacementScene == null) { yield return "必须引用原技能行与替换场景。"; yield break; }
		var node = ReplacementScene.Instantiate(); bool valid = node is Ability; node.Free();
		if (!valid) yield return "ReplacementScene根节点必须继承Ability。";
	}
	public override RewardEffectInstance Create(RewardContext context, string rewardId) => new Instance(context, rewardId, this);
	private sealed class Instance : RewardEffectInstance
	{
		private readonly AbilityReplacementRewardEffect _data;
		private AbilitySlots.Entry? _original, _replacement;
		private AbilityConfig? _row;
		private Ability? _ability;
		private bool _oldEnabled;
		private float _baseCooldown;
		private bool _basePiercing;
		public Instance(RewardContext context, string id, AbilityReplacementRewardEffect data) : base(context, id) => _data = data;
		protected override void ApplyCore()
		{
			_original = Context.Player.AbilitySlots.Equipped.Single(e => e.Row.Id == _data.TargetAbility!.Id);
			_ability = _data.ReplacementScene!.Instantiate<Ability>();
			_ability.Name = "EvolvedAbility"; _ability.AbilityId = _original.Row.Id; _ability.InputAction = _original.InputSlot;
			_row = (AbilityConfig)_original.Row.Duplicate(true); _row.NodeName = _ability.Name; _row.ClassHint = _ability.GetType().Name;
			_row.NameKey = _data.SkillNameKey; _row.DescriptionKey = _data.SkillDescriptionKey; _row.DisplayName = _data.SkillName;
			_row.RuntimeDescriptionSource = ConfigRows.ById<RewardConfig>(RewardId) ?? (GodotObject)_ability;
			_row.DescriptionTemplate = _data.SkillDescription;
			_row.Cooldown = _ability.Cooldown; _row.HoldToCharge = _ability.HoldToCharge;
			Context.Player.AddChild(_ability); _row.NodeName = _ability.Name; _baseCooldown = _ability.Cooldown;
			_basePiercing = _ability is IPiercingSpearAbility { LanceUnlimitedTargets: true };
			_oldEnabled = _original.Ability.Enabled; _original.Ability.StopForReplacement(); _original.Ability.Enabled = false;
			_replacement = Context.Player.AbilitySlots.Replace(_original, _row, _ability)
				?? throw new InvalidOperationException("武技槽在替换前已发生变化。");
			_original.Ability.RewardParametersChanged += SyncCompatibleParameters; SyncCompatibleParameters();
		}
		private void SyncCompatibleParameters()
		{
			if (_ability is IPiercingSpearAbility spear && _original?.Ability is IPiercingSpearAbility original)
			{
				spear.LanceUnlimitedTargets = _basePiercing || original.LanceUnlimitedTargets;
				_ability.Cooldown = _baseCooldown * _original.Ability.Cooldown / Mathf.Max(.01f, _data.TargetAbility!.Cooldown);
			}
		}
		protected override void RemoveCore()
		{
			if (_original != null && GodotObject.IsInstanceValid(_original.Ability))
			{
				_original.Ability.RewardParametersChanged -= SyncCompatibleParameters;
				if (_replacement != null && Context.Player.AbilitySlots.Restore(_replacement, _original)) _original.Ability.Enabled = _oldEnabled;
				else if (_replacement == null && Context.Player.AbilitySlots.Owns(_original.Ability)) _original.Ability.Enabled = _oldEnabled;
			}
			if (GodotObject.IsInstanceValid(_ability)) { _ability!.ResetForLoadout(); _ability.Enabled = false; if (_ability.IsInsideTree()) _ability.GetParent().RemoveChild(_ability); _ability.QueueFree(); }
			_row?.Dispose();
		}
	}
}

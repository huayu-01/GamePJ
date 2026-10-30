using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Config;
using ActRoguelike.Core;
using Godot;

namespace ActRoguelike.Testing;

public partial class NumericIdentityProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "NumericIdentity";
	protected override Task RunChecks()
	{
		var skill = ConfigRows.ById<AbilityConfig>("skl_a1_skill_1")!;
		Check(skill.NumericId > 0 && ReferenceEquals(skill, ConfigRows.ByNumericId<AbilityConfig>(skill.NumericId))
			&& ReferenceEquals(skill, ConfigRows.ById<AbilityConfig>(skill.NumericId.ToString())), "旧字符串和新数字存档键解析到同一技能对象");
		Check(ConfigRows.ByNumericId<RewardConfig>(skill.NumericId) == null, "数字身份跨表唯一且查错内容类型不会串行");
		Check(!skill.GetPropertyList().Any(p => p["name"].AsString() == "Id" && ((PropertyUsageFlags)p["usage"].AsInt64() & PropertyUsageFlags.Editor) != 0), "Inspector只编辑数字ID，旧别名继续可序列化");
		using var fresh = new AbilityConfig { NumericId = 9000001, Cooldown = 2.5f };
		string path = ValidationArtifacts.Path("res://Logs/evidence/update-fixes/" + NumericContentIdentity.ResourceName("skill", fresh.NumericId));
		Check(fresh.Id == "9000001" && ResourceSaver.Save(fresh, path) == Error.Ok, "新技能无需描述型字符串ID，按skill_数字规则保存");
		var restored = RuntimeResources.Load<AbilityConfig>(path);
		Check(restored.NumericId == fresh.NumericId && restored.Id == fresh.Id && restored.Cooldown == fresh.Cooldown, "数字身份和真实参数往返保存保持一致");
		var invalid = new ConfigIndexData { Skills = new[] { new AbilityConfig { NumericId = 9000010, Id = "a" }, new AbilityConfig { NumericId = 9000010, Id = "b" } } };
		Check(ContentValidator.Validate(invalid).Any(i => i.Field == "NumericId" && i.Reason.Contains("重复")), "制作校验拒绝不同内容使用相同数字ID");
		var trait = RuntimeResources.Load<ActRoguelike.Combat.Elites.EliteTraitCatalog>("res://Config/EliteTraits/catalog.tres").Traits[0];
		invalid.Skills[0].NumericId = trait.NumericId;
		Check(ContentValidator.Validate(invalid).Any(i => i.Field == "NumericId" && i.Reason.Contains("重复")), "数字身份校验覆盖技能与精英特性之间的跨域冲突");
		return Task.CompletedTask;
	}
}

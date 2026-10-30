using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Combat;
using ActRoguelike.UI;
using ActRoguelike.Player;
using Godot;

namespace ActRoguelike.Testing;

public partial class ConfigAuthoringProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "ConfigAuthoring";
	private static bool Editable(Resource row, string name) => row.GetPropertyList().Any(p => p["name"].AsString() == name
		&& ((PropertyUsageFlags)p["usage"].AsInt64() & PropertyUsageFlags.Editor) != 0);
	protected override async Task RunChecks()
	{
		var normal = ConfigRows.ById<AbilityConfig>("skl_a1_skill_1")!;
		Check(!Editable(normal, "ApplyHandler") && !Editable(normal, "Weight"), "玩家技能Inspector没有首领行为处理器或首领权重");
		var fall = ConfigRows.ById<AbilityConfig>("skl_b_fall")!;
		Check(!Editable(normal, "FallAttack") && fall is FallAbilityConfig && fall.FallAttack != null && Editable(fall, "FallAttack"), "下落攻击独立定义，普通技能不再出现下落参数");
		var combo = ConfigRows.ById<AbilityConfig>("skl_b2_w1_1")!;
		Check(combo is ComboAbilityConfig && combo.ComboSeries != null && Editable(combo, "ComboSeries") && !Editable(normal, "ComboSeries"), "连招技能独立类型保留段落资源，其他技能无连招编辑项");
		var charged = ConfigRows.ById<AbilityConfig>("skl_a2_w1_2")!;
		Check(charged is ChargedAbilityConfig && charged.HoldToCharge && Mathf.IsEqualApprox(charged.ChargeBaseSeconds, .4f), "蓄力技能迁移保持开关与数值");
		Check(Editable(charged, "ChargeBaseSeconds") && !Editable(combo, "ChargeBaseSeconds") && !Editable(normal, "ChargeBaseSeconds")
			&& !Editable(normal, "HoldToCharge") && !Editable(normal, "PerfectWindowSeconds"), "蓄力编辑项只出现在蓄力资源类型");
		foreach (string id in new[] { "bhv_awakened", "bhv_charging", "bhv_screaming", "bhv_summoner" })
		{
			var boss = ConfigRows.ById<AbilityConfig>(id);
			Check(boss is BossBehaviorConfig && boss.IsBossBuild && Editable(boss, "ApplyHandler") && Editable(boss, "Weight"), "首领专用类型保留身份／处理器／编辑入口：" + id);
		}
		Check(!Editable(normal, "HudIcon") && !Editable(normal, "HudColor") && !Editable(normal, "HudOrder"), "战斗技能定义不再导出HUD图标／染色／排序");
		var playerSkills = ConfigIndex.Settings.Weapons.SelectMany(w => w.Skills).Concat(ConfigIndex.Settings.Characters.SelectMany(c => c.Skills)).GroupBy(s => s.Id).Select(g => g.First()).ToArray();
		Check(playerSkills.Length == 19 && playerSkills.All(s => UiIcons.ForAbility(s) != null), "全部六把武器及两个角色的十九项技能图标均按数字身份解析");
		var hud = NodeSearch.FindInSubtree<AbilityHud>(Mode)!;
		await Wait(.15);
		Check(hud.SlotCount == 5 && hud.IconPathOf(normal.Id).EndsWith($"icon_{normal.NumericId}.svg"), "五个实际技能槽按数字身份规则找到独立图片");
		var ability = PlayerAbilityInput.FindByInputAction(Player, "skill_1")!;
		Check(ability.TryPress(), "实际施放后撤以检验HUD冷却来源");
		await Wait(.7);
		Check(ability.CooldownRemaining > 0 && Mathf.Abs(hud.CooldownFraction(ability.AbilityId) - ability.CooldownRemaining / ability.EffectiveCooldown) < .05f,
			"技能栏冷却读取当前技能的实际剩余时间与有效冷却");
		bool charge = ability.HoldToCharge; float cooldown = ability.Cooldown;
		hud.DisplayStyle.ShowChargeMeter = false; hud.DisplayStyle.ShowHoldStatus = false;
		Check(ability.HoldToCharge == charge && ability.Cooldown == cooldown, "HUD自身显示选项不改变战斗参数");
		await Shot("skill-hud-decoupled");
	}
}

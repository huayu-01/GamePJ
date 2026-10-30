using System.Text.RegularExpressions;

namespace ActRoguelike.UI;

public static partial class UiIcons
{
	public static Godot.Texture2D? ForEnemy(string id) => Named("Tabler/"+(id switch {
		"1339" or "enemy_scaredy_shroom"=>"mushroom","enemy_burrow_hunter"=>"circle-dot","enemy_chaser" or "enemy_heavy_slammer"=>"hand-grab","enemy_guard"=>"shield",
		"enemy_charger" or "enemy_tracking_charger"=>"arrow-big-right-lines","enemy_meteor_mage"=>"meteor","enemy_hover_diver"=>"circle-arrow-down",
		"enemy_ranged"=>"bow","enemy_exploder" or "enemy_bomb_lobber" or "enemy_arc_bomber"=>"bomb","enemy_fast_melee"=>"bolt",
		"enemy_screamer"=>"ripple","enemy_spear_guard"=>"sword","enemy_combo_fighter" or "enemy_blade_spinner"=>"swords","enemy_arc_thrower"=>"comet",_=>"skull"
	}));
	private static string? RewardSymbol(string id)
	{
		// 升级沿用本体识别图形；映射只负责表现，不推断或修改奖励机制。
		string family=Regex.Replace(id,"_plus2?$","");
		return family switch {
			"upg_life"=>"heart","upg_power"=>"sword","upg_swift"=>"bolt","upg_crit"=>"target-arrow","upg_critdmg"=>"focus-2","upg_hard" or "upg_armor"=>"shield","upg_climb"=>"arrows-up",
			"upg_storm" or "upg_wild_wind"=>"wind","upg_thunder_blast"=>"sparkles","upg_thunder_mastery"=>"bolt","upg_violence"=>"swords","upg_war_heart"=>"heart",
			"mut_burn"=>"flame","mut_freeze"=>"snowflake","mut_growth"=>"hexagons","mut_cleave"=>"swords","mut_lunge"=>"arrow-up-right","mut_pierce"=>"target-arrow","mut_ricochet"=>"arrows-shuffle","mut_haste"=>"bolt",
			"mut_blood_armor"=>"shield-half","mut_blood_battle"=>"droplet","mut_death_duel"=>"swords","mut_energy_shield" or "mut_guard"=>"shield","mut_happy_runner"=>"run","mut_soul_harvest"=>"comet","mut_stomp" or "mut_stomp_kill"=>"sparkles",
			"mut_thunder_armor"=>"shield-bolt","mut_thunder_mark"=>"circle-dot","mut_wind_armor"=>"shield-half","mut_wind_heart"=>"heart",
			"evo_affection" or "evo_mercy"=>"heart","evo_blast_palm" or "evo_ground_burst"=>"comet","evo_bloodthirst"=>"droplet","evo_dash_reset"=>"refresh","evo_divergence"=>"arrows-shuffle","evo_divine_descent"=>"circle-arrow-up-right","evo_dragon_arrow"=>"bow",
			"evo_flow"=>"brain","evo_focus" or "evo_perfect_focus"=>"focus-2","evo_golden_qi" or "evo_golden_road"=>"sparkles","evo_great_draw"=>"spiral","evo_light_leap"=>"arrows-up","evo_mindseye"=>"eye","evo_parry_refresh"=>"shield-half","evo_philander"=>"users",
			"evo_rainbow"=>"arrow-up-right","evo_salvation"=>"heart","evo_spare_dodge"=>"run","evo_stall_kill"=>"hourglass","evo_thunder_heart"=>"heart","evo_thunder_pool"=>"ripple","evo_thunder_recall"=>"bolt","evo_variation"=>"refresh","evo_war_afterimage"=>"users","evo_war_spear"=>"sword",
			"evo_wind_domain" or "evo_wind_field"=>"circle-dashed","evo_wind_sword"=>"sword","evo_wind_trail"=>"wind",
			"surv_risk_contract"=>"heart","surv_scavenger_compass"=>"compass","surv_echo_familiar"=>"users",_=>null
		};
	}
}

# -*- coding: utf-8 -*-
"""066/067与成长只读复核；供给参考与敏感性情景不等于引擎实测。"""
from pathlib import Path
import csv
import hashlib
import json
import math
import re
import sys
import zipfile
import xml.etree.ElementTree as ET

sys.stdout.reconfigure(encoding="utf-8")
OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[1]
sources = {}


def read(relative):
    path = ROOT / relative
    data = path.read_bytes()
    sources[relative] = {"sha256": hashlib.sha256(data).hexdigest(), "text": data.decode("utf-8-sig")}
    return sources[relative]["text"]


def blocks(relative):
    text = read(relative)
    headings = list(re.finditer(r"(?m)^\[([^\]]+)\]\s*$", text))
    result = []
    for index, heading in enumerate(headings):
        end = headings[index + 1].start() if index + 1 < len(headings) else len(text)
        fields = {}
        for match in re.finditer(r"(?m)^(\w+)\s*=\s*([^\r\n]+)", text[heading.end():end]):
            try:
                fields[match[1]] = json.loads(match[2])
            except ValueError:
                fields[match[1]] = match[2]
        result.append((heading[1], fields))
    return result


def row(relative):
    return next(v for k, v in blocks(relative) if k == "resource")


def csv_out(name, rows):
    with (OUT / name).open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


pressure = {v["Key"]: v["Value"] for _, v in blocks("Config/EnemyBehaviorPressure.tres") if "Key" in v}
enemies = {}
for path in sorted((ROOT / "Config/Tables/Enemies").glob("*.tres")):
    value = row(path.relative_to(ROOT).as_posix())
    if value.get("IsGlobal"):
        continue
    behaviors = re.findall(r'"([^"]+)"', value.get("PressureBehaviors", ""))
    value["effective_cost"] = max(1, math.ceil(sum(pressure[b] for b in set(behaviors)))) if behaviors else value.get("Cost", 1)
    enemies[path.stem.replace("enemy_", "", 1)] = value
mode_blocks = blocks("Config/Modes/twilight_survival.tres")
enemy_refs = {}
for header, _ in mode_blocks:
    match = re.search(r'path="res://Config/Tables/Enemies/(enemy_[^"]+)\.tres".*?id="([^"]+)"', header, re.S)
    if match:
        enemy_refs[match[2]] = match[1].replace("enemy_", "", 1)
mode = next(v for k, v in mode_blocks if k == "resource")
phases = [v for k, v in mode_blocks if k.startswith("sub_resource")]
duration = mode["DurationSeconds"]
phase_rows = []
for index, phase in enumerate(phases):
    start = duration * phase["StartFraction"]
    end = duration * phases[index + 1]["StartFraction"] if index + 1 < len(phases) else duration
    keys = [enemy_refs[k] for k in re.findall(r'ExtResource\("([^"]+)"\)', phase["Enemies"])]
    weight = sum(enemies[k].get("Weight", 1) for k in keys)
    cost = sum(enemies[k]["effective_cost"] * enemies[k].get("Weight", 1) for k in keys) / weight
    # 三个强制精英分别在180、360、540秒到期；这里按成功生成计，不宣称必定按时生成。
    milestones = sum(start <= time < end for time in (180, 360, 540))
    phase_rows.append(dict(phase=index, start_s=start, end_s=end, budget_per_s=phase["BudgetPerSecond"], budget_integral=(end-start)*phase["BudgetPerSecond"], elite_chance=phase["EliteChance"], max_alive=phase["MaxAlive"], max_pressure=phase["MaxAlivePressure"], weighted_base_cost=cost, forced_elites=milestones, enemies="/".join(keys)))
csv_out("phase-supply.csv", phase_rows)

income = []
detail = []
for efficiency in (.25, .5, 1):
    for extra in (0, 2):
        random_elites = count = 0
        for phase in phase_rows:
            effective_cost = phase["weighted_base_cost"] + phase["elite_chance"] * extra
            available = max(0, phase["budget_integral"] * efficiency - phase["forced_elites"] * (enemies["blade_spinner"]["effective_cost"] + extra))
            planned = available / effective_cost
            elite = planned * phase["elite_chance"]
            random_elites += elite
            count += planned
            detail.append(dict(budget_use_fraction=efficiency, extra_per_elite=extra, phase=phase["phase"], random_planned_enemies=planned, random_elites_expected=elite))
        for collection in (.5, .8, 1):
            dropped = 10 * (random_elites + 3) + 50
            collected = collection * dropped
            income.append(dict(budget_use_fraction=efficiency, extra_per_elite=extra, random_spawn_count_reference=count, random_elites_expected=random_elites, forced_elites=3, final_boss=1, total_hostile_kills_reference=count+4, dropped_shards_expected=dropped, pickup_fraction=collection, collected_shards_expected=collected, existing_settlement_full_clear=68, settled_total_expected=collected+68, note="成功完成三强敌/三信标/单首领；供给比例、拾取率及每精英附费为假设，非实测。基准权重忽略预算不足时候选截断、时机、压力上限和存活残留。"))
csv_out("income-scenarios.csv", income)
csv_out("elite-phase-detail.csv", detail)

dash = []
for name, distance, seconds in (("current_reference", 6, .5), ("authorized_target", 12, .3)):
    speed = distance / seconds
    lag = speed / 12 * (1 - math.exp(-12 * seconds))
    busy = max(.35, seconds)
    dash.append(dict(version=name, distance_m=distance, travel_s=seconds, mean_speed_mps=speed, distance_per_60hz_frame_m=speed/60, distance_per_30hz_frame_m=speed/30, animation_invulnerability_s=.35, vulnerable_travel_s=max(0, seconds-.35), nominal_busy_s=busy, base_cooldown_s=10, base_cycle_s=10+busy, continuous_camera_lag_at_travel_end_m=lag, three_full_charge_burst_m=distance*3, sustained_charge_interval_s=.5, charge_sustained_distance_mps=distance/.5, charge_invulnerability_fraction=.35/.5))
csv_out("dash-model.csv", dash)

growth = []
for kills in (0, 5, 50, 100, 250, 500, 750, 1000):
    # kills包含首领；b=1时不再额外加一次普通杀。
    for bosses in (0, 1):
        if bosses > kills:
            continue
        steel = kills//5 + 20*bosses
        for giant_body in (False, True):
            gain = steel + (100 if giant_body else 0)
            hp = 200 + gain
            growth.append(dict(personal_hostile_kills=kills, included_bosses=bosses, giant_body=giant_body, steel_max_health_gain=steel, total_gain_over_A200=gain, uncapped_A_max_health=hp, steel_current_health_increment_if_alive_and_uncapped=steel, giant_rune_flat_per_hit=hp*.08, gain_in_giant_flat_per_hit=gain*.08, lei_capped_max_health=min(30,hp), steel_heal_when_lei_capped=0, death_armor_gained_from_kills=kills//10, note="巨人/雷之印/死铠是不同单符文配装，不假定同时携带。巨化只加上限，不直接补100血；心之钢补血须当前生命>0且上限实际增加。"))
csv_out("growth-scenarios.csv", growth)

economy = []
for path in sorted((ROOT / "Config/Tables/AbilityCards").glob("*.tres")):
    value = row(path.relative_to(ROOT).as_posix())
    base, step, cap = value.get("UpgradeBaseCost", 20), value.get("UpgradeCostGrowth", 15), value.get("MaxLevel", 5)
    unlock = value.get("UnlockCost", 30)
    upgrades = [base+step*i for i in range(cap-1)]
    economy.append(dict(id=value["Id"], name=value["DisplayName"], unlock=unlock, upgrade_costs="/".join(map(str, upgrades)), full_unlock_and_upgrade=unlock+sum(upgrades)))
csv_out("economy-costs.csv", economy)

timing = []
for acquired in (0, 180, 360, 540):
    expected_kills = 0
    for phase in phase_rows:
        remaining = max(0, phase["end_s"]-max(acquired, phase["start_s"]))
        expected_kills += remaining*phase["budget_per_s"]/phase["weighted_base_cost"]
    # 仅展示剩余供给对成长的影响：忽略强敌预算占用、实际出场/死亡延迟。
    bosses = 1
    growth_reference = expected_kills/5+20*bosses
    timing.append(dict(acquired_at_s=acquired, future_random_kills_reference=expected_kills, final_boss_assumed=1, continuous_growth_reference=growth_reference, note="满供给、击杀全部归本人、无免费召唤；浮点为连续参考，不把floor(E[K]/5)当作E[floor(K/5)]。三强敌和首领普通计数须按真实死亡日志另加。"))
csv_out("growth-acquisition-timing.csv", timing)

# 记录执行路径与边界来源，主会话并行写入时保留本次实际读取到的文本。
for relative in ["Scripts/Abilities/BackdashAbility.cs", "Scripts/Combat/CharacterMovement.cs", "Scripts/Camera/CameraRig.cs", "Scenes/Camera/CameraRig.tscn", "Scenes/Levels/SurvivalArena.tscn", "Scenes/Player/Player.tscn", "Scripts/Run/Survival/SurvivalDirector.cs", "Scripts/Run/Survival/SurvivalDirector.Spawning.cs", "Scripts/Run/Survival/SurvivalDirector.Shards.cs", "Scripts/Run/Survival/RunShardPickup.cs", "Scripts/Config/SurvivalDefinition.cs", "Scripts/Rewards/RunController.cs", "Scripts/Run/RunModeController.cs", "Scripts/Run/AdventureCheckpoint.cs", "Scripts/Run/MetaProgress.cs", "Scripts/Rewards/RunSettlement.cs", "Scripts/Rewards/KillHealthGrowthRule.cs", "Scripts/Combat/Unit.HealthGrowth.cs", "Scripts/Combat/Unit.cs", "Scripts/Config/RewardEffects/StatRewardEffect.cs", "Scripts/Config/RewardEffects/UnitScaleRewardEffect.cs", "Config/Tables/Upgrades/reward_1341.tres", "Config/Tables/Upgrades/reward_1342.tres", "Config/Tables/Runes/rune_giant.tres", "Config/Tables/Runes/rune_lei_seal.tres", "Config/Tables/Runes/rune_death_armor.tres", "Config/Tables/Runes/rune_war_seal.tres", "Config/Tables/Upgrades/mut_blood_armor.tres", "Config/Tables/Skills/skl_a1_skill_1.tres", "Config/Tables/Skills/skl_a3_w1_2.tres", "Scripts/Abilities/HawkEyeAbility.cs", "Config/Tables/Upgrades/evo_spare_dodge.tres", "Assets/Characters/CloverWayfarer/skills/Skill_A1_Backdash.tres", "Config/EliteTraits/elite_summoner.tres", "Config/Tables/Meta/meta_quota.tres"]:
    if (ROOT / relative).exists():
        read(relative)

# 工作簿只读，原始需求不代填、不保存。
for relative in ["Scripts/Rewards/RewardPool.cs", "Config/Tables/Upgrades/upg_life.tres", "Config/Tables/Upgrades/upg_life_plus.tres", "Config/Tables/Upgrades/upg_life_plus2.tres", "Scripts/Abilities/Ability.cs", "Scripts/Rewards/RuneRules.cs", "Config/VisualProfiles/clover_wayfarer.tres"]:
    read(relative)
book = ROOT / "outputs/acceptance-20261026/更新与修复验收.xlsx"
ns = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
with zipfile.ZipFile(book) as archive:
    workbook = ET.fromstring(archive.read("xl/workbook.xml"))
    sheet = next(s for s in workbook.findall("m:sheets/m:sheet", ns) if s.attrib["name"] == "验收清单")
    rid = sheet.attrib["{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id"]
    rels = ET.fromstring(archive.read("xl/_rels/workbook.xml.rels"))
    target = next(r.attrib["Target"] for r in rels if r.attrib["Id"] == rid)
    sheet_name = target.lstrip("/") if target.startswith("/") else "xl/"+target
    strings = ["".join(t.itertext()) for t in ET.fromstring(archive.read("xl/sharedStrings.xml")).findall("m:si", ns)] if "xl/sharedStrings.xml" in archive.namelist() else []
    cells = {}
    for cell in ET.fromstring(archive.read(sheet_name)).findall(".//m:c", ns):
        if cell.attrib["r"] not in ("C75", "D75", "C76", "D76"):
            continue
        text = cell.findtext("m:v", "", ns)
        cells[cell.attrib["r"]] = strings[int(text)] if cell.attrib.get("t") == "s" else "".join(cell.find("m:is", ns).itertext()) if cell.attrib.get("t") == "inlineStr" else text
(OUT / "source-snapshot.json").write_text(json.dumps({"sources": sources, "acceptance": {"sha256": hashlib.sha256(book.read_bytes()).hexdigest(), "cells": cells}, "note": "主会话并行实现中，快照保存本次读取结果；模型目标值固定按已裁定12/.3、10/50，不修改生产。"}, ensure_ascii=False, indent=2), encoding="utf-8")
assert abs(sum(p["budget_integral"] for p in phase_rows)-2572.56)<1e-6
print(json.dumps({"phase_rows": phase_rows, "full_supply_income": [r for r in income if r["budget_use_fraction"] == 1 and r["pickup_fraction"] == 1], "costs": economy, "dash": dash, "acceptance": cells}, ensure_ascii=False, indent=2))

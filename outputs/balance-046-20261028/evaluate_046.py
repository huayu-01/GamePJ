"""046只读评估：读取现行资源，输出解析值、解析模型与简化追逐模型；不启动Godot。"""
from pathlib import Path
import csv
import hashlib
import json
import math
import re
import sys
import zipfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
sys.stdout.reconfigure(encoding="utf-8")


def scalar(raw):
    raw = raw.strip()
    if raw in ("true", "false"):
        return raw == "true"
    try:
        return json.loads(raw)
    except (ValueError, TypeError):
        return raw


def read_blocks(path):
    # 只解析资源的显式字段；不冒充Godot反射或完整继承求值。
    text = path.read_text(encoding="utf-8-sig")
    headings = list(re.finditer(r"(?m)^\[([^\]]+)\]\s*$", text))
    result = []
    for i, heading in enumerate(headings):
        end = headings[i + 1].start() if i + 1 < len(headings) else len(text)
        fields, lines = {}, {}
        start = heading.end()
        for match in re.finditer(r"(?m)^(\w+)\s*=\s*([^\r\n]+)", text[start:end]):
            key = match.group(1)
            fields[key] = scalar(match.group(2))
            lines[key] = text.count("\n", 0, start + match.start()) + 1
        result.append({"header": heading.group(1), "fields": fields, "lines": lines})
    return result


def resource(path):
    return next(b["fields"] for b in read_blocks(path) if b["header"] == "resource")


def write_csv(name, rows):
    with (OUT / name).open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def param(path, key):
    for block in read_blocks(path):
        if block["fields"].get("Key") == key:
            return block["fields"].get("Value", 0)
    raise ValueError((path, key))


def acceptance_cell():
    # 直接只读ZIP/XML，不启动Excel，不改工作簿。
    path = ROOT / "outputs/acceptance-20261026/更新与修复验收.xlsx"
    ns = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
    with zipfile.ZipFile(path) as archive:
        strings = []
        if "xl/sharedStrings.xml" in archive.namelist():
            strings = ["".join(t.itertext()) for t in ET.fromstring(archive.read("xl/sharedStrings.xml")).findall("m:si", ns)]
        workbook = ET.fromstring(archive.read("xl/workbook.xml"))
        sheet = next(s for s in workbook.findall("m:sheets/m:sheet", ns) if s.attrib["name"] == "验收清单")
        rid = sheet.attrib["{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id"]
        rels = ET.fromstring(archive.read("xl/_rels/workbook.xml.rels"))
        target = next(r.attrib["Target"] for r in rels if r.attrib["Id"] == rid)
        sheet_path = target.lstrip("/") if target.startswith("/") else "xl/" + target
        tree = ET.fromstring(archive.read(sheet_path))
        result = {}
        for cell in tree.findall(".//m:c", ns):
            if cell.attrib["r"] not in ("C55", "D55"):
                continue
            value = cell.findtext("m:v", default="", namespaces=ns)
            result[cell.attrib["r"]] = strings[int(value)] if cell.attrib.get("t") == "s" else "".join(cell.find("m:is", ns).itertext()) if cell.attrib.get("t") == "inlineStr" else value
    return {"path": str(path.relative_to(ROOT)), "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "cells": result}


source_paths = set()
for folder in ("Config/Tables/Enemies", "Config/Tables/Characters", "Config/Tables/Weapons", "Config/Tables/Skills", "Config/Tables/SkillSeries", "Config/Tables/Upgrades", "Config/Tables/Runes", "Config/Tables/Effects", "Config/EliteTraits", "Config/EnemyPlans", "Config/EnemyActions", "Config/Modes", "Config/Tables/AiProfiles"):
    source_paths.update((ROOT / folder).glob("*.tres"))
source_paths.update((ROOT / "Scripts").rglob("*.cs"))
source_paths.update((ROOT / "Scenes/Enemies").glob("*.tscn"))
source_paths.update([ROOT / "Scenes/Player/Player.tscn", ROOT / "Scenes/Combat/MeleeStrike.tscn", ROOT / "Config/EnemyCrowd.tres", ROOT / "Config/ConfigIndex.tres"])
manifest = [{"path": str(p.relative_to(ROOT)).replace("\\", "/"), "sha256": hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(source_paths)]
snapshots = {str(p.relative_to(ROOT)).replace("\\", "/"): read_blocks(p) for p in sorted(source_paths) if p.suffix in (".tres", ".tscn")}
global_row = resource(ROOT / "Config/Tables/Enemies/enemy_global.tres")
elite_source = (ROOT / "Scripts/Combat/EliteModifier.cs").read_text(encoding="utf-8-sig")
elite_speed = float(re.search(r"float MoveSpeedMultiplier.*?=\s*([\d.]+)f", elite_source).group(1))
elite_health = float(re.search(r"float HealthMultiplier.*?=\s*([\d.]+)f", elite_source).group(1))
enemy_rows = []
for path in sorted((ROOT / "Config/Tables/Enemies").glob("*.tres")):
    row = resource(path)
    if row.get("IsGlobal"):
        continue
    enemy_rows.append({"id": row.get("Id", str(row["NumericId"])), "name": row.get("DisplayName", ""), "source": str(path.relative_to(ROOT)).replace("\\", "/"), "base_speed_mps": row["MoveSpeed"], "ordinary_speed_mps": row["MoveSpeed"] * global_row["MoveSpeedMultiplier"], "elite_speed_mps": row["MoveSpeed"] * global_row["MoveSpeedMultiplier"] * elite_speed, "base_hp": row["MaxHealth"], "global_hp": row["MaxHealth"] * global_row["HealthMultiplier"], "base_armor": row.get("StartingArmor", 0), "armor_dr": row.get("ArmorDamageReduction", .3), "extra_dr": row.get("DamageReduction", 0), "scene": row["ScenePath"]})
write_csv("enemy-speeds.csv", enemy_rows)

attack_scale = param(ROOT / "Config/Tables/Skills/skl_a1_passive.tres", "AttackMoveSpeedScale")
quick = next(b["fields"]["Value"] for b in read_blocks(ROOT / "Config/Tables/Upgrades/upg_swift.tres") if b["fields"].get("Stat") == "MoveSpeedMultiplier")
march = next(b["fields"]["Value"] for b in read_blocks(ROOT / "Config/Tables/Runes/rune_march.tres") if b["fields"].get("Stat") == "MoveSpeedMultiplier")
players = []
for path in sorted((ROOT / "Config/Tables/Characters").glob("chr_*.tres")):
    row = resource(path)
    for levels in (0, 3, 5):
        for rune in (1, march):
            multiplier = (1 + quick * levels) * rune
            for mode, scale in (("walk", 1), ("sprint", row["SprintSpeedMultiplier"]), ("ground_attack", attack_scale if row["Id"] == "chr_hunter" else 0), ("maintained_hover", .5)):
                speed = row["MoveSpeed"] * scale * (1 if mode == "maintained_hover" else multiplier)
                players.append({"character": row["Id"], "quick_levels": levels, "march_multiplier": rune, "mode": mode, "speed_mps": speed, "note": "地面攻击无位移覆盖时；浮空维持忽略成长；稳态值不含加速度和控制"})
write_csv("player-speeds.csv", players)

approach = []
for e in enemy_rows:
    if e["id"] not in ("enemy_fast_melee", "enemy_chaser", "enemy_blade_spinner", "enemy_heavy_slammer", "enemy_guard", "enemy_exploder"):
        continue
    for p in players:
        if p["character"] != "chr_hunter" or p["mode"] == "maintained_hover":
            continue
        for state, scale in (("ordinary", 1), ("elite", elite_speed), ("elite_speeder", elite_speed * 1.25)):
            speed = e["ordinary_speed_mps"] * scale
            close = speed - p["speed_mps"]
            approach.append({"enemy": e["id"], "state": state, "player_mode": p["mode"], "quick_levels": p["quick_levels"], "march_multiplier": p["march_multiplier"], "enemy_speed_mps": speed, "player_speed_mps": p["speed_mps"], "closing_mps": close, "initial_center_m": 15, "comparison_radius_m": 2, "contact_s": 13 / close if close > 1e-8 else "不会追上", "model": "同向共线稳态，无攻击停顿、技能、地形、避让；统一2米仅为档位比较"})
write_csv("approach-times.csv", approach)

phases = []
mode_path = ROOT / "Config/Modes/twilight_survival.tres"
phase_blocks = [b["fields"] for b in read_blocks(mode_path) if b["header"].startswith("sub_resource")]
for index, phase in enumerate(phase_blocks):
    start = phase["StartFraction"] * 720
    end = phase_blocks[index + 1]["StartFraction"] * 720 if index + 1 < len(phase_blocks) else 720
    phases.append({"phase": index, "start_s": start, "end_s": end, "budget_per_s": phase["BudgetPerSecond"], "budget_integral": (end-start)*phase["BudgetPerSecond"], "max_alive": phase["MaxAlive"], "max_pressure": phase["MaxAlivePressure"], "hp_scale": phase["HealthScale"], "damage_scale": phase["DamageScale"], "elite_chance": phase["EliteChance"], "enemy_refs": phase["Enemies"]})
write_csv("phase-model.csv", phases)

def exact_hits(hp, armor, damage, armor_dr=.3, dr=0):
    # 与ReceiveHit相同：整击按命中前是否有甲决定减伤，破甲余伤不重新放大。
    count = 0
    while hp > 1e-8:
        effective = damage * (1 - min(.9, 1-(1-dr)*(1-armor_dr if armor > 0 else 1)))
        loss = min(armor, effective)
        armor -= loss
        hp -= effective - loss
        count += 1
        if count > 100000:
            raise RuntimeError("击杀计算未收敛")
    return count

durability = []
for e in enemy_rows:
    if e["id"] not in ("enemy_chaser", "enemy_heavy_slammer", "enemy_guard", "enemy_fast_melee"):
        continue
    for stage in (1, .65, 1.3):
        for elite in (False, True):
            hp = e["global_hp"] * stage * (elite_health if elite else 1)
            armor = e["base_armor"] + (max(120, hp*.3) if elite else 0)
            for hit in (10, 30, 100):
                armored_reduction = min(.9, 1-(1-e["extra_dr"])*(1-e["armor_dr"]))
                durability.append({"enemy": e["id"], "stage_hp_scale": stage, "elite": elite, "hp": hp, "armor": armor, "armor_dr": e["armor_dr"], "extra_dr": e["extra_dr"], "hit_before_defense": hit, "approx_ehp": hp/(1-min(.9,e["extra_dr"]))+armor/(1-armored_reduction), "exact_hits": exact_hits(hp, armor, hit, e["armor_dr"],e["extra_dr"]), "note": "无护盾、格挡、词条、回血或暴击；伤害固定，整击破甲口径；减伤共读各原型行，缺省沿UnitConfig"})
write_csv("durability.csv", durability)

# 六武器的当前数值样本：B武器基础来自正式Player实例覆盖和MeleeStrike回退。
def melee_base(node_name):
    header = f'node name="Attack" parent="{node_name}"'
    block = next(b for b in read_blocks(ROOT / "Scenes/Player/Player.tscn") if b["header"].startswith(header))
    inherited = next(b["fields"]["Damage"] for b in read_blocks(ROOT / "Scenes/Combat/MeleeStrike.tscn") if b["header"].startswith('node name="Attack"'))
    return block["fields"].get("Damage", inherited)


bow_row = resource(ROOT / "Config/Tables/Skills/skl_a3_w1_1.tres")
bow_steps = round((bow_row["ChargeMaxSeconds"]-bow_row["ChargeBaseSeconds"])/bow_row["ChargeStepSeconds"])
bow_power = 1 + (bow_row["ChargeStepScale"]-1)*bow_steps
weapon_inputs = [("能量掌", param(ROOT / "Config/Tables/Skills/skl_a1_w1_1.tres", "Damage"), 1, "skl_a1_w1_1", "单发"), ("飞刀", param(ROOT / "Config/Tables/Skills/skl_a2_w1_1.tres", "Damage"), 1, "skl_a2_w1_1", "单把命中；三把全中另乘3，仅为极端情景"), ("弓", param(ROOT / "Config/Tables/Skills/skl_a3_w1_1.tres", "Damage"), bow_power, "skl_a3_w1_1", "1.4秒满蓄，3档加算"), ("灵剑", melee_base("AbilityWeapon4Skill1"), 1, "skl_b1_w1_1", "连招首段；其余1.15/1.3/1.45/1.8"), ("镰刀", melee_base("AbilityWeapon5Skill1"), 1, "skl_b2_w1_1", "连招首段；其余1.2/1.9"), ("长枪", melee_base("AbilityWeapon6Skill1"), 1, "skl_b3_w1_1", "连招首段；其余1.1/1.25/1.1")]
combat = []
for weapon, base, power, skill, note in weapon_inputs:
    for label, strength, health, giant in (("起步", 0, 200 if weapon in ("能量掌", "飞刀", "弓") else 220, False), ("中期假设", 3, 260 if weapon in ("能量掌", "飞刀", "弓") else 280, False), ("成型巨人假设", 6, 320 if weapon in ("能量掌", "飞刀", "弓") else 340, True)):
        frozen = base * power * (1 + .15 * strength)
        mean = frozen * 1.05 + (health * .08 if giant else 0)
        combat.append({"weapon": weapon, "build": label, "strength_levels": strength, "max_hp": health, "giant": giant, "source_skill": skill, "frozen_base": frozen, "expected_raw_per_hit": mean, "echo_pair_raw_per_trigger": frozen*.7, "anchor_pulse_raw_per_trigger_per_target": frozen*.35, "note": note+"；5%暴击×2；无其他乘区。协击/脉冲是假设选取相应路线的独立敏感性，不假定同时持有；不换算真实DPS"})
write_csv("combat-scenarios.csv", combat)

def chase_model(route, standard, dt):
    # 点质量反例模型：三名敌人同点出发，无RVO、攻击循环或碰撞；只证明速度并非充分条件。
    player = [0., 0.]
    enemies = [[-15., 0.] for _ in range(3)]
    speeds = [6.5, standard, 2.3]
    samples = []
    for step in range(round(30/dt)+1):
        t = step * dt
        if route == "straight_walk":
            player = [6.5*t, 0.]
        elif route == "circle_walk":
            angle = 6.5*t/8
            player = [8*math.sin(angle), 8*(1-math.cos(angle))]
        elif route == "stationary":
            player = [0., 0.]
        elif route == "straight_attack":
            player = [6.5*attack_scale*t, 0.]
        if step > 0:
            for enemy, speed in zip(enemies, speeds):
                dx, dy = player[0]-enemy[0], player[1]-enemy[1]
                distance = math.hypot(dx,dy)
                travel = min(max(0,distance-2), speed*dt)
                if distance > 0:
                    enemy[0] += dx/distance*travel
                    enemy[1] += dy/distance*travel
        if step % max(1, round(1/dt)) == 0:
            distances = [math.dist(player, e) for e in enemies]
            samples.append({"route": route, "standard_mps": standard, "dt_s": dt, "time_s": t, "fast_gap_m": distances[0], "standard_gap_m": distances[1], "heavy_gap_m": distances[2], "fast_standard_pair_m": math.dist(enemies[0], enemies[1]), "standard_heavy_pair_m": math.dist(enemies[1], enemies[2])})
    return samples

traces = [row for route in ("straight_walk", "circle_walk", "stationary", "straight_attack") for standard in (4.2, 3.8) for dt in (1/60, 1/120) for row in chase_model(route, standard, dt)]
write_csv("pursuit-surrogate.csv", traces)

budget = sum(p["budget_integral"] for p in phases)
summary = {"evidence_level": "现行资源与源码静态核查＋解析推算＋脱离引擎的点质量反例模拟；本轮无Godot实测", "project": str(ROOT), "enemy_count_excluding_global": len(enemy_rows), "acceptance": acceptance_cell(), "global": global_row, "elite_speed": elite_speed, "player_attack_scale": attack_scale, "march": march, "quick_per_level": quick, "phase_budget_integral": budget, "enemy_experience_supply_reference": budget*2, "candidate_standard_speed": {"current": 4.2, "candidate": 3.8, "selected": False}, "assumptions": ["米是项目世界长度单位，速度米/秒；稳态模型忽略加速度", "接近模型15米初始中心距/2米比较半径不是实际技能射程", "角色B地面Attack策略无移动攻击被动时主动输入锁定；空中与技能位移独立", "普通HP=行HP×1.875×阶段；精英另×2.5；基础甲不乘上述生命倍率", "耐久计算仅无额外减伤/格挡/特性样本", "起步/中期/成型是可控试验配装，不是时间或胜率预测", "简化追逐不模拟RVO、地形、攻击停步、伤害或真实帧时"]}
(OUT / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
(OUT / "source-snapshot.json").write_text(json.dumps(snapshots, ensure_ascii=False, indent=2), encoding="utf-8")
(OUT / "source-manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")

# 只验证关键量纲与数学边界，未把模型自检包装成游戏机制测试。
assert abs(6.5*attack_scale-2.275) < 1e-9
assert abs(6.5*attack_scale*march*(1+5*quick)-5.57375) < 1e-9
assert abs(budget-2572.56) < 1e-7
assert exact_hits(100, 20, 100) == 2
assert exact_hits(100, 0, 100) == 1
assert len(enemy_rows) == 21
assert "敌群差异化" in summary["acceptance"]["cells"]["C55"]
changed = [m["path"] for m in manifest if hashlib.sha256((ROOT/m["path"]).read_bytes()).hexdigest() != m["sha256"]]
assert not changed, f"计算期间来源发生变化，需重新采集：{changed}"
print(json.dumps({"enemies": len(enemy_rows), "source_files": len(manifest), "phase_budget": budget, "real_engine_runs": 0, "source_changed_during_calculation": changed, "acceptance": summary["acceptance"]["cells"]}, ensure_ascii=False))

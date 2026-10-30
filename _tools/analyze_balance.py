"""只读平衡清单：使用引擎反射快照，保留默认值、引用来源和采样口径。"""
from pathlib import Path
import collections
import json
import sys
sys.stdout.reconfigure(encoding="utf-8")

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Logs/evidence/balance-audit"
data = json.loads((OUT / "balance-audit-20261027.json").read_text(encoding="utf-8-sig"))
nodes = {r["instance"]: r for r in data["resources"]}
index = next(r for r in nodes.values() if r["path"] == "res://Config/ConfigIndex.tres")["properties"]

def expand(value, trail=()):
    if isinstance(value, dict) and "reference" in value:
        key = value["reference"]
        if key in trail:
            return {"cycle": value.get("path") or value.get("type")}
        row = nodes.get(key)
        if row is None:
            return value
        return {"type": row["type"], "source": row["path"], **{k: expand(v, (*trail, key)) for k, v in row["properties"].items()}}
    if isinstance(value, list):
        return [expand(v, trail) for v in value]
    if isinstance(value, dict):
        return {k: expand(v, trail) for k, v in value.items()}
    return value

catalog = {k: expand(v) for k, v in index.items()}
standalone_types = {"SurvivalDefinition", "EliteTraitDefinition", "EliteTraitCatalog", "EnemyActionPlan", "EnemyBehaviorPressure", "SpecializationDefinition"}
standalone = {r["path"]: expand({"reference": r["instance"]}) for r in nodes.values()
              if r["path"] and (r["type"] in standalone_types or r["type"].endswith("TraitDefinition"))}
samples = data["samples"]
correction = OUT / "balance-audit-20261027-Scythe-Spear.json"
if correction.exists() and correction.stat().st_mtime > (OUT / "balance-audit-20261027.json").stat().st_mtime:
    fixed = json.loads(correction.read_text(encoding="utf-8-sig"))
    samples = [s for s in samples if s["weapon"] not in {"Scythe", "Spear"}] + fixed["samples"]

derived = []
for enemy in data["enemies"]:
    reduction = enemy["DamageReduction"]
    row = {**enemy, "approxEhp": enemy["MaxHealth"] / max(.1, 1-reduction) + enemy["Armor"] / max(.1, (1-reduction)*(1-enemy["ArmorDamageReduction"]))}
    row["ehpPerCost"] = row["approxEhp"] / enemy["cost"] if enemy.get("cost") else None
    derived.append(row)

resolved_plans = {}
for path, plan in standalone.items():
    if plan["type"] != "EnemyActionPlan": continue
    groups = {}
    for group in ["Opening", "Near", "Far", "Closing"]:
        groups[group] = []
        for step in plan[group]:
            # 强类型动作拥有的字段覆盖兼容段默认值，时序仍由动作段提供。
            state = {k:v for k,v in step.items() if k != "Action"}
            action = step.get("Action")
            if action:
                for key, val in action.items():
                    if key not in {"type", "source", "Kind", "Mode", "Spin", "Homing"}: state[key] = val
            groups[group].append(state)
    resolved_plans[path] = {**plan, **groups}

summary = {"sampleOrigin": "最新完整17份采样；离手0.9秒后收回，新按下等待输入帧启动；验收日志另附，不能仅凭JSON认定进程成功",
           "inventory": catalog, "standalone": standalone, "effectiveEnemies": derived, "samples": samples, "resolvedPlans": resolved_plans,
           "candidateA50": [{"weapon": s["weapon"], "action": s["action"], "distance": s["distance"], "withPrimary": s["withPrimary"],
                               "now": s["baseDps"], "candidate": s["baseDps"] * 1.5}
                              for s in samples if s["character"] == "Hunter"]}
(OUT / "balance-catalog-20261027.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")

def fmt(v):
    if isinstance(v, float): return f"{v:.4g}"
    return str(v)

def leaves(v, prefix=""):
    if isinstance(v, dict):
        for k, child in v.items():
            if k not in {"source", "Note", "DescriptionKey", "NameKey", "DisplayNameKey", "TargetAbility", "Targets", "ConvertedRewards", "RequiredEventAbility", "BonusAgainstAbility", "Pool", "NumericId", "Slot"}:
                yield from leaves(child, f"{prefix}.{k}" if prefix else k)
    elif isinstance(v, list):
        for i, child in enumerate(v): yield from leaves(child, f"{prefix}[{i}]")
    elif isinstance(v, (int, float)) and not isinstance(v, bool):
        yield f"{prefix}={fmt(v)}"

lines = ["# 当前数值清单（自动导出）", "", "来源：空白隔离档的引擎反射属性，包含未在资源中显式填写的默认值。仅列正式索引及独立玩法资源，不把反射生成的兼容副本重复计数。", "", "注意：导出字段不等于全部同时参与实战。强类型Action会覆盖EnemyActionStep的同名兼容默认值；技能行Cooldown=0可能表示沿用单位/场景值；精英通用参数有未被具体行为使用的默认字段。实际动作覆盖后的值见balance-catalog-20261027.json的resolvedPlans，已实例化单位与技能见effectiveEnemies/loadouts；效果判断见平衡评审。", "", "## 正式表", ""]
for key, values in catalog.items():
    if not isinstance(values, list): continue
    lines += [f"### {key}（{len(values)}）", "", "| 标识／名称 | 来源 | 数值 |", "|---|---|---|"]
    for row in values:
        if not isinstance(row, dict): continue
        label = row.get("DisplayName") or row.get("Title") or row.get("Id") or row.get("type")
        nums = "；".join(leaves(row)).replace("|", "\\|")
        lines.append(f"| {row.get('Id','')} {label} | {row.get('source','')} | {nums} |")
    lines.append("")
lines += ["## 独立玩法资源", ""]
for path, row in standalone.items():
    lines += [f"### {path}", "", "；".join(leaves(row)), ""]
lines += ["## 单靶采样", "", "| 武器 | 输入 | 距离 | 蓄力 | 同时左键 | 次数 | BaseDPS |", "|---|---|---:|---|---|---:|---:|"]
for s in samples:
    lines.append(f"| {s['weapon']} | {s['action']} | {s['distance']} | {s['chargeMode']} | {s['withPrimary']} | {s['hits']} | {s['baseDps']:.2f} |")
lines += ["", "BaseDPS为实际命中的攻击BaseDamage／真实秒数，去除随机暴击影响；静止无甲单靶、短窗口，不代表实战命中率、群体收益或整局胜率。"]
(ROOT / "docs/balance-values-20261027.md").write_text("\n".join(lines)+"\n", encoding="utf-8")

print("正式索引数量", {k: len(v) for k,v in catalog.items() if isinstance(v,list)})
print("独立资源", collections.Counter(v["type"] for v in standalone.values()))
print("相关类型", sorted({r["type"] for r in nodes.values() if any(x in r["type"] for x in ["Elite", "Enemy", "Pressure", "Special"])}))
print("全局", {k:v for k,v in catalog.items() if isinstance(v,(int,float))})
for key in ["Runes", "MetaUpgrades", "AbilityCards", "PressureMods", "Stages"]:
    print("TABLE",key)
    for row in catalog.get(key,[]):
        print(row.get("Id"), row.get("DisplayName"), "；".join(leaves(row)))
print("SAMPLES")
for s in samples: print(s["weapon"],s["action"],s["distance"],s["chargeMode"],s["withPrimary"],round(s["baseDps"],2),s["hits"])

"""汇总本轮已产生的验证证据，不启动引擎、不写生产数值。"""
from pathlib import Path
import difflib
import hashlib
import json
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")
OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[2]


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


head = read_json(ROOT / "Logs/evidence/survival-builds/retired-headless.json")
render = read_json(ROOT / "Logs/evidence/survival-builds/retired-render.json")
assert head["passed"] and render["passed"] and head["pass"] == 59 and render["pass"] == 60
report = ROOT / "docs/reviews/20261028/balance-046-implementation.md"
report.write_text(report.read_text(encoding="utf-8-sig").replace("无头46项、渲染47项通过", "无头59项、渲染60项通过"), encoding="utf-8")
after, diff = [], []
for item in read_json(OUT / "before-manifest.json"):
    path = ROOT / item["path"]
    data = path.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    after.append({"path": item["path"], "sha256": digest, "changed": digest != item["sha256"]})
    if digest != item["sha256"]:
        old = (OUT / "before" / (item["path"] + ".txt")).read_text(encoding="utf-8-sig")
        diff.extend(difflib.unified_diff(old.splitlines(keepends=True), data.decode("utf-8-sig").splitlines(keepends=True), fromfile="before/" + item["path"], tofile="after/" + item["path"]))
for relative in ["Scripts/Testing/SurvivalAugmentsProbe.Retired.cs", "docs/reviews/20261028/balance-046-implementation.md", "docs/reviews/20261028/balance-046.md", "outputs/balance-046-20261028/README.md"]:
    after.append({"path": relative, "sha256": hashlib.sha256((ROOT / relative).read_bytes()).hexdigest(), "before_captured": False})
(OUT / "after-manifest.json").write_text(json.dumps(after, ensure_ascii=False, indent=2), encoding="utf-8")
(OUT / "changes.diff").write_text("".join(diff), encoding="utf-8")
runs = []
for relative in ["Logs/evidence/survival-builds/run-20261028_175408.json", "Logs/evidence/survival/integration-20261028_173654.json", "Logs/evidence/survival/integration-20261028_174327.json", "Logs/evidence/survival/integration-20261028_174553.json", "Logs/evidence/elites/run-20261028_174604.json"]:
    data = (ROOT / relative).read_bytes()
    record = json.loads(data.decode("utf-8-sig"))
    runs.append({"path": relative, "sha256": hashlib.sha256(data).hexdigest(), "passed": record["Passed"], "saves_unchanged": record["SavesUnchanged"], "runs": record["Runs"]})
checks = dict(retirement_headless=59, retirement_render=60, snapshot=46, survival_render=72, armor_headless=17, armor_render=17, codex_headless=50, codex_render=53)
baseline = read_json(OUT.parent / "summary.json")
book = ROOT / baseline["acceptance"]["path"]
book_unchanged = hashlib.sha256(book.read_bytes()).hexdigest() == baseline["acceptance"]["sha256"]
evidence = {"date": "2026-10-28", "scope": "B1；十二项死战专属退出；护甲说明；最后风险表P2；不实施其他战斗候选", "checks": checks, "scripts": runs, "debug_build_warnings": 0, "debug_build_errors": 0, "release_build_warnings": 0, "release_build_errors": 0, "all_validation_scripts_saves_unchanged": all(r["saves_unchanged"] for r in runs), "acceptance_workbook_matches_initial_review": book_unchanged, "limitations": ["B1验证正式场景加载，未声称玩家压力分层已验收", "72项死战流程使用加速夹具，不是完整720秒性能/平衡", "40组武器评估矩阵待实战采样", "初次回归失败日志保留，后续独立严格复跑通过；未放宽stderr规则"], "source_diff_scope": "changes.diff仅覆盖已采集before的文件；after-manifest另列新文件和未采集前值的报告更新"}
(OUT / "evidence.json").write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding="utf-8")
assert book_unchanged
broken = []
for relative in ["docs/reviews/20261028/balance-046.md", "docs/reviews/20261028/balance-046-implementation.md", "docs/config-authoring.md", "docs/combat.md", "docs/survival-mode.md", "docs/survival-builds.md", "docs/README.md", "docs/enemy-roster.md", "outputs/balance-046-20261028/implementation/README.md", "outputs/balance-046-20261028/README.md"]:
    path = ROOT / relative
    for target in re.findall(r"\]\(([^)]+)\)", path.read_text(encoding="utf-8-sig")):
        if "://" not in target and target.split("#")[0] and not (path.parent / target.split("#")[0]).exists():
            broken.append((relative, target))
print(json.dumps({"checks": checks, "changed_baselined_files": sum(m.get("changed", False) for m in after), "workbook_unchanged": book_unchanged, "broken_links": broken}, ensure_ascii=False))
assert not broken

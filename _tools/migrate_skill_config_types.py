"""按已有能力声明迁移技能资源的脚本类型；保留文件路径、资源UID、引用与数值。"""
import argparse
import json
from pathlib import Path
import re

parser = argparse.ArgumentParser()
parser.add_argument('--apply', action='store_true')
parser.add_argument('--kinds', nargs='+', default=['boss'])
parser.add_argument('--report', required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
changes = []
for file in sorted((root / 'Config/Tables/Skills').glob('*.tres')):
    source = file.read_text(encoding='utf-8-sig')
    kind = ('boss' if re.search(r'^ApplyHandler\s*=\s*".+"', source, re.M)
            else 'fall' if re.search(r'^FallAttack\s*=', source, re.M)
            else 'combo' if re.search(r'^ComboSeries\s*=', source, re.M)
            else 'charge' if re.search(r'^HoldToCharge\s*=\s*true', source, re.M) else '')
    if kind not in args.kinds:
        continue
    target = {'boss': 'BossBehaviorConfig', 'fall': 'FallAbilityConfig', 'combo': 'ComboAbilityConfig', 'charge': 'ChargedAbilityConfig'}[kind]
    def replace_script(match):
        line = re.sub(r' uid="[^"]+"', '', match[0])
        return line.replace('Scripts/Config/AbilityConfig.cs', f'Scripts/Config/{target}.cs')
    updated = re.sub(r'^\[ext_resource[^\n]+path="res://Scripts/Config/AbilityConfig.cs"[^\n]*', replace_script, source, flags=re.M)
    updated = updated.replace('script_class="AbilityConfig"', f'script_class="{target}"')
    if updated == source:
        continue
    changes.append({'path': str(file.relative_to(root)), 'type': target})
    if args.apply:
        file.write_text(updated, encoding='utf-8')
report = root / args.report
report.parent.mkdir(parents=True, exist_ok=True)
report.write_text(json.dumps({'applied': args.apply, 'changes': changes}, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'技能定义类型迁移：{len(changes)} 项；applied={args.apply}')

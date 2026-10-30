"""创建按类型_数字ID命名的新资源；创建后仍须在Godot中配参数并加入显式索引。"""
import argparse
import json
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--type', required=True, choices=['skill', 'charge', 'combo', 'fall', 'enemy', 'augment', 'rune'])
parser.add_argument('--id', type=int)
parser.add_argument('--name', default='新内容')
parser.add_argument('--apply', action='store_true')
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
ledger_path = root / 'Config/content-id-ledger.json'
ledger = json.loads(ledger_path.read_text(encoding='utf-8'))
number = args.id or max(row['number'] for row in ledger.values()) + 1
if number <= 0 or number > 2147483647 or any(row['number'] == number for row in ledger.values()):
    raise SystemExit('数字ID已保留或不合法。')
folder, script, prefix = {
    'skill': ('Skills', 'AbilityConfig', 'skill'), 'charge': ('Skills', 'ChargedAbilityConfig', 'skill'),
    'combo': ('Skills', 'ComboAbilityConfig', 'skill'), 'fall': ('Skills', 'FallAbilityConfig', 'skill'),
    'enemy': ('Enemies', 'EnemyConfig', 'enemy'), 'augment': ('Upgrades', 'RewardConfig', 'augment'), 'rune': ('Runes', 'RuneConfig', 'rune'),
}[args.type]
relative = 'Config/Tables/%s/%s_%d.tres' % (folder, prefix, number)
target = root / relative
if target.exists():
    raise SystemExit('目标已存在，禁止覆盖。')
print(relative)
if args.apply:
    source = '[gd_resource type="Resource" script_class="%s" format=3]\n[ext_resource type="Script" path="res://Scripts/Config/%s.cs" id="1"]\n[resource]\nscript = ExtResource("1")\nNumericId = %d\nDisplayName = %s\n' % (script, script, number, json.dumps(args.name, ensure_ascii=False))
    target.write_text(source, encoding='utf-8')
    ledger['numeric:' + str(number)] = {'number': number, 'path': relative}
    ledger_path.write_text(json.dumps(ledger, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('已创建；还需编辑参数与显式索引。')

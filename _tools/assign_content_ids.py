"""为具有稳定身份的制作资源分配永不重排的全局数字ID；账本保留已退役编号。"""
import argparse
import json
from pathlib import Path
import re

parser = argparse.ArgumentParser()
parser.add_argument('--apply', action='store_true')
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
ledger_path = root / 'Config/content-id-ledger.json'
ledger = json.loads(ledger_path.read_text(encoding='utf-8')) if ledger_path.exists() else {}
reserved = {row['number']: key for key, row in ledger.items()}
next_id = max([999] + list(reserved)) + 1
seen = {}
changed = []
for file in sorted((root / 'Config').rglob('*.tres')):
    source = file.read_text(encoding='utf-8-sig')
    if '[resource]' not in source:
        continue
    header, body = source.rsplit('[resource]', 1)
    alias = re.search(r'^Id = "([^"]+)"', body, re.M)
    number_field = re.search(r'^NumericId = (\d+)', body, re.M)
    if not alias and not number_field:
        continue
    key = alias[1] if alias and (not number_field or alias[1] != number_field[1]) else 'numeric:' + number_field[1]
    if key in seen:
        raise ValueError('重复旧身份：' + key)
    seen[key] = str(file.relative_to(root))
    declared = int(number_field[1]) if number_field else 0
    number = ledger[key]['number'] if key in ledger else declared or next_id
    if declared and declared != number:
        raise ValueError('已分配数字ID被改写：' + key)
    if number in reserved and reserved[number] != key:
        raise ValueError('数字ID已被其他内容保留：' + str(number))
    reserved[number] = key
    next_id = max(next_id, number + 1)
    ledger[key] = {'number': number, 'path': str(file.relative_to(root)).replace('\\', '/')}
    if not number_field:
        changed.append(key)
        body = re.sub(r'^(script = [^\n]+)', r'\1\nNumericId = ' + str(number), body, count=1, flags=re.M)
        if args.apply:
            file.write_text(header + '[resource]' + body, encoding='utf-8')
if args.apply:
    ledger_path.write_text(json.dumps(ledger, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    # 既有路径继续兼容；技能图标先实际使用icon_数字规则，不维护手工映射。
    icons = root / 'Assets/UI/Icons/Skills'
    for key in seen:
        origin = icons / (key + '_icon.svg')
        target = icons / ('icon_' + str(ledger[key]['number']) + '.svg')
        if origin.exists() and not target.exists():
            target.write_bytes(origin.read_bytes())
print('数字身份校验：%d项；本次分配%d项；applied=%s' % (len(seen), len(changed), args.apply))
if not args.apply and changed:
    raise SystemExit('尚有未分配资源，请确认后使用--apply分配。')

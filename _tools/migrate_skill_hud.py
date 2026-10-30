"""将旧技能HUD引用迁为按身份命名的图片，图片本身的颜色原样保留。"""
import json
from pathlib import Path
import re
from datetime import datetime
root = Path(__file__).resolve().parent.parent
changed = []
for file in sorted((root / 'Config/Tables/Skills').glob('*.tres')):
    source = file.read_text(encoding='utf-8-sig')
    identity = re.search(r'^Id = "([^"]+)"', source, re.M)
    reference = re.search(r'^HudIcon = ExtResource\("([^"]+)"\)', source, re.M)
    icon = re.search(r'^\[ext_resource[^\n]+path="(res://[^"]+)"[^\n]+id="' + re.escape(reference[1]) + r'"[^\n]*\n', source, re.M) if reference else None
    if not identity or not icon:
        continue
    origin = root / icon[1][6:]
    target = root / f'Assets/UI/Icons/Skills/{identity[1]}_icon{origin.suffix}'
    if not target.exists():
        target.write_bytes(origin.read_bytes())
    updated = source[:icon.start()] + source[icon.end():]
    updated = re.sub(r'^Hud(?:Icon|Color|Order) = [^\n]+\n', '', updated, flags=re.M)
    file.write_text(updated, encoding='utf-8')
    changed.append({'skill': identity[1], 'icon': str(target.relative_to(root))})
report = root / ('Logs/evidence/update-fixes/skill-hud-migration-' + datetime.now().strftime('%Y%m%d_%H%M%S') + '.json')
report.write_text(json.dumps(changed, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'HUD资源迁移 {len(changed)} 项')

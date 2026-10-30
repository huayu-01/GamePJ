from pathlib import Path
from urllib.parse import unquote
import hashlib, json, re, struct

root = Path.cwd()
output = root / 'outputs/visual-047-048-20261028'
broken = []
for name in ['visual-047-048.md','visual-047-048-current.md','visual-047-048-initial.md']:
    report = root / 'docs/reviews/20261028' / name
    for target in re.findall(r'\]\(([^)]+)\)', report.read_text(encoding='utf-8')):
        if '://' not in target and not (report.parent / unquote(target.split('#')[0])).exists():
            broken.append(target)
page = (output / 'index.html').read_text(encoding='utf-8')
for target in re.findall(r'(?:src|href)="([^"]+)"', page):
    if '://' not in target and not (output / unquote(target)).exists():
        broken.append(target)
script = page.split('<script>')[1].split('</script>')[0]
(output / 'support/review-page-script.js').write_text(script, encoding='utf-8')
files = []
for path in sorted((output/'boards').glob('*.png')):
    data=path.read_bytes()
    width,height=struct.unpack('>II',data[16:24])
    files.append({'file':path.name,'width':width,'height':height,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
assert len(files)==17, len(files)
assert not broken, broken
summary={'broken_links':broken,'board_count':len(files),'boards':files,'capture_stderr_bytes':(output/'evidence/capture-stderr.log').stat().st_size,'board_stderr_bytes':(output/'evidence/board-stderr.log').stat().st_size,'four_board_stderr_bytes':(output/'evidence/four-stderr.log').stat().st_size,'native_board_visual_qa':'已查看A/B/C/D板；当前采用C方向和正常深度。新版毛玻璃三页及奖励中央灰透/边缘雾化v3均已视觉查看，无状态示例、按键栏可见。','html_browser_qa':'先前IAB绑定两次超时；当前HTML仅校验资源链接与JS语法，不声称完成浏览器交互验收。','latest_revision':'C推进；撤销场景顶层。UI简化毛玻璃和图标、页面按键栏；三选一卡牌中央偏灰透明，边缘磨砂。'}
(output/'evidence/delivery-verification.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(summary,ensure_ascii=False,indent=2))

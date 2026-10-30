"""汇总生存模式交付证据；失败现场保留，每项选取最后一次实际运行结果。"""
from pathlib import Path
from urllib.parse import unquote, urlparse
import hashlib, json, re, subprocess

root = Path(__file__).resolve().parents[1]
evidence = root / 'Logs/evidence/survival'
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest().upper()

latest = {}
reports = sorted(evidence.glob('integration-*.json'))
for path in reports:
    report = read(path)
    assert report['SavesUnchanged'], str(path)
    for item in report['Runs']:
        latest[item['Name']] = dict(item, Report=str(path))
required = 'clover replacement base base_memory run_flow player_flow content effects decoupling interaction slots snapshot charge interruption specialization synergy adventure adventure_render main survival'.split()
for name in required:
    item = latest[name]
    assert item['Passed'] and item['Exit'] == 0, item
    assert Path(item['Stderr']).stat().st_size == 0, item
    assert sha(Path(item['Stdout'])) == item['SHA256'], item

mode = read(evidence / 'run-20261026_002747.json')
assert mode['Passed'] and mode['SavesUnchanged']
for item in mode['Runs']:
    assert item['Exit'] == 0 and Path(item['Stderr']).stat().st_size == 0
    assert sha(Path(item['Stdout'])) == item['SHA256']
gc_path = sorted((root / 'Logs/evidence').glob('content-gc-*.json'))[-1]
gc = read(gc_path)
assert gc['Passed'] and gc['SavesUnchanged'] and len(gc['Runs']) >= 7, str(gc_path)
for item in gc['Runs']:
    assert item['Exit'] == 0 and Path(item['Stderr']).stat().st_size == 0
    assert sha(Path(item['Stdout'])) == item['StdoutSHA256']

documents = [root / 'README.md', *sorted((root / 'docs').rglob('*.md'))]
documents += [root / 'Assets/Characters/CloverWayfarer/README.md']
broken = []
for path in documents:
    markdown = re.sub(r'```[\s\S]*?```|`[^`\n]*`', '', path.read_text(encoding='utf-8-sig'))
    for target in re.findall(r'\[[^\]\n]*\]\(([^)\n]+)\)', markdown):
        target = target.strip().strip('<>')
        if not target or target.startswith('#') or urlparse(target).scheme: continue
        target = unquote(target.split('#', 1)[0])
        destination = (path.parent / target).resolve()
        # 本次校验结束时才写出的同一份报告允许自引用。
        if destination == root / 'Logs/evidence/survival-delivery-20261026.json': continue
        if target and not destination.exists(): broken.append([str(path.relative_to(root)), target])
assert not broken, broken
compile((root / '_tools/build_survival_content.py').read_text(encoding='utf-8'), '_tools/build_survival_content.py', 'exec')
diff = subprocess.run(['git', 'diff', '--check'], cwd=root, capture_output=True, text=True, encoding='utf-8')
assert diff.returncode == 0, diff.stdout
root_logs = [p.name for p in root.iterdir() if p.is_file() and (p.suffix in ('.log', '.err', '.out') or p.name.endswith('.saves.json'))]
assert not root_logs, root_logs

screenshots = {p.name: sha(p) for p in evidence.glob('*.png')}
builds = sorted((root / 'Logs/validation').glob('survival_integration_*.Debug.log'))[-1:]
builds += sorted((root / 'Logs/validation').glob('survival_integration_*.ExportRelease.log'))[-1:]
for path in builds:
    text = path.read_text(encoding='utf-8-sig')
    assert '0 个警告' in text and '0 个错误' in text, str(path)
out = dict(passed=True, date='2026-10-26', mode='手动战斗，12分钟生存阶段后首领与撤离',
           integration=[latest[name] for name in required], mode_headless_and_render=mode,
           gc_report=str(gc_path), gc_runs=gc['Runs'], original_saves_unchanged=True,
           save_sha256=gc['SaveSHA256'], screenshots=screenshots,
           builds={str(p): sha(p) for p in builds}, checked_markdown=len(documents), broken_links=broken,
           root_logs=root_logs, main_historical_red_categories=5, main_hard_metrics=8,
           limits=['首版场景与技能姿态，非最终美术', '计时完整链路采用加速配置，另验正式12分钟入场；未声称长局平衡或性能基准'])
target = root / 'Logs/evidence/survival-delivery-20261026.json'
target.write_text(json.dumps(out, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'PASS delivery: {len(required)} integration cases, {len(gc["Runs"])} GC runs, {len(documents)} documents; {target}')

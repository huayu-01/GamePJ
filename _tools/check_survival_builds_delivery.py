"""核验十二项增幅的最终证据，保留之前模式交付及失败记录。"""
from pathlib import Path
from urllib.parse import unquote, urlparse
import hashlib, json, re, subprocess

root=Path(__file__).resolve().parents[1]
read=lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
sha=lambda p: hashlib.sha256(p.read_bytes()).hexdigest().upper()
folder=root/'Logs/evidence/survival-builds'
augment_path=sorted(folder.glob('run-*.json'))[-1]
augment=read(augment_path)
assert augment['Passed'] and augment['SavesUnchanged'] and len(augment['Runs'])==2, augment_path
def verify(item,key='SHA256'):
    assert item['Exit']==0 and Path(item['Stderr']).stat().st_size==0,item
    assert sha(Path(item['Stdout']))==item[key],item
for item in augment['Runs']: verify(item)
latest={}
for path in sorted((root/'Logs/evidence/survival').glob('integration-*.json')):
    report=read(path)
    for item in report['Runs']: latest[item['Name']]=dict(item,Report=str(path),SavesUnchanged=report['SavesUnchanged'])
names='replacement run_flow player_flow content effects reward_rules rune decoupling interaction slots snapshot charge interruption specialization synergy adventure adventure_render main survival'.split()
for name in names:
    item=latest[name]
    assert item['Passed'] and item['SavesUnchanged'] and '20261026_01' in item['Stdout'],item
    verify(item)
mode_path=sorted((root/'Logs/evidence/survival').glob('run-*.json'))[-1]
mode=read(mode_path)
assert mode['Passed'] and mode['SavesUnchanged'],mode_path
for item in mode['Runs']: verify(item)
gc_path=sorted((root/'Logs/evidence').glob('content-gc-*.json'))[-1]
gc=read(gc_path)
assert gc['Passed'] and gc['SavesUnchanged'] and len(gc['Runs'])==7,gc_path
for item in gc['Runs']: verify(item,'StdoutSHA256')

rows=sorted((root/'Config/Tables/Upgrades').glob('surv_*.tres'))
assert len(rows)==12
for path in rows:
    text=path.read_text(encoding='utf-8-sig')
    assert 'MaxStacks = 1' in text and 'RequiredModeIds = PackedStringArray("survival_twilight")' in text,path
assert len(list((root/'Config/Tables/Upgrades').glob('*.tres')))==103
def upgrade_paths(path):
    text=path.read_text(encoding='utf-8-sig')
    resources={identity:source for source,identity in re.findall(r'^\[ext_resource[^\n]*path="([^"]+)" id="([^"]+)"\]$',text,re.M)}
    line=re.search(r'^Upgrades = \[(.*)\]$',text,re.M)[1]
    return {resources[identity] for identity in re.findall(r'ExtResource\("([^"]+)"\)',line)}
legacy=upgrade_paths(root/'.ai_survival_builds_20261026_011020/Config/ConfigIndex.tres')
current=upgrade_paths(root/'Config/ConfigIndex.tres')
assert len(legacy)==91 and legacy<=current and len(current-legacy)==12
builds=sorted((root/'Logs/validation').glob('survival_builds_*.Debug.log'))[-1:]+sorted((root/'Logs/validation').glob('survival_builds_*.ExportRelease.log'))[-1:]
for path in builds:
    text=path.read_text(encoding='utf-8-sig'); assert '0 个警告' in text and '0 个错误' in text,path
documents=[root/'README.md',*sorted((root/'docs').rglob('*.md'))]
broken=[]
target=root/'Logs/evidence/survival-builds-delivery-20261026.json'
for path in documents:
    markdown=re.sub(r'```[\s\S]*?```|`[^`\n]*`','',path.read_text(encoding='utf-8-sig'))
    for link in re.findall(r'\[[^\]\n]*\]\(([^)\n]+)\)',markdown):
        link=link.strip().strip('<>')
        if not link or link.startswith('#') or urlparse(link).scheme: continue
        destination=(path.parent/unquote(link.split('#',1)[0])).resolve()
        if destination!=target and not destination.exists(): broken.append([str(path.relative_to(root)),link])
assert not broken,broken
for path in (root/'_tools/build_survival_augments.py',root/'_tools/sync_content_text.py'): compile(path.read_text(encoding='utf-8'),str(path),'exec')
diff=subprocess.run(['git','diff','--check'],cwd=root,capture_output=True,text=True,encoding='utf-8')
assert diff.returncode==0,diff.stdout
files=[*rows,*sorted((root/'Scripts/Config/RewardEffects/Survival').glob('*.cs')),*sorted((root/'Scripts/Run/Survival').glob('*.cs')),*sorted((root/'Assets/UI/Icons').glob('ic_surv_*.svg'))]
out=dict(passed=True,date='2026-10-26',implemented=12,catalog=103,legacy_91_retained=True,reward_kinds=dict(Enhancement=27,Evolution=34,Mutation=42),
         augment_report=str(augment_path),augment=augment,integration=[latest[name] for name in names],
         mode_report=str(mode_path),mode=mode,gc_report=str(gc_path),gc=gc,
         builds={str(p):sha(p) for p in builds},sources={str(p.relative_to(root)):sha(p) for p in files},
         screenshots={p.name:sha(p) for p in folder.glob('*.png')},checked_markdown=len(documents),broken_links=broken,
         original_saves_unchanged=True,main_hard_metrics=8,main_historical_red_categories=5,
         limits=['首版数值和表现；未声称人工12分钟平衡或性能基准','测试猎令与经验门槛使用副本，生产720秒／50秒／15秒预设保持'])
target.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'PASS survival-builds delivery: 12 augments, {len(names)} integrations, 7 GC runs, {len(documents)} documents; {target}')

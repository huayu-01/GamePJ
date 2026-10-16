"""从已核实的免费包提取最小依赖，不导入演示、HDR、付费预设或重复全局类。"""
from pathlib import Path
import hashlib
import json
import re
import zipfile

root = Path(__file__).resolve().parents[3]
stage = root / '_art_staging' / 'art_refresh'
dest = root / 'Assets' / 'ThirdParty' / 'BinbunArtRefresh'
dest.mkdir(parents=True, exist_ok=True)

def write(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('w', encoding='utf-8', newline='\n') as stream:
        stream.write('\n'.join(line.rstrip() for line in content.replace('\r\n', '\n').splitlines()) + '\n')

def local_materials(text):
    return re.sub(r'(\[sub_resource type="(?:ShaderMaterial|ParticleProcessMaterial)"[^\n]+\]\n)', r'\1resource_local_to_scene = true\n', text)

loot = zipfile.ZipFile(stage / 'loot-vfx.zip')
prefix = 'assets/BinbunVFX/'
for name in loot.namelist():
    if not name.endswith(('.gdshader', '.gdshaderinc', '.gd')):
        continue
    # 四个 shader、三个 include 和两个控制脚本组成完整闭包。
    text = loot.read(name).decode('utf-8')
    text = re.sub(r'^class_name .*\n', '', text, flags=re.M)
    text = text.replace('extends VFXEffect', 'extends "res://Assets/ThirdParty/BinbunArtRefresh/Loot/shared/script/vfx_effect.gd"')
    if name.endswith('.gdshader'):
        # 非负 HDR 输出，避免已知的负颜色光晕伪影。
        text = re.sub(r'(ALBEDO = )([^;]+);', r'\1max(\2, vec3(0.0));', text)
        text = text.replace('pow((1.0 - UV.y) * 2.0 - 1.0, 0.2)', 'pow(max((1.0 - UV.y) * 2.0 - 1.0, 0.0), 0.2)')
    write(dest / 'Loot' / name[len(prefix):], text)
name = prefix + 'loot_effects/effects/ground/ground_loot_vfx_rare.tscn'
text = loot.read(name).decode('utf-8')
text = re.sub(r' uid="[^"]+"', '', text)
text = text.replace('res://' + prefix, 'res://Assets/ThirdParty/BinbunArtRefresh/Loot/')
write(root / 'Scenes/ArtRefresh/FxLootBeacon.tscn', local_materials(text))

battle = zipfile.ZipFile(stage / 'battle-fx.zip')
prefix = 'BattleFXFree/assets/BinbunVFX_Vol2/BattleFX/'
for relative in ['script/VFXBattleSwingBB.gd', 'shader/swing.gdshader', 'license.txt']:
    text = battle.read(prefix + relative).decode('utf-8')
    text = text.replace('extends VFXControllerBB', 'extends "res://Assets/Effects/binbun/shared/script/VFXControllerBB.gd"')
    text = re.sub(r'^class_name .*\n', '', text, flags=re.M)
    write(dest / 'Battle' / relative, text)
text = battle.read(prefix + 'effects/swing/vfx_blank_swing.tscn').decode('utf-8')
text = re.sub(r' uid="[^"]+"', '', text)
text = text.replace('res://assets/BinbunVFX_Vol2/BattleFX/', 'res://Assets/ThirdParty/BinbunArtRefresh/Battle/')
text = text.replace('script = ExtResource("1_gvyho")', 'script = ExtResource("1_gvyho")\none_shot = true\nautoplay = true')
text = text.replace('Color(1, 1, 1, 1)', 'Color(0.65, 0.96, 1, 1)')
text = text.replace('Color(0.36862746, 0.36862746, 0.36862746, 1)', 'Color(0.10, 0.65, 0.72, 1)')
write(root / 'Scenes/ArtRefresh/FxBattleSwing.tscn', local_materials(text))

manifest = []
for path in sorted(stage.glob('*.zip')):
    archive = zipfile.ZipFile(path)
    licences = [name for name in archive.namelist() if any(key in name.lower() for key in ['license.md', 'license.txt', 'credits.txt'])]
    manifest.append({'package': path.stem, 'bytes': path.stat().st_size, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'licence_files': licences})
write(root / 'docs/art-refresh-downloads.json', json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
print('已提取免费依赖；压缩包留在忽略的暂存目录。')

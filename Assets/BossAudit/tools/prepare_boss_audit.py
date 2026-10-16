"""独立检查用户首领文件，不把原素材复制进正式工程或Git。"""
from pathlib import Path
import hashlib
import json
import zipfile

source = Path(r'F:\Godot项目\3d肉鸽雨中哈迪斯\素材\模型\首领')
target = Path(r'F:\Godot项目\_boss_asset_audit')
(target / 'models').mkdir(parents=True, exist_ok=True)
manifest = []
for archive in sorted(source.glob('*.zip')):
    with zipfile.ZipFile(archive) as package:
        package_files = package.namelist()
        filename = next(n for n in package_files if n.lower().endswith('.fbx'))
        data = package.read(filename)
    name = archive.stem.replace('_asset', '')
    (target / 'models' / (name + '.fbx')).write_bytes(data)
    manifest.append({'name': name, 'source_zip': str(archive), 'sha256': hashlib.sha256(archive.read_bytes()).hexdigest(), 'fbx_bytes': len(data), 'package_files': package_files})
with (target / 'source-manifest.json').open('w', encoding='utf8', newline='\n') as stream:
    json.dump(manifest, stream, ensure_ascii=False, indent=2)
for name in ['audit.gd']:
    (target / name).write_bytes((Path(__file__).parent / name).read_bytes())
with (target / 'project.godot').open('w', encoding='utf8', newline='\n') as stream:
    stream.write('config_version=5\n\n[application]\nconfig/name="Boss Asset Audit"\nrun/main_scene="res://Audit.tscn"\n\n[display]\nwindow/size/viewport_width=1920\nwindow/size/viewport_height=1080\nwindow/stretch/mode="viewport"\n\n[rendering]\nrendering_device/driver.windows="d3d12"\n')
with (target / 'Audit.tscn').open('w', encoding='utf8', newline='\n') as stream:
    stream.write('[gd_scene load_steps=2 format=3]\n\n[ext_resource type="Script" path="res://audit.gd" id="1"]\n\n[node name="BossAudit" type="Node3D"]\nscript = ExtResource("1")\n')
print('已在独立审查工程暂存6个FBX；正式项目和原始ZIP未修改。')

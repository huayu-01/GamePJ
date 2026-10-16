"""从作者免费包提取角色，并合并武士的独立动作；不重复存放纹理和网格。"""
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import struct
import zipfile

root = Path(__file__).resolve().parents[3]
stage = root / '_art_staging' / 'selected_characters'
output = root / 'Assets' / 'Characters'

def read_glb(data):
    size = struct.unpack_from('<I', data, 12)[0]
    return json.loads(data[20:20 + size]), data[28 + size:]

def write_glb(path, document, binary):
    document['buffers'] = [{'byteLength': len(binary)}]
    encoded = json.dumps(document, separators=(',', ':')).encode('utf8')
    encoded += b' ' * (-len(encoded) % 4)
    binary += b'\0' * (-len(binary) % 4)
    path.write_bytes(struct.pack('<III', 0x46546c67, 2, 28 + len(encoded) + len(binary)) + struct.pack('<II', len(encoded), 0x4e4f534a) + encoded + struct.pack('<II', len(binary), 0x004e4942) + binary)

def external_images(document, binary, directory, prefix):
    # Godot默认会把内嵌图片再提取一份PNG；改成引用同一PNG，避免仓库重复保存约54MB。
    removed = set()
    for index, image in enumerate(document.get('images', [])):
        view_index = image.pop('bufferView')
        removed.add(view_index)
        view = document['bufferViews'][view_index]
        name = prefix + '_texture_' + str(index) + '.png'
        directory.joinpath(name).write_bytes(binary[view.get('byteOffset', 0):view.get('byteOffset', 0) + view['byteLength']])
        image['uri'] = name
    compact = b''
    views = []
    remap = {}
    for index, original in enumerate(document['bufferViews']):
        if index in removed:
            continue
        view = deepcopy(original)
        chunk = binary[view.get('byteOffset', 0):view.get('byteOffset', 0) + view['byteLength']]
        compact += b'\0' * (-len(compact) % 4)
        view['byteOffset'] = len(compact)
        compact += chunk
        remap[index] = len(views)
        views.append(view)
    def update(value):
        if isinstance(value, dict):
            for key, child in value.items():
                if key == 'bufferView':
                    value[key] = remap[child]
                else:
                    update(child)
        elif isinstance(value, list):
            for child in value:
                update(child)
    update(document)
    document['bufferViews'] = views
    return compact

sources = []
for slug, directory in [('elf-archer-lyra', 'Lyra'), ('samurai-warrior-pack', 'Samurai')]:
    archive = next(stage.glob(slug + '-*.zip'))
    target = output / directory
    target.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive) as package:
        files = package.namelist()
        readme = next(n for n in files if 'readme' in n.lower())
        (target / 'AUTHOR_README.txt').write_bytes(package.read(readme).decode('utf8').replace('\r', '').encode('utf8'))
        if directory == 'Lyra':
            selected = next(n for n in files if n.endswith('.glb') and 'Merged_Animations' in n)
            document, binary = read_glb(package.read(selected))
        else:
            selected = next(n for n in files if n.endswith('_Idle_9_withSkin.glb'))
            document, binary = read_glb(package.read(selected))
            document['animations'] = []
            for name in files:
                if not name.endswith('.glb') or '_Animation_' not in name:
                    continue
                other, other_binary = read_glb(package.read(name))
                assert [n.get('name') for n in other['nodes']] == [n.get('name') for n in document['nodes']]
                accessors = {}
                for animation in other['animations']:
                    animation = deepcopy(animation)
                    for sampler in animation['samplers']:
                        for key in ['input', 'output']:
                            old_index = sampler[key]
                            if old_index not in accessors:
                                accessor = deepcopy(other['accessors'][old_index])
                                view = deepcopy(other['bufferViews'][accessor['bufferView']])
                                chunk = other_binary[view.get('byteOffset', 0):view.get('byteOffset', 0) + view['byteLength']]
                                binary += b'\0' * (-len(binary) % 4)
                                view['byteOffset'] = len(binary)
                                view['buffer'] = 0
                                binary += chunk
                                accessor['bufferView'] = len(document['bufferViews'])
                                document['bufferViews'].append(view)
                                accessors[old_index] = len(document['accessors'])
                                document['accessors'].append(accessor)
                            sampler[key] = accessors[old_index]
                    document['animations'].append(animation)
        aliases = {'Idle_9':'idle', 'Walking':'walk', 'walking_man':'walk', 'Running':'sprint', 'running':'sprint', 'Shot_and_Slow_Fall_Backward':'die', 'Regular_Jump':'jump', 'Attack':'attack', 'Double_Combo_Attack':'double_attack', 'Block1':'block', 'Block5':'block', 'Push_Forward_and_Stop':'push'}
        for animation in document['animations']:
            name = animation['name'].split('|')[-2] if '|' in animation['name'] else animation['name']
            animation['name'] = aliases[name]
        binary = external_images(document, binary, target, directory.lower())
        write_glb(target / (directory.lower() + '.glb'), document, binary)
        for mesh in document['meshes']:
            for primitive in mesh['primitives']:
                positions = document['accessors'][primitive['attributes']['POSITION']]
                print(directory, 'vertices', positions['count'], 'bounds', positions.get('min'), positions.get('max'), flush=True)
        record = {'name': directory, 'source': 'https://grigory-petrov.itch.io/' + slug, 'zip_sha256': hashlib.sha256(archive.read_bytes()).hexdigest(), 'animations': [a['name'] for a in document['animations']], 'triangles': sum(document['accessors'][primitive['indices']]['count'] // 3 for mesh in document['meshes'] for primitive in mesh['primitives']), 'bones': len(document['skins'][0]['joints'])}
        sources.append(record)
        print(record, flush=True)
(output / 'sources.json').write_bytes((json.dumps(sources, ensure_ascii=False, indent=2) + '\n').encode('utf8'))

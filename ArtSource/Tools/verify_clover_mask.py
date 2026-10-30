"""对比试绑备份，确认弯面具时没有改变动作、骨架和其余造型。"""
import bpy
import json
import hashlib
from pathlib import Path

ROOT=Path('F:/Godot项目/项目a')


def digest(value):
    return hashlib.sha256(json.dumps(value,sort_keys=True).encode()).hexdigest()


def snapshot(path):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    curves={}
    for action in bpy.data.actions:
        items=[]
        for slot in action.slots:
            for layer in action.layers:
                for strip in layer.strips:
                    bag=strip.channelbag(slot)
                    if bag:
                        items.extend((c.data_path,c.array_index,[(tuple(k.co),k.interpolation) for k in c.keyframe_points]) for c in bag.fcurves)
        curves[action.name]=digest(sorted(items))
    meshes={o.name:digest([tuple(v.co) for v in o.data.vertices])
        for o in bpy.data.collections['CW_Character'].all_objects if o.type=='MESH'}
    rig=bpy.data.objects['CW_Rig']
    bones=digest([(b.name,b.parent.name if b.parent else None,tuple(b.head_local),tuple(b.tail_local)) for b in rig.data.bones])
    return {'curves':curves,'meshes':meshes,'bones':bones}


before=snapshot(ROOT/'ArtSource/Revisions/clover-wayfarer/v09-basic-motion-trial/clover-wayfarer.blend')
after=snapshot(ROOT/'ArtSource/Blender/clover-wayfarer/clover-wayfarer.blend')
changed=sorted(name for name in before['meshes'] if before['meshes'][name]!=after['meshes'][name])
report={'passed':before['curves']==after['curves'] and before['bones']==after['bones'] and changed==['CW_ForeheadClover','CW_JadeEyes','CW_OwlMask'],
    'animation_keyframes_identical':before['curves']==after['curves'],
    'rest_skeleton_identical':before['bones']==after['bones'],
    'modified_meshes':changed,'preserved_actions':sorted(after['curves'])}
assert report['passed'],report
(ROOT/'ArtSource/Exports/clover-wayfarer/mask-curve-validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('CW_MASK_VALIDATED '+json.dumps(report),flush=True)

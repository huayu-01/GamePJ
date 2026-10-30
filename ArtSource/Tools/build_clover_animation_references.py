"""从本地合法动画库渲染人形动作参考，不修改三叶旅童或游戏资源。"""
import bpy
import json
import math
import sys
from pathlib import Path
from mathutils import Vector

ROOT = Path('F:/Godot项目/项目a')
OUTPUT = ROOT / 'ArtSource/Animations/References/clover-wayfarer-basics'
SOURCE = ROOT / 'ArtSource/Animations/UAL/Source'
FPS = 24
SIZE = 480
CLIPS = [
    ('01-idle', '待机', 'UAL1_Standard.glb', 'Idle_Loop', '观察呼吸、轻微重心变化；适合做角色基础待机。'),
    ('02-walk', '走路', 'UAL1_Standard.glb', 'Walk_Loop', '主参考；保留脚掌落点与髋部节奏，短腿角色缩短步幅。'),
    ('03-jog', '慢跑', 'UAL1_Standard.glb', 'Jog_Fwd_Loop', '主参考；小幅前倾、较轻的上下起伏。'),
    ('04-sprint', '快跑', 'UAL1_Standard.glb', 'Sprint_Loop', '用于快速移动；当前角色需要收小原动作的步幅和前倾。'),
    ('05-jump-start', '起跳', 'UAL1_Standard.glb', 'Jump_Start', '观察蓄力压低、发力蹬地与身体伸展。'),
    ('06-air-loop', '空中循环', 'UAL1_Standard.glb', 'Jump_Loop', '参考空中重心与腿部收放；不是专门的下落动作。'),
    ('07-land', '落地', 'UAL1_Standard.glb', 'Jump_Land', '观察屈膝缓冲及回稳；斗篷下摆应有迟到的回落。'),
    ('08-hit', '轻受击', 'UAL1_Standard.glb', 'Hit_Chest', '短促躯干反应；不必照搬完整手部表现。'),
    ('09-knockback', '击退反应', 'UAL2_Standard.glb', 'Hit_Knockback', '参考大幅重心后移和回稳。'),
    ('10-death', '死亡', 'UAL1_Standard.glb', 'Death01', '参考失衡和倒地节奏；最终应检查面具和长斗篷的落地形态。'),
    ('11-roll', '翻滚', 'UAL1_Standard.glb', 'Roll', '作为闪避候选；长斗篷版本需要另行设计防穿插和回摆。'),
    ('12-cast', '基础施法', 'UAL1_Standard.glb', 'Spell_Simple_Shoot', '保留完整人形源动作作为参考，提取躯干发力与释放节奏。'),
]


def make_material(name, color):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    shader = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Roughness'].default_value = .95
    return mat


def setup():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    scene.name = 'AnimationReferences'
    scene.render.engine = 'BLENDER_EEVEE'
    scene.eevee.taa_render_samples = 48
    scene.render.resolution_x = SIZE
    scene.render.resolution_y = SIZE
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.film_transparent = False
    scene.render.fps = 30
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.look = 'AgX - Medium High Contrast'
    scene.world.use_nodes = True
    background = next(n for n in scene.world.node_tree.nodes if n.type == 'BACKGROUND')
    background.inputs[0].default_value = (.33, .38, .38, 1)
    background.inputs[1].default_value = .6
    floor = make_material('REF_Floor', (.22, .27, .28))
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.015))
    bpy.context.object.data.materials.append(floor)
    for name, location, energy, size in [('Key', (-3, -4, 5), 550, 4), ('Fill', (3, -1, 3), 260, 3)]:
        data = bpy.data.lights.new(name, 'AREA')
        data.energy = energy
        data.shape = 'DISK'
        data.size = size
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = location
        obj.rotation_euler = (Vector((0, 0, .9))-obj.location).to_track_quat('-Z', 'Y').to_euler()
    data = bpy.data.cameras.new('ReferenceCamera')
    camera = bpy.data.objects.new('ReferenceCamera', data)
    scene.collection.objects.link(camera)
    data.type = 'ORTHO'
    data.ortho_scale = 2.65
    camera.location = (3.3, -6.2, 2.7)
    camera.rotation_euler = (Vector((0, 0, .95))-camera.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = camera
    return scene


def load_actor(filename):
    before = set(bpy.data.objects)
    previous_actions = set(bpy.data.actions)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE / filename))
    imported = list(set(bpy.data.objects)-before)
    rigs = [o for o in imported if o.type == 'ARMATURE']
    assert len(rigs) == 1
    actor = rigs[0]
    meshes = [o for o in imported if o.type == 'MESH' and any(m.type == 'ARMATURE' for m in o.modifiers)]
    body_material = make_material('REF_Mannequin', (.53, .72, .66))
    for obj in meshes:
        obj.data.materials.clear()
        obj.data.materials.append(body_material)
        for poly in obj.data.polygons:
            poly.material_index = 0
            poly.use_smooth = True
    actions = {a.name: a for a in set(bpy.data.actions)-previous_actions}
    return actor, imported, actions


def render_library(scene, filename, selected, inspect=False):
    actor, imported, actions = load_actor(filename)
    print('REF_IMPORT '+json.dumps({'file':filename,'rig':actor.name,'scale':list(actor.scale),'actions':list(actions)},ensure_ascii=False),flush=True)
    for identifier, label, _, clip, note in selected:
        action = actions[clip]
        actor.animation_data_create()
        actor.animation_data.action = action
        start, end = map(float, action.frame_range)
        duration = (end-start)/30
        count = max(5, math.ceil(duration*FPS))
        # 每段使用固定镜头，先覆盖整段动作包围盒，避免起跳或倒地被画面截断。
        camera = scene.camera
        camera.rotation_euler = (Vector((0,0,.95))-Vector((3.3,-6.2,2.7))).to_track_quat('-Z','Y').to_euler()
        rotation = camera.rotation_euler.to_quaternion()
        right = rotation @ Vector((1,0,0))
        up = rotation @ Vector((0,1,0))
        axis = rotation @ Vector((0,0,1))
        samples=[]
        for index in range(0,count,max(1,count//12)):
            frame=start+index/FPS*30
            scene.frame_set(math.floor(frame),subframe=frame-math.floor(frame))
            graph=bpy.context.evaluated_depsgraph_get()
            for obj in imported:
                if obj.type!='MESH' or not any(m.type=='ARMATURE' for m in obj.modifiers): continue
                evaluated=obj.evaluated_get(graph)
                samples.extend(evaluated.matrix_world@Vector(corner) for corner in evaluated.bound_box)
        minx=min(p.dot(right) for p in samples); maxx=max(p.dot(right) for p in samples)
        miny=min(p.dot(up) for p in samples); maxy=max(p.dot(up) for p in samples)
        center=right*((minx+maxx)*.5)+up*((miny+maxy)*.5)
        camera.location=center+axis*8
        camera.data.ortho_scale=max(maxx-minx,maxy-miny)*1.20
        directory = OUTPUT/'frames'/identifier
        directory.mkdir(parents=True,exist_ok=True)
        indices = [0] if inspect else range(count)
        for index in indices:
            frame = start+min(index/FPS,duration)*30
            scene.frame_set(math.floor(frame),subframe=frame-math.floor(frame))
            scene.render.filepath = str(directory/f'{index:04d}.png')
            bpy.ops.render.render(write_still=True)
        records.append({'id':identifier,'label':label,'source_file':filename,'source_clip':clip,'source_duration_seconds':duration,'frame_start':start,'frame_end':end,'fps':FPS,'frame_count':count,'ortho_scale':camera.data.ortho_scale,'note':note})
        print('REF_CLIP_DONE '+identifier,flush=True)
        if inspect: break
    for obj in imported:
        bpy.data.objects.remove(obj,do_unlink=True)
    for action in actions.values():
        action.use_fake_user = False
        if action.users == 0: bpy.data.actions.remove(action)


scene = setup()
records = []
inspect = '--inspect' in sys.argv
for filename in ('UAL1_Standard.glb','UAL2_Standard.glb'):
    selected = [c for c in CLIPS if c[2] == filename]
    render_library(scene,filename,selected,inspect)
    if inspect: break
(OUTPUT/'render-manifest.json').write_text(json.dumps({'fps':FPS,'resolution':[SIZE,SIZE],'clips':records},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('REF_RENDER_DONE',flush=True)

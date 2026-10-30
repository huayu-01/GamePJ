"""将已筛选的人形节奏适配到短腿旅童，烘焙六项试绑动作。"""
import bpy
import json
import math
from mathutils import Matrix, Vector, Quaternion, Euler

ROOT = 'F:/Godot项目/项目a/'
BLEND = ROOT + 'ArtSource/Blender/clover-wayfarer/clover-wayfarer.blend'
FPS = 60
scene = bpy.data.scenes['CloverWayfarer']
bpy.context.window.scene = scene
rig = bpy.data.objects['CW_Rig']
rig.animation_data_clear()
for action in list(bpy.data.actions):
    if action.name in ('idle', 'walk', 'sprint', 'hit', 'die', 'jump'):
        bpy.data.actions.remove(action)
rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
order = [b.name for b in rig.data.bones]
character = list(bpy.data.collections['CW_Character'].all_objects)
meshes = [o for o in character if o.type == 'MESH']
for pb in rig.pose.bones:
    pb.rotation_mode = 'QUATERNION'
    pb.matrix_basis = Matrix.Identity(4)

# 源人形只作采样，制作完成后移除，不会进入候选模型或导出。
before = set(bpy.data.objects)
before_actions = set(bpy.data.actions)
bpy.ops.import_scene.gltf(filepath=ROOT+'ArtSource/Animations/UAL/Source/UAL1_Standard.glb')
imported = list(set(bpy.data.objects)-before)
source = next(o for o in imported if o.type == 'ARMATURE')
source_actions = {a.name: a for a in set(bpy.data.actions)-before_actions}
for obj in imported:
    obj.hide_render = True
mapping = {
    'Hips':'pelvis', 'Spine':'spine_01', 'Chest':'spine_03',
    'Neck':'neck_01', 'Head':'Head',
    'LeftThigh':'thigh_l', 'LeftShin':'calf_l', 'LeftFoot':'foot_l',
    'RightThigh':'thigh_r', 'RightShin':'calf_r', 'RightFoot':'foot_r',
}


def sample(name, phase):
    action = source_actions[name]
    source.animation_data.action = action
    start, end = action.frame_range
    frame = start+(end-start)*max(0, min(1, phase))
    scene.frame_set(math.floor(frame), subframe=frame-math.floor(frame))
    bpy.context.view_layer.update()
    result = {}
    for target, src in mapping.items():
        pb = source.pose.bones[src]
        delta = pb.matrix.to_quaternion() @ pb.bone.matrix_local.to_quaternion().inverted()
        result[target] = (delta, pb.head.copy())
    return result


def damp(q, factors):
    e = q.to_euler('XYZ')
    return Euler(tuple(e[i]*factors[i] for i in range(3)), 'XYZ').to_quaternion()


def solve_pose(rotations, hip_offset, cape_angles, root_offset=Vector((0,0,0))):
    poses = {}
    for name in order:
        bone = rig.data.bones[name]
        base = poses[bone.parent.name] @ rest[bone.parent.name].inverted() @ rest[name] if bone.parent else rest[name].copy()
        pb = rig.pose.bones[name]
        pb.location = (0,0,0)
        pb.scale = (1,1,1)
        if name == 'Root':
            pb.location = base.to_3x3().inverted() @ root_offset
            pb.rotation_quaternion = Quaternion()
        elif name.startswith('Cape'):
            pb.rotation_quaternion = Euler(cape_angles[name], 'XYZ').to_quaternion()
        else:
            desired = rotations.get(name, Quaternion()) @ rest[name].to_quaternion()
            pb.rotation_quaternion = base.to_quaternion().inverted() @ desired
            if name == 'Hips':
                pb.location = base.to_3x3().inverted() @ hip_offset
        poses[name] = base @ pb.matrix_basis
    return poses


def set_world_rotation(name, q, poses):
    bone = rig.data.bones[name]
    base = poses[bone.parent.name] @ rest[bone.parent.name].inverted() @ rest[name]
    pb = rig.pose.bones[name]
    pb.rotation_quaternion = base.to_quaternion().inverted() @ q
    poses[name] = base @ pb.matrix_basis


def foot_min_offset(side, delta):
    pivot = rig.data.bones[side+'Foot'].head_local
    objects = [o for o in meshes if o.name.startswith('CW_'+side) and ('Boot' in o.name or 'Sole' in o.name)]
    return min((delta @ (o.matrix_world @ v.co-pivot)).z for o in objects for v in o.data.vertices)


def solve_leg(side, goal, foot_delta, poses):
    thigh, shin, foot = [side+s for s in ('Thigh','Shin','Foot')]
    h = poses[thigh].translation.copy()
    l1 = rig.data.bones[thigh].length
    l2 = rig.data.bones[shin].length
    direction = goal-h
    distance = min(direction.length, l1+l2-.0005)
    direction.normalize()
    goal = h+direction*distance
    # 膝盖始终朝角色前方，避免近直腿处出现极点翻转。
    bend = Vector((0,-1,0))
    bend = (bend-direction*bend.dot(direction)).normalized()
    a = (l1*l1-l2*l2+distance*distance)/(2*distance)
    height = math.sqrt(max(0, l1*l1-a*a))
    knee = h+direction*a+bend*height
    for name, end in ((thigh,knee),(shin,goal)):
        current = poses[name]
        old_dir = current.to_quaternion() @ Vector((0,1,0))
        desired = (end-current.translation).normalized()
        q = old_dir.rotation_difference(desired) @ current.to_quaternion()
        set_world_rotation(name,q,poses)
        if name == thigh:
            poses[shin] = poses[thigh] @ rest[thigh].inverted() @ rest[shin] @ rig.pose.bones[shin].matrix_basis
        else:
            poses[foot] = poses[shin] @ rest[shin].inverted() @ rest[foot] @ rig.pose.bones[foot].matrix_basis
    set_world_rotation(foot,foot_delta @ rest[foot].to_quaternion(),poses)


def minimum_mesh_z():
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    low = 100
    for obj in meshes:
        evaluated = obj.evaluated_get(deps)
        mesh = evaluated.to_mesh()
        low = min(low, min((evaluated.matrix_world @ v.co).z for v in mesh.vertices))
        evaluated.to_mesh_clear()
    return low


def smooth(value):
    value = max(0,min(1,value))
    return value*value*(3-2*value)


def jump_height(t):
    # 仅供预览的位移曲线；实际资产不烘焙跳跃高度，交给游戏重力。
    if .08 < t < .61:
        p = (t-.08)/.53
        return 4*.30*p*(1-p)
    return 0.0


definitions = [
    ('idle','待机','Idle_Loop',2.5,True),
    ('walk','走路','Walk_Loop',4/3,True),
    ('sprint','跑步','Sprint_Loop',2/3,True),
    ('hit','受击','Hit_Chest',.46,False),
    ('die','死亡','Death01',2.4,False),
    ('jump','跳跃','Jump_Loop',.80,False),
]
manifest = {'asset':'clover-wayfarer','revision':'v09-basic-motion-trial','bake_fps':FPS,
            'candidate_only':True,'production_connected':False,'clips':[]}
idle_initial = sample('Idle_Loop',0)
for identifier, label, source_name, duration, loop in definitions:
    count = round(duration*FPS)
    duration = count/FPS
    # 先采源动作，后烘焙目标，避免两个场景时间轴相互干扰。
    records = [sample(source_name,i/count) for i in range(count+1)]
    foot_stats = {}
    for side in ('Left','Right'):
        pts = [r[side+'Foot'][1] for r in records]
        foot_stats[side] = {'center_y':(min(p.y for p in pts)+max(p.y for p in pts))*.5,
                            'min_z':min(p.z for p in pts)}
    hip_mean_z = sum(r['Hips'][1].z for r in records)/len(records)
    action = bpy.data.actions.new(identifier)
    action.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = action
    action['cw_basic_motion'] = True
    action['loop'] = loop
    action['source_clip'] = source_name
    action['source_license'] = 'CC0 / Universal Animation Library'
    floor_min = 100
    previous_quat = {}
    for index, record in enumerate(records):
        frame = index+1
        t = index/FPS
        phase = index/count
        scene.frame_set(frame)
        rotations = {}
        for name, (q,position) in record.items():
            if identifier == 'die': factors=(.95,.65,.70)
            elif name in ('Hips','Spine','Chest','Neck','Head'):
                factors=(.26,.25,.22) if identifier != 'hit' else (.62,.30,.25)
            else: factors=(.65,.35,.35)
            rotations[name] = damp(q,factors)
        cape = {n:(0,0,0) for n in ('CapeBack','CapeLeft','CapeRight')}
        if identifier in ('idle','walk','sprint'):
            speed = 2*math.pi*phase
            height = (record['Hips'][1].z-hip_mean_z)*(.13 if identifier=='sprint' else .20)
            lowering = {'idle':.012,'walk':.025,'sprint':.047}[identifier]
            hip = Vector((record['Hips'][1].x*.15,0,height-lowering))
            amount = {'idle':.012,'walk':.035,'sprint':.070}[identifier]
            cape['CapeBack'] = (-amount*.5+amount*.6*math.sin(speed-.65),0,0)
            cape['CapeLeft'] = (amount*.5*math.sin(speed-.6),0,amount*.22*math.sin(speed))
            cape['CapeRight'] = (-amount*.5*math.sin(speed-.6),0,amount*.22*math.sin(speed))
        elif identifier == 'hit':
            # 参考本身从受击峰值起步，补入30ms冲击建立，随后回稳。
            impact = smooth(min(1,t/.035))*(1-smooth((phase-.65)/.35))
            baseline = idle_initial
            for name in rotations:
                rotations[name] = damp(baseline[name][0],(.26,.25,.22)).slerp(rotations[name],impact)
            hip = Vector((0,0,-.012-.008*math.sin(math.pi*phase)))
            cape['CapeBack'] = (.075*math.sin(math.pi*phase),0,0)
        elif identifier == 'die':
            first = records[0]['Hips'][1]
            hip = Vector(((record['Hips'][1].x-first.x)*.42,
                          (record['Hips'][1].y-first.y)*.44,
                          (record['Hips'][1].z-first.z)*.45-.012))
            cape['CapeBack'] = (.08*math.sin(math.pi*min(1,phase*1.5)),0,0)
            # 成人参考末姿态弯腰仰头过多，大头角色改为躯干和后帽自然卧地。
            settle=smooth((phase-.31)/.27)
            flat=Euler((-math.pi/2,-.08,0),'XYZ').to_quaternion()
            for name in ('Hips','Spine','Chest','Neck','Head'):
                rotations[name]=rotations[name].slerp(flat,settle)
        else:
            # 快速小跳：80ms内离地，无长时间蓄力；空中收腿、落地短缓冲。
            tuck = smooth((t-.09)/.15)*(1-smooth((t-.43)/.17))
            compression = .014*math.sin(math.pi*t/.08) if t<.08 else 0
            if .61<t<.78: compression=.035*math.sin(math.pi*(t-.61)/.17)
            hip = Vector((0,0,-.012-compression))
            for name in ('Hips','Spine','Chest','Neck','Head'):
                idle_q=damp(idle_initial[name][0],(.26,.25,.22))
                air_q=damp(record[name][0],(.27,.12,.08))
                rotations[name]=idle_q.slerp(air_q,tuck)
            cape['CapeBack'] = (-.12*tuck+.07*math.sin(math.pi*max(0,min(1,(t-.61)/.19))),0,0)
            cape['CapeLeft'] = (0,0,-.04*tuck)
            cape['CapeRight'] = (0,0,.04*tuck)
        poses = solve_pose(rotations,hip,cape)
        if identifier != 'die':
            for side in ('Left','Right'):
                q, position = record[side+'Foot']
                foot_rotation = damp(q,(.12,.10,.14))
                if identifier in ('idle','hit','jump'):
                    foot_rotation = Quaternion()
                    y = -.008
                    lift = 0.0
                    if identifier == 'jump':
                        y = .035*tuck
                        lift = .115*tuck
                else:
                    y = (position.y-foot_stats[side]['center_y'])*(.31 if identifier=='walk' else .29)
                    lift = (position.z-foot_stats[side]['min_z'])*(.29 if identifier=='walk' else .27)
                z = -foot_min_offset(side,foot_rotation)+.012+lift
                goal = Vector(((1 if side=='Left' else -1)*.205,y,z))
                solve_leg(side,goal,foot_rotation,poses)
        # 死亡姿态按整身最低点贴地；巨头面具也参与，而非仅校正成人脚位。
        low = minimum_mesh_z()
        if identifier == 'die':
            root = rig.pose.bones['Root']
            root.location += rest['Root'].to_3x3().inverted() @ Vector((0,0,.012-low))
            low = minimum_mesh_z()
        floor_min = min(floor_min,low)
        for pb in rig.pose.bones:
            q=pb.rotation_quaternion.copy()
            if pb.name in previous_quat and q.dot(previous_quat[pb.name])<0:
                q.negate();pb.rotation_quaternion=q
            previous_quat[pb.name]=q.copy()
            pb.keyframe_insert('rotation_quaternion',frame=frame,group=pb.name)
            pb.keyframe_insert('location',frame=frame,group=pb.name)
    # 循环末帧与首帧严格一致，消除源待机的微小接缝。
    if loop:
        scene.frame_set(1)
        for pb in rig.pose.bones:
            pb.keyframe_insert('rotation_quaternion',frame=count+1,group=pb.name)
            pb.keyframe_insert('location',frame=count+1,group=pb.name)
    for slot in action.slots:
        for layer in action.layers:
            for strip in layer.strips:
                bag=strip.channelbag(slot)
                if bag:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points:key.interpolation='LINEAR'
    manifest['clips'].append({'name':identifier,'label':label,'duration_seconds':duration,
        'frame_start':1,'frame_end':count+1,'loop':loop,'source_clip':source_name,
        'method':('target-authored direct jump using source airborne pose' if identifier=='jump' else 'world rotation retarget + whole-body floor correction' if identifier=='die' else 'world rotation retarget + short-leg foot trajectory IK'),
        'minimum_mesh_z':round(floor_min,6),'horizontal_root_motion':False,
        'preview_vertical_trajectory_only':identifier=='jump'})
    print('CW_BAKED '+identifier,flush=True)

for obj in imported:bpy.data.objects.remove(obj,do_unlink=True)
for action in source_actions.values():
    action.use_fake_user=False
    if action.users==0:bpy.data.actions.remove(action)
scene.render.fps=FPS
scene.render.fps_base=1
scene.frame_start=1
scene.frame_end=151
rig.animation_data.action=bpy.data.actions['idle']
scene.frame_set(1)
scene['cw_motion_manifest']=json.dumps(manifest,ensure_ascii=False)
for obj in scene.objects:obj.select_set(False)
rig.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.wm.save_as_mainfile(filepath=BLEND)
for obj in character:obj.select_set(True)
bpy.ops.export_scene.gltf(filepath=ROOT+'ArtSource/Exports/clover-wayfarer/clover-wayfarer.glb',
    export_format='GLB',use_selection=True,use_active_scene=True,export_animations=True,
    export_animation_mode='ACTIONS',export_frame_range=False,export_force_sampling=True,
    export_materials='EXPORT',export_skins=True,export_yup=True,export_cameras=False,export_lights=False)
print('CW_MOTION_MANIFEST '+json.dumps(manifest,ensure_ascii=False),flush=True)

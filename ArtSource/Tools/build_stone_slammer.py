"""建立几何卡通重击敌人的可编辑源模型、刚性蒙皮骨架和动作。"""
import bpy
import math
import json
from mathutils import Vector, Quaternion, Euler

PROJECT = "F:/Godot项目/项目a"
SOURCE = PROJECT + "/ArtSource/Blender/stone-slammer"
RUNTIME = PROJECT + "/Assets/Enemies/StoneSlammer"
SCENE_NAME = "StoneSlammer_Geometric_v1"

# 只重建本工具自己的场景，其他打开的场景保持原状。
scene = bpy.data.scenes.get(SCENE_NAME)
if scene:
    for obj in list(scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
else:
    scene = bpy.data.scenes.new(SCENE_NAME)
bpy.context.window.scene = scene
scene.render.fps = 60
scene.frame_start = 1
scene.frame_end = 121
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0

def linear(value):
    return value / 12.92 if value <= .04045 else ((value + .055) / 1.055) ** 2.4

def material(name, color):
    result = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    result.use_nodes = True
    rgb = [linear(int(color[i:i+2], 16) / 255.0) for i in (0, 2, 4)]
    result.diffuse_color = (*rgb, 1)
    shader = next(n for n in result.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    shader.inputs["Base Color"].default_value = (*rgb, 1)
    shader.inputs["Roughness"].default_value = .9
    shader.inputs["Metallic"].default_value = 0
    return result

mats = {
    "stone": material("Slammer_Stone", "aeb6a4"),
    "light": material("Slammer_LightStone", "c4cbb8"),
    "joint": material("Slammer_Joints", "697466"),
    "moss": material("Slammer_Moss", "7f9c45"),
    "leather": material("Slammer_Leather", "826047"),
    "trim": material("Slammer_LeatherLight", "b68c54"),
    "dark": material("Slammer_FaceDark", "303c30"),
    "eye": material("Slammer_AmberEyes", "edb650"),
}
parts = []

def block(name, center, size, mat, bone, bevel=.08, rotation=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        modifier = obj.modifiers.new("BroadChamfers", "BEVEL")
        modifier.width = bevel
        modifier.segments = 1
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    if rotation:
        obj.rotation_euler = rotation
    obj.data.materials.append(mats[mat])
    group = obj.vertex_groups.new(name=bone)
    group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    parts.append(obj)
    return obj

def between(name, start, end, width, depth, mat, bone, bevel=.07):
    start, end = Vector(start), Vector(end)
    obj = block(name, (start+end)*.5, (width, depth, (end-start).length), mat, bone, bevel)
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0,0,1)).rotation_difference((end-start).normalized())
    return obj

def cylinder(name, start, end, radius, mat, bone):
    start, end = Vector(start), Vector(end)
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=radius, depth=(end-start).length, location=(start+end)*.5)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0,0,1)).rotation_difference((end-start).normalized())
    obj.data.materials.append(mats[mat])
    group = obj.vertex_groups.new(name=bone)
    group.add(list(range(len(obj.data.vertices))), 1, "REPLACE")
    parts.append(obj)
    return obj

# 大形只保留胸、髋、头和四肢；关节间留缝，石块不发生橡皮式拉伸。
block("Pelvis", (0,.04,1.14), (1.04,.74,.48), "joint", "Hips", .13)
block("Chest", (0,.01,1.86), (1.44,.87,1.10), "stone", "Chest", .20)
block("UpperChest", (0,-.025,2.18), (1.40,.85,.32), "light", "UpperChest", .10)
block("WaistBelt", (0,-.015,1.29), (1.12,.80,.19), "leather", "Hips", .055)
block("LoinPlate", (0,-.425,1.02), (.58,.07,.41), "trim", "Hips", .06)
block("Neck", (0,.015,2.37), (.42,.47,.28), "joint", "Neck", .07)
block("Head", (0,-.055,2.61), (.73,.67,.64), "stone", "Head", .15)
block("HeadMoss", (.035,-.005,2.918), (.55,.45,.075), "moss", "Head", .04)

for side, sign in (("Left",1),("Right",-1)):
    hip = (sign*.37,.035,1.14)
    knee = (sign*.44,.015,.71)
    ankle = (sign*.46,.015,.31)
    shoulder = (sign*.78,.01,2.20)
    elbow = (sign*1.055,-.015,1.65)
    wrist = (sign*1.12,-.115,1.12)
    between(side+"Thigh", hip, knee, .55,.65,"stone",side+"UpperLeg",.105)
    block(side+"Knee", knee, (.50,.57,.30),"joint",side+"LowerLeg",.07)
    between(side+"Shin", knee, ankle, .50,.55,"stone",side+"LowerLeg",.085)
    block(side+"Foot", (sign*.46,-.16,.18), (.63,.91,.36),"light",side+"Foot",.10)
    between(side+"UpperArm",shoulder,elbow,.51,.59,"joint",side+"UpperArm",.09)
    block(side+"Shoulder",(sign*.88,.01,2.17),(.88,.86,.84),"stone",side+"UpperArm",.19)
    block(side+"MossCap",(sign*.88,.0,2.575),(.72,.63,.095),"moss",side+"UpperArm",.06)
    between(side+"Forearm",(sign*1.07,-.035,1.66),wrist,.63,.63,"stone",side+"LowerArm",.13)
    block(side+"Cuff",(sign*1.12,-.105,1.24),(.65,.66,.14),"leather",side+"LowerArm",.04)
    block(side+"Fist",(sign*1.12,-.14,1.035),(.49,.54,.47),"light",side+"Hand",.105)
    block(side+"Thumb",(sign*.875,-.25,1.09),(.20,.29,.24),"stone",side+"Hand",.055)
    # 深色眼窝、琥珀眼和一整块眉骨，远镜头也能识别表情。
    block(side+"EyeSocket",(sign*.175,-.392,2.64),(.245,.045,.155),"dark","Head",.03)
    block(side+"Eye",(sign*.169,-.42,2.64),(.135,.025,.092),"eye","Head",.018)
    block(side+"Pupil",(sign*.156,-.438,2.64),(.044,.016,.072),"dark","Head",.008)
    block(side+"Brow",(sign*.175,-.425,2.755),(.33,.14,.12),"light","Head",.028, (0,-sign*.22,0))
block("Nose",(0,-.445,2.50),(.21,.20,.18),"light","Head",.045)
block("Mouth",(0,-.402,2.39),(.235,.027,.038),"dark","Head",.008)
between("BroadStrap",(.59,-.49,2.25),(-.43,-.49,1.43),.16,.055,"leather","Chest",.018)
block("StrapBuckle",(.02,-.535,1.79),(.235,.065,.22),"trim","Chest",.035,(0,.1,0))

# 石锤独立蒙皮到右手；形状和材质保持可单独替换。
body_parts = list(parts)
grip = Vector((-1.12,-.14,1.09))
axis = Vector((-.35,-.10,-.93)).normalized()
head = grip + axis * .65
cylinder("HammerHandle",grip-axis*.45,head,.095,"leather","RightHand")
cylinder("HandlePommel",grip-axis*.46,grip-axis*.34,.135,"trim","RightHand")
block("HammerHead",head,(1.04,.68,.63),"light","RightHand",.12,(0,.36,0))
block("HammerBand",head,(.22,.72,.67),"leather","RightHand",.045,(0,.36,0))
hammer_parts = [obj for obj in parts if obj not in body_parts]

armature = bpy.data.armatures.new("StoneSlammerSkeleton")
rig = bpy.data.objects.new("StoneSlammerRig",armature)
scene.collection.objects.link(rig)
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")

def bone(name, head, tail, parent=None, deform=True):
    result = armature.edit_bones.new(name)
    result.head, result.tail = head, tail
    result.use_deform = deform
    result.align_roll(Vector((0,-1,0)))
    if parent:
        result.parent = armature.edit_bones[parent]
    return result

bone("Root",(0,0,0),(0,0,.25),deform=False)
bone("Hips",(0,.035,1.14),(0,.035,1.42),"Root")
bone("Spine",(0,.035,1.42),(0,.02,1.65),"Hips")
bone("Chest",(0,.02,1.65),(0,.01,1.98),"Spine")
bone("UpperChest",(0,.01,1.98),(0,.01,2.20),"Chest")
bone("Neck",(0,.01,2.20),(0,.015,2.42),"UpperChest")
bone("Head",(0,.015,2.42),(0,.015,2.91),"Neck")
for side, sign in (("Left",1),("Right",-1)):
    bone(side+"Shoulder",(sign*.12,.01,2.20),(sign*.78,.01,2.20),"UpperChest")
    bone(side+"UpperArm",(sign*.78,.01,2.20),(sign*1.055,-.015,1.65),side+"Shoulder")
    bone(side+"LowerArm",(sign*1.055,-.015,1.65),(sign*1.12,-.115,1.12),side+"UpperArm")
    bone(side+"Hand",(sign*1.12,-.115,1.12),(sign*1.12,-.16,.88),side+"LowerArm")
    bone(side+"UpperLeg",(sign*.37,.035,1.14),(sign*.44,.015,.71),"Hips")
    bone(side+"LowerLeg",(sign*.44,.015,.71),(sign*.46,.015,.31),side+"UpperLeg")
    bone(side+"Foot",(sign*.46,.015,.31),(sign*.46,-.35,.13),side+"LowerLeg")
    bone(side+"Toes",(sign*.46,-.35,.13),(sign*.46,-.59,.13),side+"Foot")
bpy.ops.object.mode_set(mode="OBJECT")
rig.show_in_front = True

def join_parts(objects, name):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    # 统一原点，避免 glTF 蒙皮的对象变换与骨架变换叠加。
    scene.cursor.location = (0,0,0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    obj.parent = rig
    modifier = obj.modifiers.new("RigidStoneSkin","ARMATURE")
    modifier.object = rig
    return obj

body = join_parts(body_parts,"StoneSlammerBody")
hammer = join_parts(hammer_parts,"StoneHammer")
rig.animation_data_create()
rest_rotations = {b.name:b.matrix_local.to_quaternion() for b in armature.bones}

def reset_pose():
    for p in rig.pose.bones:
        p.rotation_mode = "QUATERNION"
        p.rotation_quaternion = Quaternion()
        p.location = (0,0,0)
        p.scale = (1,1,1)

def rotate(name, x=0, y=0, z=0):
    base = rest_rotations[name]
    rotation = Euler(tuple(math.radians(v) for v in (x,y,z)),"XYZ").to_quaternion()
    rig.pose.bones[name].rotation_quaternion = base.inverted() @ rotation @ base

def location_world(name, delta):
    rig.pose.bones[name].location = rest_rotations[name].inverted() @ Vector(delta)

def key_pose(frame):
    # 设计时间按三十帧输入，以六十帧烘焙，0.55 秒命中点恰好落在采样帧上。
    frame = (frame - 1) * 2 + 1
    for p in rig.pose.bones:
        p.keyframe_insert(data_path="rotation_quaternion",frame=frame,group=p.name)
        p.keyframe_insert(data_path="location",frame=frame,group=p.name)

actions = {}

def new_action(name):
    action = bpy.data.actions.new("StoneSlammer_"+name)
    actions[name] = action
    rig.animation_data.action = action
    action.use_fake_user = True
    return action

for name, duration in (("idle",2.0),("walk",1.2),("sprint",.9)):
    new_action(name)
    frames = round(duration*30)
    for frame in range(0,frames+1,2):
        reset_pose()
        phase = frame/frames*math.tau
        if name == "idle":
            rotate("Chest",x=math.sin(phase)*1.5)
            rotate("Head",z=math.sin(phase)*2)
            location_world("Hips",(0,0,math.sin(phase)*.018))
        else:
            stride = math.sin(phase)
            location_world("Hips",(0,0,(1-math.cos(phase*2))*.028))
            rotate("Hips",z=stride*2)
            rotate("LeftUpperLeg",x=stride*20)
            rotate("RightUpperLeg",x=-stride*20)
            rotate("LeftLowerLeg",x=max(0,-stride)*24)
            rotate("RightLowerLeg",x=max(0,stride)*24)
            rotate("LeftFoot",x=-stride*12-max(0,-stride)*18)
            rotate("RightFoot",x=stride*12-max(0,stride)*18)
            rotate("LeftUpperArm",x=-stride*16)
            rotate("RightUpperArm",x=stride*5)
            rotate("Chest",x=4,z=-stride*2)
        key_pose(frame+1)
    if frames % 2:
        key_pose(frames+1)

new_action("slam")
# 秒数以锤头落地的 0.55 为事件锚点，Godot 接入时使用同一个 ATK 时间。
slam_keys = [
    (0,0,0,0,0,0,0,0),
    (.20,-4,-8,-75,-8,-35,10,.02),
    (.43,-8,-12,-130,-15,-55,20,.025),
    (.50,-5,-7,-120,-8,-50,15,.01),
    (.55,20,35,-88,0,20,0,-.08),
    (.61,21,35,-89,0,18,0,-.085),
    (.76,7,12,-24,0,6,0,-.025),
    (.90,0,0,0,0,0,0,0),
]
for t, spine, chest, right_arm, right_elbow, left_arm, left_elbow, drop in slam_keys:
    reset_pose()
    rotate("Spine",x=spine)
    rotate("Chest",x=chest)
    rotate("RightUpperArm",x=right_arm)
    rotate("RightLowerArm",x=right_elbow)
    rotate("LeftUpperArm",x=left_arm)
    rotate("LeftLowerArm",x=left_elbow)
    rotate("Head",x=-chest*.3)
    location_world("Hips",(0,0,drop))
    key_pose(t*30+1)

new_action("hit")
for frame, angle in ((1,0),(4,-8),(9,3),(13,0)):
    reset_pose()
    rotate("Chest",x=angle)
    rotate("Head",x=-angle*.5)
    key_pose(frame)

new_action("die")
for frame, angle, lift in ((1,0,0),(8,-6,0),(21,68,.35),(31,90,.48),(37,90,.48)):
    reset_pose()
    rotate("Root",x=angle)
    location_world("Root",(0,0,lift))
    key_pose(frame)

# 动作按独立 NLA 轨导出，保持稳定名称；源文件仍可直接编辑关键帧。
rig.animation_data.action = None
for name, action in actions.items():
    track = rig.animation_data.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name,1,action)
    strip.name = name
    track.mute = True
reset_pose()
scene.frame_set(1)

# 主镜头和照明只用于源工程预览，不参与游戏导出。
def area(name, location, energy, size):
    data = bpy.data.lights.new(name,"AREA")
    data.energy, data.shape, data.size = energy,"DISK",size
    obj = bpy.data.objects.new(name,data)
    scene.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector((0,0,1.4))-obj.location).to_track_quat("-Z","Y").to_euler()
area("Slammer_Key",(4,-6,7),750,5)
area("Slammer_Fill",(-4,-3,4),400,5)
camera_data = bpy.data.cameras.new("SlammerCamera")
camera = bpy.data.objects.new("SlammerCamera",camera_data)
scene.collection.objects.link(camera)
camera.location = (5,-8,4)
camera.rotation_euler = (Vector((-.1,0,1.5))-camera.location).to_track_quat("-Z","Y").to_euler()
camera_data.type = "ORTHO"
camera_data.ortho_scale = 4.7
scene.camera = camera
scene.render.resolution_x = 1000
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
world = bpy.data.worlds.new("SlammerPreviewWorld")
world.use_nodes = True
background = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
background.inputs["Color"].default_value = (.20,.23,.18,1)
background.inputs["Strength"].default_value = .65
scene.world = world

bpy.ops.object.select_all(action="DESELECT")
for obj in (rig,body,hammer): obj.select_set(True)
bpy.context.view_layer.objects.active = rig
properties = bpy.ops.export_scene.gltf.get_rna_type().properties
mode_items = [i.identifier for i in properties["export_animation_mode"].enum_items]
mode = next(i for i in mode_items if i == "NLA_TRACKS")
options = {
    "filepath":RUNTIME+"/stone_slammer.glb",
    "export_format":"GLB", "use_selection":True, "use_active_scene":True,
    "export_animations":True, "export_animation_mode":mode,
    "export_force_sampling":True, "export_frame_range":False,
    "export_anim_slide_to_zero":True,
    "export_skins":True,"export_yup":True,
}
bpy.ops.export_scene.gltf(**options)
rig.animation_data.action = actions["idle"]
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=SOURCE+"/stone-slammer.blend")
report = {
    "height_meters":round(max((body.matrix_world @ v.co).z for v in body.data.vertices),3),
    "body_triangles":sum(len(p.vertices)-2 for p in body.data.polygons),
    "hammer_triangles":sum(len(p.vertices)-2 for p in hammer.data.polygons),
    "vertices":len(body.data.vertices)+len(hammer.data.vertices),
    "bones":len(armature.bones),"animations":list(actions),
    "impact_time_seconds":.55,
    "runtime":"Assets/Enemies/StoneSlammer/stone_slammer.glb",
    "source":"ArtSource/Blender/stone-slammer/stone-slammer.blend",
}
print(json.dumps(report,ensure_ascii=False))

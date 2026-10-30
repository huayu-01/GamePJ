"""通过 Blender MCP 建立已定稿的黑圆头小法师和独立小法杖。"""
import bpy
import math
import json
from mathutils import Vector

PROJECT = "F:/Godot项目/项目a"
SOURCE = PROJECT + "/ArtSource/Blender/ghost-mage"
RUNTIME = PROJECT + "/Assets/Enemies/GhostMage"
SCENE_NAME = "GhostMage_Approved_v1"

scene = bpy.data.scenes.get(SCENE_NAME)
if scene:
    for obj in list(scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
else:
    scene = bpy.data.scenes.new(SCENE_NAME)
bpy.context.window.scene = scene
scene.render.fps = 60
scene.frame_start, scene.frame_end = 1, 121
scene.unit_settings.system = "METRIC"

def linear(v):
    return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4

def material(name, color, emission=0):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    rgb = tuple(linear(int(color[i:i+2],16) / 255) for i in (0,2,4))
    mat.diffuse_color = (*rgb,1)
    shader = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    shader.inputs["Base Color"].default_value = (*rgb,1)
    shader.inputs["Roughness"].default_value = .82
    if emission:
        shader.inputs["Emission Color"].default_value = (*rgb,1)
        shader.inputs["Emission Strength"].default_value = emission
    return mat

mats = {
    "body": material("Ghost_Charcoal", "211c2b"),
    "hat": material("Ghost_IndigoHat", "352657"),
    "band": material("Ghost_HatBand", "725485"),
    "eye": material("Ghost_WarmEyes", "fff0bd", 1.4),
    "wood": material("Ghost_StaffWood", "755033"),
    "orb": material("Ghost_StaffOrb", "ffedb1", 1.5),
}
parts = []

def skin(obj, name, mat, bone):
    obj.name = name
    obj.data.materials.append(mats[mat])
    for poly in obj.data.polygons:
        poly.use_smooth = True
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    group = obj.vertex_groups.new(name=bone)
    group.add(list(range(len(obj.data.vertices))),1,"REPLACE")
    parts.append(obj)
    return obj

def sphere(name, location, size, mat, bone, segments=24, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,radius=1,location=location)
    obj = bpy.context.object
    obj.scale = size
    return skin(obj,name,mat,bone)

def ring_mesh(name, rings, segments, mat, bone):
    vertices, faces = [], []
    for x,y,z,rx,ry in rings:
        for i in range(segments):
            angle = i * math.tau / segments
            vertices.append((x + rx*math.cos(angle), y + ry*math.sin(angle), z))
    for row in range(len(rings)-1):
        for i in range(segments):
            a = row*segments+i
            b = row*segments+(i+1)%segments
            faces.append((a,b,b+segments,a+segments))
    faces.append(tuple(reversed(range(segments))))
    faces.append(tuple((len(rings)-1)*segments+i for i in range(segments)))
    mesh = bpy.data.meshes.new(name+"Mesh")
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    obj = bpy.data.objects.new(name,mesh)
    scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    return skin(obj,name,mat,bone)

# 头、圆润上身和收尖尾部与定稿相同；不添加腿脚、服装或南瓜。
head = sphere("GhostHead",(0,0,1.30),(.39,.33,.36),"body","Head")
body = ring_mesh("GhostBody",[
    (.27,.04,.15,.008,.008),(.32,.04,.23,.033,.025),
    (.34,.04,.32,.065,.045),(.29,.04,.42,.12,.075),
    (.17,.04,.51,.205,.12),(.035,.035,.63,.29,.19),
    (0,.025,.77,.33,.23),(0,.015,.90,.305,.225),
    (0,.01,1.00,.23,.18),(0,.005,1.065,.13,.10),
    (0,0,1.085,.025,.025)],24,"body","Body")
tail_group = body.vertex_groups.new(name="Tail")
body_group = body.vertex_groups["Body"]
for vertex in body.data.vertices:
    tail_weight = max(0,min(1,(.72-vertex.co.z)/.38))
    if tail_weight:
        body_group.add([vertex.index],1-tail_weight,"REPLACE")
        tail_group.add([vertex.index],tail_weight,"REPLACE")
# 一次细分让尾焰圆润，保留少量清楚的大形与可编辑控制环。
bpy.context.view_layer.objects.active = body
smooth = body.modifiers.new("RoundedWisp","SUBSURF")
smooth.levels = 1
bpy.ops.object.modifier_apply(modifier=smooth.name)

for sign in (-1,1):
    eye = sphere("GhostEye"+str(sign),(sign*.145,-.313,1.315),(.060,.025,.10),"eye","Head",16,10)
    eye.rotation_euler[2] = -sign*.20

# 有厚度的圆帽檐，以及自然轻弯的帽尖，避免破碎或过分歪斜。
brim = ring_mesh("WizardHatBrim",[
    (0,0,1.545,.34,.30),(0,0,1.53,.56,.43),
    (0,0,1.55,.72,.53),(0,0,1.59,.72,.53),
    (0,0,1.57,.56,.43),(0,0,1.585,.34,.30)],32,"hat","Hat")
cone = ring_mesh("WizardHatCrown",[
    (0,0,1.57,.365,.32),(0,.005,1.72,.31,.27),
    (.025,.01,1.88,.245,.21),(.085,.015,2.02,.175,.145),
    (.19,.015,2.115,.105,.09),(.31,.015,2.10,.065,.055),
    (.40,.015,2.00,.026,.025),(.435,.015,1.96,.004,.004)],24,"hat","Hat")
band = ring_mesh("WizardHatBand",[
    (0,0,1.595,.363,.317),(0,.004,1.69,.326,.284),
    (0,.004,1.695,.316,.274),(0,0,1.595,.353,.307)],32,"band","Hat")

# 不生成手掌或手臂，法杖独立悬浮；小圆珠呼应眼睛，不遮挡脸。
staff = ring_mesh("GhostSmallStaff",[
    (-.60,-.23,.46,.025,.025),(-.60,-.23,.73,.027,.027),
    (-.60,-.23,1.00,.027,.027),(-.63,-.25,1.27,.029,.029),
    (-.70,-.30,1.47,.034,.034)],10,"wood","Staff")
orb = sphere("GhostStaffOrb",(-.70,-.30,1.515),(.075,.075,.075),"orb","Staff",16,10)
socket = ring_mesh("GhostStaffSocket",[
    (-.70,-.30,1.44,.035,.035),(-.70,-.30,1.475,.065,.065),
    (-.70,-.30,1.50,.061,.061)],12,"wood","Staff")

armature = bpy.data.armatures.new("GhostMageSkeleton")
rig = bpy.data.objects.new("GhostMageRig",armature)
scene.collection.objects.link(rig)
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")

def bone(name, head, tail, parent=None):
    b = armature.edit_bones.new(name)
    b.head,b.tail = head,tail
    if parent:
        b.parent = armature.edit_bones[parent]
    return b

bone("Root",(0,0,0),(0,0,.20))
bone("Body",(0,0,.77),(0,0,1.00),"Root")
bone("Tail",(0,.03,.60),(.30,.04,.22),"Body")
bone("Head",(0,0,1.03),(0,0,1.45),"Body")
bone("Hat",(0,0,1.55),(.10,0,1.97),"Head")
# Grip 仅保留为法杖的动画枢轴，兼容既有动作，不对应可见的手。
bone("Grip",(-.28,0,.965),(-.56,-.23,.98),"Body")
bone("Staff",(-.60,-.23,.98),(-.70,-.30,1.49),"Grip")
bpy.ops.object.mode_set(mode="OBJECT")
rig.show_in_front = True

for obj in parts:
    obj.parent = rig
    modifier = obj.modifiers.new("GhostSkin","ARMATURE")
    modifier.object = rig
rig.animation_data_create()
actions = {}

def reset():
    for p in rig.pose.bones:
        p.rotation_mode = "XYZ"
        p.rotation_euler = (0,0,0)
        p.location = (0,0,0)
        p.scale = (1,1,1)

def key(frame):
    for p in rig.pose.bones:
        p.keyframe_insert("location",frame=frame)
        p.keyframe_insert("rotation_euler",frame=frame)
        p.keyframe_insert("scale",frame=frame)

def new_action(name):
    reset()
    action = bpy.data.actions.new("Ghost_"+name)
    action.use_fake_user = True
    rig.animation_data.action = action
    actions[name] = action

# 仅供外观预览的漂浮和悬浮法杖施法动作；不接伤害事件。
new_action("idle")
for frame, wave in ((1,0),(31,1),(61,0),(91,-1),(121,0)):
    reset()
    rig.pose.bones["Root"].location.y = wave*.035
    rig.pose.bones["Tail"].rotation_euler[1] = wave*.10
    rig.pose.bones["Hat"].rotation_euler[1] = -wave*.015
    key(frame)
new_action("cast")
for frame, raise_amount in ((1,0),(19,.6),(34,1),(42,.7),(61,0)):
    reset()
    rig.pose.bones["Grip"].rotation_euler[1] = raise_amount*.30
    rig.pose.bones["Grip"].location.y = raise_amount*.08
    rig.pose.bones["Body"].scale = (1-raise_amount*.035,1-raise_amount*.035,1+raise_amount*.045)
    key(frame)
rig.animation_data.action = None
for name, action in actions.items():
    track = rig.animation_data.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name,1,action)
    strip.name = name
    track.mute = True
reset()
scene.frame_set(1)

world = bpy.data.worlds.new("GhostPreviewWorld")
world.use_nodes = True
background = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
background.inputs["Color"].default_value = (.3,.3,.32,1)
background.inputs["Strength"].default_value = .65
scene.world = world
for name, location, power in (("GhostKey",(3,-4,5),550),("GhostFill",(-3,-2,3),300)):
    data = bpy.data.lights.new(name,"AREA")
    data.energy,data.size = power,4
    obj = bpy.data.objects.new(name,data)
    scene.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector((0,0,1.1))-obj.location).to_track_quat("-Z","Y").to_euler()
camera_data = bpy.data.cameras.new("GhostCamera")
camera = bpy.data.objects.new("GhostCamera",camera_data)
scene.collection.objects.link(camera)
camera.location = (3,-7,3.0)
camera.rotation_euler = (Vector((0,0,1.1))-camera.location).to_track_quat("-Z","Y").to_euler()
camera_data.type,camera_data.ortho_scale = "ORTHO",2.7
scene.camera = camera
scene.render.resolution_x,scene.render.resolution_y = 900,1000
scene.render.resolution_percentage = 100

bpy.ops.object.select_all(action="DESELECT")
for obj in [rig]+parts:
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
for obj in parts:
    obj.data.validate()
    obj.data.update()
properties = bpy.ops.export_scene.gltf.get_rna_type().properties
mode = next(i.identifier for i in properties["export_animation_mode"].enum_items if i.identifier == "NLA_TRACKS")
bpy.ops.export_scene.gltf(filepath=RUNTIME+"/ghost_mage.glb",export_format="GLB",use_selection=True,use_active_scene=True,export_animations=True,export_animation_mode=mode,export_force_sampling=True,export_frame_range=False,export_anim_slide_to_zero=True,export_skins=True,export_yup=True)
rig.animation_data.action = actions["idle"]
scene.frame_set(1)
# 使用安全模式允许的原生另存副本，保持当前源工程路径不变。
bpy.ops.wm.save_as_mainfile(filepath=SOURCE+"/ghost-mage.blend",copy=True)
report = {"source":"ArtSource/Blender/ghost-mage/ghost-mage.blend","runtime":"Assets/Enemies/GhostMage/ghost_mage.glb","triangles":sum(len(p.vertices)-2 for o in parts for p in o.data.polygons),"vertices":sum(len(o.data.vertices) for o in parts),"bones":len(armature.bones),"materials":len(mats),"animations":list(actions),"height_meters":2.12,"generation_credits":0}
print(json.dumps(report,ensure_ascii=False))

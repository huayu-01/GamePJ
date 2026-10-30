"""整理混元胆小菇原件：尺寸、简单菌盖斑点、轻量贴图及非人形动作。"""
import bpy
import math
import json
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

PROJECT = "F:/Godot项目/项目a"
SOURCE = PROJECT + "/ArtSource/Blender/scaredy-shroom"
RUNTIME = PROJECT + "/Assets/Enemies/ScaredyShroom"
SCENE_NAME = "ScaredyShroom_Hunyuan_v1"
scene = bpy.data.scenes.get(SCENE_NAME)
if scene:
    for obj in list(scene.objects):
        bpy.data.objects.remove(obj,do_unlink=True)
else:
    scene = bpy.data.scenes.new(SCENE_NAME)
bpy.context.window.scene = scene
scene.render.fps = 60
scene.frame_start,scene.frame_end = 1,121
scene.unit_settings.system = "METRIC"
bpy.ops.import_scene.gltf(filepath=PROJECT+"/ArtSource/Generated/scaredy-shroom-v1/original-01-glb.glb")
meshes = [o for o in scene.objects if o.type == "MESH"]
coords = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
low = Vector(tuple(min(p[i] for p in coords) for i in range(3)))
high = Vector(tuple(max(p[i] for p in coords) for i in range(3)))
scale = 1.55 / (high.z-low.z)
center = Vector(((low.x+high.x)*.5,(low.y+high.y)*.5,low.z))

for obj in meshes:
    world = obj.matrix_world.copy()
    for vertex in obj.data.vertices:
        vertex.co = (world @ vertex.co-center)*scale
    obj.matrix_world = Matrix.Identity(4)
    obj.name = "ScaredyShroomBody"
    obj.data.update()
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    # 保留原始 4K 结果，运行时副本压到 2K，减少显存与包体。
    for index, original in enumerate(list(obj.data.materials)):
        mat = original.copy()
        mat.name = "ShroomGeneratedSurface"
        obj.data.materials[index] = mat
        shader = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        shader.inputs["Roughness"].default_value = .9
        for node in mat.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image:
                texture = node.image.copy()
                texture.name = "Shroom_Runtime_2K"
                if max(texture.size) > 2048:
                    texture.scale(2048,2048)
                texture.pack()
                node.image = texture

def linear(value):
    return value / 12.92 if value <= .04045 else ((value+.055)/1.055)**2.4

spot = bpy.data.materials.get("Shroom_CapSpots") or bpy.data.materials.new("Shroom_CapSpots")
spot.use_nodes = True
color = tuple(linear(int("391447"[i:i+2],16)/255) for i in (0,2,4))
spot.diffuse_color = (*color,1)
shader = next(n for n in spot.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
shader.inputs["Base Color"].default_value = (*color,1)
shader.inputs["Roughness"].default_value = .9

# 少量贴合菌盖的圆形网格，保留圆润边缘，不重画原始贴图。
spot_objects = []
for obj in list(meshes):
    surface = BVHTree.FromObject(obj,bpy.context.evaluated_depsgraph_get())
    seeds = [((-.22,-.30,1.46),.115),((.25,-.23,1.43),.10),
             ((.00,.08,1.55),.125),((-.43,.10,1.29),.095),
             ((.36,.22,1.33),.105),((.12,.43,1.31),.09)]
    vertices,faces = [],[]
    for target,radius in seeds:
        center,normal,_,_ = surface.find_nearest(Vector(target))
        tangent = normal.cross(Vector((0,1,0))).normalized()
        other = normal.cross(tangent).normalized()
        offset = len(vertices)
        vertices.append(center+normal*.002)
        for ring in (1,2):
            for index in range(24):
                angle = index*math.tau/24
                point = center+(tangent*math.cos(angle)+other*math.sin(angle))*radius*ring/2
                hit,hit_normal,_,_ = surface.ray_cast(point+normal*.35,-normal,.7)
                vertices.append((hit+hit_normal*.002) if hit is not None else point+normal*.002)
        for index in range(24):
            following = (index+1)%24
            faces.append((offset,offset+1+index,offset+1+following))
            faces.append((offset+1+index,offset+25+index,offset+25+following,offset+1+following))
    data = bpy.data.meshes.new("ShroomCapSpotsMesh")
    data.from_pydata(vertices,[],faces)
    data.materials.append(spot)
    data.update()
    patches = bpy.data.objects.new("ShroomCapSpots",data)
    scene.collection.objects.link(patches)
    for polygon in data.polygons:
        polygon.use_smooth = True
    spot_objects.append(patches)
meshes.extend(spot_objects)

armature = bpy.data.armatures.new("ScaredyShroomSkeleton")
rig = bpy.data.objects.new("ScaredyShroomRig",armature)
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

bone("Root",(0,0,0),(0,0,.15))
bone("Stem",(0,0,0),(0,0,.62),"Root")
bone("Head",(0,0,.62),(0,0,1.06),"Stem")
bone("Cap",(0,0,1.06),(0,0,1.50),"Head")
bpy.ops.object.mode_set(mode="OBJECT")
inherit_items = bpy.types.Bone.bl_rna.properties["inherit_scale"].enum_items
armature.bones["Head"].inherit_scale = next(i.identifier for i in inherit_items if i.identifier == "NONE")
rig.show_in_front = True

for obj in meshes:
    groups = {name:obj.vertex_groups.new(name=name) for name in ("Stem","Head","Cap")}
    for vertex in obj.data.vertices:
        z = vertex.co.z
        head_weight = max(0,min(1,(z-.52)/.10))
        cap_weight = max(0,min(1,(z-1.00)/.06))
        weights = {"Stem":1-head_weight,"Head":head_weight*(1-cap_weight),"Cap":cap_weight}
        for name,weight in weights.items():
            if weight > 0:
                groups[name].add([vertex.index],weight,"REPLACE")
    obj.parent = rig
    modifier = obj.modifiers.new("ShroomSkin","ARMATURE")
    modifier.object = rig
    obj.data.validate()
    obj.data.update()

rig.animation_data_create()
actions = {}

def reset():
    for p in rig.pose.bones:
        p.rotation_mode = "XYZ"
        p.location = (0,0,0)
        p.rotation_euler = (0,0,0)
        p.scale = (1,1,1)

def key(time, shrink=1, sink=0, lean=0):
    reset()
    rig.pose.bones["Stem"].scale.y = shrink
    rig.pose.bones["Root"].location.y = sink
    rig.pose.bones["Head"].rotation_euler[0] = lean
    for p in rig.pose.bones:
        for prop in ("location","rotation_euler","scale"):
            p.keyframe_insert(prop,frame=time*60+1)

def new_action(name):
    action = bpy.data.actions.new("Shroom_"+name)
    action.use_fake_user = True
    rig.animation_data.action = action
    actions[name] = action

new_action("idle")
for time,shrink,lean in ((0,1,0),(.5,.985,.018),(1,1,0),(1.5,1.015,-.018),(2,1,0)):
    key(time,shrink,0,lean)
new_action("burrow")
for values in ((0,1,0,0),(.15,.75,0,-.06),(.30,.25,-.08,-.04),(.48,.25,-.75,0),(.65,.25,-1.22,0)):
    key(*values)
new_action("emerge")
for values in ((0,.25,-1.22,0),(.18,.25,-.70,0),(.34,.40,-.02,0),(.48,1.04,0,.025),(.65,1,0,0)):
    key(*values)
new_action("shoot")
for values in ((0,1,0,0),(.15,.97,0,.06),(.23,1.02,0,-.055),(.40,1,0,0)):
    key(*values)

rig.animation_data.action = None
for name,action in actions.items():
    track = rig.animation_data.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name,1,action)
    strip.name = name
    track.mute = True
reset()
scene.frame_set(1)

# 单独设置源工程观察镜头，不导出到运行时。
world = bpy.data.worlds.new("ShroomPreviewWorld")
world.use_nodes = True
background = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
background.inputs["Color"].default_value = (.3,.3,.32,1)
background.inputs["Strength"].default_value = .7
scene.world = world
for name,location,power in (("ShroomKey",(3,-4,5),500),("ShroomFill",(-3,-2,3),260)):
    data = bpy.data.lights.new(name,"AREA")
    data.energy,data.size = power,4
    obj = bpy.data.objects.new(name,data)
    scene.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector((0,0,.8))-obj.location).to_track_quat("-Z","Y").to_euler()
data = bpy.data.cameras.new("ShroomCamera")
camera = bpy.data.objects.new("ShroomCamera",data)
scene.collection.objects.link(camera)
camera.location = (2.4,-5,2.6)
camera.rotation_euler = (Vector((0,0,.8))-camera.location).to_track_quat("-Z","Y").to_euler()
data.type,data.ortho_scale = "ORTHO",2.1
scene.camera = camera
scene.render.resolution_x,scene.render.resolution_y = 900,1000
scene.render.resolution_percentage = 100

bpy.ops.object.select_all(action="DESELECT")
for obj in [rig]+meshes:
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
properties = bpy.ops.export_scene.gltf.get_rna_type().properties
mode = next(i.identifier for i in properties["export_animation_mode"].enum_items if i.identifier == "NLA_TRACKS")
bpy.ops.export_scene.gltf(filepath=RUNTIME+"/scaredy_shroom.glb",export_format="GLB",use_selection=True,use_active_scene=True,export_animations=True,export_animation_mode=mode,export_force_sampling=True,export_frame_range=False,export_anim_slide_to_zero=True,export_skins=True,export_yup=True)
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=SOURCE+"/scaredy-shroom.blend",copy=True)
print(json.dumps({"triangles":sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons),"vertices":sum(len(o.data.vertices) for o in meshes),"bones":len(armature.bones),"height_meters":1.55,"animations":list(actions),"texture_size":2048,"hunyuan_job_id":"1504328840385544192","generation_credits":25},ensure_ascii=False))

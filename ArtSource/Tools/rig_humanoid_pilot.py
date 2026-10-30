"""为首个人形试件建立可导出的基础骨架和自制验证动作。"""
import bpy
import math
import json
from mathutils import Vector, Quaternion

body = bpy.data.objects["PilotBody"]
if bpy.data.objects.get("PilotRig"):
    raise RuntimeError("试件骨架已存在，避免重复覆盖。")
armature = bpy.data.armatures.new("PilotSkeleton")
rig = bpy.data.objects.new("PilotRig", armature)
bpy.context.scene.collection.objects.link(rig)
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")

def bone(name, head, tail, parent=None, deform=True):
    item = armature.edit_bones.new(name)
    item.head = head
    item.tail = tail
    item.align_roll(Vector((0, -1, 0)))
    item.use_deform = deform
    if parent:
        item.parent = armature.edit_bones[parent]
    return item

prefix = "mixamorig:"
bone("Root", (0,0,0), (0,0,0.2), deform=False)
bone(prefix+"Hips", (0,0.03,0.98), (0,0.03,1.08), "Root")
bone(prefix+"Spine", (0,0.03,1.08), (0,0.02,1.20), prefix+"Hips")
bone(prefix+"Spine1", (0,0.02,1.20), (0,0.02,1.31), prefix+"Spine")
bone(prefix+"Spine2", (0,0.02,1.31), (0,0.03,1.44), prefix+"Spine1")
bone(prefix+"Neck", (0,0.03,1.44), (0,0.03,1.54), prefix+"Spine2")
bone(prefix+"Head", (0,0.03,1.54), (0,0.03,1.74), prefix+"Neck")
for side, sign in (("Left",1), ("Right",-1)):
    def point(x,y,z):
        return (x*sign,y,z)
    bone(prefix+side+"Shoulder", point(0.035,0.03,1.40), point(0.175,0.035,1.39), prefix+"Spine2")
    bone(prefix+side+"Arm", point(0.175,0.035,1.39), point(0.39,0.035,1.37), prefix+side+"Shoulder")
    bone(prefix+side+"ForeArm", point(0.39,0.035,1.37), point(0.59,0.04,1.37), prefix+side+"Arm")
    bone(prefix+side+"Hand", point(0.59,0.04,1.37), point(0.70,0.04,1.37), prefix+side+"ForeArm")
    bone(prefix+side+"UpLeg", point(0.09,0.035,0.98), point(0.14,0.04,0.55), prefix+"Hips")
    bone(prefix+side+"Leg", point(0.14,0.04,0.55), point(0.17,0.045,0.13), prefix+side+"UpLeg")
    bone(prefix+side+"Foot", point(0.17,0.045,0.13), point(0.17,-0.09,0.055), prefix+side+"Leg")
    bone(prefix+side+"ToeBase", point(0.17,-0.09,0.055), point(0.17,-0.155,0.045), prefix+side+"Foot")

bpy.ops.object.mode_set(mode="OBJECT")
rig.show_in_front = True
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
options = bpy.ops.object.parent_set.get_rna_type().properties["type"].enum_items
auto = next(item.identifier for item in options if item.identifier == "ARMATURE_AUTO")
bpy.ops.object.parent_set(type=auto)
for face in body.data.polygons:
    face.use_smooth = True
body.data.update()
# 运行时每个顶点最多由四根骨骼影响，避免导出时隐式裁切权重。
for vertex in body.data.vertices:
    values = sorted([(group.weight, group.group) for group in vertex.groups], reverse=True)
    keep = values[:4]
    total = sum(weight for weight, index in keep)
    for weight, index in values[4:]:
        body.vertex_groups[index].remove([vertex.index])
    for weight, index in keep:
        body.vertex_groups[index].add([vertex.index], weight / total, "REPLACE")

# 动作仅用于检验变形、循环和导出，不作为最终战斗动作。
scene = bpy.context.scene
scene.render.fps = 30
rig.animation_data_create()
for item in rig.pose.bones:
    item.rotation_mode = "QUATERNION"

def rotation(name, values):
    item = rig.pose.bones[prefix+name]
    rest = item.bone.matrix_local.to_quaternion()
    result = Quaternion()
    for axis, degrees in values:
        local_axis = rest.inverted() @ Vector(axis)
        result = Quaternion(local_axis, math.radians(degrees)) @ result
    item.rotation_quaternion = result

def key_pose(frame):
    for item in rig.pose.bones:
        item.keyframe_insert(data_path="rotation_quaternion", frame=frame, group=item.name)
        item.keyframe_insert(data_path="location", frame=frame, group=item.name)

for clip, duration in (("Idle_Pilot",60), ("Walk_Pilot",30), ("JointCheck_Pilot",90)):
    action = bpy.data.actions.new(clip)
    rig.animation_data.action = action
    for frame in range(1,duration+2):
        phase = 2*math.pi*(frame-1)/duration
        for item in rig.pose.bones:
            item.rotation_quaternion = Quaternion()
            item.location = (0,0,0)
        for side, sign in (("Left",1), ("Right",-1)):
            swing = math.sin(phase)*sign
            # 肩关节把 T 姿势转为自然垂臂，手指暂由手掌骨整体带动。
            rotation(side+"Arm", [((0,1,0),78*sign), ((1,0,0),18*swing if clip=="Walk_Pilot" else 0)])
            rotation(side+"ForeArm", [((1,0,0),-8)])
            if clip == "Walk_Pilot":
                rotation(side+"UpLeg", [((1,0,0),28*swing)])
                rotation(side+"Leg", [((1,0,0),-42*max(0,-swing))])
                rotation(side+"Foot", [((1,0,0),-12*swing+15*max(0,-swing))])
            elif clip == "JointCheck_Pilot":
                rotation(side+"Arm", [((0,1,0),78*sign*(0.5+0.5*math.cos(phase)))])
                rotation(side+"ForeArm", [((0,0,1),60*max(0,swing)*sign)])
                rotation(side+"UpLeg", [((1,0,0),20*max(0,swing))])
                rotation(side+"Leg", [((1,0,0),-40*max(0,swing))])
        if clip == "Idle_Pilot":
            rotation("Spine1", [((1,0,0),0.7*math.sin(phase))])
            rotation("Head", [((0,0,1),1.5*math.sin(phase))])
        if clip == "Walk_Pilot":
            bpy.context.view_layer.update()
            evaluated = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
            temporary_mesh = evaluated.to_mesh()
            ground = min((evaluated.matrix_world @ vertex.co).z for vertex in temporary_mesh.vertices)
            evaluated.to_mesh_clear()
            hip = rig.pose.bones[prefix+"Hips"]
            hip.location = hip.bone.matrix_local.to_3x3().inverted() @ Vector((0,0,-ground))
        key_pose(frame)
    action.use_fake_user = True
    track = rig.animation_data.nla_tracks.new()
    track.name = clip
    strip = track.strips.new(clip, 1, action)
    track.mute = True

rig.animation_data.action = bpy.data.actions["Idle_Pilot"]
scene.frame_start = 1
scene.frame_end = 61
scene.frame_set(1)
deform = {group.index for group in body.vertex_groups if group.name in armature.bones and armature.bones[group.name].use_deform}
unweighted = [vertex.index for vertex in body.data.vertices if sum(g.weight for g in vertex.groups if g.group in deform)<0.00001]
print(json.dumps({"bones":len(armature.bones), "deform_bones":sum(b.use_deform for b in armature.bones),
                  "unweighted_vertices":len(unweighted), "actions":[a.name for a in bpy.data.actions if a.name.endswith("_Pilot")]}))

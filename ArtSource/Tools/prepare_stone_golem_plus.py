"""整理混元石头人 Plus：简单平涂、刚性分区蒙皮及基础动作。"""
import bpy
import bmesh
import array
import json
import math
from mathutils import Vector, Matrix, Quaternion, Euler

PROJECT = "F:/Godot项目/项目a"
SOURCE = PROJECT + "/ArtSource/Blender/stone-golem-plus"
RUNTIME = PROJECT + "/Assets/Enemies/StoneGolemPlus"
SCENE_NAME = "StoneGolemPlus_Hunyuan_v1"
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
bpy.ops.import_scene.gltf(filepath=PROJECT + "/ArtSource/Generated/stone-golem-plus-v1/original-01-glb.glb")
originals = [obj for obj in scene.objects if obj.type == "MESH"]
coords = [obj.matrix_world @ v.co for obj in originals for v in obj.data.vertices]
low = Vector(tuple(min(p[i] for p in coords) for i in range(3)))
high = Vector(tuple(max(p[i] for p in coords) for i in range(3)))
scale = 3.0 / (high.z - low.z)
center = Vector(((low.x + high.x) * 0.5, (low.y + high.y) * 0.5, low.z))
raw_triangles = sum(len(p.vertices) - 2 for obj in originals for p in obj.data.polygons)
for obj in originals:
    world = obj.matrix_world.copy()
    for vertex in obj.data.vertices:
        vertex.co = (world @ vertex.co - center) * scale
    obj.matrix_world = Matrix.Identity(4)
    obj.data.update()


def linear(value):
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


def material(name, color):
    result = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    result.use_nodes = True
    rgb = [linear(int(color[i:i + 2], 16) / 255.0) for i in (0, 2, 4)]
    result.diffuse_color = (*rgb, 1)
    shader = next(node for node in result.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    shader.inputs["Base Color"].default_value = (*rgb, 1)
    shader.inputs["Roughness"].default_value = 1.0
    shader.inputs["Metallic"].default_value = 0.0
    return result


materials = [material("GolemPlus_Stone", "858890"), material("GolemPlus_Sockets", "252c3a"),
             material("GolemPlus_Eyes", "ffd477")]
parts = []
eyes_faces = 0
reassigned_faces = 0
anchors = [
    ("Head", (0, -0.30, 2.52), (0.36, 0.40, 0.28)),
    ("Chest", (0, 0.04, 1.98), (0.90, 0.66, 0.70)),
    ("Hips", (0, 0.04, 1.28), (0.58, 0.48, 0.40)),
]
for side, sign in (("Left", 1), ("Right", -1)):
    for name, center_point, radius in (
            ("UpperArm", (1.18, 0, 2.43), (0.63, 0.66, 0.57)),
            ("LowerArm", (1.66, -0.04, 1.64), (0.43, 0.54, 0.43)),
            ("Hand", (1.82, -0.10, 0.95), (0.49, 0.57, 0.50)),
            ("UpperLeg", (0.66, 0.04, 1.12), (0.43, 0.48, 0.36)),
            ("LowerLeg", (0.86, 0.04, 0.66), (0.44, 0.45, 0.34)),
            ("Foot", (0.94, 0.04, 0.20), (0.59, 0.68, 0.34))):
        anchors.append((side + name, (center_point[0] * sign, center_point[1], center_point[2]), radius))


def region_for(point):
    # 使用三维体积距离区分相邻拳头、膝盖和脚掌，避免水平硬切留下漂浮小片。
    nearest, minimum = "Chest", 1e10
    for name, center_point, radius in anchors:
        distance = sum(((point[i] - center_point[i]) / radius[i]) ** 2 for i in range(3))
        if distance < minimum:
            nearest, minimum = name, distance
    return nearest


for original in originals:
    mesh = original.data
    texture = next(node.image for mat in mesh.materials if mat and mat.use_nodes
                   for node in mat.node_tree.nodes if node.type == "TEX_IMAGE" and node.image)
    pixels = array.array("f", [0.0]) * len(texture.pixels)
    texture.pixels.foreach_get(pixels)
    uv = mesh.uv_layers.active.data
    face_materials = []
    for face in mesh.polygons:
        u = sum(uv[i].uv.x for i in face.loop_indices) / len(face.loop_indices)
        v = sum(uv[i].uv.y for i in face.loop_indices) / len(face.loop_indices)
        offset = (min(int(v * texture.size[1]), texture.size[1] - 1) * texture.size[0]
                  + min(int(u * texture.size[0]), texture.size[0] - 1)) * 4
        r, g, b = pixels[offset:offset + 3]
        point = face.center
        head_front = abs(point.x) < 0.34 and point.y < -0.32 and 2.35 < point.z < 2.60
        index = 0
        if head_front and r > 0.32 and g > b * 1.5 and r > b * 2.0:
            index = 2
            eyes_faces += 1
        elif head_front and (r * 0.2126 + g * 0.7152 + b * 0.0722) < 0.12:
            index = 1
        face_materials.append(index)
    mesh.materials.clear()
    for mat in materials:
        mesh.materials.append(mat)
    for face, index in zip(mesh.polygons, face_materials):
        face.material_index = index
    # 焊接 UV 裂分后消除近共面的多余边；轮廓和眼睛材质边界保留。
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.00002)
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(9), use_dissolve_boundaries=False,
                            verts=list(bm.verts), edges=list(bm.edges), delimit={"MATERIAL"})
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    labels = [region_for(face.center) for face in mesh.polygons]
    edge_faces = {}
    for face in mesh.polygons:
        for edge in face.edge_keys:
            if edge not in edge_faces:
                edge_faces[edge] = []
            edge_faces[edge].append(face.index)
    neighbors = [set() for face in mesh.polygons]
    for connected in edge_faces.values():
        for index in connected:
            neighbors[index].update(other for other in connected if other != index)
    # 每个骨骼区域保留主体连通块；孤立小片跟随相邻区域，避免抬臂后留在腰腿旁。
    for iteration in range(4):
        seen, components = set(), {}
        for index in range(len(labels)):
            if index in seen:
                continue
            label = labels[index]
            stack, component = [index], []
            seen.add(index)
            while stack:
                current = stack.pop()
                component.append(current)
                for other in neighbors[current]:
                    if other not in seen and labels[other] == label:
                        seen.add(other)
                        stack.append(other)
            if label not in components:
                components[label] = []
            components[label].append(component)
        updates = {}
        for label, groups in components.items():
            main, largest = None, -1.0
            for group in groups:
                area = sum(mesh.polygons[index].area for index in group)
                if area > largest:
                    main, largest = group, area
            for group in groups:
                if group is main:
                    continue
                votes = {}
                for index in group:
                    for other in neighbors[index]:
                        candidate = labels[other]
                        if candidate != label:
                            votes[candidate] = votes.get(candidate, 0) + 1
                choice, count = label, 0
                for candidate, votes_count in votes.items():
                    if votes_count > count:
                        choice, count = candidate, votes_count
                if choice != label:
                    for index in group:
                        updates[index] = choice
        if not updates:
            break
        for index, label in updates.items():
            labels[index] = label
        reassigned_faces += len(updates)
    regions = {}
    for face, key in zip(mesh.polygons, labels):
        if key not in regions:
            regions[key] = []
        regions[key].append(face)
    # 按关节区域分开面并封口，每个区域只绑定一个骨骼，避免岩石像橡皮一样拉伸。
    for name, faces in regions.items():
        vertices, polygons, indices, mapping = [], [], [], {}
        for face in faces:
            polygon = []
            for index in face.vertices:
                if index not in mapping:
                    mapping[index] = len(vertices)
                    vertices.append(mesh.vertices[index].co.copy())
                polygon.append(mapping[index])
            polygons.append(polygon)
            indices.append(face.material_index)
        data = bpy.data.meshes.new("GolemPlus_" + name)
        data.from_pydata(vertices, [], polygons)
        for mat in materials:
            data.materials.append(mat)
        for face, index in zip(data.polygons, indices):
            face.material_index = index
        bm = bmesh.new()
        bm.from_mesh(data)
        boundary = [edge for edge in bm.edges if edge.is_boundary]
        if boundary:
            filled = bmesh.ops.holes_fill(bm, edges=boundary, sides=0)
            for face in filled.get("faces", []):
                face.material_index = 0
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(data)
        bm.free()
        data.update()
        obj = bpy.data.objects.new("GolemPlus_" + name, data)
        scene.collection.objects.link(obj)
        group = obj.vertex_groups.new(name=name)
        group.add(list(range(len(data.vertices))), 1.0, "REPLACE")
        parts.append(obj)
    bpy.data.objects.remove(original, do_unlink=True)

if eyes_faces < 2:
    raise RuntimeError("未可靠提取眼睛区域，需要检查原图及生成结果。")

armature = bpy.data.armatures.new("StoneGolemPlusSkeleton")
rig = bpy.data.objects.new("StoneGolemPlusRig", armature)
scene.collection.objects.link(rig)
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")


def bone(name, head, tail, parent=None):
    result = armature.edit_bones.new(name)
    result.head, result.tail = head, tail
    result.align_roll(Vector((0, -1, 0)))
    if parent:
        result.parent = armature.edit_bones[parent]


bone("Root", (0, 0, 0), (0, 0, 0.2))
bone("Hips", (0, 0, 1.08), (0, 0, 1.40), "Root")
bone("Spine", (0, 0, 1.40), (0, 0, 1.75), "Hips")
bone("Chest", (0, 0, 1.75), (0, 0, 2.40), "Spine")
bone("Head", (0, -0.13, 2.34), (0, -0.13, 2.73), "Chest")
for side, sign in (("Left", 1), ("Right", -1)):
    bone(side + "UpperArm", (sign * 1.05, 0, 2.30), (sign * 1.52, 0, 1.85), "Chest")
    bone(side + "LowerArm", (sign * 1.52, 0, 1.85), (sign * 1.78, -0.04, 1.27), side + "UpperArm")
    bone(side + "Hand", (sign * 1.78, -0.04, 1.27), (sign * 1.85, -0.10, 0.83), side + "LowerArm")
    bone(side + "UpperLeg", (sign * 0.63, 0.04, 1.13), (sign * 0.78, 0.01, 0.69), "Hips")
    bone(side + "LowerLeg", (sign * 0.78, 0.01, 0.69), (sign * 0.95, 0, 0.26), side + "UpperLeg")
    bone(side + "Foot", (sign * 0.95, 0, 0.26), (sign * 0.95, -0.40, 0.13), side + "LowerLeg")
bpy.ops.object.mode_set(mode="OBJECT")
rig.show_in_front = True
bpy.ops.object.select_all(action="DESELECT")
for obj in parts:
    obj.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
body = bpy.context.object
body.name = "StoneGolemPlusBody"
body.parent = rig
modifier = body.modifiers.new("RigidRockSkin", "ARMATURE")
modifier.object = rig
for face in body.data.polygons:
    face.use_smooth = False
rig.animation_data_create()
rest = {bone.name: bone.matrix_local.to_quaternion() for bone in armature.bones}


def reset():
    for pose in rig.pose.bones:
        pose.rotation_mode = "QUATERNION"
        pose.rotation_quaternion = Quaternion()
        pose.location = (0, 0, 0)
        pose.scale = (1, 1, 1)


def rotate(name, x=0, y=0, z=0):
    rotation = Euler(tuple(math.radians(value) for value in (x, y, z)), "XYZ").to_quaternion()
    rig.pose.bones[name].rotation_quaternion = rest[name].inverted() @ rotation @ rest[name]


def move(name, delta):
    rig.pose.bones[name].location = rest[name].inverted() @ Vector(delta)


def key(time):
    for pose in rig.pose.bones:
        pose.keyframe_insert("rotation_quaternion", frame=time * 60 + 1, group=pose.name)
        pose.keyframe_insert("location", frame=time * 60 + 1, group=pose.name)


actions = {}


def action(name):
    result = bpy.data.actions.new("GolemPlus_" + name)
    result.use_fake_user = True
    rig.animation_data.action = result
    actions[name] = result


for name, duration in (("idle", 2.0), ("walk", 1.2)):
    action(name)
    for step in range(13):
        reset()
        phase = step / 12 * math.tau
        stride = math.sin(phase)
        if name == "idle":
            rotate("Chest", x=stride * 1.0)
            rotate("Head", z=stride * 1.4)
        else:
            move("Hips", (0, 0, (1 - math.cos(phase * 2)) * 0.018))
            rotate("Chest", x=3, z=-stride * 1.5)
            for side, sign in (("Left", 1), ("Right", -1)):
                rotate(side + "UpperLeg", x=stride * sign * 14)
                rotate(side + "LowerLeg", x=max(0, -stride * sign) * 18)
                rotate(side + "Foot", x=-stride * sign * 9 - max(0, -stride * sign) * 12)
                rotate(side + "UpperArm", x=-stride * sign * 9)
        key(step / 12 * duration)

action("slam")
for time, lean, arms, elbows, drop, crouch in (
        (0, 0, 0, 0, 0, 0), (0.20, -6, -65, -12, 0.0, 0),
        (0.42, -10, -130, -18, 0.0, 0), (0.52, -8, -125, -12, 0.0, 0),
        (0.62, 58, -58, 0, -0.12, 30), (0.70, 58, -58, 0, -0.12, 30),
        (0.94, 16, -12, 0, -0.07, 16), (1.20, 0, 0, 0, 0, 0)):
    reset()
    rotate("Spine", x=lean * 0.3)
    rotate("Chest", x=lean * 0.7)
    rotate("Head", x=-lean * 0.35)
    move("Hips", (0, 0, drop))
    for side in ("Left", "Right"):
        rotate(side + "UpperArm", x=arms)
        rotate(side + "LowerArm", x=elbows)
        rotate(side + "UpperLeg", x=-crouch)
        rotate(side + "LowerLeg", x=crouch * 2)
        rotate(side + "Foot", x=-crouch)
    key(time)

action("hit")
for time, angle in ((0, 0), (0.08, -7), (0.22, 3), (0.40, 0)):
    reset()
    rotate("Chest", x=angle)
    key(time)
action("die")
for time, angle, lift in ((0, 0, 0), (0.2, -8, 0), (0.7, 70, 0.35), (1.1, 90, 0.52)):
    reset()
    rotate("Root", x=angle)
    move("Root", (0, 0, lift))
    key(time)

rig.animation_data.action = None
for name, clip in actions.items():
    track = rig.animation_data.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name, 1, clip)
    strip.name = name
    track.mute = True
reset()
scene.frame_set(1)

world = bpy.data.worlds.new("GolemPlusPreviewWorld")
world.use_nodes = True
background = next(node for node in world.node_tree.nodes if node.type == "BACKGROUND")
background.inputs["Color"].default_value = (0.28, 0.30, 0.36, 1)
background.inputs["Strength"].default_value = 0.7
scene.world = world
for name, location, energy in (("GolemPlusKey", (4, -5, 7), 650), ("GolemPlusFill", (-4, -2, 4), 350)):
    data = bpy.data.lights.new(name, "AREA")
    data.energy, data.size = energy, 5
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector((0, 0, 1.5)) - obj.location).to_track_quat("-Z", "Y").to_euler()
camera_data = bpy.data.cameras.new("GolemPlusCamera")
camera = bpy.data.objects.new("GolemPlusCamera", camera_data)
scene.collection.objects.link(camera)
camera.location = (5, -9, 4)
camera.rotation_euler = (Vector((0, 0, 1.5)) - camera.location).to_track_quat("-Z", "Y").to_euler()
camera_data.type, camera_data.ortho_scale = "ORTHO", 5.4
scene.camera = camera
scene.render.resolution_x, scene.render.resolution_y = 1100, 1000
scene.render.resolution_percentage = 100

bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
body.select_set(True)
bpy.context.view_layer.objects.active = rig
properties = bpy.ops.export_scene.gltf.get_rna_type().properties
mode = next(item.identifier for item in properties["export_animation_mode"].enum_items if item.identifier == "NLA_TRACKS")
bpy.ops.export_scene.gltf(filepath=RUNTIME + "/stone_golem_plus.glb", export_format="GLB",
    use_selection=True, use_active_scene=True, export_animations=True, export_animation_mode=mode,
    export_force_sampling=True, export_frame_range=False, export_anim_slide_to_zero=True,
    export_skins=True, export_yup=True)
# 用压缩副本保存，保留当前 Blender 里其他场景和原工作文件。
bpy.ops.wm.save_as_mainfile(filepath=SOURCE + "/stone-golem-plus.blend", copy=True, compress=True)
print(json.dumps({"raw_triangles": raw_triangles,
    "triangles": sum(len(face.vertices) - 2 for face in body.data.polygons),
    "vertices": len(body.data.vertices), "height_meters": 3.0, "bones": len(armature.bones),
    "unweighted_vertices": sum(1 for vertex in body.data.vertices if not vertex.groups),
    "max_vertex_influences": max(len(vertex.groups) for vertex in body.data.vertices),
    "materials": [mat.name for mat in body.data.materials], "animations": list(actions),
    "eye_source_faces": eyes_faces, "impact_time_seconds": 0.62,
    "connectivity_corrected_faces": reassigned_faces,
    "hunyuan_job_id": "1504389242456629248", "generation_credits": 25}, ensure_ascii=False))

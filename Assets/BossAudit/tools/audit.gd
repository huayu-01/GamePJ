extends Node3D
## 独立首领导入审查。原FBX和材质不修改；所有预览变换只发生在运行期。

var _report: Array[Dictionary] = []
var _models: Array[Node3D] = []
var _camera: Camera3D
var _labels: Array[Label3D] = []

func _ready() -> void:
	_run.call_deferred()

func _vec(v: Vector3) -> Array:
	return [snappedf(v.x, 0.0001), snappedf(v.y, 0.0001), snappedf(v.z, 0.0001)]

func _all(node: Node) -> Array[Node]:
	var nodes: Array[Node] = [node]
	for child in node.get_children():
		nodes.append_array(_all(child))
	return nodes

func _inspect(model: Node3D, model_name: String) -> Dictionary:
	var record := {"name": model_name, "meshes": 0, "vertices": 0, "triangles": 0, "surfaces": 0, "skeletons": [], "animations": [], "materials": [], "skins": [], "zero_weight_vertices": 0, "weight_sum_outliers": 0, "collision_objects": 0}
	var points := PackedVector3Array()
	for node in _all(model):
		if node is CollisionObject3D:
			record.collision_objects += 1
		if node is AnimationPlayer:
			for animation_name in node.get_animation_list():
				record.animations.append({"name": animation_name, "length": node.get_animation(animation_name).length})
		if node is Skeleton3D:
			var bones: Array[Dictionary] = []
			for index in node.get_bone_count():
				bones.append({"name": node.get_bone_name(index), "parent": node.get_bone_parent(index), "world_rest": _vec(node.global_transform * node.get_bone_global_rest(index).origin)})
			record.skeletons.append({"path": str(model.get_path_to(node)), "count": node.get_bone_count(), "bones": bones})
		if node is MeshInstance3D and node.mesh:
			record.meshes += 1
			var box: AABB = node.get_aabb()
			if not node.skin:
				for corner in 8:
					points.append(node.global_transform * box.get_endpoint(corner))
			if node.skin:
				record.skins.append({"mesh": str(model.get_path_to(node)), "binds": node.skin.get_bind_count(), "skeleton_path": str(node.skeleton)})
			var rest_matrices: Array[Transform3D] = []
			var skeleton := node.get_node_or_null(node.skeleton) as Skeleton3D
			if node.skin and skeleton:
				for bind in node.skin.get_bind_count():
					var bone := skeleton.find_bone(node.skin.get_bind_name(bind))
					if bone < 0:
						bone = node.skin.get_bind_bone(bind)
					rest_matrices.append(skeleton.global_transform * skeleton.get_bone_global_rest(bone) * node.skin.get_bind_pose(bind))
			for surface in node.mesh.get_surface_count():
				record.surfaces += 1
				var arrays: Array = node.mesh.surface_get_arrays(surface)
				var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
				record.vertices += vertices.size()
				if arrays[Mesh.ARRAY_INDEX] != null:
					record.triangles += arrays[Mesh.ARRAY_INDEX].size() / 3
				else:
					record.triangles += vertices.size() / 3
				if arrays[Mesh.ARRAY_WEIGHTS] != null:
					var weights: PackedFloat32Array = arrays[Mesh.ARRAY_WEIGHTS]
					var bone_indices: PackedInt32Array = arrays[Mesh.ARRAY_BONES]
					var stride := weights.size() / maxi(vertices.size(), 1)
					for index in vertices.size():
						var total := 0.0
						var skinned := Vector3.ZERO
						for weight in stride:
							total += weights[index * stride + weight]
							if not rest_matrices.is_empty():
								skinned += (rest_matrices[bone_indices[index * stride + weight]] * vertices[index]) * weights[index * stride + weight]
						if not rest_matrices.is_empty():
							points.append(skinned)
						if total < 0.0001:
							record.zero_weight_vertices += 1
						if absf(total - 1.0) > 0.01:
							record.weight_sum_outliers += 1
				var material: Material = node.get_active_material(surface)
				if material is BaseMaterial3D:
					record.materials.append({"name": material.resource_name, "albedo": str(material.albedo_color), "texture": str(material.albedo_texture.resource_path) if material.albedo_texture else "", "vertex_colors": material.vertex_color_use_as_albedo, "transparency": material.transparency, "cull": material.cull_mode})
	var bounds := AABB(points[0], Vector3.ZERO)
	for point in points:
		bounds = bounds.expand(point)
	record["bounds_min"] = _vec(bounds.position)
	record["bounds_size"] = _vec(bounds.size)
	record["scale_for_2_8m_height"] = 2.8 / maxf(bounds.size.y, 0.01)
	print("[BossAudit] ", model_name, " size=", bounds.size, " meshes=", record.meshes, " vertices=", record.vertices, " triangles=", record.triangles, " bones=", record.skeletons[0].count if not record.skeletons.is_empty() else 0, " animations=", record.animations.size(), " zero_weights=", record.zero_weight_vertices)
	return record

func _shot(name: String) -> void:
	await RenderingServer.frame_post_draw
	get_viewport().get_texture().get_image().save_png("res://boss_audit_%s.png" % name)

func _run() -> void:
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color(0.15, 0.19, 0.23)
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color(0.75, 0.83, 0.95)
	environment.ambient_light_energy = 0.55
	var world := WorldEnvironment.new()
	world.environment = environment
	add_child(world)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-42, -28, 0)
	sun.light_energy = 1.2
	sun.shadow_enabled = true
	add_child(sun)
	_camera = Camera3D.new()
	_camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	_camera.current = true
	add_child(_camera)
	var names := ["DRIZZEL", "GEKKOU", "KASA", "KURENAI", "SAMIDALE", "SHOGUN"]
	for i in names.size():
		var packed: PackedScene = load("res://models/%s.fbx" % names[i])
		var model := packed.instantiate() as Node3D
		add_child(model)
		var record := _inspect(model, names[i])
		_report.append(record)
		var scale_value: float = record.scale_for_2_8m_height
		model.scale = Vector3.ONE * scale_value
		model.position = Vector3((i % 3 - 1) * 4.4, -float(record.bounds_min[1]) * scale_value, -3.5 if i < 3 else 3.5)
		_models.append(model)
		var label := Label3D.new()
		label.text = names[i]
		label.font_size = 48
		label.pixel_size = 0.008
		label.position = Vector3(model.position.x, -0.38, model.position.z)
		label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		add_child(label)
		_labels.append(label)
	_camera.size = 10.5
	_camera.position = Vector3(0, 12, 16)
	_camera.look_at(Vector3(0, 1.3, 0))
	await get_tree().create_timer(1.0).timeout
	await _shot("six_front")
	for i in names.size():
		for other in names.size():
			_models[other].visible = other == i
			_labels[other].visible = other == i
		_camera.size = 4.0
		_camera.position = Vector3(_models[i].position.x + 4, 2.8, _models[i].position.z + 6)
		_camera.look_at(Vector3(_models[i].position.x, 1.4, _models[i].position.z))
		await get_tree().create_timer(0.1).timeout
		await _shot(names[i].to_lower())
		var skeleton := _models[i].find_child("Skeleton3D", true, false) as Skeleton3D
		var arm := skeleton.find_bone("mixamorig_RightArm")
		var hand := skeleton.find_bone("mixamorig_RightHand")
		var original := skeleton.get_bone_pose_rotation(arm)
		var before := skeleton.global_transform * skeleton.get_bone_global_pose(hand).origin
		skeleton.set_bone_pose_rotation(arm, Quaternion(Vector3.FORWARD, 0.6) * original)
		skeleton.force_update_all_bone_transforms()
		await get_tree().process_frame
		var after := skeleton.global_transform * skeleton.get_bone_global_pose(hand).origin
		_report[i]["right_arm_pose_hand_displacement_m"] = before.distance_to(after)
		if i == 5:
			await _shot("shogun_pose")
		skeleton.set_bone_pose_rotation(arm, original)
	var output := FileAccess.open("res://audit-results.json", FileAccess.WRITE)
	output.store_string(JSON.stringify(_report, "  "))
	output.close()
	print("[BossAudit] 导入与渲染审查完成：6/6；原模型未修改；不包含Mixamo真实动作验收。")
	get_tree().quit()

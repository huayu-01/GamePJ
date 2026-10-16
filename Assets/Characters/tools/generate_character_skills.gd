extends SceneTree
## 将既有技能的事件和节奏保留，按全局rest差值适配新骨架；不在运行时改共享动画。

const BONE_MAP := {"Hips": "root", "Spine02": "torso", "Head": "head", "LeftArm": "arm-left", "RightArm": "arm-right", "LeftUpLeg": "leg-left", "RightUpLeg": "leg-right"}

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var source_model: Node3D = load("res://Assets/Models/player/mesh_player_body.glb").instantiate()
	root.add_child(source_model)
	var source_skeleton := source_model.find_child("Skeleton3D", true, false) as Skeleton3D
	var source_player := source_model.find_child("AnimationPlayer", true, false) as AnimationPlayer
	var library := AnimationLibrary.new()
	var source_animations: Array[Animation] = []
	for filename in DirAccess.get_files_at("res://Assets/Animations/skills"):
		if not filename.ends_with(".tres"):
			continue
		var animation := load("res://Assets/Animations/skills/" + filename) as Animation
		source_animations.append(animation)
		var copy := animation.duplicate(true) as Animation
		for track in copy.get_track_count():
			if copy.track_get_type(track) == Animation.TYPE_METHOD:
				copy.track_set_enabled(track, false)
		library.add_animation(animation.resource_name, copy)
	source_player.add_animation_library("source", library)
	for character_name in ["Lyra", "Samurai"]:
		var target_model: Node3D = load("res://Assets/Characters/%s/%s.glb" % [character_name, character_name.to_lower()]).instantiate()
		root.add_child(target_model)
		var skeleton := target_model.find_child("Skeleton3D", true, false) as Skeleton3D
		var native_player := target_model.find_child("AnimationPlayer", true, false) as AnimationPlayer
		var motions: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://Assets/Characters/Samurai/skill-motions.json")) if character_name == "Samurai" else {}
		var output := "res://Assets/Characters/%s/skills/" % character_name
		DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(output))
		for animation in source_animations:
			var generated := Animation.new()
			generated.resource_name = animation.resource_name
			generated.length = animation.length
			generated.loop_mode = animation.loop_mode
			var tracks: Array[int] = []
			for bone in skeleton.get_bone_count():
				var track := generated.add_track(Animation.TYPE_ROTATION_3D)
				generated.track_set_path(track, NodePath("Armature/Skeleton3D:" + skeleton.get_bone_name(bone)))
				tracks.append(track)
			var hips_track := generated.add_track(Animation.TYPE_POSITION_3D)
			generated.track_set_path(hips_track, NodePath("Armature/Skeleton3D:Hips"))
			var motion: Dictionary = motions.get(animation.resource_name, {})
			var impact_time := animation.length * 0.75
			for track in animation.get_track_count():
				if animation.track_get_type(track) != Animation.TYPE_METHOD:
					continue
				for key in animation.track_get_key_count(track):
					if animation.track_get_key_value(track, key)["args"][0] == "ATK":
						impact_time = animation.track_get_key_time(track, key)
			if not motion.is_empty():
				native_player.play(motion.clip)
			source_player.play("source/" + animation.resource_name)
			var frames := maxi(1, ceili(animation.length * 30.0))
			for frame in frames + 1:
				var time := minf(frame / 30.0, animation.length)
				if not motion.is_empty():
					# 前摇/命中/收招三点对齐既有事件，不把原片段整个加速后错开发伤害。
					var native_time := remap(time, 0.0, impact_time, motion.start, motion.impact) if time <= impact_time else remap(time, impact_time, animation.length, motion.impact, motion.end)
					native_player.seek(native_time, true)
					skeleton.force_update_all_bone_transforms()
					for bone in skeleton.get_bone_count():
						generated.rotation_track_insert_key(tracks[bone], time, skeleton.get_bone_pose_rotation(bone))
					var hips := skeleton.get_bone_pose_position(skeleton.find_bone("Hips"))
					var rest := skeleton.get_bone_rest(skeleton.find_bone("Hips")).origin
					hips.x = rest.x
					hips.z = rest.z
					generated.position_track_insert_key(hips_track, time, hips)
					continue
				source_player.seek(time, true)
				source_skeleton.force_update_all_bone_transforms()
				var global_rotations: Array[Quaternion] = []
				for bone in skeleton.get_bone_count():
					var bone_name := skeleton.get_bone_name(bone)
					var parent := skeleton.get_bone_parent(bone)
					var parent_rotation := Quaternion.IDENTITY if parent < 0 else global_rotations[parent]
					var local_rotation := skeleton.get_bone_rest(bone).basis.get_rotation_quaternion()
					if BONE_MAP.has(bone_name):
						var source_bone := source_skeleton.find_bone(BONE_MAP[bone_name])
						var delta := source_skeleton.get_bone_global_pose(source_bone).basis.get_rotation_quaternion() * source_skeleton.get_bone_global_rest(source_bone).basis.get_rotation_quaternion().inverse()
						local_rotation = parent_rotation.inverse() * delta * skeleton.get_bone_global_rest(bone).basis.get_rotation_quaternion()
					global_rotations.append((parent_rotation * local_rotation).normalized())
					generated.rotation_track_insert_key(tracks[bone], time, local_rotation.normalized())
				var root_bone := source_skeleton.find_bone("root")
				var bob := source_skeleton.get_bone_global_pose(root_bone).origin.y - source_skeleton.get_bone_global_rest(root_bone).origin.y
				var hips := skeleton.get_bone_rest(skeleton.find_bone("Hips")).origin
				hips.y += bob * (170.0 / 0.6713)
				generated.position_track_insert_key(hips_track, time, hips)
			for track in animation.get_track_count():
				if animation.track_get_type(track) == Animation.TYPE_METHOD:
					var event_track := generated.add_track(Animation.TYPE_METHOD)
					generated.track_set_path(event_track, animation.track_get_path(track))
					for key in animation.track_get_key_count(track):
						generated.track_insert_key(event_track, animation.track_get_key_time(track, key), animation.track_get_key_value(track, key))
			var result := ResourceSaver.save(generated, output + animation.resource_name + ".tres")
			assert(result == OK, "技能动画保存失败")
		print("[CharacterSkills] ", character_name, " 已适配 ", source_animations.size(), " 段技能；原生动作映射 ", motions.size(), " 段；事件与时长保持。")
		target_model.free()
	source_model.free()
	await process_frame
	quit()

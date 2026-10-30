extends SceneTree

const SOURCE_NAMES = {
	"root":"Root", "pelvis":"Hips", "spine_01":"Spine", "spine_02":"Chest", "spine_03":"UpperChest", "neck_01":"Neck", "Head":"Head",
	"clavicle_l":"LeftShoulder", "upperarm_l":"LeftUpperArm", "lowerarm_l":"LeftLowerArm", "hand_l":"LeftHand",
	"clavicle_r":"RightShoulder", "upperarm_r":"RightUpperArm", "lowerarm_r":"RightLowerArm", "hand_r":"RightHand",
	"thigh_l":"LeftUpperLeg", "calf_l":"LeftLowerLeg", "foot_l":"LeftFoot", "ball_l":"LeftToes",
	"thigh_r":"RightUpperLeg", "calf_r":"RightLowerLeg", "foot_r":"RightFoot", "ball_r":"RightToes"
}
const TARGET_NAMES = {
	"Root":"Root", "mixamorig:Hips":"Hips", "mixamorig:Spine":"Spine", "mixamorig:Spine1":"Chest", "mixamorig:Spine2":"UpperChest", "mixamorig:Neck":"Neck", "mixamorig:Head":"Head",
	"mixamorig:LeftShoulder":"LeftShoulder", "mixamorig:LeftArm":"LeftUpperArm", "mixamorig:LeftForeArm":"LeftLowerArm", "mixamorig:LeftHand":"LeftHand",
	"mixamorig:RightShoulder":"RightShoulder", "mixamorig:RightArm":"RightUpperArm", "mixamorig:RightForeArm":"RightLowerArm", "mixamorig:RightHand":"RightHand",
	"mixamorig:LeftUpLeg":"LeftUpperLeg", "mixamorig:LeftLeg":"LeftLowerLeg", "mixamorig:LeftFoot":"LeftFoot", "mixamorig:LeftToeBase":"LeftToes",
	"mixamorig:RightUpLeg":"RightUpperLeg", "mixamorig:RightLeg":"RightLowerLeg", "mixamorig:RightFoot":"RightFoot", "mixamorig:RightToeBase":"RightToes"
}

func load_glb(path: String) -> Node:
	var document = GLTFDocument.new()
	var state = GLTFState.new()
	var error = document.append_from_file(path, state)
	assert(error == OK, "GLB parsing failed")
	return document.generate_scene(state)

func rename_skeleton(model: Node, names: Dictionary) -> Skeleton3D:
	var skeleton = model.find_children("*", "Skeleton3D", true, false)[0] as Skeleton3D
	for mesh in model.find_children("*", "MeshInstance3D", true, false):
		if mesh.skin:
			for index in mesh.skin.get_bind_count():
				var name = String(mesh.skin.get_bind_name(index))
				name = name.replace("mixamorig_", "mixamorig:")
				if names.has(name):
					mesh.skin.set_bind_name(index, names[name])
	for index in skeleton.get_bone_count():
		var name = String(skeleton.get_bone_name(index))
		name = name.replace("mixamorig_", "mixamorig:")
		if names.has(name):
			skeleton.set_bone_name(index, names[name])
	for player in model.find_children("*", "AnimationPlayer", true, false):
		for clip in player.get_animation_list():
			var animation = player.get_animation(clip)
			for index in animation.get_track_count():
				var path = animation.track_get_path(index)
				if path.get_subname_count() > 0:
					var name = String(path.get_concatenated_subnames())
					if names.has(name):
						animation.track_set_path(index, NodePath(String(path.get_concatenated_names()) + ":" + names[name]))
	return skeleton

func bake_clip(player: AnimationPlayer, skeleton: Skeleton3D, clip: String) -> Animation:
	var duration = player.get_animation(clip).length
	var animation = Animation.new()
	animation.length = duration
	for index in skeleton.get_bone_count():
		var track = animation.add_track(Animation.TYPE_ROTATION_3D)
		animation.track_set_path(track, NodePath(String(skeleton.name) + ":" + skeleton.get_bone_name(index)))
	var frames = int(ceil(duration * 30))
	for frame in frames + 1:
		var time = min(float(frame) / 30.0, duration)
		player.play(clip)
		player.seek(time, true)
		player.advance(0)
		await physics_frame
		await process_frame
		for index in skeleton.get_bone_count():
			animation.rotation_track_insert_key(index, time, skeleton.get_bone_pose_rotation(index))
	return animation

func _initialize() -> void:
	call_deferred("build_pilot")

func build_pilot() -> void:
	var project_root = get_script().resource_path.get_base_dir().get_base_dir().get_base_dir().get_base_dir()
	var target = load_glb(project_root.path_join("ArtSource/Exports/humanoid-pilot/humanoid-pilot.glb"))
	root.add_child(target)
	for player in target.find_children("*", "AnimationPlayer", true, false):
		player.free()
	var skeleton = rename_skeleton(target, TARGET_NAMES)
	var output = Node3D.new()
	output.name = "GeneratedPilot"
	root.add_child(output)
	var library = AnimationLibrary.new()
	var sets = {
		"UAL1_Standard.glb": {"Idle_Loop":"idle", "Walk_Loop":"walk", "Jog_Fwd_Loop":"jog", "Sprint_Loop":"sprint", "Jump_Start":"jump_start", "Jump_Loop":"jump", "Jump_Land":"land", "Death01":"die"},
		"UAL2_Standard.glb": {"Sword_Regular_A":"attack", "Sword_Regular_B":"attack_b", "Hit_Knockback":"hit"}
	}
	for filename in sets:
		var model = load_glb(project_root.path_join("ArtSource/Animations/UAL/Source/").path_join(filename))
		root.add_child(model)
		var source_skeleton = rename_skeleton(model, SOURCE_NAMES)
		var source_player = model.find_children("*", "AnimationPlayer", true, false)[0] as AnimationPlayer
		var modifier = RetargetModifier3D.new()
		source_skeleton.add_child(modifier)
		skeleton.reparent(modifier, true)
		modifier.profile = SkeletonProfileHumanoid.new()
		modifier.set_position_enabled(false)
		modifier.set_scale_enabled(false)
		for clip in sets[filename]:
			var animation = await bake_clip(source_player, skeleton, clip)
			library.add_animation(sets[filename][clip], animation)
			print("baked ", sets[filename][clip])
		skeleton.reparent(output, true)
		model.queue_free()
		await process_frame
	skeleton.reset_bone_poses()
	var player = AnimationPlayer.new()
	player.name = "AnimationPlayer"
	output.add_child(player)
	player.add_animation_library("", library)
	var document = GLTFDocument.new()
	var state = GLTFState.new()
	assert(document.append_from_scene(output, state) == OK)
	assert(document.write_to_filesystem(state, project_root.path_join("Assets/Characters/GeneratedPilot/pilot_ual.glb")) == OK)
	print("PILOT_READY ", library.get_animation_list())
	quit()

extends SceneTree

func _initialize() -> void:
	call_deferred("validate_pilot")

func validate_pilot() -> void:
	var packed = load("res://humanoid-pilot.glb") as PackedScene
	if packed == null:
		push_error("GLB could not be loaded")
		quit(1)
		return
	var model = packed.instantiate()
	root.add_child(model)
	var skeleton = model.find_children("*", "Skeleton3D", true, false)[0] as Skeleton3D
	var player = model.find_children("*", "AnimationPlayer", true, false)[0] as AnimationPlayer
	var clips = player.get_animation_list()
	var report = {"bones": skeleton.get_bone_count(), "clips": [], "walk_pose_delta": 0.0}
	for clip in clips:
		var animation = player.get_animation(clip)
		report.clips.append({"name": clip, "duration": animation.length, "tracks": animation.get_track_count()})
		if "Walk" in clip:
			player.play(clip)
			player.seek(0.0, true)
			player.advance(0)
			var first = []
			for index in skeleton.get_bone_count():
				first.append(skeleton.get_bone_pose_rotation(index))
			player.seek(animation.length * 0.25, true)
			player.advance(0)
			for index in skeleton.get_bone_count():
				report.walk_pose_delta += first[index].angle_to(skeleton.get_bone_pose_rotation(index))
	var valid = report.bones == 23 and report.clips.size() == 3 and report.walk_pose_delta > 0.1
	report["passed"] = valid
	var output = FileAccess.open("res://validation.json", FileAccess.WRITE)
	output.store_string(JSON.stringify(report, "  ") + "\n")
	print(JSON.stringify(report))
	quit(0 if valid else 1)

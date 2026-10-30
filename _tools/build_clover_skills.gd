extends SceneTree

# 当前骨架的轻施法姿态配上原技能的事件时刻；不复制旧人形的骨轴与手臂轨道。
func _initialize() -> void:
	var packed = load("res://Assets/Characters/CloverWayfarer/clover-wayfarer.glb") as PackedScene
	var actor = packed.instantiate()
	root.add_child(actor)
	var player = actor.find_child("AnimationPlayer", true, false) as AnimationPlayer
	assert(player != null)
	print("[CloverBuild] hierarchy ", actor.get_tree_string_pretty())
	var idle = player.get_animation("idle")
	var names = ["Skill_A1_ATK1", "Skill_A1_ATK2", "ATK_A2_Scatter", "ATK_A2_ChargedVolley", "ATK_A3_Draw", "Skill_A1_Backdash", "Skill_A1_Leap"]
	for clip_name in names:
		var original = load("res://Assets/Characters/Lyra/skills/" + clip_name + ".tres") as Animation
		var clip = Animation.new()
		clip.resource_name = clip_name
		clip.length = original.length
		clip.step = 1.0 / 60.0
		var count = int(ceil(clip.length * 60))
		for source_track in range(idle.get_track_count()):
			var kind = idle.track_get_type(source_track)
			if not kind in [Animation.TYPE_POSITION_3D, Animation.TYPE_ROTATION_3D, Animation.TYPE_SCALE_3D]:
				continue
			var track = clip.add_track(kind)
			clip.track_set_path(track, idle.track_get_path(source_track))
			clip.track_set_interpolation_type(track, Animation.INTERPOLATION_LINEAR)
			for i in range(count + 1):
				var time = min(float(i) / 60.0, clip.length)
				var value
				if kind == Animation.TYPE_POSITION_3D:
					value = idle.position_track_interpolate(source_track, time * .4)
				elif kind == Animation.TYPE_SCALE_3D:
					value = idle.scale_track_interpolate(source_track, time * .4)
				else:
					value = idle.rotation_track_interpolate(source_track, time * .4)
					var bone = str(idle.track_get_path(source_track)).get_slice(":", 1)
					var pulse = sin(PI * time / clip.length)
					if bone == "Chest":
						value = value * Quaternion(Vector3.RIGHT, -.045 * pulse)
					elif bone == "Head":
						value = value * Quaternion(Vector3.RIGHT, .025 * pulse)
				clip.track_insert_key(track, time, value)
		# 原事件资源是权威，ATK/Dodge/Dodger/Action的时刻与参数逐个复制。
		for source_track in range(original.get_track_count()):
			if original.track_get_type(source_track) != Animation.TYPE_METHOD:
				continue
			var track = clip.add_track(Animation.TYPE_METHOD)
			clip.track_set_path(track, NodePath("."))
			for i in range(original.track_get_key_count(source_track)):
				clip.track_insert_key(track, original.track_get_key_time(source_track, i), original.track_get_key_value(source_track, i))
		var path = "res://Assets/Characters/CloverWayfarer/skills/" + clip_name + ".tres"
		assert(ResourceSaver.save(clip, path) == OK)
		print("[CloverBuild] ", clip_name, " tracks=", clip.get_track_count())
	actor.queue_free()
	print("[CloverBuild] PASS")
	quit(0)

extends SceneTree
## 独立观察所选模型的真实骨架和动作，不修改导入资源。

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var stage := Node3D.new()
	root.add_child(stage)
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color(0.12, 0.17, 0.19)
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color.WHITE
	environment.ambient_light_energy = 0.7
	var world := WorldEnvironment.new()
	world.environment = environment
	stage.add_child(world)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-35, -30, 0)
	stage.add_child(sun)
	var camera := Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = 4.5
	camera.position = Vector3(0, 1.8, 6)
	stage.add_child(camera)
	camera.look_at(Vector3(0, 0.9, 0))
	if "--native-attacks" in OS.get_cmdline_user_args():
		camera.size = 6.5
		camera.position = Vector3(0, 1.8, 8)
		camera.look_at(Vector3(0, 0.9, 0))
		for motion in ["attack", "double_attack"]:
			var models: Array[Node3D] = []
			for frame in 6:
				var model: Node3D = load("res://Assets/Characters/Samurai/samurai.glb").instantiate()
				model.position.x = (frame - 2.5) * 1.6
				stage.add_child(model)
				models.append(model)
				var skeleton := model.find_child("Skeleton3D", true, false) as Skeleton3D
				var grip: BoneAttachment3D = load("res://Scenes/Visual/MeshyWeaponGrip.tscn").instantiate()
				skeleton.add_child(grip)
				grip.get_node("Socket").add_child(load("res://Scenes/Weapons/Visuals/spirit_sword.tscn").instantiate())
				var player := model.find_child("AnimationPlayer", true, false) as AnimationPlayer
				var time := float(player.get_animation(motion).length) * (frame / 5.0)
				player.play(motion)
				player.seek(time, true)
				player.pause()
				var label := Label3D.new()
				label.text = "%.2fs" % time
				label.position = Vector3(0, -0.2, 0)
				label.font_size = 38
				label.pixel_size = 0.005
				model.add_child(label)
			await create_timer(0.1).timeout
			await RenderingServer.frame_post_draw
			root.get_texture().get_image().save_png("res://docs/screenshots/characters_native_%s.png" % motion)
			for model in models:
				model.free()
		stage.free()
		await process_frame
		quit()
		return
	for index in 2:
		var character_name: String = ["Lyra", "Samurai"][index]
		var packed: PackedScene = load("res://Assets/Characters/%s/%s.glb" % [character_name, character_name.to_lower()])
		var model := packed.instantiate() as Node3D
		model.position.x = -1.2 if index == 0 else 1.2
		stage.add_child(model)
		var skeleton := model.find_child("Skeleton3D", true, false) as Skeleton3D
		var bones: Array = []
		for bone in skeleton.get_bone_count():
			bones.append({"name": skeleton.get_bone_name(bone), "parent": skeleton.get_bone_parent(bone), "rest": str(skeleton.get_bone_rest(bone))})
		var player := model.find_child("AnimationPlayer", true, false) as AnimationPlayer
		var animations: Array = []
		for animation_name in player.get_animation_list():
			var animation := player.get_animation(animation_name)
			animations.append({"name": animation_name, "length": animation.length, "tracks": animation.get_track_count(), "first_path": str(animation.track_get_path(0))})
		print("[CharacterInspect] ", character_name, " ", JSON.stringify({"bones": bones, "animations": animations, "skeleton_transform": str(skeleton.global_transform)}))
		player.play("idle")
		player.advance(0.1)
	await create_timer(0.5).timeout
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png("res://docs/screenshots/characters_native_idle.png")
	stage.free()
	await process_frame
	quit()

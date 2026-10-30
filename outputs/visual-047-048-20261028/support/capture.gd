extends SceneTree

var destination = "F:/Godot项目/项目a/outputs/visual-047-048-20261028/evidence/"

func _initialize():
	call_deferred("capture_run")

func wait_for(seconds: float):
	await create_timer(seconds, true).timeout

func shot(label: String):
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(destination + label + ".png")
	print("AUDIT_SHOT ", label)

func tap(key: Key):
	var event = InputEventKey.new()
	event.keycode = key
	event.pressed = true
	Input.parse_input_event(event)
	await process_frame
	event = InputEventKey.new()
	event.keycode = key
	event.pressed = false
	Input.parse_input_event(event)
	await wait_for(0.6)

func load_stage(path: String):
	paused = false
	root.get_node("UiRouter").ResetForSceneChange()
	change_scene_to_file(path)
	await wait_for(4.0)
	var player = current_scene.find_child("Player", true, false)
	if player != null:
		player.set("IsInvulnerable", true)
	print("AUDIT_STAGE ", path)

func capture_run():
	print("AUDIT_USER ", OS.get_user_data_dir())
	await load_stage("res://Scenes/Levels/Base.tscn")
	await shot("01-base-current")
	await tap(KEY_ESCAPE)
	await shot("02-pause-current")
	await tap(KEY_ESCAPE)
	await tap(KEY_J)
	var codex = current_scene.find_child("CodexScreen", true, false)
	if codex == null:
		codex = root.find_child("CodexScreen", true, false)
	if codex != null:
		codex.ChooseCategory("enemies")
		codex.SetSearch("胆小菇")
	await wait_for(0.4)
	await shot("03-codex-current")
	await tap(KEY_ESCAPE)
	await load_stage("res://Scenes/Expedition.tscn")
	await shot("04-adventure-current")
	# 在隔离场景中展示原有组件，不写战斗数值或生产资源。
	var player = current_scene.find_child("Player", true, false)
	if player != null:
		var pos = player.global_position
		var indicator = load("res://Scenes/Combat/SkillIndicator.tscn").instantiate()
		current_scene.add_child(indicator)
		indicator.set("IsHostile", true)
		indicator.ShowAreaPoint(pos + Vector3(-2, 0, -3), 3.0)
		var line = load("res://Scenes/Combat/SkillIndicator.tscn").instantiate()
		current_scene.add_child(line)
		line.set("IsHostile", true)
		line.ShowLineArea(pos + Vector3(2, 0, 1), Vector3(0, 0, -1), 8.0, 2.0)
		await wait_for(0.4)
		await shot("05-indicator-staged-current")
		indicator.queue_free()
		line.queue_free()
	await tap(KEY_C)
	var build = current_scene.find_child("BuildScreen", true, false)
	if build != null:
		build.Open()
		build.DebugShowSample(24)
	await wait_for(0.4)
	await shot("06-build-sample-current")
	await tap(KEY_ESCAPE)
	# 由现有敌人预制体实际执行行为，作为原冒险环境内的隔离战斗取景。
	for offset in [Vector3(-2,0,-3), Vector3(3,0,-4), Vector3(-4,0,-6)]:
		var enemy = load("res://Scenes/Enemies/Chaser.tscn").instantiate()
		current_scene.add_child(enemy)
		enemy.global_position = player.global_position + offset
	await wait_for(0.7)
	await shot("09-adventure-combat-staged-current")
	await load_stage("res://Scenes/Survival.tscn")
	await wait_for(5.0)
	await shot("07-survival-current")
	# 通过真实死亡和经验拾取触发升级奖励。
	var units = get_nodes_in_group("enemy_units")
	player = current_scene.find_child("Player", true, false)
	for unit in units:
		if player != null and is_instance_valid(unit):
			unit.global_position = player.global_position + Vector3(1,0,0)
			unit.ForceKill(player)
	await wait_for(2.0)
	await shot("08-reward-current")
	print("AUDIT_COMPLETE")
	quit()

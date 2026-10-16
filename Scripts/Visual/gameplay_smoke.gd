extends Node
## 正式 Main 的真实相机/HUD 冒烟取证，不修改战斗参数与存档。

func _ready() -> void:
	_run.call_deferred()

func _run() -> void:
	await get_tree().create_timer(0.3, true).timeout
	var controller := get_parent().get_node("RunController")
	controller.call("BeginRunWithCharacter", 3, 5)
	await get_tree().create_timer(0.8, true).timeout
	var arena := get_parent().get_node("TestArena")
	var environment: Environment = arena.get_node("WorldEnvironment").environment
	var correct_environment := arena.has_node("GardenDressing") and environment.resource_path == "res://Assets/ArtRefresh/env_level_garden.tres"
	print("[ArtRefresh] 正式Main环境=", environment.resource_path, " 新装饰=", arena.has_node("GardenDressing"))
	if not correct_environment:
		push_error("正式Main未加载新环境，拒绝将旧场地截图计入验收。")
		get_tree().quit(1)
		return
	get_window().content_scale_size = Vector2i(1920, 1080)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_VIEWPORT
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var image := get_viewport().get_texture().get_image()
	var result := image.save_png("res://docs/screenshots/art_refresh_gameplay.png")
	print("[ArtRefresh] 正式Main：1920x1080截图结果=", result, " 当前相机=", get_viewport().get_camera_3d().get_path())
	get_tree().quit(0 if result == OK else 1)

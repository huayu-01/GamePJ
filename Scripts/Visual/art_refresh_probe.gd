extends Node3D
## 独立拍摄台：加载生产预制体，验证实例隔离、安全子树与真实视口；不写存档。

var _failed := 0
var _passed := 0
var _camera: Camera3D
var _arena: Node3D

func _ready() -> void:
	_run.call_deferred()

func _check(ok: bool, message: String) -> void:
	if ok:
		_passed += 1
	else:
		_failed += 1
	print("[ArtRefresh] ", "PASS " if ok else "FAIL ", message)

func _wait(seconds: float) -> void:
	await get_tree().create_timer(seconds).timeout

func _shot(name: String) -> void:
	# 只调整拍摄台的运行期渲染尺寸，兼容用户已保存的窗口分辨率，不写设置文件。
	get_window().content_scale_size = Vector2i(1920, 1080)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_VIEWPORT
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var image := get_viewport().get_texture().get_image()
	var path := "res://docs/screenshots/art_refresh_%s.png" % name
	var result := image.save_png(path)
	_check(result == OK and image.get_width() == 1920 and image.get_height() == 1080, "真实渲染截图 " + name + " 1920x1080")

func _safe(node: Node) -> bool:
	if node is CollisionObject3D or not node.get_groups().is_empty():
		return false
	var script: Script = node.get_script()
	if script and script.resource_path.ends_with("Unit.cs"):
		return false
	for child in node.get_children():
		if not _safe(child):
			return false
	return true

func _materials(node: Node) -> Array[Material]:
	var result: Array[Material] = []
	if node is GeometryInstance3D and node.material_override:
		result.append(node.material_override)
	if node is GPUParticles3D and node.process_material:
		result.append(node.process_material)
	for child in node.get_children():
		result.append_array(_materials(child))
	return result

func _add_scene(path: String, pos: Vector3) -> Node3D:
	var node := load(path).instantiate() as Node3D
	node.position = pos
	add_child(node)
	return node

func _point(position_value: Vector3, target: Vector3) -> void:
	_camera.position = position_value
	_camera.look_at(target)
	_camera.current = true

func _run() -> void:
	if "--before" in OS.get_cmdline_user_args():
		await _before()
		return
	_arena = _add_scene("res://Scenes/Levels/TestArena.tscn", Vector3.ZERO)
	_camera = Camera3D.new()
	_camera.fov = 68.0
	add_child(_camera)
	_point(Vector3(22, 14, 26), Vector3(0, 2, -8))
	_check(_safe(_arena.get_node("GardenDressing")), "环境子树：碰撞/单位/分组均为0")
	_check(_arena.find_children("*", "CollisionShape3D", true, false).size() == 7, "竞技场原7个碰撞形状保持")
	_check(_arena.get_node("Ground/Collision").shape.size == Vector3(80, 1, 80), "竞技场地面物理80x1x80保持")
	print("[ArtRefresh] ", _arena.get_node("GardenDressing").debug_state())
	var model := _add_scene("res://Assets/Models/player/mesh_player_body.glb", Vector3(0, 0, 4))
	model.rotation.y = PI
	model.scale = Vector3.ONE * 2.6814
	var enemy := _add_scene("res://Assets/Models/enemies/mesh_enemy_chaser.glb", Vector3(-3, 0, 0))
	enemy.rotation.y = 0.6
	enemy.scale = Vector3.ONE * 2.72191
	await _wait(1.5)
	await _shot("arena")
	_point(Vector3(9, 5, 15), Vector3(-1, 1.2, 0))
	await _wait(0.2)
	await _shot("readability")
	var effects := ["res://Assets/Effects/fx_combat_hit.tscn", "res://Assets/Effects/fx_combat_explosion.tscn", "res://Assets/Effects/fx_weapon_muzzle.tscn", "res://Scenes/ArtRefresh/FxLootBeacon.tscn", "res://Scenes/ArtRefresh/FxBattleSwing.tscn"]
	for i in effects.size():
		var packed := load(effects[i]) as PackedScene
		var a := packed.instantiate() as Node3D
		var b := packed.instantiate() as Node3D
		var am := _materials(a)
		var bm := _materials(b)
		var isolated := not am.is_empty() and am.size() == bm.size()
		for j in am.size():
			if j < bm.size():
				isolated = isolated and am[j] != bm[j]
		_check(isolated, effects[i].get_file() + " 材质逐项实例隔离")
		_check(_safe(a) and _safe(b), effects[i].get_file() + " 无物理/单位/分组")
		a.position = Vector3(-5 + i * 2.5, 1.2, 4)
		b.position = Vector3(-5 + i * 2.5, 1.2, 0)
		add_child(a)
		add_child(b)
		if am[0] is ShaderMaterial:
			var original: Variant = bm[0].get_shader_parameter("primary_color")
			am[0].set_shader_parameter("primary_color", Color(0.95, 0.22, 0.08))
			_check(bm[0].get_shader_parameter("primary_color") == original, effects[i].get_file() + " 修改A不会污染B")
		a.queue_free()
		b.queue_free()
		await get_tree().process_frame
	# 同一机位拍摄真实动画两个时刻，保留默认播放速度。
	model.visible = false
	enemy.visible = false
	_point(Vector3(0, 6.8, 17), Vector3(0, 1.0, 1))
	var fx_nodes: Array[Node3D] = []
	for i in effects.size():
		var fx := _add_scene(effects[i], Vector3(-6 + i * 3, 0.8, 1))
		fx_nodes.append(fx)
		if i == 1:
			fx.scale = Vector3.ONE * 0.5
		if i == 2:
			fx.rotation.x = PI / 2
			fx.scale = Vector3.ONE * 0.5
			fx.call("play")
	await _wait(0.07)
	await _shot("fx_early")
	await _wait(0.17)
	await _shot("fx_late")
	for fx in fx_nodes:
		fx.queue_free()
	await _wait(0.1)
	_arena.queue_free()
	model.queue_free()
	enemy.queue_free()
	await get_tree().process_frame
	var base := _add_scene("res://Scenes/Levels/Base.tscn", Vector3.ZERO)
	base.get_node("Player").process_mode = Node.PROCESS_MODE_DISABLED
	_point(Vector3(10, 10, 12), Vector3(0, 1, -2))
	await _wait(1.0)
	_point(Vector3(10, 10, 12), Vector3(0, 1, -2))
	_check(_safe(base.get_node("GardenDressing")), "基地装饰无物理/单位/分组")
	_check(base.get_node("Ground/Shape").shape.size == Vector3(26, 1, 26), "基地地面物理26x1x26保持")
	await _shot("base")
	var filter: Node = load("res://Scenes/ArtRefresh/OptionalPixelFilter.tscn").instantiate()
	add_child(filter)
	await _wait(0.15)
	_check(filter.get_node("Filter").mouse_filter == Control.MOUSE_FILTER_IGNORE, "可选像素滤镜不拦截输入")
	await _shot("pixel_optional")
	filter.queue_free()
	await get_tree().process_frame
	_check(find_children("OptionalPixelFilter", "", true, false).is_empty(), "像素滤镜移除即消失，生产场景默认未挂载")
	print("[ArtRefresh] 验收结束：通过 %d，失败 %d" % [_passed, _failed])
	get_tree().quit(0 if _failed == 0 else 1)

func _before() -> void:
	_camera = Camera3D.new()
	_camera.fov = 68.0
	add_child(_camera)
	var arena_path := ""
	var base_path := ""
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--before-arena="):
			arena_path = argument.trim_prefix("--before-arena=")
		if argument.begins_with("--before-base="):
			base_path = argument.trim_prefix("--before-base=")
	var arena := _add_scene(arena_path, Vector3.ZERO)
	_point(Vector3(22, 14, 26), Vector3(0, 2, -8))
	var player := _add_scene("res://Assets/Models/player/mesh_player_body.glb", Vector3(0, 0, 4))
	player.rotation.y = PI
	player.scale = Vector3.ONE * 2.6814
	var enemy := _add_scene("res://Assets/Models/enemies/mesh_enemy_chaser.glb", Vector3(-3, 0, 0))
	enemy.rotation.y = 0.6
	enemy.scale = Vector3.ONE * 2.72191
	await _wait(1.5)
	await _shot("before_arena")
	_point(Vector3(9, 5, 15), Vector3(-1, 1.2, 0))
	await _wait(0.2)
	await _shot("before_readability")
	arena.queue_free()
	player.queue_free()
	enemy.queue_free()
	await get_tree().process_frame
	var base := _add_scene(base_path, Vector3.ZERO)
	base.get_node("Player").process_mode = Node.PROCESS_MODE_DISABLED
	await _wait(1.0)
	_point(Vector3(10, 10, 12), Vector3(0, 1, -2))
	await _wait(0.2)
	await _shot("before_base")
	get_tree().quit(0 if _failed == 0 else 1)

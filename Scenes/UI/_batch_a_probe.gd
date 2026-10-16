extends Node
## A 批验收：真实按键、GUI 点击、卡牌分页、血条与真实下落帧。只在独立探针场景运行。
## 卡牌使用禁止落盘的沙箱，退出码非零即失败；截图来自游戏视口。

var _failed := 0
var _main: Node
var _screen: Node


func _ready() -> void:
	_main = get_parent()
	_run()


func _check(ok: bool, text: String) -> void:
	if not ok:
		_failed += 1
	print("[BatchA] ", "PASS " if ok else "FAIL ", text)


func _wait(seconds: float) -> void:
	await get_tree().create_timer(seconds, true).timeout


func _key(code: Key) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = true
	Input.parse_input_event(event)
	await get_tree().process_frame
	await get_tree().process_frame
	event = InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = false
	Input.parse_input_event(event)


func _click(button: Control) -> void:
	var point := button.get_global_rect().get_center()
	var motion := InputEventMouseMotion.new()
	motion.position = point
	motion.global_position = point
	Input.parse_input_event(motion)
	for pressed: bool in [true, false]:
		var event := InputEventMouseButton.new()
		event.button_index = MOUSE_BUTTON_LEFT
		event.position = point
		event.global_position = point
		event.pressed = pressed
		Input.parse_input_event(event)
		await get_tree().process_frame
	await get_tree().process_frame


func _shot(file: String) -> void:
	if DisplayServer.get_name() == "headless":
		return
	var was_paused := get_tree().paused
	get_tree().paused = false
	await RenderingServer.frame_post_draw
	var result := get_viewport().get_texture().get_image().save_png("res://docs/screenshots/" + file)
	get_tree().paused = was_paused
	_check(result == OK, "视口截图 " + file)


func _run() -> void:
	await _wait(1.0)
	var settings: Node = _main.get_node("SettingsScreen")
	await _key(KEY_ESCAPE)
	await _wait(0.4)
	_check(settings.get("IsOpen") and get_tree().paused, "真实 Esc → 设置打开、暂停=True；分类=" + str(settings.get("SectionIds")))
	_check(settings.get("SectionCount") == 4 and not _main.has_node("HelpScreen"), "四个设置分页、独立帮助页已移除")
	await _shot("a_settings_esc.png")
	await _key(KEY_H)
	await _wait(0.2)
	_check(settings.get("ActiveSection") == 3, "H → 帮助分页")
	await _shot("a_settings_help.png")
	await _key(KEY_ESCAPE)
	await _wait(0.4)
	_check(not settings.get("IsOpen") and not get_tree().paused, "Esc 关闭设置并恢复游戏")
	var vitals: RichTextLabel = _main.get_node("Hud/Root/PlayerBar/Vitals")
	_check(vitals.text.contains("[color=#") and vitals.get_parsed_text().contains("+20"), "血条内异色护甲：" + vitals.get_parsed_text())
	_check(not _main.get_node("Hud/Root").has_node("Health"), "独立血量文本已移除")
	await _shot("a_hud_vitals.png")

	_screen = _main.get_node("AbilityMatrixScreen")
	_screen.call("DebugBeginSandbox")
	await _key(KEY_K)
	await _wait(0.4)
	_check(_screen.get("IsOpen") and _screen.get("TileCount") == 4 and _screen.get("SlotCount") == 4, "Main 中 K 打开能力页，真实卡 4 / 节点 4")
	var bar: ProgressBar = _screen.get_node("Root/Panel/HBox/QuotaRail/QuotaBar")
	_check(bar.size.x >= 50 and bar.size.x <= 80 and bar.fill_mode == ProgressBar.FILL_BOTTOM_TO_TOP, "额度条宽度=" + str(bar.size.x) + "；从下至上")
	_screen.call("DebugSetQuotaOccupancy", 5, 6)
	await _shot("a_matrix_four.png")
	print("[BatchA] ", _screen.call("DebugQuotaBarReport"))
	for count: int in [0, 1, 18, 19, 37]:
		_screen.call("DebugSetCardCount", count)
		await _wait(0.06)
		_check(_screen.get("TileCount") == mini(count, 18) and _screen.get("PageCount") == maxi(1, ceili(count / 18.0)), str(count) + " 张：首屏=" + str(_screen.get("TileCount")) + " 页数=" + str(_screen.get("PageCount")))
	_screen.call("DebugSetCardCount", 19)
	await _wait(0.1)
	var first: Object = _screen.call("TileAt", 0)
	first.call("DebugRunAction", 0)
	first.call("DebugRunAction", 2)
	var level: int = first.get("Level")
	await _shot("a_matrix_page1.png")
	if DisplayServer.get_name() == "headless":
		print("[BatchA] SKIP headless 不验证 GUI 命中，调用分页入口；GUI 点击以实拍模式为准")
		_screen.call("ChangePage", 1)
	else:
		await _click(_screen.get_node("Root/Pagination/Next"))
	_check(_screen.get("PageIndex") == 1 and _screen.get("TileCount") == 1, "下一页：第 19 张单独铺在末页")
	await _shot("a_matrix_page2.png")
	_screen.call("ChangePage", 1)
	_check(_screen.get("PageIndex") == 1, "末页继续下一页不越界")
	await _key(KEY_PAGEUP)
	await _wait(0.06)
	var restored: Object = _screen.call("TileAt", 0)
	_check(_screen.get("PageIndex") == 0 and restored.get("Level") == level and level == 2, "PgUp 回首页，升级状态 Lv2 保持")
	var root: Control = _screen.get_node("Root")
	var original_size := root.size
	root.size = Vector2(720, original_size.y)
	await _wait(0.1)
	_check(_screen.get("Columns") < 6 and _screen.get("RowCount") <= 3 and _screen.get("TileCount") <= _screen.get("PageCapacity"), "缩窄画布：列=" + str(_screen.get("Columns")) + " 行=" + str(_screen.get("RowCount")) + " 容量=" + str(_screen.get("PageCapacity")))
	root.size = original_size
	await _wait(0.1)
	_check(_screen.get("Columns") == 6 and _screen.get("PageCapacity") == 18, "还原宽度 → 六列 / 容量18")
	print("[BatchA] ", _screen.call("DebugSlotPlan"))
	_screen.call("DebugEndSandbox")
	_screen.call("Close")
	await _wait(0.4)

	# 真实角色 B + 真实 Ctrl 输入；不使用视觉预演，也不改变下落速度或特效参数。
	_main.get_node("RunController").call("BeginRunWithCharacter", 4, 6)
	await _wait(0.25)
	var player: CharacterBody3D = _main.get_node("Player")
	var fall: Node = player.get_node("AbilityFallAttack")
	var wind: Node = fall.get_node("DiveWind")
	var shock: Node = fall.get_node("Shockwave")
	var before_rings: int = shock.get("SpawnCount")
	var before_groups := _group_counts()
	player.global_position = Vector3(0, 12, 0)
	player.reset_physics_interpolation()
	player.get_node("Movement").call("SetVerticalVelocity", 0.0)
	await get_tree().physics_frame
	await get_tree().physics_frame
	await _key(KEY_CTRL)
	var sampled := 0
	var active_seen := false
	for frame in 60:
		await get_tree().physics_frame
		if wind.get("SourceActive") and wind.get("Intensity") > 0.1:
			active_seen = true
			if sampled < 2 and frame % 4 == 0:
				print("[BatchA] 真实下落帧 ", frame, " y=", player.global_position.y, " ", wind.call("DebugState"))
				await _shot("a_dive_%02d.png" % sampled)
				sampled += 1
		if fall.get("HasLanded"):
			break
	_check(active_seen and wind.get("SourceFrames") > 0, "真实 Ctrl 下落期间风线出现，未用预演")
	_check(fall.get("HasLanded") and shock.get("SpawnCount") > before_rings, "落地事件与既有落圈继续生效")
	await _shot("a_dive_landed.png")
	await _wait(0.25)
	_check(not wind.get("Visible") and wind.get("Intensity") == 0, "落地后风线收起")
	_check(wind.find_children("*", "CollisionObject3D", true, false).size() == 0 and wind.get_groups().is_empty(), "风效碰撞体=0、分组=0")
	_check(before_groups == _group_counts(), "特效前后单位分组计数不变：" + str(before_groups))
	print("[BatchA] ", wind.call("DebugState"))
	print("[BatchA] === A 批验收结束：失败 ", _failed, " ===")
	get_tree().paused = false
	get_tree().quit(0 if _failed == 0 else 1)


func _group_counts() -> Array[int]:
	return [get_tree().get_nodes_in_group("player_units").size(), get_tree().get_nodes_in_group("enemy_units").size(), get_tree().get_nodes_in_group("summon_units").size()]

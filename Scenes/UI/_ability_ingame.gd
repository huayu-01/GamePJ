extends Node
## 「能力矩阵在**真的 Main.tscn 里**能按 K 打开」的探针（第三批：能力卡系统）。
##
## 用法：
##   Godot.exe --path <项目> res://Scenes/UI/_AbilityInGame.tscn
## 输出：docs/screenshots/ability_06_ingame_base.png + 若干 [AbilityInGame] 行，然后退出。
##
## 为什么值得单独有一个探针：
##   ① `AbilityMatrixScreen` 是挂在 **Scenes/Main.tscn** 上的（和 BuildScreen / SettingsScreen 并列），
##      只有走 `UiService.OpenAbilityMatrix()`（按**类型**在整棵树里找）才能证明"它真的在场景里"；
##   ② 开它的按键是界面自己的 <c>ToggleKey</c>（K），所以这里**合成一个真实的按键事件**
##      （`Input.parse_input_event`，与 SelfTest 合成按键是同一套），而不是直接调 `Open()`。
##      这条路走通了，说明玩家按 K 的那条路走通了。
##   ③ 拍到的画面是**真的基地界面 + 真的矩阵**（不是拍摄台摆的示例状态），用来做"接线成功"的证据。

const SHOT_DIR := "res://docs/screenshots/"
const MAIN_SCENE := "res://Scenes/Main.tscn"
const KEY_K := 75


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	_run()


func _frames(n: int) -> void:
	for _i in n:
		await get_tree().process_frame


func _wait(seconds: float) -> void:
	await get_tree().create_timer(seconds, true).timeout


func _shot(file: String) -> void:
	get_tree().paused = false
	await RenderingServer.frame_post_draw
	var img := get_viewport().get_texture().get_image()
	var err := img.save_png(SHOT_DIR + file)
	print("[AbilityInGame] ", SHOT_DIR + file, "  ", img.get_width(), "x", img.get_height(), " err=", err)


func _press_key(code: int) -> void:
	var down := InputEventKey.new()
	down.keycode = code
	down.physical_keycode = code
	down.pressed = true
	Input.parse_input_event(down)
	await get_tree().process_frame
	var up := InputEventKey.new()
	up.keycode = code
	up.physical_keycode = code
	up.pressed = false
	Input.parse_input_event(up)


func _run() -> void:
	var ps := load(MAIN_SCENE) as PackedScene
	if ps == null:
		print("[AbilityInGame] 找不到 ", MAIN_SCENE)
		get_tree().quit()
		return

	add_child(ps.instantiate())
	# 等 RunController 的 _boot 流程把基地界面打开（它要等玩家/武器/Stage 三件套就绪）。
	await _wait(4.0)

	var svc: Node = get_node_or_null("/root/UiService")
	if svc == null:
		print("[AbilityInGame] 拿不到 UiService（autoload 没起来？）")
		get_tree().quit()
		return

	# 按类型在整棵树里找 —— 找不到就返回 false，所以这一行同时证明"界面真的挂在 Main.tscn 上"。
	var opened: bool = svc.call("OpenAbilityMatrix")
	print("[AbilityInGame] UiService.OpenAbilityMatrix() = ", opened, "（true = Main.tscn 里真的挂着这个界面）")
	await _frames(4)

	var screen: Node = get_tree().root.find_child("AbilityMatrixScreen", true, false)
	if screen == null:
		print("[AbilityInGame] 树里找不到 AbilityMatrixScreen 节点")
		get_tree().quit()
		return

	print("[AbilityInGame] 界面 IsOpen=", screen.get("IsOpen"),
		" 本页卡数=", screen.get("TileCount"), " 页码=", screen.get("PageIndex") + 1, "/", screen.get("PageCount"),
		" 表行数=", screen.get("CardRowCount"))
	print("[AbilityInGame] 提示栏：结构在树里=", screen.get("HasKeyHintBar"),
		" 条目=", screen.get("KeyHintCount"), " 可见=", screen.get("KeyHintsVisible"),
		" ZIndex=", screen.get("KeyHintBarZIndex"), " 鼠标穿透=", screen.get("KeyHintBarIgnoresMouse"))
	print("[AbilityInGame] 状态（真的存档）：", screen.call("DebugSummary"))
	await _shot("ability_06_ingame_opened.png")

	# 先关掉（同一条 K 路），再用**合成的真实按键**打开一次 —— 证明 ToggleKey 这条路真的通。
	# ⚠️ `OverlayScreen` 有 0.3 秒防误触（`InputBlocked`），关得太早会被挡掉（实测过）。
	await _wait(0.5)
	screen.call("Close")
	await _wait(0.6)
	print("[AbilityInGame] Close() 之后 IsOpen=", screen.get("IsOpen"), "（期望 False）")
	await _press_key(KEY_K)
	await _wait(0.6)
	print("[AbilityInGame] 合成按键 K 之后 IsOpen=", screen.get("IsOpen"),
		"（期望 True —— 玩家按 K 走的就是这条 ToggleKey 路）",
		" 提示栏可见=", screen.get("KeyHintsVisible"))
	await _shot("ability_07_ingame_toggle_key.png")

	print("[AbilityInGame] 结束（全程没有写 user://；这份存档是 real 模式留下的，用完已删）")
	await _frames(6)
	get_tree().quit()

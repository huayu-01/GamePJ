extends Node3D
## 能力矩阵界面拍摄台 + 交互自检（第三批：能力卡系统，`docs/ability-cards.md`）。
##
## 用法：
##   Godot.exe --path <项目> res://Scenes/UI/_AbilityShowcase.tscn
## 输出：res://docs/screenshots/ability_*.png + 若干 [AbilityShot] 行（逐格状态 / 四种交互的返回值 / 额度栏文案），
##       跑完自动退出。
##
## 为什么要一个有背景的场景而不是纯 Control：
##   这个界面在游戏里是盖在基地/关卡之上的（Dim 是 0.94 的深色半透明），
##   拍不到背景就分不清"是界面不透明"还是"根本没画出来"。跟 `_UiShowcase.tscn` 同一个理由，直接实例化 TestArena.tscn。
##
## ⚠️ 所有操作都在**沙箱**里（`AbilityStore.DebugBeginSandbox`）：界面走的是真的解锁 / 升级 / 激活 /
##   扩增额度链路（判定、读条、缓动、震动、音效都是真代码），但碎片是内存钱包、存档禁止落盘 ——
##   拍摄台不会改动玩家的 `user://meta.cfg` 与 `user://ability_cards.cfg`。

const SHOT_DIR := "res://docs/screenshots/"
const SCREEN_SCENE := "res://Scenes/UI/AbilityMatrixScreen.tscn"

## 示例状态里"未解锁"的那一格（用来拍长按解锁）；与 AbilityMatrixScreen.DebugShowSampleStates 的顺序一致。
const LOCKED_INDEX := 6

var _screen: Node = null
var _tile: Object = null


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	_run()


func _frames(n: int) -> void:
	for _i in n:
		await get_tree().process_frame


## 按**真实时间**等（`await 帧数` 在不同帧率下差很多：本机 120fps 时 30 帧只有 0.25 秒，
## 读条（0.7 秒）就永远读不完）。`process_always = true` → 界面把树暂停了也照样计时。
func _wait(seconds: float) -> void:
	await get_tree().create_timer(seconds, true).timeout


func _shot(file: String) -> void:
	# 界面会把树暂停（OverlayScreen 的分层策略），拍照前一定要先解开，否则 frame_post_draw 不触发。
	get_tree().paused = false
	await RenderingServer.frame_post_draw
	var img := get_viewport().get_texture().get_image()
	var err := img.save_png(SHOT_DIR + file)
	print("[AbilityShot] ", SHOT_DIR + file, "  ", img.get_width(), "x", img.get_height(), " err=", err)


func _run() -> void:
	var ps := load(SCREEN_SCENE) as PackedScene
	if ps == null:
		print("[AbilityShot] 找不到 ", SCREEN_SCENE)
		get_tree().quit()
		return

	_screen = ps.instantiate()
	add_child(_screen)
	await _frames(4)

	# `-- --real`：**端到端验证模式** —— 不加"禁止落盘"那层，真的写一份 user://ability_cards.cfg
	# （钱仍然是内存钱包，所以不会花掉玩家的碎片）。跑完拿这份存档去跑一局自测，
	# 用来证明"激活的卡真的在下局开局套到玩家身上"。
	if OS.get_cmdline_user_args().has("--real"):
		_screen.call("DebugBeginSandboxWithSave")
		_screen.call("Open")
		await _frames(4)
		var tile0: Object = _screen.call("TileAt", 0)
		print("[AbilityShot] real 模式：解锁 → ", tile0.call("DebugRunAction", 0), " ｜ ", tile0.call("DebugState"))
		print("[AbilityShot] real 模式：激活 → ", tile0.call("DebugRunAction", 1), " ｜ ", tile0.call("DebugState"))
		print("[AbilityShot] real 模式状态：", _screen.call("DebugSummary"))
		print("[AbilityShot] real 模式：user://ability_cards.cfg 已写出（用完请删掉，见 docs/ability-cards.md §7）")
		await _frames(8)
		get_tree().quit()
		return

	_screen.call("DebugBeginSandbox")
	_screen.call("Open")
	await _frames(4)
	_screen.call("DebugShowSampleStates")
	await _frames(24)

	print("[AbilityShot] 额度栏：", _screen.call("DebugHeaderText"))
	print("[AbilityShot] 状态：", _screen.call("DebugSummary"))
	print("[AbilityShot] 逐格：", _screen.call("DebugMatrixReport"))
	print("[AbilityShot] 满屏矩阵算式：", _screen.call("DebugSlotPlan"))
	await _shot("ability_01_matrix_states.png")

	# ── 占位卡（用户 A5）：不可解锁 / 激活 / 升级，但**点它照常有反馈**（震动 + 失败音效）──
	print("[AbilityShot] ", _screen.call("DebugRunPlaceholderFeedback"))
	var placeholder: Object = _screen.call("PlaceholderAt", 0)
	if placeholder != null:
		placeholder.call("DebugRunAction", 0)          # 再点一次，抓"正在震动"的那一帧
		await _frames(3)
		await _shot("ability_08_placeholder_feedback.png")
		print("[AbilityShot] 占位卡点一下之后：", placeholder.call("DebugState"))
	else:
		print("[AbilityShot] 拿不到占位卡（期望 ≥ 1：默认 6×3 = 18 格 / 8 张卡）")

	# ── 交互①②：左键长按读条 → 解锁（拍一张读条中的，再拍一张解锁后的）──
	_tile = _screen.call("TileAt", LOCKED_INDEX)
	if _tile == null:
		print("[AbilityShot] 拿不到第 ", LOCKED_INDEX, " 格（期望拿得到）")
	else:
		print("[AbilityShot] 长按前：", _tile.call("DebugState"))
		_tile.call("DebugBeginCharge", true)
		await _wait(0.35)          # 读条过半：进度条可见
		print("[AbilityShot] 读条中：", _tile.call("DebugState"))
		await _shot("ability_02_charging.png")
		await _wait(0.55)          # 越过 0.7s 阈值 → 读条结束，此刻判定资源并解锁
		print("[AbilityShot] 读条结束（刚解锁）：", _tile.call("DebugState"))
		await _shot("ability_03_unlocked.png")
		_tile.call("DebugReleaseCharge")

	# ── 交互③：右键单击升级（两次，看得出等级与边框颜色在变）──
	await _wait(0.5)
	print("[AbilityShot] 右键升级 → ", _tile.call("DebugRunAction", 2), " ｜ ", _tile.call("DebugState"))
	print("[AbilityShot] 再升一级 → ", _tile.call("DebugRunAction", 2), " ｜ ", _tile.call("DebugState"))

	# ── 交互④：左键单击激活（占额度）──
	print("[AbilityShot] 左键激活 → ", _tile.call("DebugRunAction", 1), " ｜ ", _tile.call("DebugState"))
	await _wait(0.4)
	await _shot("ability_04_active_glow.png")

	# ── 交互⑤：右键长按读条 → 重置升级并归还碎片 ──
	# ⚠️ `_screen.call(...)` 的返回值是 Variant：这里必须显式标 String，不能用 `:=` 推导
	#（本项目把 GDScript 警告当错误，"从 Variant 推导类型"会直接解析失败、整个拍摄台起不来）。
	var shards_before: String = str(_screen.call("DebugSummary"))
	_tile.call("DebugBeginCharge", false)
	await _wait(0.4)
	print("[AbilityShot] 长按右键读条中：", _tile.call("DebugState"))
	await _wait(0.6)
	print("[AbilityShot] 长按右键（重置）后：", _tile.call("DebugState"))
	print("[AbilityShot] 重置前后：", shards_before, " → ", _screen.call("DebugSummary"))
	_tile.call("DebugReleaseCharge")

	# ── 失败路径：额度装不下 / 额度扩增 ──
	print("[AbilityShot] 逐条交互（含故意失败）：", _screen.call("DebugRunInteractions"))
	print("[AbilityShot] 额度扩增后：", _screen.call("DebugSummary"))
	await _wait(0.4)
	await _shot("ability_05_after_interactions.png")

	print("[AbilityShot] 沙箱结束（玩家的 user:// 文件全程未被写入）")
	_screen.call("DebugEndSandbox")
	await _frames(6)
	get_tree().quit()

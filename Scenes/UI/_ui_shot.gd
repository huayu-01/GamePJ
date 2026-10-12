extends Node3D
## UI / glow 验证拍摄台（不依赖 gdmcp 运行时桥）。
##
## 用法：
##   Godot.exe --path <项目> res://Scenes/UI/_UiShowcase.tscn
## 输出：res://docs/screenshots/ 下的 ui_*.png（界面）与 ui_glow_*.png（glow 前后），然后退出。
##
## 为什么把「界面」和「glow」放在同一个拍摄台里：
##   界面截图需要背景（弹窗有半透明 Dim，背后总得有东西），
##   而 glow 是 `Scenes/Levels/TestArena.tscn` 的 WorldEnvironment 上的设置 ——
##   本场景**直接实例化 TestArena.tscn**，所以拍到的永远是「真正的那个环境」，
##   不会出现「展示场景自己开了 glow，游戏里没开」的假证据。

const SHOT_DIR := "res://docs/screenshots/"
const ARENA_ENV := "Arena/WorldEnvironment"

## 界面截图清单：节点路径 -> 输出文件。setup 由 _setup_screen 按名字分支。
const SCREENS := [
	{"name": "BaseScreen", "file": "ui_01_basescreen.png"},
	{"name": "HelpScreen", "file": "ui_02_helpscreen.png"},
	{"name": "StartScreen", "file": "ui_03_startscreen.png"},
	{"name": "RewardScreen", "file": "ui_04_rewardscreen.png"},
	{"name": "ChooseScreen", "file": "ui_05_choosescreen.png"},
	{"name": "BossPreviewScreen", "file": "ui_06_bosspreview.png"},
	{"name": "LoadingScreen", "file": "ui_07_loadingscreen.png"},
	{"name": "SettingsScreen", "file": "ui_10_settings.png"},
	{"name": "BuildScreen", "file": "ui_11_build.png"},
]

## 图标清单（与 `Scripts/UI/UiIcons.cs` 的 Names() 同一份；探针逐个 load 一遍证明"图真的能读出来"）。
const ICONS := [
	"ic_ui_reward_enhance", "ic_ui_reward_evolution", "ic_ui_reward_mutation",
	"ic_ui_node_weapon", "ic_ui_node_resource", "ic_ui_node_heal", "ic_ui_node_special",
	"ic_ui_node_shop", "ic_ui_node_portal", "ic_ui_build_holdings",
	"ic_ui_set_graphics", "ic_ui_set_controls", "ic_ui_set_audio",
]

## 样本卡片文案（只给「卡片是运行时按数据生成」的那几个界面用；不影响界面逻辑）
const CARD_TEXTS := {
	"StartScreen": ["1. 手枪 · 均衡（弹匣 12 / 射速中）", "2. 步枪 · 连射（弹匣 30 / 射速快）"],
	"RewardScreen": ["1. 伤害 +10%　（攻击类）", "2. 最大生命 +15　（生存类）", "3. 暴击率 +4%　（攻击类）"],
	"ChooseScreen": ["1. 进化池　（把已有增幅升到下一阶段）", "2. 异化池　（强力但有代价）", "3. 商店　（用碎片换资源）"],
}

## 样本卡片的图标 —— 与真实代码同一张图标表
## （StartScreen 用武器图标、RewardScreen 用奖励类型图标、ChooseScreen 按标题取节点图标），
## 所以截图里的图标就是玩家真正会看到的那几个。
const CARD_ICONS := {
	"StartScreen": ["ic_ui_node_weapon", "ic_ui_node_weapon"],
	"RewardScreen": ["ic_ui_reward_enhance", "ic_ui_reward_enhance", "ic_ui_reward_mutation"],
	"ChooseScreen": ["ic_ui_reward_evolution", "ic_ui_reward_mutation", "ic_ui_node_shop"],
}

const EXPLODE_AT := Vector3(0.0, 1.2, 0.0)
const CAM_FX := Vector3(0.0, 2.6, 9.0)
const CAM_WIDE := Vector3(0.0, 14.0, 40.0)

## glow 试拍档位：一次运行把几组参数都拍下来，挑一组写进 TestArena.tscn。
## 为什么需要试：这张竞技场的地面本来就接近纯白（发光前地板亮度已经 ~0.9），
## hdr 阈值定低了会把整块地板也一起吃进 bloom —— 那就不是「更亮」而是「过曝」。
const GLOW_TRIALS := [
	{"tag": "z_off", "cfg": {"off": true}},
	{
		"tag": "a_thr090_i090",
		"cfg": {"glow_intensity": 0.9, "glow_bloom": 0.15, "glow_strength": 1.1, "glow_hdr_threshold": 0.9,
			"glow_blend_mode": 0, "levels": [0.0, 0.0, 1.0, 1.0, 1.0, 0.5, 0.0]},
	},
	{
		"tag": "b_thr140_i060",
		"cfg": {"glow_intensity": 0.6, "glow_bloom": 0.05, "glow_strength": 1.0, "glow_hdr_threshold": 1.4,
			"glow_blend_mode": 1, "levels": [0.0, 0.0, 0.6, 0.8, 0.6, 0.3, 0.0]},
	},
	{
		"tag": "c_thr200_i050",
		"cfg": {"glow_intensity": 0.5, "glow_bloom": 0.0, "glow_strength": 1.0, "glow_hdr_threshold": 2.0,
			"glow_blend_mode": 1, "levels": [0.0, 0.0, 0.0, 0.8, 0.6, 0.4, 0.0]},
	},
	{
		"tag": "c2_thr170_i060",
		"cfg": {"glow_intensity": 0.6, "glow_bloom": 0.0, "glow_strength": 1.0, "glow_hdr_threshold": 1.7,
			"glow_blend_mode": 1, "levels": [0.0, 0.0, 0.4, 0.8, 0.6, 0.3, 0.0]},
	},
	{
		"tag": "c3_thr200_i070",
		"cfg": {"glow_intensity": 0.7, "glow_bloom": 0.05, "glow_strength": 1.0, "glow_hdr_threshold": 2.0,
			"glow_blend_mode": 1, "levels": [0.0, 0.0, 0.0, 0.9, 0.7, 0.4, 0.0]},
	},
	{
		"tag": "d_thr300_i050",
		"cfg": {"glow_intensity": 0.5, "glow_bloom": 0.0, "glow_strength": 1.0, "glow_hdr_threshold": 3.0,
			"glow_blend_mode": 1, "levels": [0.0, 0.0, 0.0, 0.0, 0.9, 0.6, 0.0]},
	},
]

@onready var _cam: Camera3D = $Camera
@onready var _screens: Node = $Screens

## 当前这一档的文件名前缀（_shoot_arena 用它拼名字）。
var _last_prefix := "ui_glowtrial"


func _ready() -> void:
	# 界面弹窗会把树暂停（OverlayScreen 的分层策略），拍摄脚本必须不受暂停影响。
	process_mode = Node.PROCESS_MODE_ALWAYS
	_report_theme()
	_report_icons()
	_report_nameplates()
	_run()


func _frames(n: int) -> void:
	for _i in n:
		await get_tree().process_frame


func _shot(file: String) -> void:
	get_tree().paused = false
	await RenderingServer.frame_post_draw
	var img := get_viewport().get_texture().get_image()
	var err := img.save_png(SHOT_DIR + file)
	print("[UiShot] ", SHOT_DIR + file, "  ", img.get_width(), "x", img.get_height(), " err=", err)


## 交互物"场景里不显示文字"的自检（用户需求 4）：
## 实例化真正的 `RewardNode.tscn`，数它子树里有多少 `Label3D`（期望 0 个）。
func _report_nameplates() -> void:
	var ps := load("res://Scenes/Interactables/RewardNode.tscn") as PackedScene
	if ps == null:
		print("[UiShot] 名牌自检：找不到 RewardNode.tscn")
		return
	var node := ps.instantiate()
	add_child(node)
	var labels := _collect_label3d(node)
	var visible_labels := 0
	for l in labels:
		if (l as Label3D).visible:
			visible_labels += 1
	print("[UiShot] 交互物名牌：RewardNode 里的 Label3D 共 ", labels.size(), " 个（可见 ", visible_labels, " 个）—— 期望 0 / 0")
	node.queue_free()


func _collect_label3d(node: Node) -> Array:
	var found: Array = []
	for child in node.get_children():
		if child is Label3D:
			found.append(child)
		elif child is Node3D:
			found.append_array(_collect_label3d(child))
	return found


## 图标表自检：逐个 load「Assets/UI/Icons/」下的 SVG（证明"图标代替文字"真的接上了图）。
func _report_icons() -> void:
	var missing: Array = []
	for icon in ICONS:
		if ResourceLoader.load("res://Assets/UI/Icons/%s.svg" % icon) == null:
			missing.append(icon)
	print("[UiShot] 图标：", ICONS.size() - missing.size(), "/", ICONS.size(), " 张可加载",
		"" if missing.is_empty() else "  缺失=%s" % str(missing))


## 把主题 / 字体结论打进日志（截图之外的第二份证据）。
func _report_theme() -> void:
	var th := ResourceLoader.load("res://Assets/UI/Themes/ui_theme.tres", "Theme", ResourceLoader.CACHE_MODE_IGNORE) as Theme
	print("[UiShot] theme loaded = ", th != null)
	if th == null:
		return
	var f := th.default_font
	print("[UiShot] default_font = ", f.get_font_name() if f != null else "<null>", "  size = ", th.default_font_size)
	if f != null:
		print("[UiShot]   glyph 中=", f.has_char("中".unicode_at(0)), " 文=", f.has_char("文".unicode_at(0)),
			" ◆=", f.has_char("◆".unicode_at(0)), " A=", f.has_char(65), " 7=", f.has_char(55))
	print("[UiShot] Button/normal texture = ",
		(th.get_stylebox("normal", "Button") as StyleBoxTexture).texture if th.get_stylebox("normal", "Button") is StyleBoxTexture else "n/a")
	print("[UiShot] PanelContainer/panel texture = ",
		(th.get_stylebox("panel", "PanelContainer") as StyleBoxTexture).texture if th.get_stylebox("panel", "PanelContainer") is StyleBoxTexture else "n/a")


## 打开一个界面：**不调 Open()**（那要真实数据），只把它的根 Control 点亮 + 填样本文案。
## 这样拍的是「外观」，一行界面逻辑都没走。
func _setup_screen(screen: Node, name: String) -> void:
	var root := screen.get_node_or_null("Root") as Control
	if root == null:
		return
	root.visible = true
	root.modulate = Color(1, 1, 1, 1)
	root.scale = Vector2.ONE

	# 卡片是运行时按数据生成的：这里手动复制模板按钮，只为看「按钮长什么样」。
	var texts: Array = CARD_TEXTS.get(name, [])
	var tpl := screen.get_node_or_null("Root/Panel/VBox/Cards/CardTemplate") as Button
	if tpl != null and not texts.is_empty():
		var card_icons: Array = CARD_ICONS.get(name, [])
		var i := 0
		for t in texts:
			var b := tpl.duplicate() as Button
			b.visible = true
			b.text = String(t)
			if not card_icons.is_empty():
				b.icon = load("res://Assets/UI/Icons/%s.svg" % card_icons[i % card_icons.size()])
			tpl.get_parent().add_child(b)
			i += 1

	if name == "BossPreviewScreen":
		var body := screen.get_node_or_null("Root/Panel/VBox/Body") as Label
		if body != null:
			body.text = "免费挑战：剩 1 次（本次不计压力）\n\n【数值强化 · 词条】\n  ◇ 狂暴 · 伤害   ×1.25\n  ◇ 迅捷 · 移速   ×1.15\n\n【技能】\n  ◆ 尖啸回响   冷却 ×0.8\n  ◆ 召唤急速   召唤间隔 ×0.8"

	# BaseScreen 的按钮文案由 Refresh() 在 Open() 时写；这里按同样格式补上，截图才看得出按钮上的字。
	if name == "BaseScreen":
		var sample := {
			"Root/Panel/VBox/Upgrades/Health": "最大生命 +10 ／ 等级 2 　（费用 30）",
			"Root/Panel/VBox/Upgrades/Damage": "伤害 +5% ／ 等级 3 　（费用 45）",
			"Root/Panel/VBox/Upgrades/Speed": "移速 +3% ／ 等级 1 　（费用 20）",
			"Root/Panel/VBox/Upgrades/Crit": "暴击率 +3% ／ 等级 0 　（费用 15）",
			"Root/Panel/VBox/Upgrades/Reload": "装弹速度 +6% ／ 等级 1 　（费用 25）",
			"Root/Panel/VBox/Start": "开始探索（进入配置）",
			"Root/Panel/VBox/Settings": "设置（画面 / 控制 / 音量）",
			"Root/Panel/VBox/Shards": "碎片：120",
		}
		for path in sample:
			var lbl := screen.get_node_or_null(path)
			if lbl is Label:
				(lbl as Label).text = String(sample[path])
			elif lbl is Button:
				(lbl as Button).text = String(sample[path])

	# 设置菜单：分类与内容都在 _Ready / ShowSection 里生成（分类来自注册表）。
	if name == "SettingsScreen":
		screen.call("ShowSection", 0)
		print("[UiShot] 设置分类数=", screen.get("SectionCount"), " ids=", screen.get("SectionIds"),
			" 当前页=", screen.get("ActiveSection"))

	# 角色持有面板：用真实奖励目录填示例格（不依赖本局状态）。
	if name == "BuildScreen":
		screen.call("DebugShowSample", 12)

	# 按键提示栏：**每个界面都塞一组示例**（结构是基类建的，界面只负责填内容）。
	# 截图里每个界面底部都能看到那一栏 —— 这就是"多数面板都需要按键提示"的样子。
	if screen.has_method("DebugSetSampleKeyHints"):
		screen.call("DebugSetSampleKeyHints")

	_report_key_hints(screen, name)


## 按键提示栏的自检（"结构必须存在 / 层级最高 / 背景透明不吃鼠标"三件事的机器可判定形式）。
func _report_key_hints(screen: Node, name: String) -> void:
	var bar := screen.get_node_or_null("Root/KeyHintBar")
	print("[UiShot] 按键提示栏 ", name, "：节点=", bar != null,
		" 在树里=", screen.get("HasKeyHintBar"),
		" 最后子节点=", screen.get("KeyHintBarTopmost"),
		" ZIndex=", screen.get("KeyHintBarZIndex"),
		" 鼠标穿透=", screen.get("KeyHintBarIgnoresMouse"),
		" 条目=", screen.get("KeyHintCount"),
		" 可见=", screen.get("KeyHintsVisible"))


func _run() -> void:
	var only := OS.get_cmdline_user_args()
	var do_ui := only.is_empty() or only.has("ui")
	var do_glow := only.is_empty() or only.has("glow")
	var do_final := only.has("final")
	var do_hud := only.has("hud")

	await _frames(30)

	# ---------- 0. HUD 实机（TestLab = TestArena + Player + Hud） ----------
	if do_hud:
		await _shoot_hud()

	# ---------- 1. 七个界面 ----------
	if do_ui:
		_cam.position = Vector3(0, 2.2, 18)
		_cam.look_at(Vector3(0, 1.2, 0), Vector3.UP)
		for entry in SCREENS:
			var name := String(entry["name"])
			var ps := load("res://Scenes/UI/%s.tscn" % name) as PackedScene
			if ps == null:
				print("[UiShot] MISSING scene ", name)
				continue
			var screen := ps.instantiate()
			_screens.add_child(screen)
			_setup_screen(screen, name)
			await _frames(8)
			await _shot(String(entry["file"]))

			# 设置菜单有 3 页：把「控制 / 音量」也各拍一张（分类可扩展 + 4 条总线都要有证据）。
			if name == "SettingsScreen":
				for page in [{"index": 1, "file": "ui_12_settings_controls.png"}, {"index": 2, "file": "ui_13_settings_audio.png"}]:
					screen.call("ShowSection", page["index"])
					await _frames(6)
					await _shot(String(page["file"]))

			screen.queue_free()
			get_tree().paused = false
			await _frames(4)

	# ---------- 2. glow 试拍：同一次运行、同一机位、同一帧数 ----------
	if do_glow:
		var world_env := get_node_or_null(ARENA_ENV) as WorldEnvironment
		var env: Environment = world_env.environment if world_env != null else null
		print("[UiShot] arena env = ", env, "  glow_enabled = ", env.glow_enabled if env != null else "n/a")
		if env == null:
			print("[UiShot] NO ENVIRONMENT - glow shots skipped")
		else:
			for trial in GLOW_TRIALS:
				var tag := String(trial["tag"])
				_last_prefix = "ui_glowtrial_%s" % tag
				_apply_glow(env, trial["cfg"])
				await _shoot_arena()
				await _shoot_explosion(_last_prefix)
				await _shoot_muzzle(_last_prefix + "_muzzle")
				await _frames(420)   # 等烟散干净，避免上一发的烟混进下一档

	# ---------- 3. 定稿对比：**用 TestArena.tscn 文件里那组参数**（不在运行时注入） ----------
	if do_final:
		var world_env := get_node_or_null(ARENA_ENV) as WorldEnvironment
		var env: Environment = world_env.environment if world_env != null else null
		if env != null:
			print("[UiShot] FINAL cfg from file: glow_enabled=", env.glow_enabled, " thr=", env.glow_hdr_threshold,
				" intensity=", env.glow_intensity, " bloom=", env.glow_bloom, " blend=", env.glow_blend_mode)
			await _shoot_arena("ui_glow_01_arena_on.png")
			await _shoot_muzzle("ui_glow_02_muzzle_on")
			await _shoot_explosion("ui_glow_03_explosion_on")
			await _frames(420)
			env.glow_enabled = false
			await _shoot_arena("ui_glow_04_arena_off.png")
			await _shoot_muzzle("ui_glow_05_muzzle_off")
			await _shoot_explosion("ui_glow_06_explosion_off")
			env.glow_enabled = true

	await _frames(4)
	get_tree().quit()


## HUD 实机截图：本体是 TestArena（已在场景里）+ **真的 Player.tscn + Hud.tscn**。
## 不用 TestLab 是因为它带 3 个训练假人，玩家会在几帧内被打死、血条永远是空的。
func _shoot_hud() -> void:
	var player_scene := load("res://Scenes/Player/Player.tscn") as PackedScene
	var hud_scene := load("res://Scenes/UI/Hud.tscn") as PackedScene
	if player_scene == null or hud_scene == null:
		print("[UiShot] MISSING Player/Hud")
		return
	var player := player_scene.instantiate()
	add_child(player)
	(player as Node3D).global_position = Vector3(0, 0.5, 6)
	var hud := hud_scene.instantiate()
	add_child(hud)
	# HUD 上的构筑图标条：用真实奖励目录塞几个示例（真实一局里它由 RunBuild 驱动）。
	if hud.has_method("DebugSetSampleBuildIcons"):
		hud.call("DebugSetSampleBuildIcons", 6)
	await _frames(45)
	await _shot("ui_08_hud_ingame.png")
	await _frames(30)
	await _shot("ui_09_hud_crosshair.png")
	player.queue_free()
	await _frames(10)


## 空场全景（证明「更亮」不是「整块地板过曝」）。
func _shoot_arena(file_name := "") -> void:
	_cam.position = CAM_WIDE
	_cam.look_at(Vector3(0, 2, -5), Vector3.UP)
	await _frames(6)
	await _shot(file_name if not file_name.is_empty() else _last_prefix + "_arena.png")


## 爆炸：同一机位、同一帧数，只换 glow 参数。只留 Binbun 粒子（判定球藏掉），
## 这样对比的是**美术特效本身**，不会被那层半透明判定球的高光盖住。
func _shoot_explosion(prefix: String) -> void:
	_last_prefix = prefix
	_cam.position = CAM_FX
	_cam.look_at(EXPLODE_AT, Vector3.UP)
	await _frames(4)
	var ex := _detonate(2.0, 1.5, EXPLODE_AT, false)
	await _frames(10)
	await _shot("%s_t0.png" % prefix)
	await _frames(30)
	await _shot("%s_t1.png" % prefix)
	if ex != null:
		ex.queue_free()


## 枪口火光：Binbun 的一次性 0.2s 闪光，近距离拍，看 glow 是否只提亮了火光本身。
func _shoot_muzzle(prefix: String) -> void:
	_last_prefix = prefix
	_cam.position = Vector3(0.0, 1.3, 3.2)
	_cam.look_at(Vector3(0, 1.25, 0), Vector3.UP)
	await _frames(4)
	var ps := load("res://Assets/Effects/fx_weapon_muzzle.tscn") as PackedScene
	if ps == null:
		print("[UiShot] MISSING muzzle fx")
		return
	var fx := ps.instantiate()
	get_tree().current_scene.add_child(fx)
	(fx as Node3D).global_position = Vector3(0, 1.25, 0)
	fx.call("play")
	await _frames(3)
	await _shot("%s_t0.png" % prefix)
	await _frames(4)
	await _shot("%s_t1.png" % prefix)
	fx.queue_free()


## 把一档 glow 参数写进环境（levels 覆盖 7 个频段）。cfg = {"off": true} 时不发光。
func _apply_glow(env: Environment, cfg: Dictionary) -> void:
	if cfg.has("off"):
		env.glow_enabled = false
		print("[UiShot] glow cfg = OFF (基线)")
		return
	env.glow_enabled = true
	env.glow_normalized = true
	env.glow_intensity = cfg["glow_intensity"]
	env.glow_strength = cfg["glow_strength"]
	env.glow_bloom = cfg["glow_bloom"]
	env.glow_blend_mode = cfg["glow_blend_mode"]
	env.glow_hdr_threshold = cfg["glow_hdr_threshold"]
	env.glow_hdr_scale = 2.0
	env.glow_hdr_luminance_cap = 12.0
	var levels: Array = cfg["levels"]
	for i in 7:
		env.set("glow_levels/%d" % (i + 1), 0.0 if i >= levels.size() else float(levels[i]))
	print("[UiShot] glow cfg = ", cfg)


func _detonate(radius: float, fx_scale: float, at: Vector3, show_sphere: bool) -> Node:
	var ps := load("res://Scenes/Combat/Explosion.tscn") as PackedScene
	if ps == null:
		return null
	var ex := ps.instantiate()
	get_tree().current_scene.add_child(ex)
	(ex as Node3D).global_position = at
	var fx := ex.get_node_or_null("FX") as Node3D
	if fx != null:
		fx.scale = Vector3.ONE * fx_scale
	var mesh := ex.get_node_or_null("Mesh") as MeshInstance3D
	if mesh != null:
		mesh.visible = show_sphere
	ex.call("Detonate", null, 0.0, radius, PackedStringArray())
	return ex

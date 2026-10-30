extends SceneTree

var base = "F:/Godot项目/项目a/outputs/visual-047-048-20261028/"
var canvas: Control
var font: Font
var cache = {}
var titles = ["A · 叶脉灼印", "B · 折纸符契", "C · 涂抹刀锋"]
var letters = ["a", "b", "c"]
var phases = ["出现 · 0%", "蓄势／锁定 · 75%", "触发 · 100%", "消退 · 结束后"]
var rows = ["自爆／陨石圆形预警", "冲锋路径", "重击／落地 AOE", "玩家瞄准预览"]

func _initialize():
	var source_font = FontFile.new()
	source_font.load_dynamic_font("F:/Godot项目/项目a/Assets/Fonts/NotoSansSC/NotoSansSC.ttf")
	font = FontVariation.new()
	font.base_font = source_font
	font.variation_embolden = 0.65
	call_deferred("run")

func texture(path: String):
	if not cache.has(path):
		cache[path] = ImageTexture.create_from_image(Image.load_from_file(base + path))
	return cache[path]

func box(rect: Rect2, color: Color):
	var node = ColorRect.new()
	node.position = rect.position
	node.size = rect.size
	node.color = color
	canvas.add_child(node)
	return node

func label(text: String, at: Vector2, width: float, font_size: int = 24, color = Color("213f39")):
	var node = Label.new()
	node.text = text
	node.position = at
	node.size.x = width
	node.add_theme_font_override("font", font)
	node.add_theme_font_size_override("font_size", font_size)
	node.add_theme_color_override("font_color", color)
	node.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	canvas.add_child(node)
	return node

func picture(path: String, rect: Rect2):
	var node = TextureRect.new()
	node.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	node.texture = texture(path)
	node.position = rect.position
	node.size = rect.size
	node.stretch_mode = TextureRect.STRETCH_SCALE
	canvas.add_child(node)
	return node

func sprite(direction: int, row: int, phase: int, rect: Rect2, alpha: float = 0.9):
	var atlas = AtlasTexture.new()
	atlas.atlas = texture("boards/vfx-" + letters[direction] + "-atlas.png")
	var size = atlas.atlas.get_size() / 4.0
	atlas.region = Rect2(Vector2(phase, row) * size, size)
	var node = TextureRect.new()
	node.texture = atlas
	node.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	node.position = rect.position
	node.size = rect.size
	node.modulate.a = alpha
	canvas.add_child(node)

func stage(direction: int, rect: Rect2, dark: bool = false, phase: int = 1):
	picture("evidence/07-survival-current.png" if dark else "evidence/09-adventure-combat-staged-current.png", rect)
	var s = rect.size / Vector2(1920,1080)
	# 固定屏幕位置与尺度；三版使用同一图集格序和同一底图。
	sprite(direction, 0, phase, Rect2(rect.position + Vector2(655,570)*s, Vector2(455,340)*s), .8)
	sprite(direction, 1, phase, Rect2(rect.position + Vector2(1020,490)*s, Vector2(370,420)*s), .75)
	sprite(direction, 3, 0, Rect2(rect.position + Vector2(675,520)*s, Vector2(475,355)*s), .85)
	# 第二个圆形警告用于交叠观察，不改变底图。
	sprite(direction, 2, 0, Rect2(rect.position + Vector2(835,585)*s, Vector2(400,300)*s), .65)

func reset():
	if canvas != null:
		canvas.queue_free()
		await process_frame
	canvas = Control.new()
	root.add_child(canvas)
	box(Rect2(0,0,2048,1536), Color("f2f0e5"))

func save(name: String):
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(base + "boards/" + name + ".png")
	print("BOARD ", name)

func run():
	await reset()
	label("047 · 同背景视觉方向比较", Vector2(40,18), 1700, 42)
	label("概念合成｜相同工程实拍、镜头、落点与叠放尺度｜用于选方向，不是运行效果验收", Vector2(42,78), 1950, 23)
	for direction in range(3):
		var x = 36 + direction * 674
		label(titles[direction],Vector2(x,130),640,32)
		stage(direction,Rect2(x,183,640,360))
		label(["主题结合最紧；蓄势叶脉偏密，须减光。", "形状识别强；折面易被误读为地上实体。", "负空间最多；建议以此承担常驻危险提示。"][direction],Vector2(x,556),630,24)
		label("圆形关键帧：出现 → 锁定 → 触发 → 消退",Vector2(x,641),640,21)
		for phase in range(4):
			box(Rect2(x + phase*159,684,155,145),Color("cbd9d0"))
			sprite(direction,0,phase,Rect2(x+phase*159,692,155,116))
		label("暗色实拍底图 · 同一组重叠警告",Vector2(x,875),640,25)
		stage(direction,Rect2(x,925,640,360),true)
	label("设计取舍：硬边界固定；内部动势表达时间；主体色与端点形区分敌我；终止后立即撤掉闭合危险线。",Vector2(40,1340),1950,27)
	label("本板是透明概念图集在实拍上的原生画布排版。未模拟地形深度、模型遮挡分类或真实伤害时序。",Vector2(40,1405),1950,23,Color("646a62"))
	await save("047-comparison")
	for direction in range(3):
		await reset()
		label("047 · " + titles[direction] + " · 四类技能关键帧",Vector2(40,18),1900,39)
		label("概念稿｜从左到右：出现、蓄势／锁定、触发、消退。每行相同底图及大小；不是可直接导入的最终序列帧。",Vector2(40,80),1950,21)
		for phase in range(4):
			label(phases[phase],Vector2(280+phase*437,135),420,26)
		for row in range(4):
			var y = 195 + row*305
			label(rows[row],Vector2(34,y+90),205,24)
			for phase in range(4):
				var x = 264+phase*437
				picture("evidence/09-adventure-combat-staged-current.png",Rect2(x,y,420,236))
				sprite(direction,row,phase,Rect2(x+90,y+40,220,165),.85)
		label("范围轮廓＝几何真值；笔触＝装饰。图集中触发高度、消退残圈和玩家弧线粗度仍需按报告收敛。",Vector2(40,1450),1950,23)
		await save("047-direction-"+letters[direction])
	quit()

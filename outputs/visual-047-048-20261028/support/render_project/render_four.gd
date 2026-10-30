extends "res://render.gd"

func sprite(direction: int, row: int, phase: int, rect: Rect2, alpha: float = .9):
	if direction < 3:
		super.sprite(direction,row,phase,rect,alpha)
		return
	var node = load("res://minimal_indicator.gd").new()
	node.row = row
	node.phase = phase
	node.position = rect.position
	node.size = rect.size
	canvas.add_child(node)

func run():
	letters.append("d")
	titles.append("D · 极简轮廓")
	await reset()
	label("047 · D 极简轮廓 · 四类技能关键帧",Vector2(40,18),1900,39)
	label("新增视觉方案｜去填充、去纹样、去粒子、去辉光；机制／伤害范围／时机不变。原生线条规范稿。",Vector2(40,80),1950,23)
	for phase in range(4):
		label(phases[phase],Vector2(280+phase*437,135),420,26)
	for row in range(4):
		var y = 195 + row*305
		label(rows[row],Vector2(34,y+90),205,24)
		for phase in range(4):
			var x = 264+phase*437
			picture("evidence/09-adventure-combat-staged-current.png",Rect2(x,y,420,236))
			sprite(3,row,phase,Rect2(x+90,y+40,220,165))
	label("连续边＝敌方范围；开放端点＝玩家预览；时间短楔只表达临近触发；结束后不留闭合危险线。",Vector2(40,1450),1950,23)
	await save("047-direction-d")
	root.size = Vector2i(2560,1536)
	await process_frame
	await reset()
	box(Rect2(0,0,2560,1536),Color("f2f0e5"))
	label("047 · 四版同背景比较 · 新增极简轮廓",Vector2(40,18),2450,42)
	label("概念合成｜底图、视角、位置和尺度完全一致；D使用原生线条，去掉生成参考中的多余辉光。",Vector2(40,80),2450,24)
	for direction in range(4):
		var x = 32 + direction*632
		label(titles[direction],Vector2(x,137),600,32)
		stage(direction,Rect2(x,190,600,337.5))
		label(["叶脉主题鲜明；装饰和亮度较多。","折面轮廓鲜明；空间误读风险。","疏松笔触；艺术表现与可读性折中。","极少信息；内部全透明，轮廓最直接。"][direction],Vector2(x,550),590,24)
		label("出现 → 锁定 → 触发 → 消退",Vector2(x,651),590,24)
		for phase in range(4):
			box(Rect2(x+phase*150,700,146,138),Color("cbd9d0"))
			sprite(direction,0,phase,Rect2(x+phase*150,705,146,110))
		label("同一暗色底图 · 多警告重叠",Vector2(x,883),590,24)
		stage(direction,Rect2(x,936,600,337.5),true)
	label("D版取舍：降低主题装饰和爆发感，优先让玩家看到人物脚下、危险边界与冲锋方向。",Vector2(40,1340),2450,29)
	label("四版均为方案稿；图中未模拟真实深度／地形贴合。最终边界由原技能几何驱动，不能由贴图决定。",Vector2(40,1415),2450,24,Color("646a62"))
	await save("047-comparison-four")
	quit()

extends Control

var row = 0
var phase = 1

func stroke(points: PackedVector2Array, core: Color, faded: bool = false):
	var dark = Color("4d2031") if row < 3 else Color("153650")
	if faded:
		dark.a = 0.25
		core.a = 0.25
	draw_polyline(points, dark, 4.0, true)
	draw_polyline(points, core, 2.4, true)

func point(x: float, y: float):
	return Vector2(x,y) * size

func ellipse(center: Vector2, radius: Vector2, start: float, end: float):
	var points = PackedVector2Array()
	for i in range(65):
		var angle = lerp(start,end,float(i)/64.0)
		points.append(center + Vector2(cos(angle),sin(angle))*radius)
	return points

func _draw():
	# 极简方案直接使用原生连续线条，不将生图辉光或轮廓漂移带进规范。
	var core = Color("ff715c") if row < 3 else Color("63e0ec")
	if phase == 2:
		core = Color("fff3db") if row < 3 else Color("d9ffff")
	if row == 0 or row == 2:
		var center = point(.5,.64)
		var radius = point(.43,.18)
		if phase == 3:
			stroke(ellipse(center,radius,.08*PI,.34*PI),core,true)
			stroke(ellipse(center,radius,1.08*PI,1.34*PI),core,true)
			return
		stroke(ellipse(center,radius,0,TAU),core)
		if row == 0:
			var h = .065 if phase == 0 else .025
			stroke(PackedVector2Array([point(.477,.47),point(.5,.47+h),point(.523,.47)]),core)
		else:
			stroke(PackedVector2Array([point(.475,.64),point(.525,.64)]),core)
			stroke(PackedVector2Array([point(.5,.60),point(.5,.68)]),core)
	elif row == 1:
		var corners = [point(.16,.86),point(.35,.22),point(.65,.22),point(.84,.86)]
		if phase == 3:
			stroke(PackedVector2Array([corners[0],point(.20,.72)]),core,true)
			stroke(PackedVector2Array([point(.8,.72),corners[3]]),core,true)
			return
		stroke(PackedVector2Array([corners[0],corners[1],corners[2],corners[3],corners[0]]),core)
		stroke(PackedVector2Array([point(.45,.60),point(.5,.50),point(.55,.60)]),core)
	else:
		var points = PackedVector2Array()
		for i in range(49):
			var t = float(i)/48.0
			points.append(point(.12+.73*t,.76-2.0*t*(1.0-t)))
		if phase != 3:
			stroke(points,core)
		else:
			stroke(PackedVector2Array(points.slice(0,7)),core,true)
			stroke(PackedVector2Array(points.slice(39,48)),core,true)
		stroke(ellipse(point(.85,.78),point(.095,.04),.20*PI,1.80*PI),core,phase==3)
		stroke(PackedVector2Array([point(.82,.70),point(.85,.76),point(.88,.70)]),core,phase==3)

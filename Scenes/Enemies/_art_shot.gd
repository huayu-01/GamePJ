extends Node3D
## 美术验证用：把展示场景渲染成 PNG（不依赖 gdmcp 运行时桥）。
## 用法：Godot.exe --path <项目> res://Scenes/Enemies/_ArtShowcase.tscn
## 输出：res://docs/screenshots/ 下的若干张图，然后退出。

const SHOTS := [
	{"pos": Vector3(0, 2.0, 11.0), "look": Vector3(0, 1.1, -2.0), "file": "res://docs/screenshots/art_20_enemies_all.png"},
	{"pos": Vector3(-6, 1.8, 6.0), "look": Vector3(-6, 1.1, -2.0), "file": "res://docs/screenshots/art_21_enemies_left.png"},
	{"pos": Vector3(6, 1.8, 6.0), "look": Vector3(5, 1.1, -2.0), "file": "res://docs/screenshots/art_22_enemies_right.png"},
	{"pos": Vector3(13, 3.6, 1.5), "look": Vector3(13, 1.6, -5.0), "file": "res://docs/screenshots/art_23_boss.png"},
]

const FX_ORIGIN := Vector3(-16.0, 1.2, 0.0)

@onready var _cam: Camera3D = $Camera

func _ready() -> void:
	_run()

func _frames(n: int) -> void:
	for _i in n:
		await get_tree().process_frame

func _shot(path: String) -> void:
	await RenderingServer.frame_post_draw
	var img := get_viewport().get_texture().get_image()
	var err := img.save_png(path)
	print("[ArtShot] ", path, " err=", err)

func _detonate(radius: float, fx_scale: float = 1.0, at: Vector3 = FX_ORIGIN, show_sphere: bool = true) -> Node:
	var ps := load("res://Scenes/Combat/Explosion.tscn")
	var ex: Node = (ps as PackedScene).instantiate()
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

func _spawn_hit(pos: Vector3) -> Node:
	var ps := load("res://Assets/Effects/fx_combat_hit.tscn")
	var fx: Node = (ps as PackedScene).instantiate()
	get_tree().current_scene.add_child(fx)
	(fx as Node3D).global_position = pos
	return fx

func _run() -> void:
	await _frames(40)
	for s in SHOTS:
		_cam.position = s["pos"]
		_cam.look_at(s["look"], Vector3.UP)
		await _frames(4)
		await _shot(String(s["file"]))

	# ---- 爆炸特效（Binbun Explosion FX）：左=判定球+粒子，右=只留粒子 ----
	_cam.position = Vector3(-16, 2.6, 9.0)
	_cam.look_at(Vector3(-16, 1.2, 0.0), Vector3.UP)
	_detonate(2.0, 1.5, Vector3(-22, 1.2, 0), true)
	_detonate(2.0, 1.5, Vector3(-10, 1.2, 0), false)
	await _frames(14)
	await _shot("res://docs/screenshots/art_30_explosion_t0.png")
	await _frames(30)
	await _shot("res://docs/screenshots/art_31_explosion_t1.png")
	await _frames(60)
	await _shot("res://docs/screenshots/art_32_explosion_t2.png")

	# ---- 命中特效（Binbun Hit FX）----
	var hit_pos := FX_ORIGIN + Vector3(0, 0, -6)
	_cam.position = hit_pos + Vector3(0, 1.0, 4.6)
	_cam.look_at(hit_pos, Vector3.UP)
	_spawn_hit(hit_pos)
	await _frames(10)
	await _shot("res://docs/screenshots/art_33_hit_t0.png")
	await _frames(14)
	await _shot("res://docs/screenshots/art_34_hit_t1.png")

	# ---- 爆炸范围 vs 判定球（radius=1 基准，用 2.0 引爆，视觉应与 2m 半径相称）----
	get_tree().quit()

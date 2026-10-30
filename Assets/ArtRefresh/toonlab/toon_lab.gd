extends Node3D

## 卡通方案对比实验室（纯表现，不改玩法、不改存档、不改生产场景）。
##
## 用真实的 TestArena（地面/石柱/树/环境/太阳）＋ 两个真实 GLB 模型，
## 依次切 5 种状态各拍一张，用来对比 L1-c（光照）/ L1-a（内置 toon）/ L1-a'（自定义 toon）/ L1-b（描边）。
##
## 全部开关只作用于本场景内的运行时实例；`.tres` 不会被写盘。

const OUT_DIR := "res://docs/screenshots"
const ARENA := "res://Scenes/Levels/TestArena.tscn"
const PLAYER_MODEL := "res://Assets/Models/player/mesh_player_body.glb"
const ENEMY_MODEL := "res://Assets/Models/enemies/mesh_enemy_chaser.glb"
const TOON_SHADER := "res://Assets/ArtRefresh/toonlab/toonlab_toon.gdshader"
const OUTLINE_SHADER := "res://Assets/ArtRefresh/toonlab/toonlab_outline.gdshader"
const UNIT_OUTLINE_SHADER := "res://Assets/ArtRefresh/toonlab/toonlab_unit_outline.gdshader"

const PLAYER_SCALE := 2.6814
const ENEMY_SCALE := 2.72191

var _camera: Camera3D
var _sun: DirectionalLight3D
var _env: Environment
var _outline: MeshInstance3D
var _units: Array[Node3D] = []
var _hull: ShaderMaterial
var _log: Array[String] = []


func _ready() -> void:
	await get_tree().process_frame
	_build()
	await get_tree().create_timer(0.5).timeout

	_log.append("材质统计（按面）：%s" % _count_kinds())
	await _shot("toonlab_0_baseline")
	_apply_lighting()
	await _settle()
	await _shot("toonlab_1_lighting")
	_apply_toon_builtin()
	await _settle()
	await _shot("toonlab_2_builtin_toon")
	_apply_toon_shader()
	await _settle()
	await _shot("toonlab_3_toon_shader")
	_add_outline()
	await _settle()
	await _shot("toonlab_4_outline_fullscreen")

	# --- 用户裁定的方向：内置 Toon + 不要高光 + 描边只给单位 ---
	_remove_fullscreen_outline()
	_apply_toon_builtin_nospec()
	await _settle()
	await _shot("toonlab_5_unit_no_outline")

	for w in [0.02, 0.035]:
		_apply_unit_outline(w)
		await _settle()
		await _shot("toonlab_6_unit_outline_n%03d" % int(w * 1000.0))

	for s in [1.03, 1.05]:
		_apply_unit_outline_scale(s)
		await _settle()
		await _shot("toonlab_7_unit_outline_s%03d" % int((s - 1.0) * 1000.0))

	for line in _log:
		print("[toonlab] ", line)
	print("[toonlab] done")
	get_tree().quit(0)


func _build() -> void:
	# --- 真实关卡 ---
	var arena: Node = load(ARENA).instantiate()
	add_child(arena)
	for n in _walk(arena):
		if n is WorldEnvironment:
			_env = (n as WorldEnvironment).environment
		elif n is DirectionalLight3D:
			_sun = n
	_log.append("arena ok, env=%s sun=%s" % [_env != null, _sun != null])

	# --- 两个真实模型（yaw=0 即面朝相机：GLB 原生朝 +Z） ---
	_spawn(PLAYER_MODEL, Vector3(1.0, 0.0, 1.0), PLAYER_SCALE, 0.0)
	_spawn(ENEMY_MODEL, Vector3(3.1, 0.0, -0.1), ENEMY_SCALE, 335.0)

	# --- 相机 ---
	_camera = Camera3D.new()
	_camera.fov = 50.0
	_camera.near = 0.05
	_camera.far = 300.0
	add_child(_camera)
	_camera.global_position = Vector3(1.9, 1.35, 6.2)
	_camera.look_at(Vector3(2.0, 0.90, 0.3), Vector3.UP)
	_camera.current = true
	_log.append("camera at %s" % _camera.global_position)


func _spawn(path: String, pos: Vector3, s: float, yaw_deg: float) -> Node3D:
	var node: Node3D = load(path).instantiate()
	add_child(node)
	node.transform = Transform3D(
		Basis(Vector3.UP, deg_to_rad(yaw_deg)).scaled(Vector3(s, s, s)),
		pos)
	_play_idle(node)
	_units.append(node)
	return node


func _play_idle(root: Node) -> void:
	for n in _walk(root):
		if n is AnimationPlayer:
			var ap: AnimationPlayer = n
			var names: PackedStringArray = ap.get_animation_list()
			for want in ["idle", "Idle", "IDLE", "idle_loop"]:
				if names.has(want):
					ap.play(want)
					return
			for name in names:
				if name != "RESET":
					ap.play(name)
					return


func _walk(root: Node) -> Array[Node]:
	var out: Array[Node] = [root]
	for child in root.get_children():
		out.append_array(_walk(child))
	return out


func _walk_meshes(root: Node) -> Array[MeshInstance3D]:
	var out: Array[MeshInstance3D] = []
	for n in _walk(root):
		if n is MeshInstance3D:
			out.append(n)
	return out


func _all_meshes() -> Array[MeshInstance3D]:
	return _walk_meshes(self)


func _settle() -> void:
	for _i in 3:
		await get_tree().process_frame


func _shot(name: String) -> void:
	await RenderingServer.frame_post_draw
	var img: Image = get_viewport().get_texture().get_image()
	var path := "%s/%s.png" % [OUT_DIR, name]
	var err := img.save_png(path)
	_log.append("shot %s -> %s (%dx%d)" % [name, error_string(err), img.get_width(), img.get_height()])


# ---------------------------------------------------------------- L1-c 光照
func _apply_lighting() -> void:
	if _env != null:
		_env.ssao_enabled = false
		_env.ambient_light_energy = 0.30
		_env.glow_intensity = 0.18
		_env.glow_blend_mode = Environment.GLOW_BLEND_MODE_ADDITIVE
		_env.fog_density = 0.001
	if _sun != null:
		_sun.shadow_blur = 0.0
		_sun.light_angular_distance = 0.0
	_log.append("L1-c applied: ssao=off ambient=0.30 glow=0.18 fog=0.001 shadow_blur=0 angular=0")


# ------------------------------------------------------- L1-a 内置 Toon 模式
func _apply_toon_builtin() -> void:
	var count := 0
	for mi in _all_meshes():
		if mi.mesh == null:
			continue
		for i in mi.mesh.get_surface_count():
			var src := _source_material(mi, i)
			if src is StandardMaterial3D:
				var sm: StandardMaterial3D = (src as StandardMaterial3D).duplicate()
				sm.diffuse_mode = BaseMaterial3D.DIFFUSE_TOON
				sm.specular_mode = BaseMaterial3D.SPECULAR_TOON
				sm.roughness = 0.0
				mi.set_surface_override_material(i, sm)
				count += 1
	_log.append("L1-a builtin toon: %d surfaces -> diffuse=TOON specular=TOON roughness=0" % count)


# --------------------------------------------------- L1-a' 自定义 toon shader
func _apply_toon_shader() -> void:
	var shader: Shader = load(TOON_SHADER)
	var count := 0
	for mi in _all_meshes():
		if mi.mesh == null:
			continue
		for i in mi.mesh.get_surface_count():
			var src := _source_material(mi, i)
			if src is not StandardMaterial3D:
				continue
			var std: StandardMaterial3D = src
			var sm := ShaderMaterial.new()
			sm.shader = shader
			sm.set_shader_parameter("albedo_color", std.albedo_color)
			sm.set_shader_parameter("use_texture", std.albedo_texture != null)
			sm.set_shader_parameter("albedo_texture", std.albedo_texture)
			sm.set_shader_parameter("cuts", 3)
			sm.set_shader_parameter("wrap", 0.15)
			sm.set_shader_parameter("steepness", 1.6)
			sm.set_shader_parameter("use_specular", true)
			sm.set_shader_parameter("specular_shininess", 40.0)
			sm.set_shader_parameter("use_rim", true)
			sm.set_shader_parameter("rim_color", Color(1.0, 1.0, 1.0, 0.30))
			mi.set_surface_override_material(i, sm)
			count += 1
	_log.append("L1-a' custom toon shader: %d surfaces (cuts=3 wrap=0.15 rim on)" % count)


# ------------------------------ 用户裁定方向：内置 Toon + 不要高光 + 只单位描边
func _apply_toon_builtin_nospec() -> void:
	var count := 0
	for mi in _all_meshes():
		if mi.mesh == null:
			continue
		for i in mi.mesh.get_surface_count():
			var src := _source_material(mi, i)
			if src is not StandardMaterial3D:
				continue
			var sm: StandardMaterial3D = (src as StandardMaterial3D).duplicate()
			sm.diffuse_mode = BaseMaterial3D.DIFFUSE_TOON
			# 用户裁定：高光不要 → 关掉高光通道（roughness 仍按裁定保持 0）
			sm.specular_mode = BaseMaterial3D.SPECULAR_DISABLED
			sm.roughness = 0.0
			mi.set_surface_override_material(i, sm)
			count += 1
	_log.append("裁定版：%d 面 = diffuse TOON + specular DISABLED + roughness 0（无高光）" % count)


func _remove_fullscreen_outline() -> void:
	if _outline != null and is_instance_valid(_outline):
		_outline.queue_free()
		_outline = null
	_log.append("全屏描边已移除（按裁定：描边只给单位）")


func _apply_unit_outline(width: float) -> void:
	if _hull == null:
		_hull = ShaderMaterial.new()
		_hull.shader = load(UNIT_OUTLINE_SHADER)
	_hull.set_shader_parameter("width", width)

	var count := 0
	for unit in _units:
		for mi in _walk_meshes(unit):
			if mi.mesh == null:
				continue
			for i in mi.mesh.get_surface_count():
				var m: Material = mi.get_surface_override_material(i)
				if m is StandardMaterial3D and m.next_pass == null:
					m.next_pass = _hull
					count += 1
	_log.append("单位反向壳描边：width=%.3f m，挂到 %d 个面" % [width, count])


func _apply_unit_outline_scale(factor: float) -> void:
	if _hull == null:
		_hull = ShaderMaterial.new()
		_hull.shader = load(UNIT_OUTLINE_SHADER)
	_hull.set_shader_parameter("mode", 1)
	_hull.set_shader_parameter("scale_factor", factor)

	var count := 0
	for unit in _units:
		for mi in _walk_meshes(unit):
			if mi.mesh == null:
				continue
			for i in mi.mesh.get_surface_count():
				var m: Material = mi.get_surface_override_material(i)
				if m is StandardMaterial3D and m.next_pass == null:
					m.next_pass = _hull
					count += 1
	_log.append("单位缩放壳描边：scale=%.3f，新挂 %d 个面" % [factor, count])


func _source_material(mi: MeshInstance3D, i: int) -> Material:
	# 优先取网格自带材质；很多预制体是共享的 material_override —— 也要接住
	var mesh: Mesh = mi.mesh
	if mesh != null:
		var m: Material = mesh.surface_get_material(i)
		if m != null:
			return m
	if mi.material_override != null:
		return mi.material_override
	return mi.get_surface_override_material(i)


func _count_kinds() -> String:
	var std := 0
	var other := 0
	for mi in _all_meshes():
		if mi.mesh == null:
			continue
		for i in mi.mesh.get_surface_count():
			if _source_material(mi, i) is StandardMaterial3D:
				std += 1
			else:
				other += 1
	return "Standard=%d 非Standard=%d" % [std, other]


# ------------------------------------------------------------- L1-b 描边
func _add_outline() -> void:
	var mat := ShaderMaterial.new()
	mat.shader = load(OUTLINE_SHADER)
	mat.set_shader_parameter("shadow_strength", 0.45)
	mat.set_shader_parameter("highlight_strength", 0.12)
	mat.render_priority = 127

	var quad := QuadMesh.new()
	quad.size = Vector2(4.0, 4.0)
	_outline = MeshInstance3D.new()
	_outline.mesh = quad
	_outline.material_override = mat
	_outline.extra_cull_margin = 16384.0
	_outline.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_camera.add_child(_outline)
	_outline.position = Vector3(0.0, 0.0, -1.0)
	_log.append("L1-b outline quad added (fullscreen post-process mode)")

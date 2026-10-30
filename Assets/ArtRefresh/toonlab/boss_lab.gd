extends Node3D

## 首领模型 × 卡通渲染 对比实验室（纯表现，不碰生产）。
##
## 6 个首领 FBX 来自 `F:\Godot项目\_boss_asset_audit\models\`（用户素材包 3d肉鸽雨中哈迪斯\素材\模型\首领），
## 已复制到 `Assets/Models/bosses/`。缩放/包围盒**直接取审计结果** `_boss_asset_audit/audit-results.json`
## （`scale_for_2_8m_height` / `bounds_min` / `bounds_size`），因为蒙皮模型的运行时 AABB 不可靠
## （第一次跑用 AABB 自动缩放，DRIZZEL 被算成 0.01m → 放大 315 倍，已废弃）。
##
## 机位用**摄影棚**而不是 TestArena：首领排一排要看材质，不要和关卡景深/阴影打架。

const OUT_DIR := "res://docs/screenshots"
const BOSS_DIR := "res://Assets/Models/bosses"
const ENV := "res://Assets/ArtRefresh/env_level_garden.tres"
const UNIT_OUTLINE_SHADER := "res://Assets/ArtRefresh/toonlab/toonlab_unit_outline.gdshader"
const MASK_SHADER := "res://Assets/ArtRefresh/toonlab/toonlab_mask.gdshader"
const UNIT_OUTLINE_POST_SHADER := "res://Assets/ArtRefresh/toonlab/toonlab_unit_outline_post.gdshader"

## 遮罩层：单位的一份白色剪影只渲染进 SubViewport，主相机不渲染它。
const MASK_BIT := 1

var _mask_viewport: SubViewport
var _mask_camera: Camera3D
var _post_mat: ShaderMaterial
var _post: ColorRect

const SPACING := 2.6
const CAM_POS := Vector3(0.0, 1.45, 12.0)
const CAM_LOOK := Vector3(0.0, 1.30, 0.0)

# name → [2.8m 缩放, bounds_min, bounds_size]（来自 audit-results.json）
const BOSSES := {
	"DRIZZEL": [1.3237, Vector3(-0.921, 0.000, -0.443), Vector3(1.841, 2.115, 0.972)],
	"GEKKOU": [0.7967, Vector3(-0.822, -1.314, -1.215), Vector3(1.645, 3.514, 1.788)],
	"KASA": [1.3051, Vector3(-1.205, -0.001, -3.277), Vector3(2.729, 2.146, 4.301)],
	"KURENAI": [1.2670, Vector3(-0.711, -0.002, -0.745), Vector3(1.421, 2.210, 1.421)],
	"SAMIDALE": [1.6831, Vector3(-0.611, -0.006, -0.312), Vector3(1.242, 1.664, 0.753)],
	"SHOGUN": [1.0670, Vector3(-0.663, -1.009, -0.357), Vector3(1.326, 2.624, 0.607)],
}

var _camera: Camera3D
var _env: Environment
var _sun: DirectionalLight3D
var _bosses: Array[Node3D] = []
var _hull: ShaderMaterial
var _log: Array[String] = []


func _ready() -> void:
	await get_tree().process_frame
	_build()
	await get_tree().create_timer(0.6).timeout

	await _shot("bosslab_0_baseline")
	_apply_lighting()
	_apply_toon_nospec()
	await _settle()
	await _shot("bosslab_1_toon")

	# ① 全部网格都挂缩放壳描边 → 薄片（斗笠/披风）会被翻成黑色大块
	_apply_unit_outline(1.03, false)
	await _settle()
	await _shot("bosslab_2_outline_all_030")

	# ② 只给实体部件挂（排除 Cape/Hat/Cloth 这类薄片）—— 但对薄片混在主体 mesh 里的模型无效
	_clear_outline()
	for s in [1.03, 1.05]:
		_apply_unit_outline(s, true)
		await _settle()
		await _shot("bosslab_3_outline_body_%03d" % int((s - 1.0) * 1000.0))

	# ③ 法线壳：薄片只会变厚一点点，不会翻出大黑块 —— 首领的候选方案
	_clear_outline()
	for w in [0.02, 0.04]:
		_apply_normal_outline(w)
		await _settle()
		await _shot("bosslab_4_outline_normal_%03d" % int(w * 1000.0))

	# ④ B 方案：单位单独一份白色剪影进 SubViewport → 屏幕空间描边（只描单位）
	_clear_outline()
	_setup_screen_outline()
	await _settle()
	for t in [2.0, 4.0]:
		_post_mat.set_shader_parameter("thickness_px", t)
		await _settle()
		await _shot("bosslab_5_screen_%02d" % int(t))

	for line in _log:
		print("[bosslab] ", line)
	print("[bosslab] done")
	get_tree().quit(0)


func _build() -> void:
	# --- 环境（庭院天空/雾/环境光，和关卡同一份资源） ---
	var we := WorldEnvironment.new()
	we.environment = load(ENV)
	add_child(we)
	_env = we.environment

	# --- 太阳（硬阴影，卡通要的这一口） ---
	_sun = DirectionalLight3D.new()
	_sun.rotation_degrees = Vector3(-48.0, -32.0, 0.0)
	_sun.light_energy = 1.15
	_sun.shadow_enabled = true
	add_child(_sun)

	# --- 摄影棚地面 ---
	var ground := MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size = Vector2(60.0, 60.0)
	ground.mesh = plane
	var gm := StandardMaterial3D.new()
	gm.albedo_color = Color(0.42, 0.45, 0.40)
	gm.roughness = 1.0
	ground.material_override = gm
	add_child(ground)

	# --- 6 只首领排一排 ---
	var names := BOSSES.keys()
	var count := names.size()
	for i in count:
		var x: float = (float(i) - (float(count) - 1.0) * 0.5) * SPACING
		_bosses.append(_spawn_boss(names[i], x))

	_camera = Camera3D.new()
	_camera.fov = 40.0
	_camera.near = 0.05
	_camera.far = 300.0
	add_child(_camera)
	_camera.global_position = CAM_POS
	_camera.look_at(CAM_LOOK, Vector3.UP)
	_camera.current = true
	_log.append("camera at %s → %s，%d 只首领，间距 %.1fm" % [CAM_POS, CAM_LOOK, count, SPACING])


func _spawn_boss(name: String, x: float) -> Node3D:
	var spec: Array = BOSSES[name]
	var s: float = spec[0]
	var bmin: Vector3 = spec[1]
	var bsize: Vector3 = spec[2]

	var packed: PackedScene = load("%s/%s.fbx" % [BOSS_DIR, name])
	var node: Node3D = packed.instantiate()
	add_child(node)
	node.scale = Vector3.ONE * s
	# 脚底贴地、水平居中；缩放取审计值，不猜
	node.position = Vector3(
		x - (bmin.x + bsize.x * 0.5) * s,
		-bmin.y * s,
		-(bmin.z + bsize.z * 0.5) * s)

	var meshes := _walk_meshes(node)
	var surfaces := 0
	for mi in meshes:
		if mi.mesh != null:
			surfaces += mi.mesh.get_surface_count()
	_log.append("%s 网格：%s" % [name, ", ".join(_mesh_names(meshes))])
	_log.append("%s：缩放 %.3f → 高 %.2fm、宽 %.2fm；MeshInstance %d / 面 %d；脚底 y=%.2f"
		% [name, s, bsize.y * s, bsize.x * s, meshes.size(), surfaces, node.position.y])
	return node


func _mesh_names(meshes: Array[MeshInstance3D]) -> Array[String]:
	var names: Array[String] = []
	for mi in meshes:
		names.append("%s(%d)" % [mi.name, mi.mesh.get_surface_count() if mi.mesh != null else 0])
	return names


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


func _settle() -> void:
	for _i in 3:
		await get_tree().process_frame


# ------------------------------------------------------------ B 方案：只描单位
func _process(_delta: float) -> void:
	_sync_mask_camera()


func _sync_mask_camera() -> void:
	if _mask_camera == null or _camera == null:
		return
	_mask_camera.global_transform = _camera.global_transform
	_mask_camera.fov = _camera.fov
	_mask_camera.near = _camera.near
	_mask_camera.far = _camera.far
	_mask_camera.projection = _camera.projection
	_mask_camera.size = _camera.size


func _setup_screen_outline() -> void:
	# 主相机不再渲染遮罩层
	_camera.cull_mask = 0xFFFFF & ~(1 << MASK_BIT)

	var vp := SubViewport.new()
	vp.size = Vector2i(get_viewport().get_visible_rect().size)
	vp.transparent_bg = true
	vp.own_world_3d = false
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	vp.render_target_clear_mode = SubViewport.CLEAR_MODE_ALWAYS
	add_child(vp)
	_mask_viewport = vp

	_mask_camera = Camera3D.new()
	_mask_camera.cull_mask = 1 << MASK_BIT
	vp.add_child(_mask_camera)
	_mask_camera.current = true
	_sync_mask_camera()

	var dups := 0
	for boss in _bosses:
		dups += _build_mask_duplicates(boss)

	var layer := CanvasLayer.new()
	layer.layer = 1
	add_child(layer)

	_post = ColorRect.new()
	_post.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_post_mat = ShaderMaterial.new()
	_post_mat.shader = load(UNIT_OUTLINE_POST_SHADER)
	_post_mat.set_shader_parameter("mask_tex", vp.get_texture())
	_post_mat.set_shader_parameter("thickness_px", 2.0)
	_post.material = _post_mat
	layer.add_child(_post)
	_post.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)

	_log.append("B 方案：mask SubViewport %s（%d 份剪影副本），主相机 cull_mask=0x%X，mask 相机 cull_mask=0x%X"
		% [vp.size, dups, _camera.cull_mask, _mask_camera.cull_mask])


func _build_mask_duplicates(unit: Node3D) -> int:
	var white := ShaderMaterial.new()
	white.shader = load(MASK_SHADER)
	var count := 0
	for mi in _walk_meshes(unit):
		if mi.mesh == null:
			continue
		var dup := MeshInstance3D.new()
		dup.name = String(mi.name) + "_Mask"
		dup.mesh = mi.mesh
		dup.skin = mi.skin
		dup.skeleton = mi.skeleton
		dup.material_override = white
		dup.layers = 1 << MASK_BIT
		dup.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		mi.get_parent().add_child(dup)
		dup.transform = mi.transform
		count += 1
	return count


func _shot(name: String) -> void:
	await RenderingServer.frame_post_draw
	var img: Image = get_viewport().get_texture().get_image()
	var err := img.save_png("%s/%s.png" % [OUT_DIR, name])
	_log.append("shot %s -> %s (%dx%d)" % [name, error_string(err), img.get_width(), img.get_height()])


func _apply_lighting() -> void:
	if _env != null:
		_env.ssao_enabled = false
		_env.ambient_light_energy = 0.18
		_env.glow_intensity = 0.18
		_env.glow_blend_mode = Environment.GLOW_BLEND_MODE_ADDITIVE
		_env.fog_density = 0.001
	if _sun != null:
		_sun.shadow_blur = 0.0
		_sun.light_angular_distance = 0.0
		_sun.light_energy = 1.55
	_log.append("光照：关 SSAO / ambient 0.30 / glow 0.18 / fog 0.001 / 硬阴影")


func _apply_toon_nospec() -> void:
	var count := 0
	for boss in _bosses:
		for mi in _walk_meshes(boss):
			if mi.mesh == null:
				continue
			for i in mi.mesh.get_surface_count():
				var src := _source_material(mi, i)
				if src is not StandardMaterial3D:
					continue
				var sm: StandardMaterial3D = (src as StandardMaterial3D).duplicate()
				sm.diffuse_mode = BaseMaterial3D.DIFFUSE_TOON
				sm.specular_mode = BaseMaterial3D.SPECULAR_DISABLED
				sm.roughness = 0.0
				mi.set_surface_override_material(i, sm)
				count += 1
	_log.append("内置 Toon（无高光）：%d 个面" % count)


func _source_material(mi: MeshInstance3D, i: int) -> Material:
	var mesh: Mesh = mi.mesh
	if mesh != null:
		var m: Material = mesh.surface_get_material(i)
		if m != null:
			return m
	if mi.material_override != null:
		return mi.material_override
	return mi.get_surface_override_material(i)


## 薄片部件：斗笠 / 披风 / 布片。这些没有厚度，缩放壳会把它们的背面翻到外面 → 黑色大块，
## 所以候选方案只给"实体部件"挂描边。（名字来自 audit-results.json 的 mesh 路径）
const THIN_PARTS := ["Cape", "Hat", "Cloth", "Umbrella", "Skirt", "Wing"]


func _apply_unit_outline(scale_factor: float, body_only: bool) -> void:
	if _hull == null:
		_hull = ShaderMaterial.new()
		_hull.shader = load(UNIT_OUTLINE_SHADER)
	_hull.set_shader_parameter("mode", 1)
	_hull.set_shader_parameter("scale_factor", scale_factor)

	var count := 0
	var skipped := 0
	for boss in _bosses:
		for mi in _walk_meshes(boss):
			if mi.mesh == null:
				continue
			if body_only and _is_thin(mi.name):
				skipped += mi.mesh.get_surface_count()
				continue
			for i in mi.mesh.get_surface_count():
				var m: Material = mi.get_surface_override_material(i)
				if m is StandardMaterial3D and m.next_pass == null:
					m.next_pass = _hull
					count += 1
	_log.append("描边 scale=%.3f（%s）：挂 %d 个面，跳过薄片 %d 个面"
		% [scale_factor, "只实体" if body_only else "全部", count, skipped])


func _is_thin(node_name: String) -> bool:
	for key in THIN_PARTS:
		if node_name.contains(key):
			return true
	return false


func _clear_outline() -> void:
	for boss in _bosses:
		for mi in _walk_meshes(boss):
			if mi.mesh == null:
				continue
			for i in mi.mesh.get_surface_count():
				var m: Material = mi.get_surface_override_material(i)
				if m is StandardMaterial3D and m.next_pass != null:
					m.next_pass = null
	_log.append("描边已全部清除")


func _apply_normal_outline(width: float) -> void:
	if _hull == null:
		_hull = ShaderMaterial.new()
		_hull.shader = load(UNIT_OUTLINE_SHADER)
	_hull.set_shader_parameter("mode", 0)
	_hull.set_shader_parameter("width", width)

	var count := 0
	for boss in _bosses:
		for mi in _walk_meshes(boss):
			if mi.mesh == null:
				continue
			for i in mi.mesh.get_surface_count():
				var m: Material = mi.get_surface_override_material(i)
				if m is StandardMaterial3D and m.next_pass == null:
					m.next_pass = _hull
					count += 1
	_log.append("法线壳描边 width=%.3f：挂 %d 个面" % [width, count])

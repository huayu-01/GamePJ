extends Node3D
## 抽取工具（**可复用**：改下面的 `MAP` 就能换一组宝石 / 换映射）：
## 把 `Meshes/gemN.fbx` 里那颗宝石的网格抽成独立的 `Assets/Models/nodes/mesh_node_<kind>.tres`。
##
## 为什么不直接把场景的 mesh 指向 `gem3.fbx::ArrayMesh_xxxxx`：
## 导入器给子资源的名字带随机后缀（实测 12 颗各不相同），重新导入后可能变化 →
## 手写引用会**静默失效**（节点变成看不见的空壳）。抽成 `.tres` 之后几何数据独立于那个命名。
##
## 同时**逐颗实测并修正**三件只有量了才知道的事（见 docs/art-onboarding.md §三.1.1）：
##   ① 有向体积 V == Σ a·(b×c)/6：**V > 0 才是正面朝外的逆时针绕序**；V < 0 = 绕序反了
##      （Godot 默认剔除背面 → 会看到宝石"内壁"，就是空心/破面那种观感）→ 交换三角形后两个索引；
##   ② 法线与"顶点相对形心"的平均点积：< 0 = 法线朝内（受光反向，面发黑）→ 取负；
##   ③ 轴心（AABB 的 min.y）与尺寸 → 决定场景里的 `position.y` 与 `scale`，**不要凭猜**。
## ①② **独立判定、各自修正**（第一轮就是只量了一个才踩坑）。
##
## 用法：Godot.exe --path <项目> res://Scenes/Interactables/_ExtractGem.tscn
##
## 输出：
##   ① 12 颗的实测量打印到日志（`[ExtractGem] gem N …`）；
##   ② 12 颗并排对比图 `docs/screenshots/gem_00_sheet.png`（挑形状用，粘土渲染 = 只看形状不看贴图）；
##   ③ 按 `MAP` 抽出的 `mesh_node_<kind>.tres`（绕序已修 + 材质已挂在该网格的 `_surfaces` 里）。

const SRC_FMT := "res://Assets/Models/nodes/gem%d.fbx"
const DST_FMT := "res://Assets/Models/nodes/mesh_node_%s.tres"
const MAT_FMT := "res://Assets/Materials/mat_node_%s.tres"
const SHEET := "res://docs/screenshots/gem_00_sheet.png"
const SHEET_TILT := "res://docs/screenshots/gem_01_sheet_tilt.png"

## 哪一颗宝石给哪种节点类型（值 = 包内 `Meshes/gemN.fbx` 的 N）。
## 挑选依据 = **形状辨识度**（远处 / 混战里一眼认出），高价值节点优先给最好认的形状：
## 见 docs/art-onboarding.md §三.1.3 的表（含每颗的形状描述与尺寸实测量）。
const MAP := {
	"weapon": 7,       # 细长六棱晶（刀锋感）
	"enhancement": 5,  # 厚实盾形宝石（唯一 1.0 深的实体块之一）
	"evolution": 1,    # 经典多面钻石（最好认，最高价值）
	"mutation": 4,     # 四角星（异变爆发）
	"resource": 10,    # 六边形晶块（矿）
	"heal": 8,         # 水滴 / 长五边形（药水）
	"special": 6,      # 倒五边形（本垒板状，形状少见）
	"shop": 9,         # 八边形圆块（币）
	"portal": 11,      # 正方形板块（门 / 传送口）
}

## 留下备用的三颗（**没有**被上面用到；加新节点类型时从这里挑，别再从头选）。
const SPARE := [2, 3, 12]

const CELL_X := 1.7
const CELL_Y := 1.75
const COLS := 4

func _ready() -> void:
	DisplayServer.window_set_size(Vector2i(1600, 900))
	_build_sheet_stage()

	for n in range(1, 13):
		var mesh := _fixed_mesh(n)
		if mesh == null:
			continue
		_cache[n] = mesh
		var inst := MeshInstance3D.new()
		inst.mesh = mesh
		# 粘土渲染：只看**形状**，不看 FBX 里那张（本项目根本不读的）调色板贴图
		var clay := StandardMaterial3D.new()
		clay.albedo_color = Color(0.82, 0.83, 0.86)
		clay.roughness = 0.6
		inst.material_override = clay
		var col := (n - 1) % COLS
		var row := (n - 1) / COLS
		inst.position = Vector3((col - (COLS - 1) * 0.5) * CELL_X, ((1 - row) * CELL_Y), 0.0)
		add_child(inst)

		var label := Label3D.new()
		label.text = "gem%d" % n
		label.font_size = 64
		label.pixel_size = 0.0032
		label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		label.no_depth_test = true
		label.position = inst.position + Vector3(0.0, -0.72, 0.3)
		add_child(label)

	# ── ① 两张并排图（挑形状 + 逐颗确认绕序修好了没有"内壁"）──
	# 正交投影 = 12 颗一样大、没有近大远小，方便按"远处能不能认出"排序
	var cam: Camera3D = $Camera
	cam.projection = Camera3D.PROJECTION_ORTHOGONAL
	cam.size = 5.6
	cam.position = Vector3(0.0, 0.0, 6.0)
	cam.rotation_degrees = Vector3.ZERO
	await _frames(8)
	await _shot(SHEET)

	cam.size = 7.4
	cam.position = Vector3(4.4, 2.6, 5.2)
	cam.look_at(Vector3(0.0, 0.0, 0.0), Vector3.UP)
	await _frames(8)
	await _shot(SHEET_TILT)

	# ── ② 抽取（几何 + 材质）──
	for kind in MAP:
		_extract(kind, int(MAP[kind]))

	print("[ExtractGem] 备用（没被用到的三颗，加新节点类型时从它们里挑）：", SPARE)
	get_tree().quit()

## 读一颗宝石、**实测**它的三件事并修正，返回修好的 ArrayMesh。
func _fixed_mesh(n: int) -> ArrayMesh:
	var src_path := SRC_FMT % n
	if not ResourceLoader.exists(src_path):
		print("[ExtractGem] 缺文件：", src_path)
		return null

	var scene: PackedScene = load(src_path)
	var root: Node = scene.instantiate()
	var mi: MeshInstance3D = _first_mesh(root)
	if mi == null:
		print("[ExtractGem] 失败：", src_path, " 里没有 MeshInstance3D")
		root.free()
		return null

	var src: ArrayMesh = mi.mesh as ArrayMesh
	var arrays: Array = src.surface_get_arrays(0)
	var verts: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]
	var index: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]

	var center := Vector3.ZERO
	for v in verts:
		center += v
	center /= float(verts.size())

	var volume := _signed_volume(verts, index, center)
	var outward := _normal_outwardness(verts, normals, center)
	var winding_fixed := volume < 0.0
	var normals_fixed := outward < 0.0

	# ① 绕序反了 → 每个三角形交换后两个索引
	if winding_fixed:
		for t in range(0, index.size(), 3):
			var tmp := index[t + 1]
			index[t + 1] = index[t + 2]
			index[t + 2] = tmp
		arrays[Mesh.ARRAY_INDEX] = index

	# ② 法线朝内 → 取负（切线一起取负，保持切线基一致）
	if normals_fixed:
		for i in range(normals.size()):
			normals[i] = -normals[i]
		arrays[Mesh.ARRAY_NORMAL] = normals
		if arrays[Mesh.ARRAY_TANGENT] is PackedFloat32Array:
			var tangents: PackedFloat32Array = arrays[Mesh.ARRAY_TANGENT]
			for i in range(0, tangents.size(), 4):
				tangents[i] = -tangents[i]
				tangents[i + 1] = -tangents[i + 1]
				tangents[i + 2] = -tangents[i + 2]
			arrays[Mesh.ARRAY_TANGENT] = tangents

	var out := ArrayMesh.new()
	out.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	var aabb := out.get_aabb()
	print("[ExtractGem] gem%-2d 顶点=%-4d 三角形=%-4d | V=%+.6f→%+.6f（%s） 法线外向度=%+.4f→%+.4f（%s） | 轴心 min.y=%+.4f size=(%.4f, %.4f, %.4f) 形心=(%.4f, %.4f, %.4f)" % [
		n, verts.size(), index.size() / 3,
		volume, _signed_volume(verts, index, center),
		"交换索引" if winding_fixed else "本来就对",
		outward, _normal_outwardness(verts, arrays[Mesh.ARRAY_NORMAL], center),
		"取负" if normals_fixed else "本来就对",
		aabb.position.y, aabb.size.x, aabb.size.y, aabb.size.z,
		center.x, center.y, center.z])
	root.free()
	return out

## 把第 n 颗抽成 `mesh_node_<kind>.tres`，并把 `mat_node_<kind>.tres` 挂上去。
func _extract(kind: String, n: int) -> void:
	var mesh := _fixed_mesh_cached(n)
	if mesh == null:
		return

	var dst := DST_FMT % kind
	var mat_path := MAT_FMT % kind
	if ResourceLoader.exists(mat_path):
		mesh.surface_set_material(0, load(mat_path))
	else:
		print("[ExtractGem] ⚠ ", kind, " 缺材质 ", mat_path, "（抽出来的网格会退回默认材质）")

	mesh.resource_name = "mesh_node_" + kind
	var err := ResourceSaver.save(mesh, dst)
	print("[ExtractGem] 保存 ", dst, " ← gem", n, " err=", err)

var _cache: Dictionary = {}

func _fixed_mesh_cached(n: int) -> ArrayMesh:
	if not _cache.has(n):
		_cache[n] = _fixed_mesh(n)
	return _cache[n]

## 有向体积（散度定理）：正 = 逆时针朝外，负 = 绕序反了。
func _signed_volume(verts: PackedVector3Array, index: PackedInt32Array, center: Vector3) -> float:
	var total := 0.0
	for t in range(0, index.size(), 3):
		var a := verts[index[t]] - center
		var b := verts[index[t + 1]] - center
		var c := verts[index[t + 2]] - center
		total += a.dot(b.cross(c))
	return total / 6.0

## 法线外向度：顶点相对形心的方向 · 法线 的平均值（正 = 朝外）。
func _normal_outwardness(verts: PackedVector3Array, normals: PackedVector3Array, center: Vector3) -> float:
	if normals.size() == 0:
		return 1.0
	var total := 0.0
	for i in range(verts.size()):
		var dir := (verts[i] - center)
		if dir.length_squared() > 0.000001:
			total += dir.normalized().dot(normals[i])
	return total / float(verts.size())

func _first_mesh(n: Node) -> MeshInstance3D:
	for c in n.get_children():
		if c is MeshInstance3D:
			return c
		var deeper := _first_mesh(c)
		if deeper != null:
			return deeper
	return null

func _build_sheet_stage() -> void:
	var key := DirectionalLight3D.new()
	key.rotation_degrees = Vector3(-45.0, -40.0, 0.0)
	key.light_energy = 1.5
	add_child(key)

	# 补一盏对着镜头这一侧的光，免得正视那张图里全是暗面（只看形状也要看得见面的朝向）
	var fill := DirectionalLight3D.new()
	fill.rotation_degrees = Vector3(-18.0, 25.0, 0.0)
	fill.light_energy = 0.9
	add_child(fill)

	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0.13, 0.14, 0.17)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color(0.7, 0.72, 0.78)
	env.ambient_light_energy = 1.0
	var world := WorldEnvironment.new()
	world.environment = env
	add_child(world)

func _frames(n: int) -> void:
	for _i in n:
		await get_tree().process_frame

func _shot(path: String) -> void:
	await RenderingServer.frame_post_draw
	var img := get_viewport().get_texture().get_image()
	print("[ExtractGem] 截图 ", path, " err=", img.save_png(path))

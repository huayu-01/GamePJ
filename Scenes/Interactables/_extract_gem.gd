extends Node
## 临时工具（用完即删，步骤已抄进 docs/art-onboarding.md §三.1）：
## 把 FBX 里那颗宝石的网格抽成独立的 `mesh_node_gem.tres`。
##
## 为什么不直接把 `RewardNode.tscn` 的 mesh 指向 `gem.fbx::ArrayMesh_xxxxx`：
## 导入器给子资源的名字带随机后缀（实测 gem1~gem12 各是 ArrayMesh_1p084 / _kbc0q / …），
## 重新导入后可能变化 → 手写引用会**静默失效**（节点变成看不见的空壳）。
##
## 顺便**实测并修正**两件只有量了才知道的事（见 docs/art-onboarding.md §三.2 的"轴心/朝向要实测"）：
##   ① 有向体积 V == Σ a·(b×c)/6：**V > 0 才是正面朝外的逆时针绕序**；V < 0 = 绕序反了
##      （Godot 默认剔除背面 → 会看到宝石"内壁"，就是空心/破面那种观感）；
##   ② 法线与"顶点相对形心"的平均点积：< 0 = 法线朝内（受光反向，面发黑）。
## 两者独立判定、各自修正。
##
## 用法：Godot.exe --path <项目> res://Scenes/Interactables/_ExtractGem.tscn

const SRC := "res://Assets/Models/nodes/gem_node_reward.fbx"
const DST := "res://Assets/Models/nodes/mesh_node_gem.tres"

func _ready() -> void:
	var scene: PackedScene = load(SRC)
	var root: Node = scene.instantiate()
	var mi: MeshInstance3D = _first_mesh(root)
	if mi == null:
		print("[ExtractGem] 失败：FBX 里没有 MeshInstance3D")
		get_tree().quit()
		return

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
	print("[ExtractGem] 源=", SRC, " 顶点=", verts.size(), " 三角形=", index.size() / 3,
		" | 有向体积 V=", "%.6f" % volume, "（>0 才正面朝外） 法线外向度=", "%.4f" % outward, "（>0 才朝外）")

	# ① 绕序反了 → 每个三角形交换后两个索引
	if volume < 0.0:
		for t in range(0, index.size(), 3):
			var tmp := index[t + 1]
			index[t + 1] = index[t + 2]
			index[t + 2] = tmp
		arrays[Mesh.ARRAY_INDEX] = index

	# ② 法线朝内 → 取负（切线一起取负，保持切线基一致）
	if outward < 0.0:
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

	var fixed_volume := _signed_volume(verts, index, center)
	print("[ExtractGem] 修正后：V=", "%.6f" % fixed_volume, " 法线外向度=",
		"%.4f" % _normal_outwardness(verts, arrays[Mesh.ARRAY_NORMAL], center))

	var out := ArrayMesh.new()
	out.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	out.resource_name = "mesh_node_gem"
	var aabb := out.get_aabb()
	var err := ResourceSaver.save(out, DST)
	print("[ExtractGem] 保存=", DST, " err=", err, " AABB min=", aabb.position, " size=", aabb.size)
	root.free()
	get_tree().quit()

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

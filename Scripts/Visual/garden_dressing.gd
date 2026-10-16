extends Node3D
## 纯视觉庭院：没有碰撞体、单位、分组或战斗数据写入；替换模型只需换导出场景。

@export_group("布局")
@export var base_mode := false
@export var seed_value := 24617
@export var tree_count := 32
@export var perimeter_radius := 36.0
@export var perimeter_jitter := 2.0
@export var ground_height := 0.0
@export var tree_scene: PackedScene
@export_group("形状")
@export var tree_height := 7.0
@export var rock_count := 24
@export var rock_scale := 3.0
@export var distant_radius := 80.0
@export var oak_ratio := 0.65
@export var cloud_count := 9
@export var cloud_height := 27.0
@export var cloud_radius := 76.0
@export_group("颜色")
@export var leaf_color := Color(0.22, 0.43, 0.27)
@export var leaf_light_color := Color(0.39, 0.55, 0.28)
@export var trunk_color := Color(0.28, 0.24, 0.17)
@export var rock_color := Color(0.30, 0.39, 0.36)
@export var trim_color := Color(0.57, 0.56, 0.39)

var _rng := RandomNumberGenerator.new()

func _ready() -> void:
	_rng.seed = seed_value
	var leaves := _material(leaf_color)
	var light_leaves := _material(leaf_light_color)
	var bark := _material(trunk_color)
	var stone := _material(rock_color)
	for i in tree_count:
		var angle := TAU * float(i) / tree_count + _rng.randf_range(-0.06, 0.06)
		var radius := perimeter_radius + _rng.randf_range(0.0, perimeter_jitter)
		var pos := Vector3(sin(angle) * radius, ground_height, cos(angle) * radius)
		var height := tree_height * _rng.randf_range(0.72, 1.35)
		if tree_scene:
			var tree := tree_scene.instantiate() as Node3D
			tree.position = pos
			tree.scale = Vector3.ONE * height / tree_height
			add_child(tree)
		else:
			_mesh(_cylinder(0.18, 0.30, height * 0.60, 7), pos + Vector3.UP * height * 0.30, bark)
			if _rng.randf() < oak_ratio:
				for cluster in 5:
					var turn := TAU * cluster / 5.0
					var crown := SphereMesh.new()
					crown.radius = height * 0.23
					crown.height = height * 0.42
					crown.radial_segments = 8
					crown.rings = 4
					var offset := Vector3(sin(turn) * height * 0.16, height * (0.67 + 0.12 * sin(turn * 2.0)), cos(turn) * height * 0.16)
					_mesh(crown, pos + offset, leaves if i % 3 else light_leaves)
			else:
				for tier in 3:
					var width := height * (0.34 - tier * 0.065)
					_mesh(_cylinder(0.0, width, height * 0.43, 8), pos + Vector3.UP * height * (0.47 + tier * 0.19), leaves if i % 3 else light_leaves)
			if base_mode:
				_mesh(_cylinder(height * 0.27, height * 0.18, height * 1.6, 6), pos - Vector3.UP * height * 0.8, stone)
	for i in rock_count:
		var angle := TAU * float(i) / rock_count
		var radius := perimeter_radius - 1.0 + _rng.randf_range(0.0, 7.0)
		var size := rock_scale * _rng.randf_range(0.55, 1.25)
		var rock := _mesh(_cylinder(size * 0.5, size, size * 1.2, 5), Vector3(sin(angle) * radius, ground_height - 0.3, cos(angle) * radius), stone)
		rock.rotation = Vector3(_rng.randf_range(-0.2, 0.2), angle, 0.12)
	# 远景山峰只画轮廓，不扩张可行走区域。
	for i in 12:
		var angle := TAU * i / 12.0
		var peak := _mesh(_cylinder(0.0, 15.0, _rng.randf_range(15.0, 26.0), 5), Vector3(sin(angle) * distant_radius, -4.0, cos(angle) * distant_radius), stone)
		peak.rotation.y = angle
	var cloud_material := _material(Color(0.83, 0.89, 0.84))
	cloud_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	for i in cloud_count:
		var angle := TAU * i / maxi(cloud_count, 1)
		for puff in 3:
			var cloud := SphereMesh.new()
			cloud.radius = 4.0 + puff * 1.5
			cloud.height = 4.2 + puff
			cloud.radial_segments = 12
			cloud.rings = 5
			var cloud_pos := Vector3(sin(angle) * cloud_radius + puff * 4.0, cloud_height + sin(angle * 2.0) * 3.0, cos(angle) * cloud_radius)
			var instance := _mesh(cloud, cloud_pos, cloud_material)
			instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	if base_mode:
		_base_details(stone)

func _base_details(stone: Material) -> void:
	var trim := _material(trim_color)
	# 平台边框与终端基座均为网格，交互范围与地板物理保持原样。
	for side in [-1.0, 1.0]:
		var edge := BoxMesh.new()
		edge.size = Vector3(26.0, 0.22, 0.25)
		_mesh(edge, Vector3(0.0, 0.05, side * 12.85), trim)
		var other := BoxMesh.new()
		other.size = Vector3(0.25, 0.22, 26.0)
		_mesh(other, Vector3(side * 12.85, 0.05, 0.0), trim)
	for x in [-7.0, -2.4, 2.4, 7.0]:
		_mesh(_cylinder(1.28, 1.42, 0.16, 8), Vector3(x, 0.06, 0.0), stone)
		var ring := TorusMesh.new()
		ring.inner_radius = 1.23
		ring.outer_radius = 1.30
		ring.rings = 40
		ring.ring_segments = 4
		_mesh(ring, Vector3(x, 0.16, 0.0), trim)

func _cylinder(top: float, bottom: float, height: float, sides: int) -> CylinderMesh:
	var mesh := CylinderMesh.new()
	mesh.top_radius = top
	mesh.bottom_radius = bottom
	mesh.height = height
	mesh.radial_segments = sides
	mesh.rings = 1
	return mesh

func _material(color: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.roughness = 0.92
	material.diffuse_mode = BaseMaterial3D.DIFFUSE_TOON
	material.specular_mode = BaseMaterial3D.SPECULAR_DISABLED
	return material

func _mesh(mesh: Mesh, pos: Vector3, material: Material) -> MeshInstance3D:
	var instance := MeshInstance3D.new()
	instance.mesh = mesh
	instance.material_override = material
	instance.position = pos
	add_child(instance)
	return instance

func debug_state() -> String:
	return "庭院装饰：子节点=%d 树=%d 外圈=%.1f 碰撞=0 单位=0 分组=0" % [get_child_count(), tree_count, perimeter_radius]

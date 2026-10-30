@tool
extends RefCounted

const OUTPUT = "res://Scenes/Levels/NatureBenchmark/"
const ASSETS = "res://Assets/Environment/NatureBenchmark/"
const MODELS = "res://Assets/Environment/StylizedNature/Models/"
const SEED = 20261021
var rng = RandomNumberGenerator.new()
var root: Node3D
var meshes: Dictionary = {}
var trunk_radii: Dictionary = {}
var materials: Dictionary = {}
var batches: Dictionary = {}
var placements: Array = []
var batch_count = 0

func own(parent: Node, child: Node) -> void:
	parent.add_child(child)
	child.owner = root

func save_resource(resource: Resource, path: String) -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(path.get_base_dir()))
	assert(ResourceSaver.save(resource, path) == OK, "无法保存 " + path)
	resource.take_over_path(path)

func hill(p: Vector2, center: Vector2, height: float, spread: Vector2) -> float:
	var q = (p - center) / spread
	return height * exp(-q.length_squared())

func elevation(x: float, z: float) -> float:
	var p = Vector2(x, z)
	var h = hill(p, Vector2(-12,-8), 4.1, Vector2(6,8))
	h += hill(p, Vector2(16,-16), 4.6, Vector2(9,9))
	h += hill(p, Vector2(-25,-32), 8.0, Vector2(12,10))
	h += hill(p, Vector2(16,-37), 7.5, Vector2(14,9))
	h += hill(p, Vector2(27,5), 3.5, Vector2(10,14))
	# 战斗空地平缓，外围保留起伏；视觉网格和碰撞使用同一高度函数。
	var flatten = smoothstep(4.0, 9.0, p.distance_to(Vector2(0,2)))
	return h * flatten + sin(x * 0.23) * cos(z * 0.31) * 0.10 * flatten

func road_x(z: float) -> float:
	return sin(z * 0.19) * 2.1 + z * 0.045

func grass_allowed(x: float, z: float) -> bool:
	var path_width = 1.45 + sin(z * 0.5) * 0.2
	if abs(x - road_x(z)) < path_width + rng.randf_range(-0.05,0.45):
		return false
	var center = Vector2(x * 0.92, z - 2.0).length()
	if center < 3.2 + rng.randf_range(-0.2,0.5):
		return false
	return true

func surface_material(original: StandardMaterial3D, asset: String, box: AABB) -> Material:
	var key = asset + "_" + original.resource_name
	if materials.has(key):
		return materials[key]
	var name = original.resource_name
	var result: Material
	if name.begins_with("Leaves") or name == "Leaves_Pine":
		var leaf = ShaderMaterial.new()
		leaf.shader = load(ASSETS + "Shaders/foliage.gdshader")
		leaf.set_shader_parameter("leaf_texture", original.albedo_texture)
		leaf.set_shader_parameter("crown_center", box.get_center())
		leaf.set_shader_parameter("crown_radius", box.size.y * 0.5)
		var color = Color("99cc36")
		if "Pine" in name: color = Color("65a63c")
		if "Twisted" in name: color = Color("b4d85b")
		if name == "Leaves":
			color = Color("b2d887")
			leaf.set_shader_parameter("texture_color_weight", 0.85)
			leaf.set_shader_parameter("normal_blend", 0.6)
			leaf.set_shader_parameter("wind_strength", 0.05)
		leaf.set_shader_parameter("leaf_color", color)
		result = leaf
	elif name == "Grass":
		if materials.has("SharedGrass"): return materials.SharedGrass
		var grass = ShaderMaterial.new()
		grass.shader = load(ASSETS + "Shaders/grass.gdshader")
		materials.SharedGrass = grass
		return grass
	else:
		var matte = original.duplicate() as StandardMaterial3D
		matte.diffuse_mode = BaseMaterial3D.DIFFUSE_BURLEY
		matte.specular_mode = BaseMaterial3D.SPECULAR_DISABLED
		matte.roughness = 1.0
		matte.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
		if "Rocks" in name:
			matte.albedo_color = Color(1.45,1.45,1.36)
			matte.normal_scale = 0.6
		result = matte
	materials[key] = result
	return result

func mesh_data(asset: String) -> Array:
	if meshes.has(asset): return meshes[asset]
	var source = load(MODELS + asset + ".gltf").instantiate()
	var collector = load("res://ArtSource/Tools/NatureArena/build_arena.gd").new()
	var entries: Array = []
	collector.collect_meshes(source, Transform3D.IDENTITY, entries)
	var result: Array = []
	var trunk_radius = 0.35
	for item in entries:
		var original: MeshInstance3D = item.node
		var mesh = original.mesh.duplicate() as ArrayMesh
		for surface in mesh.get_surface_count():
			var arrays = mesh.surface_get_arrays(surface)
			var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			var box = AABB(vertices[0], Vector3.ZERO)
			for vertex in vertices: box = box.expand(vertex)
			if "Bark" in original.get_active_material(surface).resource_name:
				for vertex in vertices:
					var point: Vector3 = item.transform * vertex
					if point.y > 0.15 and point.y < 1.65:
						trunk_radius = max(trunk_radius,Vector2(point.x,point.z).length()*0.85)
			mesh.surface_set_material(surface, surface_material(original.get_active_material(surface), asset, box))
		result.append({"mesh":mesh,"pose":item.transform})
	source.free()
	meshes[asset] = result
	trunk_radii[asset] = min(trunk_radius,1.4)
	return result

func place(parent: Node3D, asset: String, x: float, z: float, scale_value: float, yaw: float, kind: String, sink := 0.0) -> void:
	var position = Vector3(x, elevation(x,z) - sink, z)
	var pose = Transform3D(Basis(Vector3.UP, deg_to_rad(yaw)).scaled(Vector3.ONE * scale_value), position)
	placements.append({"asset":asset,"x":x,"z":z,"scale":scale_value,"yaw":yaw,"kind":kind})
	if kind == "grass" or kind == "detail":
		var chunk = Vector2i(floori(x / 8.0), floori(z / 8.0))
		var key = asset + "_" + str(chunk.x) + "_" + str(chunk.y)
		if not batches.has(key): batches[key] = {"asset":asset,"poses":[],"chunk":chunk}
		batches[key].poses.append(pose)
		return
	var instance = Node3D.new()
	instance.name = asset + "_" + str(parent.get_child_count()).pad_zeros(3)
	instance.transform = pose
	instance.set_meta("source_model", MODELS + asset + ".gltf")
	own(parent, instance)
	for entry in mesh_data(asset):
		var visual = MeshInstance3D.new()
		visual.mesh = entry.mesh
		visual.transform = entry.pose
		own(instance, visual)
	if kind in ["tree","rock"] and abs(x) < 15.5 and abs(z) < 15.5:
		var body = StaticBody3D.new()
		body.collision_layer = 1
		body.collision_mask = 0
		own(instance, body)
		var collision = CollisionShape3D.new()
		if kind == "tree":
			var shape = CylinderShape3D.new()
			shape.radius = trunk_radii[asset]
			shape.height = 3.2
			collision.shape = shape
			collision.position.y = 1.6
		else:
			var mesh: ArrayMesh = mesh_data(asset)[0].mesh
			var box = mesh.get_aabb()
			var shape = BoxShape3D.new()
			shape.size = box.size * Vector3(0.78,0.9,0.78)
			collision.shape = shape
			collision.position = box.get_center()
		own(body, collision)

func terrain() -> void:
	var node = Node3D.new()
	node.name = "Terrain"
	own(root,node)
	var tool = SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	var steps = 120
	var size = 80.0
	var grid: Array[Vector3] = []
	for iz in steps + 1:
		for ix in steps + 1:
			var x = -size * 0.5 + ix * size / steps
			var z = -size * 0.5 + iz * size / steps
			var p = Vector3(x,elevation(x,z),z)
			grid.append(p)
			var delta = 0.05
			var dx = (elevation(x+delta,z) - elevation(x-delta,z)) / (delta*2.0)
			var dz = (elevation(x,z+delta) - elevation(x,z-delta)) / (delta*2.0)
			tool.set_normal(Vector3(-dx,1,-dz).normalized())
			tool.set_uv(Vector2(x,z) * 0.25)
			tool.add_vertex(p)
	for iz in steps:
		for ix in steps:
			var a = iz * (steps+1) + ix
			for index in [a,a+1,a+steps+1,a+1,a+steps+2,a+steps+1]: tool.add_index(index)
	var mesh = tool.commit()
	var material = ShaderMaterial.new()
	material.shader = load(ASSETS + "Shaders/ground.gdshader")
	save_resource(material,ASSETS+"Materials/ground.tres")
	mesh.surface_set_material(0,material)
	save_resource(mesh,ASSETS+"terrain.res")
	var visual = MeshInstance3D.new()
	visual.name = "SculptedGround"
	visual.mesh = mesh
	own(node,visual)
	var body = StaticBody3D.new()
	body.name = "MatchingCollision"
	body.collision_layer = 1
	body.collision_mask = 0
	own(node,body)
	var shape = CollisionShape3D.new()
	shape.shape = mesh.create_trimesh_shape()
	own(body,shape)

func vegetation() -> void:
	var trees = Node3D.new()
	trees.name = "Trees"
	own(root,trees)
	# 手工安排主景的轮廓与遮挡，后景再用确定性散布补足林线。
	var authored = [
		["CommonTree_2",-8,3,1.4,24],["CommonTree_4",-9,-2,1.3,65],
		["CommonTree_1",-6,-9,1.0,130],["CommonTree_3",-13,-5,1.15,45],
		["CommonTree_5",-12,9,1.1,180],["CommonTree_3",-7,13,1.2,15],
		["CommonTree_1",7,3,1.25,210],["CommonTree_4",11,8,1.45,70],
		["CommonTree_2",10,-4,1.1,40],["CommonTree_5",8,-11,1.25,125],
		["CommonTree_1",-4,-15,0.9,80],["CommonTree_3",3,-17,1.0,230],
		["CommonTree_4",14,-15,1.2,170],["Pine_2",-16,-9,1.3,80],
		["Pine_3",-18,1,1.5,120],["Pine_1",17,0,1.3,20],
		["Pine_4",18,-11,1.35,90],["CommonTree_2",-13,-21,1.1,50],
		["CommonTree_5",7,-25,1.1,90],["CommonTree_4",0,-30,1.0,20],
		["Pine_3",-22,-20,1.2,140],["Pine_1",23,-19,1.4,160]]
	for entry in authored: place(trees,entry[0],entry[1],entry[2],entry[3],entry[4],"tree")
	for index in 52:
		var x = rng.randf_range(-36,36)
		var z = rng.randf_range(-37,26)
		if abs(x) < 19 and z > -23: continue
		var asset = ("Pine_" if index % 3 == 0 else "CommonTree_") + str(index % 5 + 1)
		place(trees,asset,x,z,rng.randf_range(0.85,1.5),rng.randf_range(0,360),"backdrop")
	var rocks = Node3D.new()
	rocks.name = "RockGroups"
	own(root,rocks)
	for entry in [[-11,-7,3.0,35],[-9,-11,2.4,85],[-14,-4,2.0,20],[-7,-5,1.15,90],
		[12,-10,2.4,120],[14,-14,2.0,240],[8,-13,1.4,10],[6,1,0.8,25],
		[-5,6,0.85,140],[10,11,1.0,250],[-8,11,0.8,100],[3,-22,2.0,80],
		[-22,-25,4.0,80],[18,-30,3.3,25]]:
		place(rocks,"Rock_Medium_"+str(rocks.get_child_count()%3+1),entry[0],entry[1],entry[2],entry[3],"rock",0.22)
	var plants = Node3D.new()
	plants.name = "Understory"
	own(root,plants)
	for center in [Vector2(-5,4),Vector2(5,6),Vector2(-5,-5),Vector2(6,-7),Vector2(-10,8),Vector2(11,-4),Vector2(-4,-12),Vector2(7,12)]:
		place(plants,"Bush_Common_Flowers",center.x,center.y,0.65,rng.randf_range(0,360),"bush")
		for index in 12:
			var p = center + Vector2(rng.randf_range(-2,2),rng.randf_range(-2,2))
			if not grass_allowed(p.x,p.y): continue
			var asset = ["Flower_3_Group","Flower_4_Group","Clover_1","Fern_1","Mushroom_Common","Plant_1"][index%6]
			var scale_value = 0.10 if asset == "Fern_1" else rng.randf_range(0.22,0.4)
			place(plants,asset,p.x,p.y,scale_value,rng.randf_range(0,360),"detail")
	for index in 95:
		var z = rng.randf_range(-21,15)
		var x = road_x(z) + rng.randf_range(-1.8,1.8)
		place(plants,"Pebble_Round_"+str(index%5+1),x,z,rng.randf_range(0.06,0.16),rng.randf_range(0,360),"detail")
	for entry in [[-4.7,8.5,0.17],[-6.2,2.2,0.16],[5.2,10.5,0.18],[4.2,-4.8,0.14],[-4.5,-10,0.14]]:
		place(plants,"Fern_1",entry[0],entry[1],entry[2],rng.randf_range(0,360),"detail")
	for center in [Vector2(-7.5,3.5),Vector2(7.1,2.7),Vector2(-5.8,6.5)]:
		for index in 3:
			place(plants,"Mushroom_Common",center.x+index*0.25,center.y+rng.randf_range(-0.3,0.3),rng.randf_range(0.24,0.38),rng.randf_range(0,360),"detail")
	# 高密度草采用八米区块的 MultiMesh；路面和战斗空地保留轮廓。
	for index in 26000:
		var x = rng.randf_range(-27,27)
		var z = rng.randf_range(-34,21)
		if not grass_allowed(x,z): continue
		if abs(x)>18 and rng.randf()>0.42: continue
		var scale_value = rng.randf_range(0.22,0.44)
		var asset = "Grass_Common_Short"
		if index % 7 == 0: asset = "Grass_Wispy_Short"
		if index % 13 == 0:
			asset = "Grass_Common_Tall"
			scale_value *= 0.9
		# 中央边缘的草更低，避免遮挡角色与攻击范围。
		if Vector2(x,z-2).length() < 6: scale_value *= 0.65
		place(plants,asset,x,z,scale_value,rng.randf_range(0,360),"grass")
	var grass_root = Node3D.new()
	grass_root.name = "FoliageChunks"
	own(root,grass_root)
	for key in batches:
		var data: Dictionary = batches[key]
		var chunk_origin = Vector3(data.chunk.x * 8.0 + 4.0,0,data.chunk.y * 8.0 + 4.0)
		for entry in mesh_data(data.asset):
			var multimesh = MultiMesh.new()
			multimesh.transform_format = MultiMesh.TRANSFORM_3D
			multimesh.mesh = entry.mesh
			multimesh.instance_count = data.poses.size()
			var bounds = AABB()
			for index in data.poses.size():
				var pose: Transform3D = data.poses[index] * entry.pose
				pose.origin -= chunk_origin
				multimesh.set_instance_transform(index,pose)
				var box: AABB = pose * entry.mesh.get_aabb()
				bounds = box if index == 0 else bounds.merge(box)
			multimesh.custom_aabb = bounds.grow(0.4)
			var batch = MultiMeshInstance3D.new()
			batch.name = key.validate_node_name()
			batch.position = chunk_origin
			batch.multimesh = multimesh
			batch.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			batch.visibility_range_end = 65.0 if data.asset.begins_with("Grass") else 50.0
			batch.visibility_range_end_margin = 5.0
			own(grass_root,batch)
		batch_count += 1

func lighting() -> void:
	var world = WorldEnvironment.new()
	world.name = "ArtLighting"
	var environment = Environment.new()
	var sky = Sky.new()
	var sky_material = ShaderMaterial.new()
	sky_material.shader = load(ASSETS + "Shaders/sky.gdshader")
	var noise = FastNoiseLite.new()
	noise.seed = SEED
	noise.noise_type = FastNoiseLite.TYPE_SIMPLEX_SMOOTH
	noise.frequency = 0.012
	noise.fractal_octaves = 5
	var clouds = NoiseTexture2D.new()
	clouds.width = 512
	clouds.height = 512
	clouds.seamless = true
	clouds.noise = noise
	sky_material.set_shader_parameter("cloud_noise",clouds)
	sky.sky_material = sky_material
	sky.radiance_size = Sky.RADIANCE_SIZE_128
	environment.background_mode = Environment.BG_SKY
	environment.sky = sky
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color("c0d8e3")
	environment.ambient_light_energy = 0.38
	environment.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	environment.tonemap_exposure = 1.0
	environment.tonemap_white = 4.0
	environment.ssao_enabled = true
	environment.ssao_radius = 1.2
	environment.ssao_intensity = 1.1
	environment.ssao_light_affect = 0.12
	environment.fog_enabled = true
	environment.fog_light_color = Color("cfdfc3")
	environment.fog_density = 0.0015
	environment.fog_sky_affect = 0.0
	environment.fog_height = -1.0
	environment.fog_height_density = 0.025
	world.environment = environment
	own(root,world)
	save_resource(environment,ASSETS+"Materials/environment.tres")
	var sun = DirectionalLight3D.new()
	sun.name = "WarmAfternoonSun"
	sun.rotation_degrees = Vector3(-42,-35,0)
	sun.light_color = Color("fff0ca")
	sun.light_energy = 1.3
	sun.light_angular_distance = 1.2
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 85
	sun.shadow_bias = 0.06
	sun.shadow_normal_bias = 0.8
	own(root,sun)

func markers_and_bounds() -> void:
	var markers = Node3D.new()
	markers.name = "CombatMarkers"
	own(root,markers)
	for entry in [["PlayerStart",Vector3(0,0.1,6)],["Center",Vector3(0,0,0)],["North",Vector3(0,0,-11)],["South",Vector3(0,0,11)],["West",Vector3(-10,0,0)],["East",Vector3(10,0,0)]]:
		var marker = Marker3D.new()
		marker.name = entry[0]
		marker.position = entry[1]
		marker.position.y += elevation(marker.position.x,marker.position.z)
		own(markers,marker)
	var views = Node3D.new()
	views.name = "ArtViews"
	own(root,views)
	for entry in [["Hero",Vector3(6,3.4,13),Vector3(-1,2,-6)],["Trail",Vector3(1,2.3,0),Vector3(-2,2,-14)]]:
		var camera = Camera3D.new()
		camera.name = entry[0]
		camera.position = entry[1]
		camera.fov = 63
		own(views,camera)
		camera.basis = Basis.looking_at(entry[2] - entry[1])
	var bounds = Node3D.new()
	bounds.name = "WorldBounds"
	own(root,bounds)
	for entry in [[Vector3(-15.7,5,0),Vector3(0.5,14,32)],[Vector3(15.7,5,0),Vector3(0.5,14,32)],[Vector3(0,5,-15.7),Vector3(32,14,0.5)],[Vector3(0,5,15.7),Vector3(32,14,0.5)]]:
		var body = StaticBody3D.new()
		body.position = entry[0]
		body.collision_layer = 1
		body.collision_mask = 0
		own(bounds,body)
		var collision = CollisionShape3D.new()
		var shape = BoxShape3D.new()
		shape.size = entry[1]
		collision.shape = shape
		own(body,collision)

func build() -> Dictionary:
	# 无图形后端的编辑器不会保留 MultiMesh 缓冲，必须使用正常渲染的编辑器生成。
	assert(DisplayServer.get_name() != "headless", "请在有图形渲染的 Godot 编辑器中生成草地实例")
	rng.seed = SEED
	root = Node3D.new()
	root.name = "NatureBenchmark"
	root.add_to_group("nav_geometry",true)
	root.set_meta("art_version","forest-clearing-v1")
	root.set_meta("playable_size_meters",30)
	root.set_meta("seed",SEED)
	terrain()
	vegetation()
	lighting()
	markers_and_bounds()
	var region = NavigationRegion3D.new()
	region.name = "Navigation"
	var navmesh = NavigationMesh.new()
	navmesh.agent_radius = 0.75
	navmesh.agent_height = 3
	navmesh.cell_size = 0.25
	navmesh.cell_height = 0.25
	navmesh.agent_max_slope = 30
	navmesh.geometry_parsed_geometry_type = NavigationMesh.PARSED_GEOMETRY_STATIC_COLLIDERS
	navmesh.geometry_collision_mask = 1
	navmesh.filter_baking_aabb = AABB(Vector3(-15.5,-3,-15.5),Vector3(31,18,31))
	region.navigation_mesh = navmesh
	own(root,region)
	EditorInterface.get_base_control().add_child(root)
	var geometry = NavigationMeshSourceGeometryData3D.new()
	NavigationServer3D.parse_source_geometry_data(navmesh,geometry,root)
	NavigationServer3D.bake_from_source_geometry_data(navmesh,geometry)
	save_resource(navmesh,ASSETS+"navigation.tres")
	for key in materials:
		save_resource(materials[key],ASSETS+"Materials/"+str(key).validate_filename()+".tres")
	var packed = PackedScene.new()
	assert(packed.pack(root) == OK)
	save_resource(packed,OUTPUT+"NatureBenchmark.tscn")
	var counts: Dictionary = {}
	for item in placements: counts[item.kind] = counts.get(item.kind,0) + 1
	var report = {"scene":OUTPUT+"NatureBenchmark.tscn","seed":SEED,"playable_size":30,"backdrop_size":80,"counts":counts,"foliage_batches":batch_count,"navigation_polygons":navmesh.get_polygon_count(),"navigation_agent_height":3.0,"navigation_agent_radius":0.75}
	var file = FileAccess.open("res://ArtSource/Tools/NatureArena/benchmark_build.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"  "))
	file.close()
	root.free()
	return report

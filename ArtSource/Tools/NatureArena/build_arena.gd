@tool
extends RefCounted

const OUTPUT = "res://Scenes/Levels/NatureArena/"
const ASSETS = "res://Assets/Environment/StylizedNature/"
var material_cache: Dictionary = {}
var prefab_cache: Dictionary = {}
var layout: Dictionary

func save_resource(resource: Resource, path: String) -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(path.get_base_dir()))
	assert(ResourceSaver.save(resource, path) == OK, "Could not save " + path)
	resource.take_over_path(path)

func save_scene(root: Node, path: String) -> void:
	var packed = PackedScene.new()
	assert(packed.pack(root) == OK)
	save_resource(packed, path)

func add_owned(parent: Node, child: Node, owner: Node) -> void:
	parent.add_child(child)
	child.owner = owner

func style_material(original: Material) -> Material:
	if not original is StandardMaterial3D:
		return original
	var key = original.resource_name.validate_filename()
	if material_cache.has(key):
		return material_cache[key]
	var result = original.duplicate() as StandardMaterial3D
	result.diffuse_mode = BaseMaterial3D.DIFFUSE_TOON
	result.specular_mode = BaseMaterial3D.SPECULAR_DISABLED
	result.roughness = 0.95
	result.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
	save_resource(result, ASSETS + "Materials/" + key + ".tres")
	material_cache[key] = result
	return result

func collect_meshes(node: Node, transform: Transform3D, output: Array) -> void:
	if node is Node3D:
		transform = transform * node.transform
	if node is MeshInstance3D:
		output.append({"node": node, "transform": transform})
	for child in node.get_children():
		collect_meshes(child, transform, output)

func prefab(asset: String, kind: String) -> PackedScene:
	if prefab_cache.has(asset):
		return prefab_cache[asset]
	var source = load(ASSETS + "Models/" + asset + ".gltf").instantiate()
	var meshes: Array = []
	collect_meshes(source, Transform3D.IDENTITY, meshes)
	var root = Node3D.new()
	root.name = asset
	root.set_meta("source_model", ASSETS + "Models/" + asset + ".gltf")
	var bounds = AABB()
	var first = true
	var trunk_radius = 0.38
	for item in meshes:
		var original: MeshInstance3D = item.node
		var visual = MeshInstance3D.new()
		visual.name = "Visual" + str(root.get_child_count())
		visual.mesh = original.mesh
		visual.transform = item.transform
		for surface in visual.mesh.get_surface_count():
			visual.set_surface_override_material(surface, style_material(original.get_active_material(surface)))
			if kind == "tree" and "Bark" in original.get_active_material(surface).resource_name:
				var vertices: PackedVector3Array = original.mesh.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]
				for vertex in vertices:
					var point: Vector3 = item.transform * vertex
					if point.y > 0.1 and point.y < 1.8:
						trunk_radius = max(trunk_radius, Vector2(point.x, point.z).length() * 0.85)
		add_owned(root, visual, root)
		var box: AABB = item.transform * original.mesh.get_aabb()
		bounds = box if first else bounds.merge(box)
		first = false
	if kind in ["tree", "rock"]:
		var body = StaticBody3D.new()
		body.name = "CollisionBody"
		body.collision_layer = 1
		body.collision_mask = 0
		add_owned(root, body, root)
		var collision = CollisionShape3D.new()
		collision.name = "Shape"
		if kind == "tree":
			var shape = CylinderShape3D.new()
			shape.radius = min(trunk_radius, 1.8)
			shape.height = min(4.0, bounds.size.y * 0.6)
			collision.shape = shape
			collision.position.y = shape.height * 0.5
		else:
			var shape = BoxShape3D.new()
			shape.size = bounds.size * Vector3(0.78, 0.9, 0.78)
			collision.shape = shape
			collision.position = bounds.get_center()
		add_owned(body, collision, root)
	save_scene(root, OUTPUT + "Prefabs/" + asset + ".tscn")
	root.free()
	source.free()
	var packed = load(OUTPUT + "Prefabs/" + asset + ".tscn") as PackedScene
	prefab_cache[asset] = packed
	return packed

func create_sector(sector: String) -> PackedScene:
	var root = Node3D.new()
	root.name = "Sector_" + sector
	var origin = Vector3(-30 if "W" in sector else 30, 0, -30 if "N" in sector else 30)
	var foliage: Dictionary = {}
	for item in layout.placements:
		if item.sector != sector:
			continue
		var asset: String = item.asset
		var packed = prefab(asset, item.kind)
		var pose = Transform3D(Basis(Vector3.UP, item.yaw).scaled(Vector3.ONE * item.scale), Vector3(item.x, item.y, item.z) - origin)
		if item.kind == "foliage":
			if not foliage.has(asset):
				foliage[asset] = []
			foliage[asset].append(pose)
			continue
		var instance = packed.instantiate() as Node3D
		instance.name = asset + "_" + str(root.get_child_count()).pad_zeros(3)
		instance.transform = pose
		instance.set_meta("placement_id", layout.placements.find(item))
		add_owned(root, instance, root)
	for asset in foliage:
		var packed: PackedScene = prefab_cache[asset]
		var temporary = packed.instantiate()
		for visual in temporary.get_children():
			if not visual is MeshInstance3D:
				continue
			var mesh = visual.mesh.duplicate() as ArrayMesh
			for surface in mesh.get_surface_count():
				mesh.surface_set_material(surface, visual.get_active_material(surface))
			var multimesh = MultiMesh.new()
			multimesh.transform_format = MultiMesh.TRANSFORM_3D
			multimesh.mesh = mesh
			multimesh.instance_count = foliage[asset].size()
			for index in foliage[asset].size():
				multimesh.set_instance_transform(index, foliage[asset][index] * visual.transform)
			var batch = MultiMeshInstance3D.new()
			batch.name = "Foliage_" + asset
			batch.multimesh = multimesh
			batch.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			add_owned(root, batch, root)
		temporary.free()
	save_scene(root, OUTPUT + "Sectors/" + sector + ".tscn")
	root.free()
	return load(OUTPUT + "Sectors/" + sector + ".tscn")

func add_box(parent: Node, owner: Node, name: String, position: Vector3, size: Vector3) -> void:
	var body = StaticBody3D.new()
	body.name = name
	body.position = position
	body.collision_layer = 1
	body.collision_mask = 0
	add_owned(parent, body, owner)
	var shape = BoxShape3D.new()
	shape.size = size
	var collision = CollisionShape3D.new()
	collision.name = "Shape"
	collision.shape = shape
	add_owned(body, collision, owner)

func build() -> Dictionary:
	layout = JSON.parse_string(FileAccess.get_file_as_string("res://ArtSource/Blender/NatureArena/layout.json"))
	var root = Node3D.new()
	root.name = "NatureArena"
	root.add_to_group("nav_geometry", true)
	root.set_meta("layout_seed", layout.seed)
	root.set_meta("arena_size_meters", 120)
	var ground = Node3D.new()
	ground.name = "Terrain"
	add_owned(root, ground, root)
	add_box(ground, root, "GroundCollision", Vector3(0,-0.5,0), Vector3(120,1,120))
	var ground_mesh = PlaneMesh.new()
	ground_mesh.size = Vector2(120,120)
	var ground_material = ShaderMaterial.new()
	ground_material.shader = load(ASSETS + "arena_ground.gdshader")
	save_resource(ground_material, ASSETS + "Materials/arena_ground.tres")
	ground_mesh.material = ground_material
	var surface = MeshInstance3D.new()
	surface.name = "GroundSurface"
	surface.mesh = ground_mesh
	add_owned(ground, surface, root)
	var chunks = Node3D.new()
	chunks.name = "Sectors"
	add_owned(root, chunks, root)
	for sector in ["NW","NE","SW","SE"]:
		var chunk = create_sector(sector).instantiate()
		chunk.position = Vector3(-30 if "W" in sector else 30, 0, -30 if "N" in sector else 30)
		add_owned(chunks, chunk, root)
	var walls = Node3D.new()
	walls.name = "WorldBounds"
	add_owned(root, walls, root)
	add_box(walls,root,"North",Vector3(0,3,-59.5),Vector3(120,6,1))
	add_box(walls,root,"South",Vector3(0,3,59.5),Vector3(120,6,1))
	add_box(walls,root,"West",Vector3(-59.5,3,0),Vector3(1,6,120))
	add_box(walls,root,"East",Vector3(59.5,3,0),Vector3(1,6,120))
	var markers = Node3D.new()
	markers.name = "CombatMarkers"
	add_owned(root, markers, root)
	for entry in [["PlayerStart",Vector3(0,0.08,8)],["Center",Vector3(0,0,0)],["North",Vector3(0,0,-34)],["South",Vector3(0,0,34)],["West",Vector3(-34,0,0)],["East",Vector3(34,0,0)]]:
		var marker = Marker3D.new()
		marker.name = entry[0]
		marker.position = entry[1]
		marker.add_to_group("nature_combat_markers",true)
		add_owned(markers,marker,root)
	var world = WorldEnvironment.new()
	world.name = "Lighting"
	var environment = Environment.new()
	var sky = Sky.new()
	var sky_material = ProceduralSkyMaterial.new()
	sky_material.sky_top_color = Color("7aaac2")
	sky_material.sky_horizon_color = Color("d1e2cb")
	sky_material.ground_horizon_color = Color("d1e2cb")
	sky.sky_material = sky_material
	environment.background_mode = Environment.BG_SKY
	environment.sky = sky
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color("d1dddf")
	environment.ambient_light_energy = 0.65
	environment.fog_enabled = true
	environment.fog_light_color = Color("b9d1cc")
	environment.fog_density = 0.0012
	world.environment = environment
	add_owned(root,world,root)
	var sun = DirectionalLight3D.new()
	sun.name = "Sun"
	sun.rotation_degrees = Vector3(-47,-32,0)
	sun.light_color = Color("fff0cb")
	sun.light_energy = 1.25
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 95
	add_owned(root,sun,root)
	var navigation = NavigationRegion3D.new()
	navigation.name = "Navigation"
	var navmesh = NavigationMesh.new()
	navmesh.agent_radius = 0.75
	navmesh.agent_height = 2.0
	navmesh.cell_size = 0.25
	navmesh.cell_height = 0.25
	navmesh.agent_max_slope = 30
	navmesh.geometry_parsed_geometry_type = NavigationMesh.PARSED_GEOMETRY_STATIC_COLLIDERS
	navmesh.geometry_collision_mask = 1
	navigation.navigation_mesh = navmesh
	add_owned(root,navigation,root)
	EditorInterface.get_base_control().add_child(root)
	var geometry = NavigationMeshSourceGeometryData3D.new()
	NavigationServer3D.parse_source_geometry_data(navmesh,geometry,root)
	NavigationServer3D.bake_from_source_geometry_data(navmesh,geometry)
	save_resource(navmesh,ASSETS+"arena_navigation.tres")
	save_scene(root,OUTPUT+"NatureArena.tscn")
	var report = {"placements":layout.placements.size(),"prefabs":prefab_cache.size(),"materials":material_cache.size(),"navigation_polygons":navmesh.get_polygon_count(),"size_meters":120,"scene":OUTPUT+"NatureArena.tscn"}
	root.free()
	return report

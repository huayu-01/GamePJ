extends Node
## 只给森林样板中的三只敌人重设上色，用同一组调色板保留各自造型。

const PAINT = preload("res://Assets/Materials/EnemyAnime/anime_paint.gdshader")
const OUTLINE = preload("res://Assets/Materials/EnemyAnime/anime_outline.gdshader")
const PALETTE_PATH = "res://Assets/Materials/EnemyAnime/palette.json"
const SHROOM_REGIONS = preload("res://Assets/Materials/EnemyAnime/shroom_regions.png")
var _records: Array[Dictionary] = []
var _cache: Dictionary = {}
var enabled: bool = true
var styled_surfaces: int = 0
var _label: Label

func _ready() -> void:
	enabled = not OS.get_cmdline_user_args().has("--enemy-art-original")
	await get_tree().process_frame
	var palette: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(PALETTE_PATH))
	for path in ["EnemyModelShowcase/ScaredyShroom", "EnemyModelShowcase/GhostMage", "StoneSlammerShowcase/StoneSlammer/Model"]:
		var root = get_parent().get_node_or_null(path)
		if root == null:
			continue
		for node in root.find_children("*", "MeshInstance3D", true, false):
			var mesh = node as MeshInstance3D
			if mesh.mesh == null:
				continue
			for surface in range(mesh.mesh.get_surface_count()):
				var previous: Material = mesh.get_surface_override_material(surface)
				var source: Material = previous if previous != null else mesh.mesh.surface_get_material(surface)
				if not source is StandardMaterial3D:
					continue
				var key: String = source.resource_name.get_slice(".",0)
				if not palette.has(key):
					continue
				var material = _material_for(key, palette[key])
				_records.append({"mesh":mesh,"surface":surface,"previous":previous,"paint":material})
	styled_surfaces = _records.size()
	_set_enabled(enabled)
	var canvas = CanvasLayer.new()
	canvas.name = "AnimeStyleHelp"
	add_child(canvas)
	_label = Label.new()
	_label.position = Vector2(1420, 132)
	_label.add_theme_font_size_override("font_size",20)
	_label.add_theme_constant_override("outline_size",6)
	_label.add_theme_color_override("font_outline_color",Color(0.03,0.03,0.05))
	canvas.add_child(_label)
	_update_label()
	print("[EnemyAnime] surfaces=",styled_surfaces," enabled=",enabled)

func _material_for(key: String, colors: Dictionary) -> ShaderMaterial:
	if _cache.has(key):
		return _cache[key]
	var material = ShaderMaterial.new()
	material.resource_name = "Anime_" + key
	material.shader = PAINT
	material.set_shader_parameter("paint_color",Color(colors.lit))
	material.set_shader_parameter("shade_color",Color(colors.shade))
	material.set_shader_parameter("light_color",Color(colors.high))
	material.set_shader_parameter("brush_amount",colors.get("brush",0.035))
	material.set_shader_parameter("emissive_amount",colors.get("emission",0.0))
	material.set_shader_parameter("edge_light",colors.get("rim",0.045))
	if colors.get("mushroom",false):
		material.set_shader_parameter("mushroom_repaint",true)
		material.set_shader_parameter("region_texture",SHROOM_REGIONS)
	if colors.get("outline",true):
		var outline = ShaderMaterial.new()
		outline.shader = OUTLINE
		outline.set_shader_parameter("ink_color",Color("#292338"))
		outline.set_shader_parameter("ink_width",0.006 if key.begins_with("Ghost") else 0.007)
		material.next_pass = outline
	_cache[key] = material
	return material

func _set_enabled(value: bool) -> void:
	enabled = value
	for record in _records:
		if is_instance_valid(record.mesh):
			record.mesh.set_surface_override_material(record.surface, record.paint if enabled else record.previous)
	_update_label()

func _update_label() -> void:
	if is_instance_valid(_label):
		_label.text = "F12 上色对比：" + ("动漫色块" if enabled else "原材质")

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo and event.physical_keycode == KEY_F12:
		_set_enabled(not enabled)
		get_viewport().set_input_as_handled()

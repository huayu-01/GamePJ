extends Node3D
## 独立石头人 Plus 外观样件：上色、骨骼动作和森林展示镜头。

const PAINT = preload("res://Assets/Materials/EnemyAnime/anime_paint.gdshader")
const OUTLINE = preload("res://Assets/Enemies/StoneGolemPlus/golem_outline.gdshader")
@export var showcase_controls: bool = false
var player: AnimationPlayer
var styled_surfaces: int = 0
var _camera: Camera3D
var _view: int = -1
var _busy: bool = false

func _ready() -> void:
	player = find_children("*", "AnimationPlayer", true, false)[0] as AnimationPlayer
	for clip in ["idle", "walk"]:
		player.get_animation(clip).loop_mode = Animation.LOOP_LINEAR
	player.play("idle")
	_apply_palette()
	if not showcase_controls:
		return
	_camera = Camera3D.new()
	_camera.name = "GolemPlusCamera"
	_camera.fov = 43.0
	add_child(_camera)
	var canvas = CanvasLayer.new()
	canvas.name = "GolemPlusHelp"
	add_child(canvas)
	var label = Label.new()
	label.text = "石头人 Plus\nF3 近景 / 侧面 / 跟随\nF4 空手重砸"
	label.position = Vector2(1420, 184)
	label.add_theme_font_size_override("font_size", 20)
	label.add_theme_constant_override("outline_size", 6)
	label.add_theme_color_override("font_outline_color", Color("#202436"))
	canvas.add_child(label)
	await get_tree().physics_frame
	var query = PhysicsRayQueryParameters3D.create(global_position + Vector3.UP * 10.0, global_position + Vector3.DOWN * 10.0, 1)
	var hit = get_world_3d().direct_space_state.intersect_ray(query)
	if not hit.is_empty():
		global_position.y = hit.position.y + 0.025
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--golem-plus-capture="):
			_capture(argument.get_slice("=", 1))

func _apply_palette() -> void:
	var palette = {
		"GolemPlus_Stone": ["#858890", "#3c4558", "#b3b7c1"],
		"GolemPlus_Sockets": ["#252c3a", "#161d2b", "#394258"],
		"GolemPlus_Eyes": ["#ffd477", "#f5bd53", "#ffe4a1"],
	}
	var materials: Dictionary = {}
	for node in find_children("*", "MeshInstance3D", true, false):
		var mesh = node as MeshInstance3D
		for surface in range(mesh.mesh.get_surface_count()):
			var source = mesh.mesh.surface_get_material(surface)
			var key: String = source.resource_name.get_slice(".", 0)
			if not palette.has(key):
				continue
			if not materials.has(key):
				var material = ShaderMaterial.new()
				material.shader = PAINT
				material.set_shader_parameter("paint_color", Color(palette[key][0]))
				material.set_shader_parameter("shade_color", Color(palette[key][1]))
				material.set_shader_parameter("light_color", Color(palette[key][2]))
				material.set_shader_parameter("brush_amount", 0.0)
				material.set_shader_parameter("edge_light", 0.015)
				if key == "GolemPlus_Eyes":
					material.set_shader_parameter("emissive_amount", 0.35)
				else:
					var ink = ShaderMaterial.new()
					ink.shader = OUTLINE
					ink.set_shader_parameter("ink_color", Color("#252c3b"))
					ink.set_shader_parameter("ink_width", 0.004)
					material.next_pass = ink
				materials[key] = material
			mesh.set_surface_override_material(surface, materials[key])
			styled_surfaces += 1

func demonstrate() -> void:
	if _busy:
		return
	_busy = true
	player.play("slam")
	await player.animation_finished
	player.play("idle")
	_busy = false

func _unhandled_input(event: InputEvent) -> void:
	if not showcase_controls or not event is InputEventKey or not event.pressed or event.echo:
		return
	if event.physical_keycode == KEY_F3:
		_view = (_view + 1) % 3
		_set_view(_view)
		get_viewport().set_input_as_handled()
	elif event.physical_keycode == KEY_F4:
		demonstrate()
		get_viewport().set_input_as_handled()

func _set_view(index: int) -> void:
	if index == 2:
		get_parent().get_node("Player/CameraRig/SpringArm/Camera").make_current()
		return
	var target = global_position + Vector3.UP * 1.45
	_camera.global_position = target + (Vector3(3.7, 1.3, 6.9) if index == 0 else Vector3(6.8, 1.1, 1.8))
	_camera.look_at(target)
	_camera.make_current()
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE

func _sample(clip: String, time: float) -> void:
	player.play(clip)
	player.seek(time, true)
	player.advance(0)
	player.pause()

func _capture(view: String) -> void:
	for frame in range(90):
		await get_tree().physics_frame
	var skeleton = find_children("*", "Skeleton3D", true, false)[0] as Skeleton3D
	var clips_ok: bool = true
	for clip in ["idle", "walk", "slam", "hit", "die"]:
		clips_ok = clips_ok and player.has_animation(clip)
	_sample("idle", 0.0)
	await get_tree().process_frame
	var arm_index: int = skeleton.find_bone("RightUpperArm")
	var rest = skeleton.get_bone_pose_rotation(arm_index)
	_sample("slam", 0.42)
	await get_tree().process_frame
	await get_tree().process_frame
	var arm_delta: float = rest.angle_to(skeleton.get_bone_pose_rotation(arm_index))
	await demonstrate()
	var returned: bool = player.current_animation == "idle" and player.is_playing()
	var report = {"passed": clips_ok and skeleton.get_bone_count() >= 15 and arm_delta > 0.2 and returned and styled_surfaces > 0,
		"animations_present": clips_ok, "bones": skeleton.get_bone_count(), "styled_surfaces": styled_surfaces,
		"slam_arm_delta_radians": arm_delta, "demo_returned_to_idle": returned,
		"scope": "混元生成、Blender 整理及绑定的独立外观样件，未接正式敌人战斗"}
	var file = FileAccess.open("res://docs/screenshots/stone_golem_plus_validation.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(report, "  "))
	file.close()
	for layer in get_parent().find_children("*", "CanvasLayer", true, false):
		layer.visible = false
	# 单件截图暂时隐藏其他样件，避免前景角色挡住脚部和动作。
	for path in ["StoneSlammerShowcase", "EnemyModelShowcase", "Player"]:
		get_parent().get_node(path).visible = false
	_set_view(1 if view == "side" else 0)
	_sample("slam" if view in ["windup", "impact"] else ("walk" if view == "walk" else "idle"),
		0.42 if view == "windup" else (0.62 if view == "impact" else (0.3 if view == "walk" else 0.0)))
	for frame in range(24):
		await RenderingServer.frame_post_draw
	var path = "res://docs/screenshots/stone_golem_plus_" + view + ".png"
	var result = get_viewport().get_texture().get_image().save_png(path)
	print("[GolemPlus] ", JSON.stringify(report), " screenshot=", result, " path=", path)
	get_tree().call_deferred("quit", 0 if result == OK and report.passed else 1)

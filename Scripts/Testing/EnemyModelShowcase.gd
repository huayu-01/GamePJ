extends Node3D
## 在既有森林美术样板中预览两个模型，镜头和动作只作用于本样件。

var _camera: Camera3D
var _view: int = -1
var _hud: CanvasLayer
@onready var _mushroom: EnemyVisualPreview = $ScaredyShroom
@onready var _mage: EnemyVisualPreview = $GhostMage

func _ready() -> void:
	_camera = Camera3D.new()
	_camera.name = "EnemyArtCamera"
	_camera.fov = 48
	_camera.far = 150
	add_child(_camera)
	_hud = CanvasLayer.new()
	_hud.name = "EnemyArtHelp"
	add_child(_hud)
	var help = Label.new()
	help.position = Vector2(1420, 30)
	help.text = "蘑菇与小法师\nF10 切换预览镜头\nF11 演示缩地与施法"
	help.add_theme_font_size_override("font_size", 20)
	help.add_theme_constant_override("outline_size", 6)
	help.add_theme_color_override("font_outline_color", Color(0.03, 0.04, 0.03))
	_hud.add_child(help)
	await get_tree().physics_frame
	for model in [_mushroom, _mage]:
		var query = PhysicsRayQueryParameters3D.create(model.global_position + Vector3.UP * 10, model.global_position + Vector3.DOWN * 10, 1)
		var hit = get_world_3d().direct_space_state.intersect_ray(query)
		if not hit.is_empty():
			model.global_position.y = hit.position.y + 0.025
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--enemy-art-capture="):
			_capture(argument.get_slice("=", 1))

func _unhandled_input(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo:
		return
	if event.physical_keycode == KEY_F10:
		_view = (_view + 1) % 4
		_set_view(_view)
		get_viewport().set_input_as_handled()
	elif event.physical_keycode == KEY_F11:
		_mushroom.demonstrate()
		_mage.demonstrate()
		get_viewport().set_input_as_handled()

func _set_view(index: int) -> void:
	if index == 3:
		get_parent().get_node("Player/CameraRig/SpringArm/Camera").make_current()
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
		return
	var target: Vector3
	if index == 0:
		target = (_mushroom.global_position + _mage.global_position) * 0.5 + Vector3.UP * 0.9
		_camera.global_position = target + Vector3(3.1, 1.7, 7.0)
	elif index == 1:
		target = _mushroom.global_position + Vector3.UP * 0.82
		_camera.global_position = target + Vector3(1.9, 0.65, 3.8)
	else:
		target = _mage.global_position + Vector3.UP * 0.95
		_camera.global_position = target + Vector3(1.9, 0.65, 3.8)
	_camera.look_at(target)
	_camera.make_current()
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE

func _sample(player: AnimationPlayer, clip: String, time: float) -> void:
	player.play(clip)
	player.seek(time, true)
	player.advance(0)
	player.pause()

func _validate() -> Dictionary:
	var shroom_player: AnimationPlayer = _mushroom.player
	var mage_player: AnimationPlayer = _mage.player
	var shroom_skeleton = _mushroom.find_children("*", "Skeleton3D", true, false)[0] as Skeleton3D
	var mage_skeleton = _mage.find_children("*", "Skeleton3D", true, false)[0] as Skeleton3D
	var clips_ok: bool = true
	for clip in ["idle", "burrow", "emerge", "shoot"]:
		clips_ok = clips_ok and shroom_player.has_animation(clip)
	for clip in ["idle", "cast"]:
		clips_ok = clips_ok and mage_player.has_animation(clip)
	_sample(mage_player, "idle", 0)
	await get_tree().process_frame
	var grip_rest = mage_skeleton.get_bone_pose_rotation(mage_skeleton.find_bone("Grip"))
	_sample(shroom_player, "burrow", 0.60)
	_sample(mage_player, "cast", 0.50)
	await get_tree().process_frame
	await get_tree().process_frame
	var sunk_y = shroom_skeleton.get_bone_pose_position(shroom_skeleton.find_bone("Root")).y
	var grip_angle = grip_rest.angle_to(mage_skeleton.get_bone_pose_rotation(mage_skeleton.find_bone("Grip")))
	var passed: bool = clips_ok and shroom_skeleton.get_bone_count() == 4 and mage_skeleton.get_bone_count() == 7 and sunk_y < -0.7 and grip_angle > 0.05
	var report = {"passed":passed, "animations_present":clips_ok, "mushroom_bones":shroom_skeleton.get_bone_count(), "mage_bones":mage_skeleton.get_bone_count(), "burrow_root_y":sunk_y, "cast_grip_delta_radians":grip_angle, "scope":"外观模型与演示动作，未接生产敌人战斗"}
	_sample(shroom_player, "idle", 0)
	_sample(mage_player, "idle", 0)
	# 走与 F11 相同的完整演示入口，确认缩地／出土和施法都能回到待机。
	_mage.demonstrate()
	await _mushroom.demonstrate()
	var returned_to_idle: bool = shroom_player.current_animation == "idle" and mage_player.current_animation == "idle" and shroom_player.is_playing() and mage_player.is_playing()
	report["demo_cycle_returned_to_idle"] = returned_to_idle
	report["passed"] = passed and returned_to_idle
	_sample(shroom_player, "idle", 0)
	_sample(mage_player, "idle", 0)
	return report

func _capture(view: String) -> void:
	for frame in range(90):
		await get_tree().physics_frame
	var report: Dictionary = await _validate()
	var style = get_parent().get_node_or_null("EnemyAnimePreview")
	if style != null:
		report["anime_style_enabled"] = style.enabled
		report["styled_surfaces"] = style.styled_surfaces
		report["passed"] = report.passed and style.styled_surfaces > 0
	var file = FileAccess.open("res://docs/screenshots/enemy_models_validation.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(report, "  "))
	file.close()
	for layer in get_parent().find_children("*", "CanvasLayer", true, false):
		layer.visible = false
	_set_view(0 if view == "pair" else (1 if view.begins_with("mushroom") else 2))
	if view == "mushroom-burrow":
		_sample(_mushroom.player, "burrow", 0.28)
	elif view == "mage-cast":
		_sample(_mage.player, "cast", 0.50)
	for frame in range(24):
		await RenderingServer.frame_post_draw
	var suffix: String = "_original" if OS.get_cmdline_user_args().has("--enemy-art-original") else ""
	var path = "res://docs/screenshots/enemy_models_" + view + suffix + ".png"
	var result = get_viewport().get_texture().get_image().save_png(path)
	print("[EnemyArt] ", JSON.stringify(report), " screenshot=",result," path=",path)
	# 通过现有退出入口延后一帧清理，避免在渲染回调中销毁视口。
	get_tree().call_deferred("quit", 0 if result == OK and report.passed else 1)

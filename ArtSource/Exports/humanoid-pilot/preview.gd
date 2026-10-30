extends Node3D

var player: AnimationPlayer
var clips: PackedStringArray
var caption: Label

func _ready() -> void:
	var model = load("res://humanoid-pilot.glb").instantiate()
	add_child(model)
	for item in model.find_children("*", "AnimationPlayer", true, false):
		player = item
	clips = player.get_animation_list()
	var environment = WorldEnvironment.new()
	environment.environment = Environment.new()
	environment.environment.background_mode = Environment.BG_COLOR
	environment.environment.background_color = Color(0.13, 0.16, 0.19)
	environment.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.environment.ambient_light_color = Color(0.85, 0.9, 1.0)
	environment.environment.ambient_light_energy = 0.7
	add_child(environment)
	var light = DirectionalLight3D.new()
	light.rotation_degrees = Vector3(-40, -35, 0)
	light.light_energy = 1.5
	add_child(light)
	var camera = Camera3D.new()
	camera.position = Vector3(2.3, 1.65, 3.4)
	add_child(camera)
	camera.look_at(Vector3(0, 0.95, 0))
	camera.current = true
	caption = Label.new()
	caption.position = Vector2(20, 20)
	add_child(caption)
	play_clip("Walk")

func play_clip(fragment: String) -> void:
	for clip in clips:
		if fragment in clip:
			player.get_animation(clip).loop_mode = Animation.LOOP_LINEAR
			player.play(clip)
			caption.text = "1: Idle   2: Walk   3: Joint check\n" + clip
			return

func _unhandled_key_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		match event.keycode:
			KEY_1: play_clip("Idle")
			KEY_2: play_clip("Walk")
			KEY_3: play_clip("JointCheck")

class_name EnemyVisualPreview
extends Node3D
## 非人形外观样件的动作演示，不参与敌人战斗结算。

@export_enum("mushroom", "mage") var preview_kind: String = "mushroom"
var player: AnimationPlayer
var _busy: bool = false

func _ready() -> void:
	player = find_children("*", "AnimationPlayer", true, false)[0] as AnimationPlayer
	player.get_animation("idle").loop_mode = Animation.LOOP_LINEAR
	player.play("idle")

func demonstrate() -> void:
	if _busy:
		return
	_busy = true
	player.play("burrow" if preview_kind == "mushroom" else "cast")
	await player.animation_finished
	if preview_kind == "mushroom":
		await get_tree().create_timer(0.25).timeout
		player.play("emerge")
		await player.animation_finished
	player.play("idle")
	_busy = false

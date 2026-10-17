extends Node3D
## 一次性特效的「独立生命周期」外壳。
##
## 为什么需要它：`Scenes/Combat/Explosion.tscn` 的 `Explosion.Lifetime` 只有 0.12s，
## 而且 `AttackHitbox._PhysicsProcess` 会在这段时间里持续判定命中 —— 也就是说
## **这个时长是玩法窗口，不是美术参数，不能为了看清特效去改它**。
## 但 Binbun 的爆炸特效 `main` 动画有 2.6s，做成子节点会在第 0.12s 被连根删掉。
##
## 做法：出生当帧（`call_deferred`，此时宿主已经摆好位置/缩放）把自己
## `reparent()` 到场景根并保留世界变换，于是宿主被 QueueFree 时特效不受影响，
## 播完之后再自毁。**零 C# 改动、零玩法参数改动。**
##
## 用法：`FX`(挂本脚本) / `Vfx`(特效实例) 两层，见 `Scenes/Combat/Explosion.tscn`。

## 动画播完后再多留一点，避免粒子尾部被切掉。
@export var extra_life: float = 0.4

## 找不到 AnimationPlayer / main 动画时用这个时长兜底（秒）。
@export var fallback_life: float = 3.0

## 美术追加倍率（在继承来的宿主缩放之上再乘一次）。
@export var fx_scale: float = 1.0

func _ready() -> void:
	call_deferred("_detach")

func _detach() -> void:
	var holder := get_parent()
	var root := get_tree().current_scene
	if holder == null or root == null or holder == root:
		return
	# 显式搬运世界变换：`reparent(..., true)` 在本项目（physics_interpolation=true）下
	# 实测会把缩放丢掉，所以这里自己存/自己写。
	var gt := global_transform
	reparent(root, false)
	global_transform = gt
	scale = scale * fx_scale
	_self_destruct()

func _self_destruct() -> void:
	var life := fallback_life
	var ap := find_child("AnimationPlayer", true, false)
	if ap is AnimationPlayer and ap.has_animation("main"):
		life = (ap as AnimationPlayer).get_animation("main").length
	# 原生信号调用原生 queue_free，不保留会被深度资源重载取消的脚本协程。
	var cleanup := Timer.new()
	cleanup.one_shot = true
	add_child(cleanup)
	cleanup.timeout.connect(Callable(self, "queue_free"), CONNECT_ONE_SHOT)
	cleanup.start(maxf(0.001, life + extra_life))

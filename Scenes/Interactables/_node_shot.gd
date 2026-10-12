extends Node3D
## 节点模型 / 状态动画拍摄台（用户需求 §五 #15：节点换模型 + 悬浮 + 待机/激活/失效）。
##
## 用法：Godot.exe --path <项目> res://Scenes/Interactables/_NodeShowcase.tscn
## 输出：res://docs/screenshots/node_4x_*.png + 一份逐项读数日志（Float.y 采样、当前动画、transparency）。
##
## 它**只读/只播表现**：三个节点都是真的 `RewardNode.tscn`，"激活"那一个靠**真的 Player 站在范围内**
## （走真的 `PlayerInteractInput` / `PlayerLocator`），"失效"那一个只是把 `Enabled` 置 false
## （与 `StageController` 关掉没抽中的节点、降临物下落中同一条路径）。

const NODE_SCENE := "res://Scenes/Interactables/RewardNode.tscn"
const PLAYER_SCENE := "res://Scenes/Player/Player.tscn"
const SHOTS := "res://docs/screenshots/"

var _idle: Node3D
var _active: Node3D
var _dead: Node3D
var _player: Node3D
var _cam: Camera3D

func _ready() -> void:
	await _frames(40)

	_idle = _spawn_node(Vector3(-4.0, 0.0, 0.0), "待机")
	_active = _spawn_node(Vector3(0.0, 0.0, 0.0), "激活")
	_dead = _spawn_node(Vector3(4.0, 0.0, 0.0), "失效")
	_dead.Enabled = false

	# 真的玩家：站在中间那个节点 2.4 米处（交互半径 3.2），于是它 CanInteract=true → active。
	_player = (load(PLAYER_SCENE) as PackedScene).instantiate()
	add_child(_player)
	_player.global_position = Vector3(0.0, 0.6, 2.4)
	if _player.has_node("CameraRig"):
		var rig := _player.get_node("CameraRig") as Node
		rig.process_mode = Node.PROCESS_MODE_DISABLED

	_cam = $Camera
	_cam.current = true

	await _frames(40)

	var mesh: MeshInstance3D = _active.get_node("Float/Mesh")
	var aabb := mesh.get_aabb()
	var surface_mat := mesh.mesh.surface_get_material(0)
	var override_mat := mesh.get_surface_override_material(0)
	print("[NodeShot] 模型：网格=", mesh.mesh.resource_path,
		" 局部 AABB min=", _v(aabb.position), " size=", _v(aabb.size),
		"（**轴心在 min.y=", "%.4f" % aabb.position.y, " → 脚底**）",
		" 场景缩放=", _v(mesh.scale), " 悬浮基准 y=", mesh.position.y)
	print("[NodeShot] 材质：面材质=", (surface_mat.resource_path if surface_mat != null else "（无）"),
		" 实例覆盖（按类型上色）=", (override_mat.resource_path if override_mat != null else "（无）"),
		" material_override=", (mesh.material_override.resource_path if mesh.material_override != null else "（无）"),
		" transparency=", "%.2f" % mesh.transparency)
	print("[NodeShot] 交互状态读数：待机[CanInteract=", _idle.CanInteract, " Enabled=", _idle.Enabled,
		" DemoState=", _idle.DebugStateAnimation, "]",
		" 激活[CanInteract=", _active.CanInteract, " 玩家距离=", "%.2f" % _player.global_position.distance_to(_active.global_position),
		" DemoState=", _active.DebugStateAnimation, "]",
		" 失效[CanInteract=", _dead.CanInteract, " Enabled=", _dead.Enabled, " DemoState=", _dead.DebugStateAnimation, "]")

	# ── ① 悬浮 + 状态动画读数（各采样 1.5 秒 / 约 90 帧）──
	await _sample("待机", _idle)
	await _sample("激活", _active)
	await _sample("失效", _dead)

	# ── ② 状态切换（证明状态机是活的：同一节点 idle → dead → idle）──
	await _log_state("把 Enabled 置 false", _idle, false)
	await _log_state("把 Enabled 置回 true", _idle, true)

	# ── ③ 截图：一张全景 + 三张近景 ──
	await _shoot_from(Vector3(0, 2.6, -7.5), Vector3(0, 1.1, 0), "node_40_states.png")
	await _shoot_close(_idle, "node_41_idle.png")
	await _shoot_close(_active, "node_42_active.png")
	await _shoot_close(_dead, "node_43_dead.png")

	# ── ④ 三档**光圈颜色**（= 价值程度：低绿 / 中青蓝 / 高金）──
	# Kind：3 = Mutation（tag_high 金）、1 = Enhancement（tag_mid 青蓝）、4 = Resource（tag_low 绿）
	_idle.Kind = 3
	_active.Kind = 1
	_dead.Kind = 4
	_dead.Enabled = true
	await _frames(20)
	print("[NodeShot] ── 三档光圈（颜色来自 Config/Tables/ValueTags/tag_*.tres 的 AuraColor 列）──")
	for node in [_idle, _active, _dead]:
		_aura_readout(node)
	await _shoot_from(Vector3(0.0, 4.0, -9.0), Vector3(0.0, 1.0, 0.0), "node_45_aura_tiers.png")

	# ── ⑤ 失效节点（!Enabled）的光圈**要变暗**：同一个 Kind 并排对比 ──
	_idle.Kind = 4
	_dead.Kind = 4
	_dead.Enabled = false
	_active.visible = false
	await _frames(20)
	print("[NodeShot] ── 失效对比（左 x=-4 可用 / 右 x=+4 Enabled=false）──")
	_aura_readout(_idle)
	_aura_readout(_dead)
	await _shoot_from(Vector3(0.0, 4.2, -10.0), Vector3(0.0, 1.0, 0.0), "node_46_aura_dead.png")

	# ── ⑥ 9 种类型总览：形状都不一样（挑形状的依据见 docs/art-onboarding.md §三.1.3）──
	_active.visible = true
	for node in [_idle, _active, _dead]:
		node.visible = false
	# 玩家挪到画面外（只隐藏不够：他身上带的光/特效会落在网格中间，把中间那个节点的光圈照白）
	_player.visible = false
	_player.global_position = Vector3(0.0, 0.6, 30.0)
	var spawned: Array[Node3D] = []
	for i in range(9):
		var node: Node3D = _spawn_node(Vector3((i % 3 - 1) * 4.5, 0.0, (i / 3 - 1) * 4.0), "Kind=%d" % i)
		node.Kind = i
		spawned.append(node)
	await _frames(20)
	print("[NodeShot] ── 9 种类型（Kind 0..8 = Weapon…Portal）──")
	for node in spawned:
		_aura_readout(node)
	await _shoot_from(Vector3(0.0, 5.5, 9.0), Vector3(0.0, 0.6, 0.0), "node_44_kinds9.png")
	# 远处（26 米，混战距离）：形状基本看不清了，这时候**靠光圈颜色认价值**
	await _shoot_from(Vector3(0.0, 5.5, 26.0), Vector3(0.0, 1.2, 0.0), "node_47_kinds9_far.png")

	get_tree().quit()

## 逐项读数：形状（网格资源）/ 网格自带材质 / 按类型覆盖材质 / 光圈三环颜色 / 状态动画
func _aura_readout(node: Node3D) -> void:
	var mesh: MeshInstance3D = node.get_node("Float/Mesh")
	var ring: MeshInstance3D = node.get_node("Aura/Ring")
	var rim: MeshInstance3D = node.get_node("Aura/Rim")
	var glow: MeshInstance3D = node.get_node("Aura/Glow")
	print("[NodeShot] Kind=", node.Kind,
		" 形状=", mesh.mesh.resource_path.get_file(),
		" 网格自带材质=", _res_name(mesh.mesh.surface_get_material(0)),
		" 实例覆盖=", _res_name(mesh.get_surface_override_material(0)),
		" | 光圈色带=", _color_of(ring.get_surface_override_material(0)),
		" 暗描边=", _color_of(rim.get_surface_override_material(0)),
		" 外发光=", _color_of(glow.get_surface_override_material(0)),
		" | 环 transparency=", "%.2f" % ring.transparency,
		" 状态动画=", node.DebugStateAnimation)

func _res_name(res: Resource) -> String:
	return res.resource_path.get_file() if res != null else "（无）"

func _color_of(mat: Material) -> String:
	if mat is StandardMaterial3D:
		var c := (mat as StandardMaterial3D).albedo_color
		return "(%.2f, %.2f, %.2f, a=%.2f)" % [c.r, c.g, c.b, c.a]
	return "（无）"

func _spawn_node(at: Vector3, label: String) -> Node3D:
	var node: Node3D = (load(NODE_SCENE) as PackedScene).instantiate()
	add_child(node)
	node.global_position = at
	print("[NodeShot] 放置节点（", label, "）@", _v(at), " Prompt=", node.Prompt)
	return node

func _sample(label: String, node: Node3D) -> void:
	var float_node: Node3D = node.get_node("Float")
	var mesh: MeshInstance3D = node.get_node("Float/Mesh")
	var player: AnimationPlayer = node.get_node("Animation")
	var lo := 1e9
	var hi := -1e9
	for _i in range(90):
		await get_tree().process_frame
		lo = min(lo, float_node.position.y)
		hi = max(hi, float_node.position.y)
	print("[NodeShot] ", label, "：状态动画=", node.DebugStateAnimation,
		" Animation.current_animation=", player.current_animation,
		" Float.y 采样 min=", "%.4f" % lo, " max=", "%.4f" % hi, "（跨度 ", "%.4f" % (hi - lo), " 米）",
		" Mesh.transparency=", "%.2f" % mesh.transparency,
		" Mesh.scale=", "%.2f" % mesh.scale.x,
		" 世界高度 min=", "%.2f" % (node.global_position.y + 0.55 + lo),
		" max=", "%.2f" % (node.global_position.y + 0.55 + hi))

func _log_state(action: String, node: Node3D, enabled: bool) -> void:
	node.Enabled = enabled
	await _frames(12)
	var player: AnimationPlayer = node.get_node("Animation")
	print("[NodeShot] 状态切换：", action, " → Enabled=", node.Enabled,
		" 状态动画=", node.DebugStateAnimation, " Animation.current_animation=", player.current_animation)

func _shoot_close(node: Node3D, file: String) -> void:
	await _shoot_from(node.global_position + Vector3(0, 1.5, -3.6), node.global_position + Vector3(0, 1.0, 0), file)

func _shoot_from(from: Vector3, at: Vector3, file: String) -> void:
	_cam.global_position = from
	_cam.look_at(at, Vector3.UP)
	await _frames(6)
	await _shot(SHOTS + file)

func _frames(n: int) -> void:
	for _i in n:
		await get_tree().process_frame

func _shot(path: String) -> void:
	await RenderingServer.frame_post_draw
	var img := get_viewport().get_texture().get_image()
	print("[NodeShot] 截图 ", path, " err=", img.save_png(path))

func _v(v: Vector3) -> String:
	return "(%.3f, %.3f, %.3f)" % [v.x, v.y, v.z]

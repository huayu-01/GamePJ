@tool
extends Node

var passed := 0
var failed := 0

func _ready() -> void:
	if Engine.is_editor_hint() and "--interaction-editor-check" in OS.get_cmdline_user_args():
		call_deferred("run_probe")

func check_condition(condition: bool, message: String) -> void:
	if condition:
		passed += 1
	else:
		failed += 1
	print("[InteractionEditor] %s %s" % ["PASS" if condition else "FAIL", message])

func frames() -> void:
	for index in range(4):
		await get_tree().process_frame

func run_probe() -> void:
	check_condition(Engine.is_editor_hint(), "在真实编辑器模式验证工具脚本")
	var host := Node3D.new()
	var old_mesh := MeshInstance3D.new()
	old_mesh.mesh = BoxMesh.new()
	host.add_child(old_mesh)
	var component: Node3D = load("res://Scripts/Interaction/InteractionComponent.cs").new()
	host.add_child(component)
	get_tree().root.add_child(host)
	component.set("Kind", 1)
	await frames()
	var preview := component.get_node_or_null("TypeModel")
	check_condition(preview != null and component.get_child_count() == 1, "选择类型只生成预览，不启动交互业务")
	check_condition(not old_mesh.visible, "编辑器预览替换宿主旧模型")
	check_condition(preview != null and preview.owner == null, "预览不作为业务节点保存入场景")
	var prior_faces: PackedVector3Array = component.get_node("TypeModel/Mesh").mesh.get_faces()
	component.set("Kind", 2)
	await frames()
	check_condition(component.get_child_count() == 1 and component.get_node("TypeModel/Mesh").mesh.get_faces() != prior_faces, "切换类型更新几何且不累积预览")
	component.set("Kind", 0)
	await frames()
	check_condition(component.get_child_count() == 0 and old_mesh.visible, "None清理预览并恢复移除组件前的编辑外观")
	host.queue_free()
	await frames()
	print("[InteractionEditor] 编辑器模型验收结束：通过 %d，失败 %d" % [passed, failed])
	# 使用正常编辑器场景入口，避免--editor与--script组合的退出泄漏。
	while EditorInterface.get_resource_filesystem().is_scanning():
		await get_tree().create_timer(0.1).timeout
	await get_tree().create_timer(0.3).timeout
	get_tree().quit(0 if failed == 0 else 1)

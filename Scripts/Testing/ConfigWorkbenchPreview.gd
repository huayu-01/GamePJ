extends Control

## 无编辑器环境下的工作台预览：加载真实 Config/Tables 资源，截一张图用于人工检查平铺密度。
## 注意：不要在编辑器里运行；它不是 @tool，避免打开场景时创建第二个工作台或 quit 编辑器。
func _ready() -> void:
	if Engine.is_editor_hint():
		return
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var dock := preload("res://addons/config_workbench/workbench_dock.gd").new()
	dock.name = "ConfigWorkbench"
	add_child(dock)
	dock.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	await get_tree().process_frame
	await get_tree().process_frame
	dock.call("_show_path", "res://Config/Tables/Upgrades/reward_1351.tres")
	await get_tree().create_timer(1.2).timeout
	if DisplayServer.get_name() != "headless":
		await RenderingServer.frame_post_draw
		var dir := ProjectSettings.globalize_path("res://Logs/evidence/config-workbench")
		DirAccess.make_dir_recursive_absolute(dir)
		var image := get_viewport().get_texture().get_image()
		image.save_png(dir + "/reward-1351-flat.png")

		# 走“打开”按钮同一条延迟路径，验证不会在信号发射期间 free 表单。
		dock.call("_open_external", "res://Config/Tables/Skills/skl_a3_w1_1.tres")
		await get_tree().create_timer(.6).timeout
		dock.call("scroll_bottom")
		await get_tree().create_timer(.3).timeout
		await RenderingServer.frame_post_draw
		var skill_image := get_viewport().get_texture().get_image()
		skill_image.save_png(dir + "/skill-params-flat.png")
		get_tree().quit()

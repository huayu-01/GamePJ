@tool
extends EditorPlugin

const WorkbenchScreen := preload("res://addons/config_workbench/workbench_dock.gd")

var _screen: Control


func _enter_tree() -> void:
	_screen = WorkbenchScreen.new()
	_screen.name = "ConfigWorkbench"
	get_editor_interface().get_editor_main_screen().add_child(_screen)
	_screen.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_screen.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_screen.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_make_visible(false)


func _exit_tree() -> void:
	if is_instance_valid(_screen):
		_screen.queue_free()


func _get_plugin_name() -> String:
	return "配置工作台"


func _has_main_screen() -> bool:
	return true


func _make_visible(visible: bool) -> void:
	if is_instance_valid(_screen):
		_screen.visible = visible
		if visible:
			_screen.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
			_screen.call("on_show")


func _get_plugin_icon() -> Texture2D:
	return get_editor_interface().get_base_control().get_theme_icon("Tools", "EditorIcons")

extends Node
## 仅用于隔离并行B批绘制错误的全量战斗回归；原Main自测错误仍单独记录，不伪装通过。

func _ready() -> void:
	for node in get_parent().find_children("*", "Control", true, false):
		var script: Script = node.get_script()
		if script and script.resource_path == "res://Scripts/UI/AbilityHud.cs":
			node.hide()
			print("[ArtRefresh] 隔离回归：仅隐藏并行B批AbilityHud绘制，原Main自测错误另行记录；完整SelfTest与其他UI保持。")

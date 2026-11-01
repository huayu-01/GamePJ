@tool
extends Control

## 配置工作台 0.1：只读平铺原型。
## - 左侧按表筛选资源；右侧把当前资源的全部可编辑属性铺开，不折叠。
## - Effects / TargetAbility / Params / ParamEntry 递归展开，直接看到深层参数。
## - Note 按“参数用途”展示，旧变更说明标黄提示。
## 下一步才接编辑/保存；当前不改任何 .tres。

const TABLES := [
	{"id": "skills", "label": "技能", "path": "res://Config/Tables/Skills"},
	{"id": "upgrades", "label": "增幅", "path": "res://Config/Tables/Upgrades"},
	{"id": "enemies", "label": "敌人", "path": "res://Config/Tables/Enemies"},
	{"id": "characters", "label": "角色", "path": "res://Config/Tables/Characters"},
	{"id": "weapons", "label": "武器", "path": "res://Config/Tables/Weapons"},
	{"id": "bosses", "label": "Boss", "path": "res://Config/Tables/Bosses"},
	{"id": "effects", "label": "效果", "path": "res://Config/Tables/Effects"},
	{"id": "runes", "label": "符文", "path": "res://Config/Tables/Runes"},
	{"id": "nodes", "label": "节点", "path": "res://Config/Tables/Nodes"},
	{"id": "ai", "label": "AI", "path": "res://Config/Tables/AiProfiles"},
]

const SKIP_FIELDS := ["script", "resource_path", "resource_name", "resource_local_to_scene", "resource_scene_unique_id"]
const INLINE_EXTERNAL := ["TargetAbility", "RequiredEventAbility", "BonusAgainstAbility", "Tuning", "SpecializationBehavior"]
const INLINE_ARRAYS := ["Effects", "Params", "AttackParams", "HandlerParams"]
const COMPACT_ARRAYS := ["Skills", "ConvertedRewards", "SpecializationTargets", "RequiredWeaponTags", "RequiredCharacterIds", "Requires"]

var _table_filter: OptionButton
var _search: LineEdit
var _list: ItemList
var _split: HSplitContainer
var _form: VBoxContainer
var _targets: Array[VBoxContainer] = []
var _status: Label
var _title: Label
var _entries: Array[Dictionary] = []
var _visible: Array[Dictionary] = []
var _selected_path := ""
var _filesystem_path := ""
var _legacy_note := RegEx.new()
var _id_re := RegEx.new()
var _name_re := RegEx.new()


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	size_flags_horizontal = Control.SIZE_EXPAND_FILL
	size_flags_vertical = Control.SIZE_EXPAND_FILL
	custom_minimum_size = Vector2(440, 420)
	if Engine.is_editor_hint():
		var theme := EditorInterface.get_editor_theme()
		if theme != null:
			self.theme = theme
	_legacy_note.compile("★|文档|第[一二三四五六七八九十0-9]+轮|J\\d+|验收|修复|用户|当前|原版|旧|批次|本轮|新增|改动|预设|→")
	_id_re.compile("(?m)^Id = \"(.*)\"$")
	_name_re.compile("(?m)^DisplayName = \"(.*)\"$")
	_build_ui()
	call_deferred("_refresh")


func on_show() -> void:
	if _entries.is_empty():
		_refresh()
	_fit_split()


func _fit_split() -> void:
	if _split == null:
		return
	# split_offset 是“相对中线的偏移”，不是左边宽度：目标 20% = 中线 - 30% 宽。
	# 左边至少 200px，小窗口时不压扁列表；右侧始终吃剩余宽度。
	var target := maxi(200, int(_split.size.x * 0.2))
	_split.split_offset = target - int(_split.size.x * 0.5)


func _process(_delta: float) -> void:
	if not Engine.is_editor_hint():
		return
	# 在 FileSystem 里点中一个 .tres，工作台直接跟着打开；只响应选择变化，不抢焦点。
	var paths := EditorInterface.get_selected_paths()
	if paths.is_empty():
		return
	var path := str(paths[0])
	if path == _filesystem_path:
		return
	_filesystem_path = path
	if path.ends_with(".tres") and path != _selected_path:
		_show_path(path)


func _build_ui() -> void:
	var root := VBoxContainer.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	root.size_flags_vertical = Control.SIZE_EXPAND_FILL
	root.add_theme_constant_override("separation", 6)
	add_child(root)

	var top := HBoxContainer.new()
	top.add_theme_constant_override("separation", 8)
	top.custom_minimum_size.y = 34
	root.add_child(top)
	var title := Label.new()
	title.text = "配置工作台"
	title.add_theme_font_size_override("font_size", 19)
	top.add_child(title)
	var badge := Label.new()
	badge.text = "只读预览"
	badge.add_theme_color_override("font_color", Color(1, 0.72, 0.28))
	top.add_child(badge)
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	top.add_child(spacer)
	_table_filter = OptionButton.new()
	_table_filter.add_item("全部")
	for table in TABLES:
		_table_filter.add_item(str(table["label"]))
	_table_filter.item_selected.connect(_on_filter_changed)
	top.add_child(_table_filter)
	_search = LineEdit.new()
	_search.placeholder_text = "搜索 Id / 名称 / 路径"
	_search.clear_button_enabled = true
	_search.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_search.text_changed.connect(_on_search_changed)
	top.add_child(_search)
	var refresh := Button.new()
	refresh.text = "刷新"
	refresh.pressed.connect(_refresh)
	top.add_child(refresh)

	_status = Label.new()
	_status.text = "只读原型：所有字段平铺；Effects / TargetAbility / Params 递归展开"
	_status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	root.add_child(_status)

	_split = HSplitContainer.new()
	_split.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_split.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_split.split_offset = 300
	_split.resized.connect(_fit_split)
	root.add_child(_split)

	_list = ItemList.new()
	_list.custom_minimum_size = Vector2(200, 0)
	_list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_list.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_list.item_selected.connect(_on_item_selected)
	_split.add_child(_list)

	var scroll := ScrollContainer.new()
	scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	_split.add_child(scroll)

	_form = VBoxContainer.new()
	_form.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_form.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_form.add_theme_constant_override("separation", 5)
	scroll.add_child(_form)
	_targets = [_form]
	call_deferred("_fit_split")


func _refresh() -> void:
	_entries.clear()
	for table in TABLES:
		_scan_dir(str(table["path"]), str(table["label"]))
	_entries.sort_custom(func(a, b): return str(a["path"]) < str(b["path"]))
	_rebuild_list()
	_status.text = "只读原型：%d 个资源；所有字段平铺，Effects / TargetAbility / Params 递归展开" % _entries.size()


func _scan_dir(path: String, table_label: String) -> void:
	var dir := DirAccess.open(path)
	if dir == null:
		return
	dir.list_dir_begin()
	var name := dir.get_next()
	while name != "":
		if dir.current_is_dir():
			if not name.begins_with("."):
				_scan_dir(path + "/" + name, table_label)
		elif name.ends_with(".tres"):
			var res_path := path + "/" + name
			# 扫描阶段只读文本，不实例化资源：避免为 300+ 行配置在启动时触发全部
			# C# 反序列化，也让工作台打开更快。真正选中某一行时才 load。
			var text := FileAccess.get_file_as_string(res_path)
			var id := _regex_first(_id_re, text)
			var display := _regex_first(_name_re, text)
			if display.is_empty():
				display = name.get_basename()
			_entries.append({"path": res_path, "table": table_label, "id": id, "display": display})
		name = dir.get_next()
	dir.list_dir_end()


func _rebuild_list() -> void:
	var keep := _selected_path
	_list.clear()
	_visible.clear()
	var query := _search.text.strip_edges().to_lower() if _search != null else ""
	for entry in _entries:
		if not _matches_table(entry):
			continue
		var haystack := (str(entry["path"]) + " " + str(entry["id"]) + " " + str(entry["display"])).to_lower()
		if not query.is_empty() and not haystack.contains(query):
			continue
		_visible.append(entry)
		var label := "%s · %s" % [entry["display"], entry["id"]]
		if str(entry["id"]).is_empty():
			label = str(entry["display"])
		var index := _list.add_item(label)
		_list.set_item_metadata(index, entry["path"])
		if str(entry["path"]) == keep:
			_list.select(index)
	if _selected_path.is_empty() and not _visible.is_empty():
		_list.select(0)
		_on_item_selected(0)


func _matches_table(entry: Dictionary) -> bool:
	var index := _table_filter.selected if _table_filter != null else 0
	return index <= 0 or str(entry["table"]) == str(TABLES[index - 1]["label"])


func _on_filter_changed(_index: int) -> void:
	_rebuild_list()


func _on_search_changed(_text: String) -> void:
	_rebuild_list()


func _on_item_selected(index: int) -> void:
	if index < 0:
		return
	_show_path(str(_list.get_item_metadata(index)))


func _show_path(path: String) -> void:
	if path.is_empty():
		return
	var res := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_REUSE)
	if not (res is Resource):
		_status.text = "无法加载：%s" % path
		return
	_selected_path = path
	_clear_form()
	_title = Label.new()
	_title.text = "%s  ·  %s" % [_text(res.get("DisplayName")), path]
	_title.add_theme_font_size_override("font_size", 19)
	_title.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_form.add_child(_title)
	var type_line := Label.new()
	type_line.text = "%s  |  %s" % [_type_name(res), path]
	type_line.add_theme_color_override("font_color", Color(0.62, 0.72, 0.85))
	type_line.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_form.add_child(type_line)
	_render_resource(res, 0, [], false)
	_form.add_child(HSeparator.new())


func _clear_form() -> void:
	for child in _form.get_children():
		child.free()
	_targets = [_form]


func _target() -> VBoxContainer:
	return _targets[_targets.size() - 1] if not _targets.is_empty() else _form


func _add_control(control: Control) -> void:
	_target().add_child(control)


func _nested_card(depth: int) -> VBoxContainer:
	# 左侧缩进 + 边框，让 6-8 层递归仍能一眼看出父子关系。
	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", mini(depth * 14, 70))
	_target().add_child(margin)
	var panel := PanelContainer.new()
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.075, 0.09, 0.12, 0.62)
	style.border_color = Color(0.26, 0.42, 0.58, 0.85)
	style.set_border_width_all(1)
	style.set_corner_radius_all(4)
	style.content_margin_left = 8
	style.content_margin_right = 8
	style.content_margin_top = 5
	style.content_margin_bottom = 5
	panel.add_theme_stylebox_override("panel", style)
	margin.add_child(panel)
	var body := VBoxContainer.new()
	body.add_theme_constant_override("separation", 4)
	panel.add_child(body)
	return body


func _small_card_style() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.1, 0.12, 0.16, 0.72)
	style.border_color = Color(0.24, 0.34, 0.48, 0.8)
	style.set_border_width_all(1)
	style.set_corner_radius_all(3)
	style.content_margin_left = 7
	style.content_margin_right = 7
	style.content_margin_top = 4
	style.content_margin_bottom = 4
	return style


func _render_resource(res: Resource, depth: int, stack: Array, draw_header: bool = true) -> void:
	if res == null:
		return
	if depth > 10:
		_add_note("递归超过 10 层，已停止（可能是资源循环）。", Color(1, 0.65, 0.2))
		return
	var key := res.resource_path if not res.resource_path.is_empty() else "sub:%d" % res.get_instance_id()
	if stack.has(key):
		_add_note("↩ 已在上层展开：%s" % _resource_label(res), Color(0.6, 0.7, 0.8))
		return
	var next_stack := stack.duplicate()
	next_stack.append(key)

	var body := _target()
	if draw_header and depth > 0:
		body = _nested_card(depth)
	_targets.append(body)

	if draw_header:
		var header := PanelContainer.new()
		var hbox := HBoxContainer.new()
		hbox.add_theme_constant_override("separation", 8)
		header.add_child(hbox)
		var type_label := Label.new()
		type_label.text = "%s▸ %s" % ["  ".repeat(depth), _type_name(res)]
		type_label.add_theme_font_size_override("font_size", 16)
		hbox.add_child(type_label)
		var path_label := Label.new()
		path_label.text = res.resource_path if not res.resource_path.is_empty() else "内嵌子资源"
		path_label.add_theme_color_override("font_color", Color(0.65, 0.75, 0.88))
		path_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		path_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		hbox.add_child(path_label)
		if not res.resource_path.is_empty() and res.resource_path != _selected_path:
			var open := Button.new()
			open.text = "打开"
			open.tooltip_text = "在配置工作台打开这个外部资源"
			open.pressed.connect(_open_external.bind(res.resource_path), CONNECT_DEFERRED)
			hbox.add_child(open)
		_add_control(header)

	for prop in res.get_property_list():
		var prop_name := str(prop.get("name", ""))
		var usage: int = int(prop.get("usage", 0))
		# 只保留策划写的 ExportGroup；Godot 自动生成的类名 Category（RefCounted/
		# Resource/ConfigRow/AbilityConfig…）不是字段分组，全部丢掉。
		if usage & PROPERTY_USAGE_CATEGORY:
			continue
		if usage & PROPERTY_USAGE_GROUP:
			if not prop_name.is_empty() and prop_name not in ["Resource", "RefCounted"]:
				_add_section(prop_name)
			continue
		if not (usage & PROPERTY_USAGE_EDITOR):
			continue
		if prop_name.begins_with("_") or prop_name in SKIP_FIELDS:
			continue
		var value: Variant = res.get(prop_name)
		_render_property(prop_name, value, int(prop.get("type", TYPE_NIL)), int(prop.get("hint", 0)), str(prop.get("hint_string", "")), depth, next_stack)
	_targets.pop_back()


func _render_property(prop_name: String, value: Variant, type: int, hint: int, hint_string: String, depth: int, stack: Array) -> void:
	if value is PackedScene:
		var packed := value as PackedScene
		_add_control(_kv_row(prop_name, "场景：%s" % packed.resource_path))
		return
	if value is Resource:
		_render_resource_property(prop_name, value as Resource, depth, stack)
		return
	if value is Array:
		_render_array_property(prop_name, value as Array, depth, stack)
		return
	var row := _kv_row(prop_name, _display_value(value, type, hint, hint_string))
	_add_control(row)


func _render_resource_property(prop_name: String, value: Resource, depth: int, stack: Array) -> void:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 8)
	var label := Label.new()
	label.text = prop_name
	label.custom_minimum_size.x = 190
	label.add_theme_color_override("font_color", Color(0.7, 0.8, 0.95))
	row.add_child(label)
	var value_label := Label.new()
	value_label.text = _resource_label(value)
	value_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	value_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	row.add_child(value_label)
	if not value.resource_path.is_empty() and value.resource_path != _selected_path:
		var open := Button.new()
		open.text = "打开"
		open.pressed.connect(_open_external.bind(value.resource_path), CONNECT_DEFERRED)
		row.add_child(open)
	_add_control(row)
	if _should_inline(value, prop_name, depth):
		_render_resource(value, depth + 1, stack, true)
	else:
		_add_note("外部资源：%s（点“打开”进入，避免误改共享文件）" % value.resource_path, Color(0.55, 0.68, 0.82))


func _render_array_property(prop_name: String, array: Array, depth: int, stack: Array) -> void:
	_add_section("%s  [%d]" % [prop_name, array.size()])
	if array.is_empty():
		_add_note("（空）", Color(0.55, 0.6, 0.68))
		return
	for i in array.size():
		var value: Variant = array[i]
		if value is Resource:
			var res := value as Resource
			if _is_param_entry(res):
				_render_param_entry(res, i)
			elif prop_name in COMPACT_ARRAYS and res.resource_path != "":
				_render_compact_resource("%s[%d]" % [prop_name, i], res)
			else:
				_render_resource(res, depth + 1, stack, true)
		elif value is Array:
			_add_note("%s[%d] = %s" % [prop_name, i, _short(value)], Color(0.75, 0.78, 0.85))
		elif value is Dictionary:
			_add_note("%s[%d] = %s" % [prop_name, i, _short(value)], Color(0.75, 0.78, 0.85))
		else:
			_add_note("%s[%d] = %s" % [prop_name, i, _display_value(value, typeof(value), 0, "")], Color(0.8, 0.82, 0.88))


func _render_compact_resource(prop_name: String, res: Resource) -> void:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 8)
	var label := Label.new()
	label.text = prop_name
	label.custom_minimum_size.x = 190
	row.add_child(label)
	var value := Label.new()
	value.text = _resource_label(res)
	value.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	value.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	row.add_child(value)
	if res.resource_path != "":
		var open := Button.new()
		open.text = "打开"
		open.pressed.connect(_open_external.bind(res.resource_path), CONNECT_DEFERRED)
		row.add_child(open)
	_add_control(row)


func _render_param_entry(res: Resource, index: int) -> void:
	var card := PanelContainer.new()
	card.add_theme_stylebox_override("panel", _small_card_style())
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 3)
	card.add_child(box)
	_add_control(card)

	var head := HBoxContainer.new()
	head.add_theme_constant_override("separation", 8)
	var key := Label.new()
	key.text = "[%d] %s" % [index, _text(res.get("Key"))]
	key.add_theme_font_size_override("font_size", 15)
	key.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	head.add_child(key)
	var value: Variant = res.get("Value")
	var value_label := Label.new()
	value_label.text = "%s  (%s)" % [_short(value), type_string(typeof(value))]
	value_label.add_theme_color_override("font_color", Color(0.75, 0.9, 1.0))
	head.add_child(value_label)
	box.add_child(head)

	var note := _text(res.get("Note"))
	var note_label := Label.new()
	note_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	if note.is_empty():
		note_label.text = "说明：未填写（应写清用在哪里、单位/范围）"
		note_label.add_theme_color_override("font_color", Color(1, 0.65, 0.2))
	elif _legacy_note.search(note) != null:
		note_label.text = "说明（疑似变更记录，建议改成用途）：%s" % note
		note_label.add_theme_color_override("font_color", Color(1, 0.75, 0.25))
	else:
		note_label.text = "说明：%s" % note
		note_label.add_theme_color_override("font_color", Color(0.72, 0.82, 0.9))
	box.add_child(note_label)


func _should_inline(res: Resource, prop_name: String, depth: int) -> bool:
	if depth >= 10:
		return false
	if res.resource_path.is_empty():
		return true
	if prop_name in INLINE_EXTERNAL:
		return true
	var script := res.get_script()
	if script != null and script.resource_path.contains("/RewardEffects/"):
		return true
	return false


func _display_value(value: Variant, type: int, hint: int, hint_string: String) -> String:
	if value == null:
		return "（空）"
	if value is PackedScene:
		return "场景：%s" % value.resource_path
	if type == TYPE_BOOL:
		return "是" if bool(value) else "否"
	if hint == PROPERTY_HINT_ENUM and not hint_string.is_empty():
		var names := hint_string.split(",")
		var index := int(value)
		if index >= 0 and index < names.size():
			return "%s  [%d]" % [names[index], index]
	if type == TYPE_PACKED_STRING_ARRAY:
		var parts: PackedStringArray = value
		return "、".join(parts) if not parts.is_empty() else "（空）"
	if value is Array:
		return "[%d] %s" % [(value as Array).size(), _short(value)]
	if value is Dictionary:
		return _short(value)
	if type == TYPE_STRING or type == TYPE_STRING_NAME:
		return str(value)
	if type == TYPE_NIL:
		return "%s  (%s)" % [_short(value), type_string(typeof(value))]
	return str(value)


func _resource_label(res: Resource) -> String:
	var type := _type_name(res)
	if res.resource_path.is_empty():
		return "%s（内嵌子资源）" % type
	return "%s  %s" % [type, res.resource_path]


func _type_name(res: Resource) -> String:
	var script := res.get_script()
	if script != null and not script.resource_path.is_empty():
		return script.resource_path.get_file().get_basename()
	return res.get_class()


func _is_param_entry(res: Resource) -> bool:
	var script := res.get_script()
	if script == null:
		return false
	return script.resource_path.get_file().get_basename() == "ParamEntry"


func _kv_row(name: String, value_text: String) -> HBoxContainer:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 8)
	var label := Label.new()
	label.text = name
	label.custom_minimum_size.x = 190
	label.add_theme_color_override("font_color", Color(0.72, 0.8, 0.92))
	row.add_child(label)
	var value := Label.new()
	value.text = value_text
	value.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	value.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	row.add_child(value)
	return row


func _add_section(title: String) -> void:
	var sep := HSeparator.new()
	_add_control(sep)
	var label := Label.new()
	label.text = title
	label.add_theme_font_size_override("font_size", 16)
	label.add_theme_color_override("font_color", Color(0.82, 0.85, 0.95))
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_add_control(label)


func _add_note(text: String, color: Color) -> void:
	var label := Label.new()
	label.text = text
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.add_theme_color_override("font_color", color)
	_add_control(label)


func _short(value: Variant) -> String:
	var text := str(value)
	if text.length() > 140:
		return text.substr(0, 137) + "…"
	return text


func _text(value: Variant) -> String:
	if value == null:
		return ""
	return str(value)


func _regex_first(re: RegEx, text: String) -> String:
	var match := re.search(text)
	return match.get_string(1) if match != null else ""


func _open_external(path: String) -> void:
	if path.is_empty():
		return
	# 按钮 pressed 信号还在发射时不能 free 表单（会触发 “freed while signal emitted”）。
	# 推到下一帧再重建右侧。
	_show_path.call_deferred(path)


func scroll_bottom() -> void:
	var parent := _form.get_parent()
	if parent is ScrollContainer:
		(parent as ScrollContainer).scroll_vertical = 1 << 20

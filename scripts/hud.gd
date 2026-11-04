extends CanvasLayer

signal action_requested(action: String)
signal menu_closed

var money_label: Label
var tool_label: Label
var goal_label: Label
var hint_label: Label
var message_label: Label
var progress: ProgressBar
var menu_panel: PanelContainer
var menu_title: Label
var menu_content: Label
var menu_buttons: VBoxContainer


func _ready() -> void:
	_build_hud()
	_build_menu()


func _build_hud() -> void:
	var top := HBoxContainer.new()
	top.position = Vector2(18, 14)
	top.add_theme_constant_override("separation", 20)
	add_child(top)
	money_label = _label("Münzen: 0", 22)
	tool_label = _label("Werkzeug: Hände", 22)
	top.add_child(money_label)
	top.add_child(tool_label)

	goal_label = _label("", 18)
	goal_label.position = Vector2(18, 54)
	goal_label.add_theme_color_override("font_color", Color(0.9, 0.94, 0.78))
	add_child(goal_label)

	var crosshair := Label.new()
	crosshair.text = "+"
	crosshair.add_theme_font_size_override("font_size", 26)
	crosshair.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	crosshair.position = Vector2(-7, -17)
	add_child(crosshair)

	progress = ProgressBar.new()
	progress.set_anchors_and_offsets_preset(Control.PRESET_CENTER_BOTTOM)
	progress.position = Vector2(-130, -95)
	progress.size = Vector2(260, 20)
	progress.show_percentage = false
	progress.visible = false
	add_child(progress)

	hint_label = _label("WASD bewegen · Maus schauen · E interagieren · I Inventar", 17)
	hint_label.set_anchors_and_offsets_preset(Control.PRESET_CENTER_BOTTOM)
	hint_label.position = Vector2(-340, -54)
	hint_label.size = Vector2(680, 28)
	hint_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	add_child(hint_label)

	message_label = _label("", 18)
	message_label.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
	message_label.position = Vector2(-350, 16)
	message_label.size = Vector2(700, 32)
	message_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	add_child(message_label)


func _build_menu() -> void:
	menu_panel = PanelContainer.new()
	menu_panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	menu_panel.position = Vector2(-260, -250)
	menu_panel.size = Vector2(520, 500)
	menu_panel.visible = false
	add_child(menu_panel)

	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 10)
	menu_panel.add_child(column)
	menu_title = _label("Dino Genesis", 30)
	column.add_child(menu_title)
	menu_content = _label("", 18)
	menu_content.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	menu_content.size_flags_vertical = Control.SIZE_EXPAND_FILL
	column.add_child(menu_content)
	menu_buttons = VBoxContainer.new()
	menu_buttons.add_theme_constant_override("separation", 7)
	column.add_child(menu_buttons)
	_add_button("Schließen (ESC)", "close")


func _label(text_value: String, font_size: int) -> Label:
	var label := Label.new()
	label.text = text_value
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.8))
	label.add_theme_constant_override("shadow_offset_x", 1)
	label.add_theme_constant_override("shadow_offset_y", 1)
	return label


func _add_button(text_value: String, action: String) -> void:
	var button := Button.new()
	button.text = text_value
	button.custom_minimum_size = Vector2(0, 38)
	button.pressed.connect(_on_button_pressed.bind(action))
	menu_buttons.add_child(button)


func _on_button_pressed(action: String) -> void:
	if action == "close":
		menu_closed.emit()
	else:
		action_requested.emit(action)


func set_hud(money: int, tool_name: String, goal: String, hint: String) -> void:
	money_label.text = "Münzen: %d" % money
	tool_label.text = "Werkzeug: %s" % tool_name
	goal_label.text = goal
	hint_label.text = hint


func show_message(text_value: String) -> void:
	message_label.text = text_value


func set_dig_progress(value: float) -> void:
	progress.visible = value > 0.0
	progress.value = value * 100.0


func open_menu(title: String, content: String, actions: Array[Dictionary]) -> void:
	menu_title.text = title
	menu_content.text = content
	for child in menu_buttons.get_children():
		child.queue_free()
	for action: Dictionary in actions:
		_add_button(str(action["label"]), str(action["action"]))
	_add_button("Schließen (ESC)", "close")
	menu_panel.visible = true


func close_menu() -> void:
	menu_panel.visible = false

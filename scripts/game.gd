extends Node3D

const PlayerScript := preload("res://scripts/player.gd")
const WorldScript := preload("res://scripts/world.gd")
const HudScript := preload("res://scripts/hud.gd")
const SAVE_PATH := "user://dino_genesis_save.json"

const FOSSILS := {
	"ammonite": {"name": "Ammonit", "rarity": "Gewöhnlich", "price": 16, "research": 8},
	"fish": {"name": "Fischfossil", "rarity": "Gewöhnlich", "price": 18, "research": 9},
	"leaf": {"name": "Blattfossil", "rarity": "Gewöhnlich", "price": 21, "research": 10},
	"feather": {"name": "Federfossil", "rarity": "Selten", "price": 42, "research": 20},
	"tooth": {"name": "Zahnfossil", "rarity": "Selten", "price": 55, "research": 28},
	"claw": {"name": "Kralle", "rarity": "Sehr selten", "price": 90, "research": 44},
}
const TOOLS := {
	"hands": {"name": "Hände", "cost": 0, "dig_time": 3.2},
	"shovel": {"name": "Einfache Schaufel", "cost": 55, "dig_time": 1.8},
	"advanced_shovel": {"name": "Verbesserte Schaufel", "cost": 140, "dig_time": 1.0},
}

var world
var player
var hud
var money := 0
var inventory: Array[String] = []
var unlocked_tools: Array[String] = ["hands"]
var active_tool := "hands"
var research_points := 0
var birds_caught := 0
var eggs := 0
var dinosaurs := 0
var zoo_income := 0
var mined_sites: Array[String] = []
var caught_birds: Array[String] = []
var dig_target: Node3D
var dig_time := 0.0
var income_timer := 0.0
var menu_kind := ""
var world_seed := 42042
var goal := "Finde ein Fossil und grabe es mit der linken Maustaste aus."


func _ready() -> void:
	_load_game()
	world = WorldScript.new()
	world.name = "World"
	world.seed = world_seed
	add_child(world)
	player = PlayerScript.new()
	player.name = "Player"
	player.position = Vector3(0, 3.0, 2.0)
	add_child(player)
	hud = HudScript.new()
	add_child(hud)
	hud.action_requested.connect(_on_menu_action)
	hud.menu_closed.connect(_close_menu)
	_apply_mined_sites()
	_apply_caught_birds()
	_add_existing_dinosaurs()
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	_update_hud()


func _process(delta: float) -> void:
	if not is_instance_valid(player):
		return
	if Input.is_action_just_pressed("pause"):
		if menu_kind != "":
			_close_menu()
		else:
			_open_menu("pause")
		return
	if Input.is_action_just_pressed("inventory"):
		if menu_kind != "":
			_close_menu()
		else:
			_open_menu("inventory")
	if menu_kind != "":
		return

	var aimed: Node3D = player.get_aimed_interactable()
	var hint := "Kein Ziel im Blick"
	if is_instance_valid(aimed):
		var kind := str(aimed.get_meta("kind", ""))
		match kind:
			"fossil":
				hint = "Linke Maustaste halten: %s ausgraben" % str(FOSSILS[aimed.get_meta("fossil_key")]["name"])
			"bird":
				hint = "E drücken: Vogel einfangen"
			"market":
				hint = "E drücken: Marktplatz öffnen"
			"lab":
				hint = "E drücken: Forschungslabor öffnen"
			"zoo":
				hint = "E drücken: Zoo verwalten"
	if is_instance_valid(aimed) and Input.is_action_just_pressed("interact"):
		_interact(aimed)
	if is_instance_valid(aimed) and str(aimed.get_meta("kind", "")) == "fossil" and Input.is_action_pressed("dig"):
		_dig(aimed, delta)
	else:
		dig_target = null
		dig_time = 0.0
		hud.set_dig_progress(0.0)
	_update_hud(hint)
	_update_zoo_income(delta)


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		if menu_kind == "" and Input.mouse_mode != Input.MOUSE_MODE_CAPTURED:
			Input.mouse_mode = Input.MOUSE_MODE_CAPTURED


func _dig(site: Node3D, delta: float) -> void:
	if site != dig_target:
		dig_target = site
		dig_time = 0.0
	dig_time += delta
	var duration: float = TOOLS[active_tool]["dig_time"]
	hud.set_dig_progress(dig_time / duration)
	if dig_time >= duration:
		var fossil_key := str(site.get_meta("fossil_key"))
		inventory.append(fossil_key)
		mined_sites.append(str(site.get_meta("site_index", -1)))
		site.queue_free()
		dig_target = null
		dig_time = 0.0
		goal = "Verkaufe Fossilien oder untersuche sie im Labor."
		hud.show_message("%s ausgegraben." % str(FOSSILS[fossil_key]["name"]))
		hud.set_dig_progress(0.0)
		_save_game()


func _interact(target: Node3D) -> void:
	match str(target.get_meta("kind", "")):
		"bird":
			var bird_index := str(target.get_meta("bird_index", -1))
			if caught_birds.has(bird_index):
				return
			caught_birds.append(bird_index)
			target.queue_free()
			birds_caught += 1
			hud.show_message("Vogel gefangen. Bring ihn ins Forschungslabor.")
			_save_game()
		"market":
			_open_menu("market")
		"lab":
			_open_menu("lab")
		"zoo":
			_open_menu("zoo")


func _open_menu(kind: String) -> void:
	menu_kind = kind
	player.set_controls_enabled(false)
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	var title := "Inventar"
	var actions: Array[Dictionary] = []
	var content := _status_text()
	match kind:
		"market":
			title = "Marktplatz"
			actions = [
				{"label": "Ein Fossil verkaufen", "action": "sell_one"},
				{"label": "Einfache Schaufel kaufen (55 Münzen)", "action": "buy_shovel"},
				{"label": "Verbesserte Schaufel kaufen (140 Münzen)", "action": "buy_advanced"},
				{"label": "Spiel speichern", "action": "save"},
			]
		"lab":
			title = "Forschungslabor"
			actions = [
				{"label": "Fossil analysieren", "action": "analyze"},
				{"label": "Dinosaurier-Ei erzeugen (25 DNA + 1 Vogel)", "action": "create_egg"},
				{"label": "Spiel speichern", "action": "save"},
			]
		"zoo":
			title = "Dino-Zoo"
			actions = [
				{"label": "Ei ausbrüten", "action": "hatch"},
				{"label": "Spiel speichern", "action": "save"},
			]
		"pause":
			title = "Pause"
			actions = [{"label": "Spiel speichern", "action": "save"}]
		"inventory":
			title = "Inventar & Fortschritt"
	hud.open_menu(title, content, actions)


func _status_text() -> String:
	var fossil_lines := "Keine Fossilien"
	if not inventory.is_empty():
		fossil_lines = ""
		for fossil_key: String in inventory:
			fossil_lines += "• %s (%s)\n" % [str(FOSSILS[fossil_key]["name"]), str(FOSSILS[fossil_key]["rarity"])]
	return "Münzen: %d\nFossilien:\n%s\nDNA-Fragmente: %d\nGefangene Vögel: %d\nEier: %d\nDinosaurier im Zoo: %d\nZoo-Einnahmen: %d" % [
		money, fossil_lines, research_points, birds_caught, eggs, dinosaurs, zoo_income
	]


func _on_menu_action(action: String) -> void:
	match action:
		"sell_one":
			_sell_fossil()
		"buy_shovel":
			_buy_tool("shovel")
		"buy_advanced":
			_buy_tool("advanced_shovel")
		"analyze":
			_analyze_fossil()
		"create_egg":
			_create_egg()
		"hatch":
			_hatch_dinosaur()
		"save":
			_save_game()
			hud.show_message("Spiel gespeichert.")
	_open_menu(menu_kind)


func _sell_fossil() -> void:
	if inventory.is_empty():
		hud.show_message("Du hast keine Fossilien zu verkaufen.")
		return
	var key: String = str(inventory.pop_front())
	var fossil: Dictionary = FOSSILS[key]
	money += int(fossil["price"])
	hud.show_message("%s für %d Münzen verkauft." % [str(fossil["name"]), int(fossil["price"])])
	_save_game()


func _buy_tool(tool_key: String) -> void:
	if unlocked_tools.has(tool_key):
		active_tool = tool_key
		hud.show_message("%s ausgerüstet." % str(TOOLS[tool_key]["name"]))
		return
	var cost: int = TOOLS[tool_key]["cost"]
	if money < cost:
		hud.show_message("Dafür fehlen dir Münzen.")
		return
	money -= cost
	unlocked_tools.append(tool_key)
	active_tool = tool_key
	hud.show_message("%s gekauft und ausgerüstet." % str(TOOLS[tool_key]["name"]))
	_save_game()


func _analyze_fossil() -> void:
	if inventory.is_empty():
		hud.show_message("Du hast keine Fossilien im Inventar.")
		return
	var key: String = str(inventory.pop_front())
	var fossil: Dictionary = FOSSILS[key]
	var points := int(fossil["research"])
	research_points += points
	hud.show_message("%s analysiert: +%d DNA." % [str(fossil["name"]), points])
	goal = "Fange einen Vogel und rekonstruiere im Labor ein Dino-Ei."
	_save_game()


func _create_egg() -> void:
	if birds_caught < 1:
		hud.show_message("Du brauchst einen gefangenen Vogel.")
	elif research_points < 25:
		hud.show_message("Es fehlen DNA-Fragmente (benötigt: 25).")
	else:
		birds_caught -= 1
		research_points -= 25
		eggs += 1
		hud.show_message("Ein Dino-Ei wurde im Labor erzeugt.")
		_save_game()


func _hatch_dinosaur() -> void:
	if eggs < 1:
		hud.show_message("Du hast kein Ei zum Ausbrüten.")
		return
	eggs -= 1
	dinosaurs += 1
	_add_dinosaur()
	goal = "Dein Zoo ist geöffnet: Besucher bringen regelmäßig Einnahmen."
	hud.show_message("Ein kleiner Velociraptor ist geschlüpft und im Zoo.")
	_save_game()


func _add_dinosaur() -> void:
	var dino := Node3D.new()
	dino.position = Vector3(0, world.height_at(0, 22), 22)
	world.add_child(dino)
	var body := MeshInstance3D.new()
	var body_mesh := SphereMesh.new()
	body_mesh.radius = 0.8
	body_mesh.height = 1.0
	body.mesh = body_mesh
	body.position = Vector3(0, 1.0, 0)
	body.scale = Vector3(1.3, 0.8, 1.7)
	body.material_override = _simple_material(Color(0.32, 0.48, 0.24))
	dino.add_child(body)
	var head := MeshInstance3D.new()
	var head_mesh := SphereMesh.new()
	head_mesh.radius = 0.42
	head.mesh = head_mesh
	head.position = Vector3(0, 1.45, -1.05)
	head.material_override = _simple_material(Color(0.38, 0.54, 0.28))
	dino.add_child(head)
	for side in [-1.0, 1.0]:
		var leg := MeshInstance3D.new()
		var leg_mesh := CapsuleMesh.new()
		leg_mesh.radius = 0.14
		leg_mesh.height = 0.7
		leg.mesh = leg_mesh
		leg.position = Vector3(side * 0.38, 0.38, 0)
		leg.material_override = _simple_material(Color(0.28, 0.4, 0.21))
		dino.add_child(leg)


func _simple_material(color_value: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color_value
	material.roughness = 0.9
	return material


func _add_existing_dinosaurs() -> void:
	for index in range(dinosaurs):
		_add_dinosaur()


func _update_zoo_income(delta: float) -> void:
	if dinosaurs <= 0:
		return
	income_timer += delta
	if income_timer >= 30.0:
		income_timer = 0.0
		money += 12 * dinosaurs
		zoo_income += 12 * dinosaurs
		hud.show_message("Besucher im Zoo: +%d Münzen." % (12 * dinosaurs))
		_save_game()


func _update_hud(hint: String = "") -> void:
	var hint_text := hint
	if hint_text == "":
		hint_text = "WASD bewegen · E interagieren · Linksklick graben · I Inventar · ESC Pause"
	hud.set_hud(money, str(TOOLS[active_tool]["name"]), goal, hint_text)


func _close_menu() -> void:
	menu_kind = ""
	hud.close_menu()
	player.set_controls_enabled(true)
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED


func _apply_mined_sites() -> void:
	if mined_sites.is_empty():
		return
	var sites := get_tree().get_nodes_in_group("fossil_site")
	for site: Node3D in sites:
		if mined_sites.has(str(site.get_meta("site_index", -1))):
			site.queue_free()


func _apply_caught_birds() -> void:
	for bird: Node3D in world.get_tree().get_nodes_in_group("wildlife"):
		if caught_birds.has(str(bird.get_meta("bird_index", -1))):
			bird.queue_free()


func _save_game() -> void:
	var data := {
		"money": money,
		"inventory": inventory,
		"unlocked_tools": unlocked_tools,
		"active_tool": active_tool,
		"research_points": research_points,
		"birds_caught": birds_caught,
		"eggs": eggs,
		"dinosaurs": dinosaurs,
		"zoo_income": zoo_income,
		"mined_sites": mined_sites,
		"caught_birds": caught_birds,
		"world_seed": world_seed,
		"goal": goal,
	}
	var file := FileAccess.open(SAVE_PATH, FileAccess.WRITE)
	if file == null:
		push_error("Spielstand konnte nicht gespeichert werden: %s" % error_string(FileAccess.get_open_error()))
		return
	file.store_string(JSON.stringify(data))


func _load_game() -> void:
	if not FileAccess.file_exists(SAVE_PATH):
		return
	var file := FileAccess.open(SAVE_PATH, FileAccess.READ)
	if file == null:
		push_warning("Spielstand konnte nicht geöffnet werden; ein neues Spiel wird gestartet.")
		return
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if not parsed is Dictionary:
		push_warning("Der Spielstand ist ungültig; ein neues Spiel wird gestartet.")
		return
	var data: Dictionary = parsed
	money = maxi(0, int(data.get("money", 0)))
	inventory.clear()
	for item: Variant in data.get("inventory", []):
		if FOSSILS.has(str(item)):
			inventory.append(str(item))
	unlocked_tools = ["hands"]
	for tool: Variant in data.get("unlocked_tools", ["hands"]):
		if TOOLS.has(str(tool)) and not unlocked_tools.has(str(tool)):
			unlocked_tools.append(str(tool))
	active_tool = str(data.get("active_tool", "hands"))
	if not unlocked_tools.has(active_tool):
		active_tool = "hands"
	research_points = maxi(0, int(data.get("research_points", 0)))
	birds_caught = maxi(0, int(data.get("birds_caught", 0)))
	eggs = maxi(0, int(data.get("eggs", 0)))
	dinosaurs = maxi(0, int(data.get("dinosaurs", 0)))
	zoo_income = maxi(0, int(data.get("zoo_income", 0)))
	mined_sites.clear()
	for site: Variant in data.get("mined_sites", []):
		mined_sites.append(str(site))
	caught_birds.clear()
	for bird: Variant in data.get("caught_birds", []):
		caught_birds.append(str(bird))
	world_seed = int(data.get("world_seed", world_seed))
	goal = str(data.get("goal", goal))

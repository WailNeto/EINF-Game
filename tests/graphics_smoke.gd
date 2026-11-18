extends SceneTree


func _initialize() -> void:
	call_deferred("_run_test")


func _run_test() -> void:
	var scene := load("res://scenes/main.tscn") as PackedScene
	var game := scene.instantiate()
	root.add_child(game)
	await process_frame
	await process_frame

	var world = game.get_node("World")
	var player: CharacterBody3D = game.get_node("Player")
	var camera: Camera3D = player.get("camera")
	var hud: CanvasLayer = game.get_node("HUD")
	var sun: DirectionalLight3D = world.get("sun")
	for setting: String in game.graphics_settings:
		if int(game.graphics_settings[setting]) != 3:
			push_error("The game did not start with the %s option set to Ultra" % setting)
			quit(1)
			return
	if float(world.get("world_size")) != 280.0:
		push_error("Expected the expanded 280 m world")
		quit(1)
		return
	if world.get_children().filter(func(node: Node) -> bool: return node is MultiMeshInstance3D).is_empty():
		push_error("Vegetation was not grouped into MultiMesh chunks")
		quit(1)
		return
	if world.get("terrain_lod_near").size() != 49 or world.get("terrain_lod_far").size() != 49:
		push_error("Terrain was not split into near and far LOD chunks")
		quit(1)
		return
	var ground_material: ShaderMaterial = world.get("ground_material")
	if not str(ground_material.shader.code).contains("cull_disabled") or not str(ground_material.shader.code).contains("depth_draw_opaque"):
		push_error("Terrain material must render as opaque and double-sided")
		quit(1)
		return
	var buildings: Array = world.get("cullable_buildings")
	if buildings.size() != 3:
		push_error("Expected three close, accessible buildings")
		quit(1)
		return
	var architecture: Array[String] = []
	var building_x_positions: Array[float] = []
	for building: Node3D in buildings:
		architecture.append(str(building.get_meta("architecture")))
		building_x_positions.append(building.position.x)
		var ramp_collision: StaticBody3D = building.get_node_or_null("EntranceRampCollision")
		if not is_instance_valid(ramp_collision) or ramp_collision.rotation.x <= 0.0:
			push_error("The %s entrance is missing its ground-level access ramp" % str(building.get_meta("building_type")))
			quit(1)
			return
	if architecture.size() != 3 or architecture[0] == architecture[1] or architecture[1] == architecture[2]:
		push_error("Buildings do not have distinct silhouettes")
		quit(1)
		return
	building_x_positions.sort()
	if building_x_positions.back() - building_x_positions.front() > 40.0:
		push_error("The market, lab, and zoo are not grouped together")
		quit(1)
		return

	var birds: Array = world.get("wildlife")
	var living_bird_count := 0
	for bird: Variant in birds:
		if is_instance_valid(bird) and not bird.is_queued_for_deletion() and bird is CharacterBody3D:
			living_bird_count += 1
	if living_bird_count == 0:
		push_error("Birds must use the animated roaming animal controller")
		quit(1)
		return
	world.spawn_dinosaur(999)
	var animal: CharacterBody3D = world.dinosaurs.back()
	animal.set("target", animal.position + Vector3(2.0, 0.0, 0.0))
	animal.set("idle_time", 0.0)
	var animal_start := animal.position
	await create_timer(0.8).timeout
	if animal.position.distance_to(animal_start) < 0.1:
		push_error("Wildlife did not move toward its roaming target")
		quit(1)
		return

	for fossil_key: String in ["ammonite", "fish", "leaf", "feather", "tooth", "claw"]:
		var fossil_mesh: ArrayMesh = world.call("_create_fossil_mesh", fossil_key)
		if fossil_mesh.get_surface_count() == 0 or fossil_mesh.surface_get_array_len(0) == 0:
			push_error("Fossil mesh is missing for %s" % fossil_key)
			quit(1)
			return
	var switch_targets: Array = get_nodes_in_group("interactable").filter(
		func(node: Node) -> bool: return node.get_meta("kind", "") == "lab_switch"
	)
	if switch_targets.is_empty():
		push_error("The laboratory switch is missing")
		quit(1)
		return
	game.lab_switch_on = false
	game.research_points = 100
	game.birds_caught = 1
	game.eggs = 0
	game._create_egg()
	if game.eggs != 0 or game.birds_caught != 1 or game.research_points != 100:
		push_error("Dinosaur creation was not gated by the laboratory switch")
		quit(1)
		return
	game._open_menu("pause")
	if not hud.menu_content.text.contains("Welt-Seed:") or hud.menu_buttons.get_child_count() < 5:
		push_error("The pause menu did not show a copyable seed and new-world action")
		quit(1)
		return
	var current_seed := str(game.world_seed)
	game._on_menu_action("copy_seed")
	if DisplayServer.get_name() != "headless" and DisplayServer.clipboard_get() != current_seed:
		push_error("The seed copy action did not put the current seed on the clipboard")
		quit(1)
		return
	var chunks: Dictionary = world.get("instance_chunks")
	var has_broadleaf := false
	var has_rock := false
	for chunk: Dictionary in chunks.values():
		has_broadleaf = has_broadleaf or chunk.has("tree_0")
		has_rock = has_rock or chunk.has("rock_0")
	var tree_count := 0
	for chunk: Dictionary in chunks.values():
		for tree_kind: String in ["tree_0", "tree_1", "tree_2"]:
			if chunk.has(tree_kind):
				tree_count += chunk[tree_kind].size()
	if not has_broadleaf or not has_rock:
		push_error("Detailed tree and rock meshes were not instanced")
		quit(1)
		return
	if tree_count < 900:
		push_error("Ultra vegetation density was not applied: %d trees" % tree_count)
		quit(1)
		return
	await physics_frame
	for building: Node3D in buildings:
		var radius := float(building.get_meta("footprint").x) * 0.5
		if building.get_meta("building_type") == "market":
			radius = float(building.get_meta("footprint").y) * 0.5
		var ray_start := building.global_position + Vector3(0, 1.5, radius + 2.4)
		var ray_end := building.global_position + Vector3(0, 1.5, 0)
		var door_query := PhysicsRayQueryParameters3D.create(ray_start, ray_end)
		var door_result: Dictionary = world.get_world_3d().direct_space_state.intersect_ray(door_query)
		if not door_result.is_empty():
			push_error("The %s entrance is blocked by collision" % str(building.get_meta("building_type")))
			quit(1)
			return

	game._close_menu()
	player.position = Vector3(-18, 0.2, -5.0)
	player.velocity = Vector3.ZERO
	player.rotation = Vector3.ZERO
	camera.rotation = Vector3.ZERO
	await physics_frame
	_send_key(KEY_W, true)
	await create_timer(1.8).timeout
	_send_key(KEY_W, false)
	if player.position.z > -10.0 or player.position.y > 0.75:
		push_error("The market entrance ramp was not traversable without jumping")
		quit(1)
		return

	game._open_menu("settings")
	if hud.menu_title.text != "Grafikeinstellungen" or hud.menu_buttons.get_child_count() != 9:
		push_error("Graphics settings menu did not expose all eight options")
		quit(1)
		return

	var settings: Dictionary = game.graphics_settings.duplicate()
	settings["render_distance"] = 3
	settings["shadow_quality"] = 3
	settings["fog_quality"] = 3
	settings["vegetation_quality"] = 3
	settings["texture_quality"] = 3
	settings["anti_aliasing"] = 3
	world.apply_graphics_settings(settings, camera)
	await process_frame
	var environment: Environment = world.get("environment")
	var far_terrain: Array = world.get("terrain_lod_far")
	if camera.far != 760.0 or far_terrain[0].visibility_range_end != 760.0 or not environment.ssao_enabled or not environment.ssil_enabled or not environment.ssr_enabled or not environment.sdfgi_enabled or not environment.volumetric_fog_enabled:
		push_error("Ultra render distance or Forward+ lighting options were not applied")
		quit(1)
		return
	if root.get_viewport().msaa_3d != Viewport.MSAA_8X or root.get_viewport().screen_space_aa != Viewport.SCREEN_SPACE_AA_FXAA or not is_equal_approx(root.get_viewport().scaling_3d_scale, 1.35) or not is_equal_approx(float(ground_material.get_shader_parameter("detail_scale")), 2.0):
		push_error("Ultra anti-aliasing, render scaling, or terrain detail settings were not applied: MSAA=%d FXAA=%d scale=%.2f detail=%.2f" % [
			root.get_viewport().msaa_3d,
			root.get_viewport().screen_space_aa,
			root.get_viewport().scaling_3d_scale,
			float(ground_material.get_shader_parameter("detail_scale")),
		])
		quit(1)
		return
	if int(world.get("vegetation_quality")) != 3 or sun.directional_shadow_max_distance != 340.0:
		push_error("Ultra vegetation density or shadow distance did not update")
		quit(1)
		return

	var save_path := "user://dino_genesis_save.json"
	var had_save := FileAccess.file_exists(save_path)
	var saved_data := ""
	if had_save:
		var saved_file := FileAccess.open(save_path, FileAccess.READ)
		if saved_file == null:
			push_error("Could not preserve the current save before testing world reset")
			quit(1)
			return
		saved_data = saved_file.get_as_text()
	game.lab_switch_on = false
	game.birds_caught = 1
	game.research_points = 100
	game._interact(switch_targets[0])
	game._create_egg()
	if not game.lab_switch_on or game.eggs != 1 or game.birds_caught != 0 or game.research_points != 75:
		_restore_save(save_path, had_save, saved_data)
		push_error("Activating the laboratory switch did not enable dinosaur creation")
		quit(1)
		return
	var previous_seed: int = game.world_seed
	game.money = 42
	game.inventory.append("tooth")
	game._start_new_world()
	if game.world_seed == previous_seed or game.money != 0 or not game.inventory.is_empty() or game.eggs != 0 or game.lab_switch_on:
		_restore_save(save_path, had_save, saved_data)
		push_error("Starting a new world did not change the seed and reset progression")
		quit(1)
		return
	if not _restore_save(save_path, had_save, saved_data):
		quit(1)
		return
	print("Opaque terrain, varied MultiMesh trees and rocks, distinct nearby buildings with clear entrances, graphics settings, and LOD tests passed")
	print("Roaming animals, distinct fossil meshes, lab-switch gating, copyable seeds, and new-world reset tests passed")
	quit()


func _restore_save(path: String, existed: bool, contents: String) -> bool:
	if existed:
		var file := FileAccess.open(path, FileAccess.WRITE)
		if file == null:
			push_error("Could not restore the original save after the smoke test")
			return false
		file.store_string(contents)
		return true
	if FileAccess.file_exists(path):
		var absolute_path := ProjectSettings.globalize_path(path)
		var error := DirAccess.remove_absolute(absolute_path)
		if error != OK:
			push_error("Could not remove the temporary smoke-test save: %s" % error_string(error))
			return false
	return true


func _send_key(key: int, pressed: bool) -> void:
	var event := InputEventKey.new()
	event.physical_keycode = key
	event.keycode = key
	event.pressed = pressed
	Input.parse_input_event(event)

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
	if float(world.get("world_size")) != 200.0:
		push_error("Expected the expanded 200 m world")
		quit(1)
		return
	if world.get_children().filter(func(node: Node) -> bool: return node is MultiMeshInstance3D).is_empty():
		push_error("Vegetation was not grouped into MultiMesh chunks")
		quit(1)
		return
	if world.get("terrain_lod_near").size() != 25 or world.get("terrain_lod_far").size() != 25:
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
	if architecture.size() != 3 or architecture[0] == architecture[1] or architecture[1] == architecture[2]:
		push_error("Buildings do not have distinct silhouettes")
		quit(1)
		return
	building_x_positions.sort()
	if building_x_positions.back() - building_x_positions.front() > 40.0:
		push_error("The market, lab, and zoo are not grouped together")
		quit(1)
		return
	var chunks: Dictionary = world.get("instance_chunks")
	var has_broadleaf := false
	var has_rock := false
	for chunk: Dictionary in chunks.values():
		has_broadleaf = has_broadleaf or chunk.has("tree_0")
		has_rock = has_rock or chunk.has("rock_0")
	if not has_broadleaf or not has_rock:
		push_error("Detailed tree and rock meshes were not instanced")
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

	game._open_menu("settings")
	var hud: CanvasLayer = game.get_node("HUD")
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
	if camera.far != 620.0 or far_terrain[0].visibility_range_end != 620.0 or not environment.ssao_enabled or not environment.ssr_enabled or not environment.volumetric_fog_enabled:
		push_error("Render distance or Forward+ lighting options were not applied")
		quit(1)
		return
	if root.get_viewport().msaa_3d != Viewport.MSAA_8X or float(ground_material.get_shader_parameter("detail_scale")) != 1.5:
		push_error("Anti-aliasing or terrain detail settings were not applied")
		quit(1)
		return
	if int(world.get("vegetation_quality")) != 3:
		push_error("Vegetation density did not update")
		quit(1)
		return
	print("Opaque terrain, varied MultiMesh trees and rocks, distinct nearby buildings with clear entrances, graphics settings, and LOD tests passed")
	quit()

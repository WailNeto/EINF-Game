extends SceneTree


func _initialize() -> void:
	call_deferred("_run_test")


func _run_test() -> void:
	var scene := load("res://scenes/main.tscn") as PackedScene
	var game := scene.instantiate()
	root.add_child(game)
	await process_frame
	await physics_frame
	var player: CharacterBody3D = game.get_node("Player")
	await create_timer(0.5).timeout
	for key: int in [KEY_W, KEY_A, KEY_S, KEY_D]:
		var before := player.position
		_send_key(key, true)
		await create_timer(0.25).timeout
		_send_key(key, false)
		if player.position.distance_to(before) <= 0.25:
			push_error("Physical key %s did not move the player" % key)
			quit(1)
			return
		await create_timer(0.2).timeout

	var camera: Camera3D = player.get("camera")
	var camera_base_height := camera.position.y
	var camera_bob := 0.0
	_send_key(KEY_W, true)
	for frame in range(24):
		await process_frame
		camera_bob = maxf(camera_bob, absf(camera.position.y - camera_base_height))
	_send_key(KEY_W, false)
	if camera_bob < 0.01:
		push_error("The camera did not respond to walking movement")
		quit(1)
		return

	var walk_start := player.position
	_send_key(KEY_W, true)
	await create_timer(0.3).timeout
	_send_key(KEY_W, false)
	var walk_distance := walk_start.distance_to(player.position)
	await create_timer(0.2).timeout
	var sprint_start := player.position
	_send_key(KEY_SHIFT, true)
	_send_key(KEY_W, true)
	await create_timer(0.3).timeout
	_send_key(KEY_W, false)
	_send_key(KEY_SHIFT, false)
	var sprint_distance := sprint_start.distance_to(player.position)
	if sprint_distance <= walk_distance * 1.2:
		push_error("Shift+W did not sprint faster than walking: walk=%f sprint=%f" % [walk_distance, sprint_distance])
		quit(1)
		return

	await create_timer(0.3).timeout
	var jump_start := player.position.y
	_send_key(KEY_SPACE, true)
	await create_timer(0.1).timeout
	_send_key(KEY_SPACE, false)
	var jump_peak := player.position.y
	for frame in range(30):
		await process_frame
		jump_peak = maxf(jump_peak, player.position.y)
	if jump_peak <= jump_start + 0.3:
		push_error("Space did not make the player jump")
		quit(1)
		return
	if jump_peak > jump_start + 1.5:
		push_error("The jump is still higher than the intended earth-like jump")
		quit(1)
		return

	_send_key(KEY_E, true)
	await create_timer(0.1).timeout
	_send_key(KEY_E, false)
	if game.menu_kind != "inventory":
		push_error("E did not open the inventory with no target")
		quit(1)
		return
	print("WASD, Shift+W sprint, Space jump, and E inventory smoke test passed")
	quit()


func _send_key(key: int, pressed: bool) -> void:
	var event := InputEventKey.new()
	event.physical_keycode = key
	event.keycode = key
	event.pressed = pressed
	Input.parse_input_event(event)

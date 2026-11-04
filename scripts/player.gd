extends CharacterBody3D

const WALK_SPEED := 6.0
const SPRINT_SPEED := 9.0
const JUMP_VELOCITY := 4.4
const LOOK_SENSITIVITY := 0.0025
const INTERACTION_DISTANCE := 4.5
var controls_enabled := true
var camera: Camera3D
var held_physical_keys: Dictionary = {}
var jump_requested := false
var camera_bob_phase := 0.0
var landing_bob := 0.0


func _ready() -> void:
	_add_input_action("move_forward", KEY_W)
	_add_input_action("move_back", KEY_S)
	_add_input_action("move_left", KEY_A)
	_add_input_action("move_right", KEY_D)
	_add_input_action("jump", KEY_SPACE)
	_add_input_action("sprint", KEY_SHIFT)
	_add_input_action("interact", KEY_F)
	_add_input_action("inventory", KEY_E)
	_add_input_action("inventory", KEY_I)
	_add_input_action("pause", KEY_ESCAPE)
	if not InputMap.has_action("dig"):
		InputMap.add_action("dig")
	var dig_event := InputEventMouseButton.new()
	dig_event.button_index = MOUSE_BUTTON_LEFT
	InputMap.action_add_event("dig", dig_event)

	var collision := CollisionShape3D.new()
	var capsule := CapsuleShape3D.new()
	capsule.radius = 0.38
	capsule.height = 1.8
	collision.shape = capsule
	collision.position.y = 0.9
	add_child(collision)

	camera = Camera3D.new()
	camera.position.y = 1.58
	camera.current = true
	add_child(camera)


func _add_input_action(action_name: String, key: int) -> void:
	if not InputMap.has_action(action_name):
		InputMap.add_action(action_name)
	var event := InputEventKey.new()
	event.physical_keycode = key
	event.keycode = key
	InputMap.action_add_event(action_name, event)


func _unhandled_input(event: InputEvent) -> void:
	if not controls_enabled:
		return
	if event is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		rotate_y(-event.relative.x * LOOK_SENSITIVITY)
		camera.rotate_x(-event.relative.y * LOOK_SENSITIVITY)
		camera.rotation.x = clampf(camera.rotation.x, deg_to_rad(-85), deg_to_rad(85))


func _input(event: InputEvent) -> void:
	if event is InputEventKey:
		var key_event := event as InputEventKey
		var key := key_event.physical_keycode if key_event.physical_keycode != KEY_NONE else key_event.keycode
		if key in [KEY_W, KEY_A, KEY_S, KEY_D, KEY_SHIFT, KEY_SPACE]:
			held_physical_keys[key] = key_event.pressed
		if key == KEY_SPACE and key_event.pressed and not key_event.echo:
			jump_requested = true


func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_WINDOW_FOCUS_OUT:
		held_physical_keys.clear()
		jump_requested = false


func _physics_process(delta: float) -> void:
	if not controls_enabled:
		jump_requested = false
		return
	var was_on_floor := is_on_floor()
	if not is_on_floor():
		velocity.y -= 9.8 * delta
	elif jump_requested or Input.is_action_just_pressed("jump"):
		velocity.y = JUMP_VELOCITY
	jump_requested = false

	var input_vector := Input.get_vector("move_left", "move_right", "move_forward", "move_back")
	input_vector.x += (1.0 if held_physical_keys.get(KEY_D, false) else 0.0) - (1.0 if held_physical_keys.get(KEY_A, false) else 0.0)
	input_vector.y += (1.0 if held_physical_keys.get(KEY_S, false) else 0.0) - (1.0 if held_physical_keys.get(KEY_W, false) else 0.0)
	input_vector = input_vector.limit_length(1.0)
	var direction := (transform.basis * Vector3(input_vector.x, 0.0, input_vector.y)).normalized()
	var sprinting_forward: bool = (Input.is_action_pressed("sprint") or bool(held_physical_keys.get(KEY_SHIFT, false))) and input_vector.y < -0.5
	var speed := SPRINT_SPEED if sprinting_forward else WALK_SPEED
	if direction:
		velocity.x = direction.x * speed
		velocity.z = direction.z * speed
	else:
		velocity.x = move_toward(velocity.x, 0.0, speed * 7.0 * delta)
		velocity.z = move_toward(velocity.z, 0.0, speed * 7.0 * delta)
	move_and_slide()
	if not was_on_floor and is_on_floor():
		landing_bob = -0.075
	landing_bob = move_toward(landing_bob, 0.0, delta * 0.48)
	var horizontal_speed := Vector2(velocity.x, velocity.z).length()
	var bob_height := 0.0
	var bob_sway := 0.0
	if is_on_floor() and horizontal_speed > 0.25:
		camera_bob_phase += delta * (10.5 if sprinting_forward else 8.0)
		var amplitude := 0.055 if sprinting_forward else 0.034
		bob_height = absf(sin(camera_bob_phase)) * amplitude
		bob_sway = cos(camera_bob_phase * 0.5) * amplitude * 0.32
	camera.position.y = lerpf(camera.position.y, 1.58 + bob_height + landing_bob, minf(delta * 12.0, 1.0))
	camera.position.x = lerpf(camera.position.x, bob_sway, minf(delta * 10.0, 1.0))


func set_controls_enabled(enabled: bool) -> void:
	controls_enabled = enabled
	if not enabled:
		velocity = Vector3.ZERO


func get_aimed_interactable() -> Node3D:
	var origin := camera.global_position
	var end := origin + (-camera.global_transform.basis.z * INTERACTION_DISTANCE)
	var query := PhysicsRayQueryParameters3D.create(origin, end)
	query.exclude = [get_rid()]
	query.collide_with_areas = true
	var result := get_world_3d().direct_space_state.intersect_ray(query)
	if result.is_empty():
		return null
	var collider: Object = result.get("collider")
	if collider is Node3D and collider.is_in_group("interactable"):
		return collider as Node3D
	return null

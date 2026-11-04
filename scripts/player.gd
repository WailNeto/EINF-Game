extends CharacterBody3D

const WALK_SPEED := 6.0
const SPRINT_SPEED := 9.0
const JUMP_VELOCITY := 5.2
const LOOK_SENSITIVITY := 0.0025
const INTERACTION_DISTANCE := 4.5

var controls_enabled := true
var camera: Camera3D


func _ready() -> void:
	_add_input_action("move_forward", KEY_W)
	_add_input_action("move_back", KEY_S)
	_add_input_action("move_left", KEY_A)
	_add_input_action("move_right", KEY_D)
	_add_input_action("jump", KEY_SPACE)
	_add_input_action("sprint", KEY_SHIFT)
	_add_input_action("interact", KEY_E)
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
	InputMap.action_add_event(action_name, event)


func _unhandled_input(event: InputEvent) -> void:
	if not controls_enabled:
		return
	if event is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		rotate_y(-event.relative.x * LOOK_SENSITIVITY)
		camera.rotate_x(-event.relative.y * LOOK_SENSITIVITY)
		camera.rotation.x = clampf(camera.rotation.x, deg_to_rad(-85), deg_to_rad(85))


func _physics_process(delta: float) -> void:
	if not controls_enabled:
		return
	if not is_on_floor():
		velocity.y -= 9.8 * delta
	elif Input.is_action_just_pressed("jump"):
		velocity.y = JUMP_VELOCITY

	var input_vector := Vector2.ZERO
	if Input.is_physical_key_pressed(KEY_A):
		input_vector.x -= 1.0
	if Input.is_physical_key_pressed(KEY_D):
		input_vector.x += 1.0
	if Input.is_physical_key_pressed(KEY_W):
		input_vector.y -= 1.0
	if Input.is_physical_key_pressed(KEY_S):
		input_vector.y += 1.0
	input_vector = input_vector.normalized()
	var direction := (transform.basis * Vector3(input_vector.x, 0.0, input_vector.y)).normalized()
	var speed := SPRINT_SPEED if Input.is_action_pressed("sprint") else WALK_SPEED
	if direction:
		velocity.x = direction.x * speed
		velocity.z = direction.z * speed
	else:
		velocity.x = move_toward(velocity.x, 0.0, speed * 7.0 * delta)
		velocity.z = move_toward(velocity.z, 0.0, speed * 7.0 * delta)
	move_and_slide()


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

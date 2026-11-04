extends CharacterBody3D

var species := "bird"
var world: Node3D
var roam_center := Vector3.ZERO
var roam_radius := 10.0
var animal_seed := 1
var bird_index := -1

var rng := RandomNumberGenerator.new()
var target := Vector3.ZERO
var idle_time := 0.0
var animation_clock := 0.0
var gait_speed := 0.0
var speed_variation := 1.0
var hop_timer := 0.0
var body_pivot: Node3D
var left_wing: Node3D
var right_wing: Node3D
var left_leg: Node3D
var right_leg: Node3D
var tail_pivot: Node3D
var head_pivot: Node3D

const GRAVITY := 9.81


func configure(world_node: Node3D, animal_species: String, center: Vector3, radius: float, seed_value: int, index: int = -1) -> void:
	world = world_node
	species = animal_species
	roam_center = center
	roam_radius = radius
	animal_seed = seed_value
	bird_index = index


func _ready() -> void:
	rng.seed = animal_seed
	speed_variation = rng.randf_range(0.9, 1.1)
	hop_timer = rng.randf_range(1.0, 3.0)
	position = roam_center
	if species == "bird":
		add_to_group("interactable")
		add_to_group("wildlife")
		set_meta("kind", "bird")
		set_meta("bird_index", bird_index)
		_build_bird()
	else:
		add_to_group("dinosaurs")
		_build_dinosaur()
	_choose_target()
	idle_time = rng.randf_range(0.3, 1.8)


func _physics_process(delta: float) -> void:
	animation_clock += delta
	if not is_on_floor():
		velocity.y -= GRAVITY * delta
	else:
		velocity.y = 0.0

	idle_time = maxf(0.0, idle_time - delta)
	var offset := Vector2(target.x - global_position.x, target.z - global_position.z)
	if offset.length() < 0.8:
		_choose_target()
		offset = Vector2(target.x - global_position.x, target.z - global_position.z)
	var moving := idle_time <= 0.0 and offset.length() > 0.8
	var speed := (1.45 if species == "bird" else 1.05) * speed_variation
	var direction := Vector3(offset.x, 0.0, offset.y).normalized() if moving else Vector3.ZERO
	gait_speed = speed if moving else 0.0
	if species == "bird" and moving and is_on_floor():
		hop_timer -= delta
		if hop_timer <= 0.0:
			velocity.y = 2.15
			hop_timer = rng.randf_range(2.5, 5.0)
	if moving:
		velocity.x = direction.x * speed
		velocity.z = direction.z * speed
		rotation.y = lerp_angle(rotation.y, atan2(-direction.x, -direction.z), minf(delta * 4.5, 1.0))
	else:
		velocity.x = move_toward(velocity.x, 0.0, 3.5 * delta)
		velocity.z = move_toward(velocity.z, 0.0, 3.5 * delta)
	move_and_slide()

	var ground_height := float(world.call("height_at", position.x, position.z))
	if is_on_floor() and absf(position.y - ground_height) > 0.2:
		position.y = move_toward(position.y, ground_height, delta * 4.0)
	_animate(delta, moving)


func _choose_target() -> void:
	var angle := rng.randf_range(0.0, TAU)
	var distance := sqrt(rng.randf()) * roam_radius
	target = roam_center + Vector3(cos(angle) * distance, 0.0, sin(angle) * distance)
	target.y = float(world.call("height_at", target.x, target.z))
	idle_time = rng.randf_range(0.5, 2.8) if rng.randf() < 0.38 else 0.0


func _build_bird() -> void:
	var collision := CollisionShape3D.new()
	var capsule := CapsuleShape3D.new()
	capsule.radius = 0.34
	capsule.height = 1.15
	collision.shape = capsule
	collision.position.y = 0.58
	add_child(collision)

	body_pivot = Node3D.new()
	body_pivot.position.y = 0.56
	add_child(body_pivot)
	_add_ellipsoid(body_pivot, Vector3(0.0, 0.0, 0.0), Vector3(0.36, 0.31, 0.55), _mat(Color(0.46, 0.34, 0.2), 0.9))
	_add_ellipsoid(body_pivot, Vector3(0.0, -0.07, -0.12), Vector3(0.28, 0.22, 0.37), _mat(Color(0.69, 0.57, 0.38), 0.94))

	head_pivot = Node3D.new()
	head_pivot.position = Vector3(0.0, 0.22, -0.42)
	body_pivot.add_child(head_pivot)
	_add_ellipsoid(head_pivot, Vector3(0.0, 0.16, -0.09), Vector3(0.22, 0.24, 0.23), _mat(Color(0.49, 0.36, 0.2), 0.86))
	_add_ellipsoid(head_pivot, Vector3(0.0, 0.07, -0.28), Vector3(0.15, 0.11, 0.23), _mat(Color(0.77, 0.61, 0.37), 0.84))
	var beak := MeshInstance3D.new()
	var beak_mesh := CylinderMesh.new()
	beak_mesh.top_radius = 0.012
	beak_mesh.bottom_radius = 0.105
	beak_mesh.height = 0.26
	beak_mesh.radial_segments = 8
	beak.mesh = beak_mesh
	beak.position = Vector3(0.0, 0.075, -0.47)
	beak.rotation.x = deg_to_rad(-74.0)
	beak.material_override = _mat(Color(0.88, 0.59, 0.12), 0.45)
	head_pivot.add_child(beak)
	for side: float in [-1.0, 1.0]:
		_add_ellipsoid(head_pivot, Vector3(side * 0.18, 0.2, -0.16), Vector3(0.07, 0.075, 0.07), _mat(Color(0.94, 0.86, 0.63), 0.32))
		_add_ellipsoid(head_pivot, Vector3(side * 0.212, 0.205, -0.19), Vector3(0.033, 0.045, 0.04), _mat(Color(0.06, 0.045, 0.025), 0.2))
		_add_ellipsoid(head_pivot, Vector3(side * 0.224, 0.22, -0.21), Vector3(0.012, 0.014, 0.012), _mat(Color.WHITE, 0.12))

	for side: float in [-1.0, 1.0]:
		var wing := Node3D.new()
		wing.position = Vector3(side * 0.3, 0.12, -0.02)
		body_pivot.add_child(wing)
		if side < 0.0:
			left_wing = wing
		else:
			right_wing = wing
		_add_ellipsoid(wing, Vector3(side * 0.22, 0.02, 0.02), Vector3(0.31, 0.075, 0.28), _mat(Color(0.4, 0.28, 0.16), 0.88))
		for feather_index in range(4):
			var feather := MeshInstance3D.new()
			var feather_mesh := CapsuleMesh.new()
			feather_mesh.radius = 0.055
			feather_mesh.height = 0.36
			feather.mesh = feather_mesh
			feather.position = Vector3(side * (0.32 + feather_index * 0.045), -0.035, -0.12 + feather_index * 0.1)
			feather.rotation.z = side * deg_to_rad(74.0)
			feather.material_override = _mat(Color(0.54, 0.4, 0.24), 0.9)
			wing.add_child(feather)
	for feather_index in range(3):
		var tail_feather := MeshInstance3D.new()
		var tail_mesh := CapsuleMesh.new()
		tail_mesh.radius = 0.065
		tail_mesh.height = 0.5
		tail_feather.mesh = tail_mesh
		tail_feather.position = Vector3((float(feather_index) - 1.0) * 0.12, -0.02, 0.52)
		tail_feather.rotation.x = deg_to_rad(86.0)
		tail_feather.rotation.z = deg_to_rad((float(feather_index) - 1.0) * 12.0)
		tail_feather.material_override = _mat(Color(0.39, 0.28, 0.17), 0.88)
		body_pivot.add_child(tail_feather)

	for side: float in [-1.0, 1.0]:
		var leg := Node3D.new()
		leg.position = Vector3(side * 0.14, -0.2, 0.05)
		body_pivot.add_child(leg)
		if side < 0.0:
			left_leg = leg
		else:
			right_leg = leg
		_add_capsule(leg, Vector3(0, -0.12, 0), 0.045, 0.27, _mat(Color(0.49, 0.36, 0.18), 0.75))
		_add_ellipsoid(leg, Vector3(0, -0.255, -0.08), Vector3(0.09, 0.035, 0.13), _mat(Color(0.75, 0.55, 0.23), 0.6))
		for toe: float in [-1.0, 0.0, 1.0]:
			_add_tapered_piece(leg, Vector3(toe * 0.052, -0.26, -0.12), Vector3(toe * 0.07, -0.27, -0.22), 0.027, 0.008, _mat(Color(0.83, 0.68, 0.38), 0.55))


func _build_dinosaur() -> void:
	var collision := CollisionShape3D.new()
	var capsule := CapsuleShape3D.new()
	capsule.radius = 0.48
	capsule.height = 1.55
	collision.shape = capsule
	collision.position.y = 0.78
	add_child(collision)

	body_pivot = Node3D.new()
	body_pivot.position = Vector3(0.0, 0.94, 0.0)
	add_child(body_pivot)
	var hide_color := Color(0.31, 0.4, 0.19)
	var belly_color := Color(0.59, 0.54, 0.34)
	_add_ellipsoid(body_pivot, Vector3(0.0, 0.0, 0.0), Vector3(0.57, 0.43, 0.94), _mat(hide_color, 0.91))
	_add_ellipsoid(body_pivot, Vector3(0.0, -0.22, -0.03), Vector3(0.43, 0.23, 0.7), _mat(belly_color, 0.94))
	head_pivot = Node3D.new()
	head_pivot.position = Vector3(0.0, 0.18, -0.67)
	body_pivot.add_child(head_pivot)
	_add_tapered_piece(body_pivot, Vector3(0, 0.02, -0.5), Vector3(0, 0.42, -0.91), 0.23, 0.13, _mat(hide_color, 0.9))
	_add_ellipsoid(head_pivot, Vector3(0, 0.12, -0.29), Vector3(0.3, 0.27, 0.39), _mat(hide_color, 0.9))
	_add_ellipsoid(head_pivot, Vector3(0, -0.08, -0.52), Vector3(0.22, 0.11, 0.37), _mat(belly_color, 0.88))
	for side: float in [-1.0, 1.0]:
		_add_ellipsoid(head_pivot, Vector3(side * 0.235, 0.2, -0.32), Vector3(0.09, 0.095, 0.095), _mat(Color(0.87, 0.67, 0.2), 0.42))
		_add_ellipsoid(head_pivot, Vector3(side * 0.285, 0.21, -0.35), Vector3(0.035, 0.058, 0.048), _mat(Color(0.045, 0.035, 0.02), 0.2))
		_add_ellipsoid(head_pivot, Vector3(side * 0.314, 0.232, -0.37), Vector3(0.012, 0.015, 0.012), _mat(Color.WHITE, 0.15))
		for tooth_index in range(4):
			var tooth := MeshInstance3D.new()
			var tooth_mesh := CylinderMesh.new()
			tooth_mesh.top_radius = 0.006
			tooth_mesh.bottom_radius = 0.025
			tooth_mesh.height = 0.11
			tooth_mesh.radial_segments = 5
			tooth.mesh = tooth_mesh
			tooth.position = Vector3(side * 0.17, -0.14, -0.37 - tooth_index * 0.12)
			tooth.material_override = _mat(Color(0.92, 0.85, 0.66), 0.62)
			head_pivot.add_child(tooth)

	tail_pivot = Node3D.new()
	tail_pivot.position = Vector3(0, 0.02, 0.68)
	body_pivot.add_child(tail_pivot)
	var tail_radii := [0.34, 0.27, 0.2, 0.14, 0.08]
	for segment in range(tail_radii.size() - 1):
		_add_tapered_piece(
			tail_pivot,
			Vector3(0, 0, segment * 0.34),
			Vector3(0, -0.045 * float(segment + 1), (segment + 1) * 0.34),
			tail_radii[segment],
			tail_radii[segment + 1],
			_mat(hide_color.darkened(float(segment) * 0.035), 0.9)
		)

	left_leg = _build_dinosaur_leg(-1.0, hide_color)
	right_leg = _build_dinosaur_leg(1.0, hide_color)
	for side: float in [-1.0, 1.0]:
		var arm := Node3D.new()
		arm.position = Vector3(side * 0.39, -0.04, -0.36)
		body_pivot.add_child(arm)
		_add_tapered_piece(arm, Vector3.ZERO, Vector3(side * 0.08, -0.28, -0.16), 0.12, 0.07, _mat(hide_color.lightened(0.06), 0.92))
		for claw_index in range(2):
			_add_tapered_piece(arm, Vector3(side * (0.08 + claw_index * 0.035), -0.26, -0.16), Vector3(side * (0.1 + claw_index * 0.035), -0.34, -0.22), 0.032, 0.005, _mat(Color(0.83, 0.75, 0.52), 0.6))
	for spike_index in range(7):
		var spike := MeshInstance3D.new()
		var spike_mesh := CylinderMesh.new()
		spike_mesh.top_radius = 0.01
		spike_mesh.bottom_radius = 0.1 - float(spike_index) * 0.008
		spike_mesh.height = 0.2
		spike_mesh.radial_segments = 5
		spike.mesh = spike_mesh
		spike.position = Vector3(0, 0.39, -0.42 + spike_index * 0.15)
		spike.material_override = _mat(Color(0.48, 0.49, 0.24), 0.92)
		body_pivot.add_child(spike)
	for spot_index in range(10):
		var side := -1.0 if spot_index % 2 == 0 else 1.0
		var z := -0.55 + float(spot_index / 2) * 0.23
		_add_ellipsoid(body_pivot, Vector3(side * 0.48, 0.1 + float(spot_index % 3) * 0.08, z), Vector3(0.06, 0.055, 0.09), _mat(Color(0.18, 0.25, 0.11), 0.96))


func _build_dinosaur_leg(side: float, hide_color: Color) -> Node3D:
	var leg := Node3D.new()
	leg.position = Vector3(side * 0.36, -0.17, 0.38)
	body_pivot.add_child(leg)
	_add_tapered_piece(leg, Vector3.ZERO, Vector3(0, -0.38, 0.02), 0.2, 0.12, _mat(hide_color.darkened(0.04), 0.9))
	_add_tapered_piece(leg, Vector3(0, -0.36, 0.02), Vector3(0, -0.72, -0.1), 0.11, 0.08, _mat(hide_color, 0.9))
	_add_ellipsoid(leg, Vector3(0, -0.76, -0.18), Vector3(0.17, 0.085, 0.26), _mat(hide_color.lightened(0.04), 0.86))
	for toe: float in [-1.0, 0.0, 1.0]:
		_add_tapered_piece(leg, Vector3(toe * 0.09, -0.76, -0.3), Vector3(toe * 0.1, -0.75, -0.43), 0.045, 0.006, _mat(Color(0.8, 0.73, 0.51), 0.6))
	return leg


func _animate(delta: float, moving: bool) -> void:
	var gait := sin(animation_clock * 8.0) if moving else 0.0
	if species == "bird":
		if is_instance_valid(body_pivot):
			body_pivot.position.y = lerpf(body_pivot.position.y, 0.56 + (absf(gait) * 0.06 if moving else sin(animation_clock * 2.0) * 0.025), minf(delta * 8.0, 1.0))
		if is_instance_valid(left_wing):
			var flap := sin(animation_clock * 11.0) * 0.62 if not is_on_floor() else sin(animation_clock * 2.0) * 0.045
			left_wing.rotation.z = lerpf(left_wing.rotation.z, flap, minf(delta * 12.0, 1.0))
			right_wing.rotation.z = lerpf(right_wing.rotation.z, -flap, minf(delta * 12.0, 1.0))
		if is_instance_valid(left_leg):
			var leg_swing := gait * 0.55 if moving and is_on_floor() else 0.0
			left_leg.rotation.x = leg_swing
			right_leg.rotation.x = -leg_swing
	else:
		if is_instance_valid(left_leg):
			var amplitude := 0.48 if moving else 0.025
			left_leg.rotation.x = gait * amplitude
			right_leg.rotation.x = -gait * amplitude
		if is_instance_valid(tail_pivot):
			tail_pivot.rotation.y = sin(animation_clock * 2.5) * 0.18
		if is_instance_valid(head_pivot):
			head_pivot.rotation.x = sin(animation_clock * 1.8) * 0.035 + (0.02 if not moving else 0.0)
		if is_instance_valid(body_pivot):
			body_pivot.position.y = lerpf(body_pivot.position.y, 0.94 + (absf(gait) * 0.035 if moving else 0.0), minf(delta * 6.0, 1.0))


func _add_ellipsoid(parent: Node3D, offset: Vector3, scale_value: Vector3, material: Material) -> MeshInstance3D:
	var visual := MeshInstance3D.new()
	var mesh := SphereMesh.new()
	mesh.radius = 0.5
	mesh.height = 1.0
	mesh.radial_segments = 16
	mesh.rings = 10
	visual.mesh = mesh
	visual.position = offset
	visual.scale = scale_value * 2.0
	visual.material_override = material
	parent.add_child(visual)
	return visual


func _add_capsule(parent: Node3D, offset: Vector3, radius: float, height: float, material: Material) -> void:
	var visual := MeshInstance3D.new()
	var mesh := CapsuleMesh.new()
	mesh.radius = radius
	mesh.height = height
	visual.mesh = mesh
	visual.position = offset
	visual.material_override = material
	parent.add_child(visual)


func _add_tapered_piece(parent: Node3D, start: Vector3, finish: Vector3, base_radius: float, tip_radius: float, material: Material) -> void:
	var visual := MeshInstance3D.new()
	var mesh := CylinderMesh.new()
	mesh.top_radius = tip_radius
	mesh.bottom_radius = base_radius
	mesh.height = start.distance_to(finish)
	mesh.radial_segments = 8
	visual.mesh = mesh
	visual.position = (start + finish) * 0.5
	visual.quaternion = Quaternion(Vector3.UP, (finish - start).normalized())
	visual.material_override = material
	parent.add_child(visual)


func _mat(color_value: Color, roughness: float) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color_value
	material.roughness = roughness
	return material

extends Node3D

const WORLD_SIZE := 80.0
const TERRAIN_STEPS := 100
const CLEARINGS := [Vector2(0, 0), Vector2(-18, -8), Vector2(18, -8), Vector2(0, 22)]

var noise := FastNoiseLite.new()
var rng := RandomNumberGenerator.new()
var seed := 42042
var fossil_sites: Array[Node3D] = []
var wildlife: Array[Node3D] = []


func _ready() -> void:
	noise.seed = seed
	noise.frequency = 0.035
	noise.fractal_octaves = 4
	rng.seed = seed
	_build_environment()
	_build_terrain()
	_build_paths()
	_build_landmarks()
	_scatter_vegetation()
	_spawn_fossils()
	_spawn_birds()


func height_at(x: float, z: float) -> float:
	var height := noise.get_noise_2d(x, z) * 2.0
	var blend := 1.0
	for clearing: Vector2 in CLEARINGS:
		var distance := Vector2(x, z).distance_to(clearing)
		var clearing_blend := smoothstep(4.0, 12.0, distance)
		blend = minf(blend, clearing_blend)
	return height * blend


func _build_environment() -> void:
	var world_environment := WorldEnvironment.new()
	var environment := Environment.new()
	environment.background_mode = Environment.BG_SKY
	var sky := Sky.new()
	var sky_material := ProceduralSkyMaterial.new()
	sky_material.sky_top_color = Color(0.16, 0.38, 0.7)
	sky_material.sky_horizon_color = Color(0.72, 0.84, 0.91)
	sky_material.ground_bottom_color = Color(0.19, 0.25, 0.16)
	sky_material.ground_horizon_color = Color(0.58, 0.65, 0.47)
	sky.sky_material = sky_material
	environment.sky = sky
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color(0.68, 0.73, 0.8)
	environment.ambient_light_energy = 0.7
	environment.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	world_environment.environment = environment
	add_child(world_environment)

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-48, -32, 0)
	sun.light_color = Color(1.0, 0.91, 0.76)
	sun.light_energy = 1.25
	sun.shadow_enabled = true
	add_child(sun)


func _build_terrain() -> void:
	var surface := SurfaceTool.new()
	surface.begin(Mesh.PRIMITIVE_TRIANGLES)
	var step := WORLD_SIZE / float(TERRAIN_STEPS)
	for z_index in range(TERRAIN_STEPS):
		for x_index in range(TERRAIN_STEPS):
			var x0 := -WORLD_SIZE * 0.5 + x_index * step
			var x1 := x0 + step
			var z0 := -WORLD_SIZE * 0.5 + z_index * step
			var z1 := z0 + step
			var a := Vector3(x0, height_at(x0, z0), z0)
			var b := Vector3(x0, height_at(x0, z1), z1)
			var c := Vector3(x1, height_at(x1, z0), z0)
			var d := Vector3(x1, height_at(x1, z1), z1)
			_add_ground_vertex(surface, a)
			_add_ground_vertex(surface, b)
			_add_ground_vertex(surface, c)
			_add_ground_vertex(surface, c)
			_add_ground_vertex(surface, b)
			_add_ground_vertex(surface, d)
	surface.generate_normals()
	var terrain_mesh := surface.commit()
	var terrain := MeshInstance3D.new()
	terrain.mesh = terrain_mesh
	var ground_material := StandardMaterial3D.new()
	ground_material.vertex_color_use_as_albedo = true
	ground_material.roughness = 1.0
	ground_material.cull_mode = BaseMaterial3D.CULL_DISABLED
	terrain.material_override = ground_material
	add_child(terrain)
	terrain.create_trimesh_collision()

func _add_ground_vertex(surface: SurfaceTool, vertex: Vector3) -> void:
	var variation := sin(vertex.x * 0.27 + vertex.z * 0.19) * 0.035
	var grass := Color(0.24 + variation, 0.43 + variation, 0.19 + variation)
	surface.set_color(grass)
	surface.set_uv(Vector2(vertex.x, vertex.z) * 0.12)
	surface.add_vertex(vertex)


func _build_paths() -> void:
	_add_path(Vector2(-18, -8), Vector2(-10, -3))
	_add_path(Vector2(18, -8), Vector2(10, -3))
	_add_path(Vector2(0, 22), Vector2(0, 11))


func _add_path(destination: Vector2, control: Vector2) -> void:
	var path := MeshInstance3D.new()
	var surface := SurfaceTool.new()
	surface.begin(Mesh.PRIMITIVE_TRIANGLES)
	var points: Array[Vector3] = []
	for index in range(41):
		var t := float(index) / 40.0
		var inverse_t := 1.0 - t
		var p2 := Vector2.ZERO * (inverse_t * inverse_t) + control * (2.0 * inverse_t * t) + destination * (t * t)
		points.append(Vector3(p2.x, height_at(p2.x, p2.y) + 0.04, p2.y))
	for index in range(points.size() - 1):
		var start := points[index]
		var finish := points[index + 1]
		var tangent := Vector2(finish.x - start.x, finish.z - start.z).normalized()
		var side := Vector3(-tangent.y, 0, tangent.x) * 1.25
		var a := start + side
		var b := start - side
		var c := finish + side
		var d := finish - side
		for vertex: Vector3 in [a, b, c, c, b, d]:
			surface.set_color(Color(0.43, 0.31, 0.2))
			surface.add_vertex(vertex)
	surface.generate_normals()
	path.mesh = surface.commit()
	path.material_override = _material(Color(0.43, 0.31, 0.2), 1.0)
	add_child(path)


func _build_landmarks() -> void:
	_add_building(Vector3(-18, height_at(-18, -8), -8), Vector3(7, 4, 7), Color(0.22, 0.48, 0.68), "MARKT")
	_add_building(Vector3(18, height_at(18, -8), -8), Vector3(7, 4, 7), Color(0.5, 0.29, 0.61), "LABOR")
	_add_building(Vector3(0, height_at(0, 22), 22), Vector3(10, 4, 10), Color(0.3, 0.53, 0.33), "ZOO")
	_add_interactable(Vector3(-18, height_at(-18, -3) + 1.0, -3), "market")
	_add_interactable(Vector3(18, height_at(18, -3) + 1.0, -3), "lab")
	_add_interactable(Vector3(0, height_at(0, 28) + 1.0, 28), "zoo")


func _add_building(origin: Vector3, size: Vector3, wall_color: Color, sign_text: String) -> void:
	var building := Node3D.new()
	building.position = origin
	add_child(building)
	var body := MeshInstance3D.new()
	var box := BoxMesh.new()
	box.size = size
	body.mesh = box
	body.position.y = size.y * 0.5
	body.material_override = _material(wall_color)
	building.add_child(body)
	var roof := MeshInstance3D.new()
	var roof_mesh := BoxMesh.new()
	roof_mesh.size = Vector3(size.x + 0.5, 0.28, size.z + 0.5)
	roof.mesh = roof_mesh
	roof.position.y = size.y + 0.14
	roof.material_override = _material(Color(0.35, 0.2, 0.15))
	building.add_child(roof)
	var door := MeshInstance3D.new()
	var door_mesh := BoxMesh.new()
	door_mesh.size = Vector3(1.2, 2.2, 0.12)
	door.mesh = door_mesh
	door.position = Vector3(0, 1.1, size.z * 0.5 + 0.06)
	door.material_override = _material(Color(0.24, 0.16, 0.1))
	building.add_child(door)
	var sign := Label3D.new()
	sign.text = sign_text
	sign.position = Vector3(0, size.y + 1.15, size.z * 0.5 + 0.1)
	sign.font_size = 48
	sign.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	building.add_child(sign)
	var body_collision := StaticBody3D.new()
	var collision := CollisionShape3D.new()
	var collision_shape := BoxShape3D.new()
	collision_shape.size = Vector3(size.x, size.y, size.z)
	collision.shape = collision_shape
	collision.position.y = size.y * 0.5
	body_collision.add_child(collision)
	building.add_child(body_collision)


func _add_interactable(position: Vector3, kind: String) -> void:
	var target := Area3D.new()
	target.position = position
	target.add_to_group("interactable")
	target.set_meta("kind", kind)
	var collision := CollisionShape3D.new()
	var shape := SphereShape3D.new()
	shape.radius = 2.7
	collision.shape = shape
	target.add_child(collision)
	add_child(target)


func _scatter_vegetation() -> void:
	for index in range(70):
		var x := rng.randf_range(-36, 36)
		var z := rng.randf_range(-36, 36)
		if _in_clearing(x, z, 9.0):
			continue
		_add_tree(x, z)
	for index in range(55):
		var x := rng.randf_range(-37, 37)
		var z := rng.randf_range(-37, 37)
		if _in_clearing(x, z, 7.0):
			continue
		_add_bush(x, z)
	for index in range(40):
		var x := rng.randf_range(-37, 37)
		var z := rng.randf_range(-37, 37)
		if _in_clearing(x, z, 6.0):
			continue
		_add_rock(x, z)


func _add_tree(x: float, z: float) -> void:
	var ground := height_at(x, z)
	var trunk_height := rng.randf_range(2.4, 4.0)
	var tree := Node3D.new()
	tree.position = Vector3(x, ground, z)
	add_child(tree)
	var trunk := MeshInstance3D.new()
	var trunk_mesh := CylinderMesh.new()
	trunk_mesh.top_radius = 0.22
	trunk_mesh.bottom_radius = 0.38
	trunk_mesh.height = trunk_height
	trunk.mesh = trunk_mesh
	trunk.position.y = trunk_height * 0.5
	trunk.material_override = _material(Color(0.34, 0.22, 0.12))
	tree.add_child(trunk)
	for canopy_index in range(3):
		var canopy := MeshInstance3D.new()
		var canopy_mesh := SphereMesh.new()
		canopy_mesh.radius = rng.randf_range(1.0, 1.45)
		canopy_mesh.height = canopy_mesh.radius * 1.8
		canopy.mesh = canopy_mesh
		canopy.position = Vector3(
			rng.randf_range(-0.45, 0.45),
			trunk_height + rng.randf_range(-0.2, 0.75),
			rng.randf_range(-0.45, 0.45)
		)
		canopy.material_override = _material(Color(0.13, rng.randf_range(0.34, 0.48), 0.17))
		tree.add_child(canopy)


func _add_bush(x: float, z: float) -> void:
	var bush := MeshInstance3D.new()
	var mesh := SphereMesh.new()
	mesh.radius = rng.randf_range(0.45, 0.75)
	mesh.height = mesh.radius * 1.5
	bush.mesh = mesh
	bush.position = Vector3(x, height_at(x, z) + mesh.radius * 0.5, z)
	bush.scale = Vector3(1.3, 0.8, 1.1)
	bush.material_override = _material(Color(0.2, 0.39, 0.16))
	add_child(bush)


func _add_rock(x: float, z: float) -> void:
	var rock := MeshInstance3D.new()
	var mesh := SphereMesh.new()
	mesh.radius = rng.randf_range(0.4, 0.9)
	rock.mesh = mesh
	rock.position = Vector3(x, height_at(x, z) + 0.22, z)
	rock.scale = Vector3(1.3, 0.75, 1.0)
	rock.material_override = _material(Color(0.43, 0.43, 0.39))
	add_child(rock)


func _spawn_fossils() -> void:
	for index in range(15):
		var x := rng.randf_range(-34, 34)
		var z := rng.randf_range(-34, 34)
		if _in_clearing(x, z, 7.5):
			continue
		var site := StaticBody3D.new()
		site.position = Vector3(x, height_at(x, z) + 0.12, z)
		site.add_to_group("interactable")
		site.add_to_group("fossil_site")
		site.set_meta("kind", "fossil")
		site.set_meta("fossil_key", ["ammonite", "fish", "leaf", "feather", "tooth", "claw"][rng.randi_range(0, 5)])
		site.set_meta("site_index", index)
		var marker := MeshInstance3D.new()
		var marker_mesh := SphereMesh.new()
		marker_mesh.radius = 0.75
		marker_mesh.height = 0.15
		marker.mesh = marker_mesh
		marker.material_override = _material(Color(0.68, 0.54, 0.25))
		site.add_child(marker)
		var collision := CollisionShape3D.new()
		var shape := SphereShape3D.new()
		shape.radius = 0.9
		collision.shape = shape
		site.add_child(collision)
		add_child(site)
		fossil_sites.append(site)


func _spawn_birds() -> void:
	for index in range(5):
		var x := rng.randf_range(-30, 30)
		var z := rng.randf_range(10, 27)
		if _in_clearing(x, z, 5.0):
			continue
		var bird := StaticBody3D.new()
		bird.position = Vector3(x, height_at(x, z), z)
		bird.add_to_group("interactable")
		bird.add_to_group("wildlife")
		bird.set_meta("kind", "bird")
		bird.set_meta("bird_index", index)
		var body := MeshInstance3D.new()
		var body_mesh := SphereMesh.new()
		body_mesh.radius = 0.38
		body_mesh.height = 0.7
		body.mesh = body_mesh
		body.position.y = 0.72
		body.material_override = _material(Color(0.82, 0.72, 0.4))
		bird.add_child(body)
		var collision_shape := CollisionShape3D.new()
		var sphere := SphereShape3D.new()
		sphere.radius = 1.0
		collision_shape.shape = sphere
		bird.add_child(collision_shape)
		add_child(bird)
		wildlife.append(bird)


func _in_clearing(x: float, z: float, radius: float) -> bool:
	for clearing: Vector2 in CLEARINGS:
		if Vector2(x, z).distance_to(clearing) < radius:
			return true
	return false


func _material(color_value: Color, roughness: float = 0.9) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color_value
	material.roughness = roughness
	return material

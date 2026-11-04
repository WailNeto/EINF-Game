extends Node3D

const WORLD_SIZE := 200.0
const TERRAIN_STEPS := 160
const CHUNK_SIZE := 40.0
const CLEARINGS := [Vector2(0, 0), Vector2(-18, -14), Vector2(0, -14), Vector2(18, -14), Vector2(0, 55)]
const NEON_CYAN := Color(0.18, 0.88, 1.0)
const NEON_AMBER := Color(1.0, 0.67, 0.25)

var noise := FastNoiseLite.new()
var mountain_noise := FastNoiseLite.new()
var rng := RandomNumberGenerator.new()
var seed := 42042
var world_size := WORLD_SIZE
var fossil_sites: Array[Node3D] = []
var wildlife: Array[Node3D] = []
var graphics_settings: Dictionary = {}
var instance_chunks: Dictionary = {}
var cullable_buildings: Array[Node3D] = []
var vegetation_instances: Array[MultiMeshInstance3D] = []
var terrain_lod_near: Array[MeshInstance3D] = []
var terrain_lod_far: Array[MeshInstance3D] = []
var ground_material: ShaderMaterial
var environment: Environment
var sun: DirectionalLight3D
var vegetation_quality := -1
var render_distance := 380.0


func _ready() -> void:
	noise.seed = seed
	noise.frequency = 0.012
	noise.fractal_octaves = 5
	noise.fractal_lacunarity = 2.1
	noise.fractal_gain = 0.48
	mountain_noise.seed = seed + 917
	mountain_noise.frequency = 0.007
	mountain_noise.fractal_octaves = 3
	rng.seed = seed
	_build_environment()
	_build_terrain()
	_build_paths()
	_build_landmarks()
	_scatter_vegetation()
	_add_path_lighting()
	_spawn_fossils()
	_spawn_birds()


func height_at(x: float, z: float) -> float:
	var rolling := noise.get_noise_2d(x, z) * 13.0
	var ridge := 1.0 - absf(mountain_noise.get_noise_2d(x, z))
	var mountains := pow(maxf(ridge - 0.38, 0.0), 1.35) * 39.0
	var height := rolling + mountains - 1.5
	var blend := 1.0
	for clearing: Vector2 in CLEARINGS:
		var distance := Vector2(x, z).distance_to(clearing)
		var clearing_blend := smoothstep(10.0, 28.0, distance)
		blend = minf(blend, clearing_blend)
	return height * blend


func _build_environment() -> void:
	var world_environment := WorldEnvironment.new()
	environment = Environment.new()
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
	environment.ambient_light_energy = 0.85
	environment.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	environment.glow_enabled = true
	environment.glow_intensity = 0.3
	environment.glow_bloom = 0.12
	world_environment.environment = environment
	add_child(world_environment)

	sun = DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-48, -32, 0)
	sun.light_color = Color(1.0, 0.91, 0.76)
	sun.light_energy = 1.45
	sun.shadow_enabled = true
	add_child(sun)


func _build_terrain() -> void:
	_build_terrain_lod_chunks()
	var step := WORLD_SIZE / float(TERRAIN_STEPS)
	var height_data := PackedFloat32Array()
	ground_material = ShaderMaterial.new()
	var ground_shader := Shader.new()
	ground_shader.code = """
shader_type spatial;
render_mode cull_disabled, depth_draw_opaque, diffuse_burley, specular_schlick_ggx;

uniform float detail_scale = 1.0;
varying vec3 world_position;
varying vec3 world_normal;

float hash21(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash21(i), hash21(i + vec2(1.0, 0.0)), f.x),
		mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), f.x), f.y);
}

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}

void fragment() {
	vec2 p = world_position.xz;
	float broad = value_noise(p * 0.11) * 0.55 + value_noise(p * 0.037) * 0.45;
	float detail = value_noise(p * 1.65 * detail_scale);
	float slope = 1.0 - clamp(world_normal.y, 0.0, 1.0);
	float rock = smoothstep(0.28, 0.64, slope + (broad - 0.5) * 0.18);
	float patch = smoothstep(0.35, 0.72, broad);
	vec3 grass = mix(vec3(0.115, 0.19, 0.082), vec3(0.25, 0.34, 0.13), patch);
	grass *= 0.82 + detail * 0.36;
	vec3 stone = mix(vec3(0.24, 0.25, 0.22), vec3(0.42, 0.4, 0.33), detail);
	ALBEDO = mix(grass, stone, rock);
	ROUGHNESS = mix(0.94, 0.86, rock);
	SPECULAR = 0.22;
}
"""
	ground_material.shader = ground_shader
	ground_material.set_shader_parameter("detail_scale", 1.0)
	for terrain: MeshInstance3D in terrain_lod_near + terrain_lod_far:
		terrain.material_override = ground_material
	var ground_body := StaticBody3D.new()
	ground_body.name = "GroundCollision"
	var ground_collision := CollisionShape3D.new()
	var heightmap := HeightMapShape3D.new()
	heightmap.map_width = TERRAIN_STEPS + 1
	heightmap.map_depth = TERRAIN_STEPS + 1
	for z_index in range(TERRAIN_STEPS + 1):
		var z := -WORLD_SIZE * 0.5 + z_index * step
		for x_index in range(TERRAIN_STEPS + 1):
			var x := -WORLD_SIZE * 0.5 + x_index * step
			height_data.append(height_at(x, z))
	heightmap.map_data = height_data
	ground_collision.shape = heightmap
	ground_collision.scale = Vector3(step, 1.0, step)
	ground_body.add_child(ground_collision)
	add_child(ground_body)


func _build_terrain_lod_chunks() -> void:
	var chunks_per_axis := int(WORLD_SIZE / CHUNK_SIZE)
	var full_resolution := int(TERRAIN_STEPS / chunks_per_axis)
	var far_resolution := maxi(4, full_resolution / 4)
	for chunk_z in range(chunks_per_axis):
		for chunk_x in range(chunks_per_axis):
			var center_x := -WORLD_SIZE * 0.5 + chunk_x * CHUNK_SIZE + CHUNK_SIZE * 0.5
			var center_z := -WORLD_SIZE * 0.5 + chunk_z * CHUNK_SIZE + CHUNK_SIZE * 0.5
			for resolution: int in [full_resolution, far_resolution]:
				var terrain := MeshInstance3D.new()
				terrain.mesh = _create_terrain_chunk_mesh(chunk_x, chunk_z, resolution, center_x, center_z)
				terrain.position = Vector3(center_x, 0.0, center_z)
				terrain.visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF
				terrain.visibility_range_begin_margin = 18.0
				terrain.visibility_range_end_margin = 24.0
				add_child(terrain)
				if resolution == full_resolution:
					terrain_lod_near.append(terrain)
				else:
					terrain_lod_far.append(terrain)
	_update_terrain_ranges()


func _create_terrain_chunk_mesh(chunk_x: int, chunk_z: int, resolution: int, center_x: float, center_z: float) -> ArrayMesh:
	var surface := SurfaceTool.new()
	surface.begin(Mesh.PRIMITIVE_TRIANGLES)
	var step := CHUNK_SIZE / float(resolution)
	var origin_x := -WORLD_SIZE * 0.5 + chunk_x * CHUNK_SIZE
	var origin_z := -WORLD_SIZE * 0.5 + chunk_z * CHUNK_SIZE
	for z_index in range(resolution):
		for x_index in range(resolution):
			var x0 := origin_x + x_index * step
			var x1 := x0 + step
			var z0 := origin_z + z_index * step
			var z1 := z0 + step
			var a := Vector3(x0 - center_x, height_at(x0, z0), z0 - center_z)
			var b := Vector3(x0 - center_x, height_at(x0, z1), z1 - center_z)
			var c := Vector3(x1 - center_x, height_at(x1, z0), z0 - center_z)
			var d := Vector3(x1 - center_x, height_at(x1, z1), z1 - center_z)
			for vertex: Vector3 in [a, b, c, c, b, d]:
				var world_x := vertex.x + center_x
				var world_z := vertex.z + center_z
				surface.set_normal(_terrain_normal(world_x, world_z))
				surface.add_vertex(vertex)
	surface.index()
	return surface.commit()


func _terrain_normal(x: float, z: float) -> Vector3:
	const NORMAL_STEP := 0.35
	return Vector3(
		height_at(x - NORMAL_STEP, z) - height_at(x + NORMAL_STEP, z),
		NORMAL_STEP * 2.0,
		height_at(x, z - NORMAL_STEP) - height_at(x, z + NORMAL_STEP)
	).normalized()


func _update_terrain_ranges() -> void:
	var lod_switch := render_distance * 0.5
	for terrain: MeshInstance3D in terrain_lod_near:
		terrain.visibility_range_begin = 0.0
		terrain.visibility_range_end = lod_switch
		terrain.visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_DISABLED
	for terrain: MeshInstance3D in terrain_lod_far:
		terrain.visibility_range_begin = lod_switch
		terrain.visibility_range_end = render_distance
		terrain.visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_DISABLED


func _build_paths() -> void:
	_add_path(Vector2(-18, -14), Vector2(-9, -8))
	_add_path(Vector2(0, -14), Vector2(0, -8))
	_add_path(Vector2(18, -14), Vector2(9, -8))
	_add_path(Vector2(0, 55), Vector2(0, 28))


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
	_add_building(Vector3(-18, height_at(-18, -14), -14), Vector3(14, 5.5, 11), Color(0.30, 0.39, 0.43), "MARKT")
	_add_building(Vector3(0, height_at(0, -14), -14), Vector3(15, 7.5, 15), Color(0.25, 0.37, 0.43), "LABOR")
	_add_building(Vector3(18, height_at(18, -14), -14), Vector3(18, 6.0, 18), Color(0.34, 0.42, 0.34), "ZOO")


func _add_building(origin: Vector3, size: Vector3, wall_color: Color, sign_text: String) -> void:
	var building := Node3D.new()
	building.position = origin
	building.set_meta("building_type", sign_text.to_lower())
	add_child(building)
	cullable_buildings.append(building)
	var kind := "market" if sign_text == "MARKT" else "lab" if sign_text == "LABOR" else "zoo"
	building.set_meta("architecture", "gabled_warehouse" if kind == "market" else "octagonal_research_dome" if kind == "lab" else "glass_atrium")
	var accent := NEON_AMBER if kind == "market" else NEON_CYAN if kind == "lab" else Color(0.48, 1.0, 0.58)
	var wall_material := _material(wall_color, 0.52, 0.62)
	var trim_material := _material(Color(0.11, 0.17, 0.2), 0.3, 0.82)
	var floor_material := _material(Color(0.28, 0.31, 0.29), 0.78, 0.16)
	var window_material := _glass_material(Color(0.18, 0.48, 0.57, 0.58))
	var radius := size.x * 0.5
	var entrance_depth := size.z * 0.5 if kind == "market" else radius

	_add_box_piece(building, Vector3(size.x, 0.34, size.z), Vector3(0, 0.17, 0), floor_material, true)
	match kind:
		"market":
			_add_rectangular_walls(building, size.x, size.z, size.y, 2.8, wall_material, trim_material, window_material)
			_add_gabled_roof(building, size.x, size.z, size.y, wall_material, trim_material)
		"lab":
			_add_polygon_walls(building, radius, size.y, 8, 2.8, wall_material, trim_material, window_material)
			_add_dome_roof(building, radius, size.y, accent, false)
		"zoo":
			_add_polygon_walls(building, radius, size.y, 12, 3.0, wall_material, trim_material, window_material)
			_add_dome_roof(building, radius, size.y, accent, true)

	_add_sign(building, sign_text, accent, Vector3(0, 3.65, entrance_depth + 0.55))
	_add_interior_furnishing(building, kind, size, accent)
	_add_building_light(building, size.y - 0.35, accent)
	var front_position := Vector3(0, 1.2, entrance_depth + 1.8)
	_add_interactable(origin + front_position, kind)
	building.set_meta("entrance_local_position", front_position)
	building.set_meta("footprint", Vector2(size.x, size.z))


func _add_rectangular_walls(parent: Node3D, width: float, depth: float, height: float, door_width: float, wall_material: Material, trim_material: Material, window_material: Material) -> void:
	var thickness := 0.48
	var lower_height := 1.65
	var window_height := height - 2.11
	var lower_y := 0.34 + lower_height * 0.5
	var window_y := 0.34 + lower_height + window_height * 0.5
	var cap_height := 0.46
	for wall_z: float in [-depth * 0.5, depth * 0.5]:
		var is_front: bool = wall_z > 0.0
		if is_front:
			var half_segment := (width - door_width) * 0.25
			for side: float in [-1.0, 1.0]:
				var x: float = side * (door_width * 0.5 + half_segment)
				for band: Dictionary in [
					{"size": Vector3(half_segment * 2.0, lower_height, thickness), "y": lower_y, "material": wall_material},
					{"size": Vector3(half_segment * 2.0, window_height, thickness * 0.24), "y": window_y, "material": window_material},
					{"size": Vector3(half_segment * 2.0, cap_height, thickness), "y": height - cap_height * 0.5 + 0.34, "material": wall_material},
				]:
					_add_box_piece(parent, band["size"], Vector3(x, band["y"], wall_z), band["material"], true)
			_add_box_piece(parent, Vector3(door_width, 0.42, thickness), Vector3(0, 3.58, wall_z), wall_material, true)
		else:
			_add_box_piece(parent, Vector3(width, lower_height, thickness), Vector3(0, lower_y, wall_z), wall_material, true)
			_add_box_piece(parent, Vector3(width, window_height, thickness * 0.24), Vector3(0, window_y, wall_z), window_material, true)
			_add_box_piece(parent, Vector3(width, cap_height, thickness), Vector3(0, height - cap_height * 0.5 + 0.34, wall_z), wall_material, true)
	for side: float in [-1.0, 1.0]:
		var x: float = side * width * 0.5
		_add_box_piece(parent, Vector3(thickness, lower_height, depth), Vector3(x, lower_y, 0), wall_material, true)
		_add_box_piece(parent, Vector3(thickness * 0.24, window_height, depth), Vector3(x, window_y, 0), window_material, true)
		_add_box_piece(parent, Vector3(thickness, cap_height, depth), Vector3(x, height - cap_height * 0.5 + 0.34, 0), wall_material, true)
	for x: float in [-width * 0.5, width * 0.5]:
		_add_box_piece(parent, Vector3(0.12, window_height + 0.22, 0.12), Vector3(x, window_y, depth * 0.5 + 0.3), trim_material, false)
	_add_door_frame(parent, door_width, 3.25, depth * 0.5 + 0.08, trim_material, NEON_AMBER)


func _add_polygon_walls(parent: Node3D, radius: float, height: float, sides: int, door_width: float, wall_material: Material, trim_material: Material, window_material: Material) -> void:
	var chord := 2.0 * radius * sin(PI / float(sides))
	var wall_thickness := 0.42
	for index in range(sides):
		var angle := TAU * float(index) / float(sides)
		var normal := Vector3(sin(angle), 0, cos(angle))
		var center := normal * (radius * cos(PI / float(sides)))
		var is_door_wall := index == 0
		var half_piece := (chord - door_width) * 0.25
		var lower_height := 1.55
		var window_height := height - 2.01
		var lower_y := 0.34 + lower_height * 0.5
		var window_y := 0.34 + lower_height + window_height * 0.5
		var cap_height := 0.46
		if is_door_wall:
			for side: float in [-1.0, 1.0]:
				var x_offset: float = side * (door_width * 0.5 + half_piece)
				var panel_center := center + Vector3(cos(angle) * x_offset, 0, -sin(angle) * x_offset)
				_add_wall_panel(parent, Vector3(half_piece * 2.0, lower_height, wall_thickness), panel_center + Vector3.UP * lower_y, angle, wall_material, true)
				_add_wall_panel(parent, Vector3(half_piece * 2.0, window_height, wall_thickness * 0.22), panel_center + Vector3.UP * window_y, angle, window_material, true)
				_add_wall_panel(parent, Vector3(half_piece * 2.0, cap_height, wall_thickness), panel_center + Vector3.UP * (height - cap_height * 0.5 + 0.34), angle, wall_material, true)
		else:
			_add_wall_panel(parent, Vector3(chord + 0.18, lower_height, wall_thickness), center + Vector3.UP * lower_y, angle, wall_material, true)
			_add_wall_panel(parent, Vector3(chord + 0.18, window_height, wall_thickness * 0.22), center + Vector3.UP * window_y, angle, window_material, true)
			_add_wall_panel(parent, Vector3(chord + 0.18, cap_height, wall_thickness), center + Vector3.UP * (height - cap_height * 0.5 + 0.34), angle, wall_material, true)
			for pillar_offset in [-chord * 0.44, chord * 0.44]:
				var pillar_pos := center + Vector3(cos(angle) * pillar_offset, window_y, -sin(angle) * pillar_offset) + normal * (wall_thickness * 0.24)
				_add_wall_panel(parent, Vector3(0.1, window_height, 0.12), pillar_pos, angle, trim_material, false)
	_add_door_frame(parent, door_width, 3.0, radius + 0.22, trim_material, NEON_CYAN)


func _add_wall_panel(parent: Node3D, size: Vector3, position: Vector3, rotation: float, material: Material, with_collision: bool) -> void:
	var visual := _box_piece(parent, size, position, material, with_collision)
	if with_collision:
		var body := visual.get_parent() as StaticBody3D
		body.rotation.y = rotation
	else:
		visual.rotation.y = rotation


func _add_door_frame(parent: Node3D, width: float, height: float, front_z: float, trim_material: Material, accent: Color) -> void:
	for side in [-1.0, 1.0]:
		_add_box_piece(parent, Vector3(0.32, height, 0.38), Vector3(side * (width * 0.5 + 0.16), height * 0.5 + 0.34, front_z), trim_material, true)
	_add_box_piece(parent, Vector3(width + 0.55, 0.28, 0.42), Vector3(0, height + 0.34, front_z), trim_material, true)
	_add_box_piece(parent, Vector3(width + 0.5, 0.08, 0.48), Vector3(0, 0.42, front_z), _emissive_material(accent), false)


func _add_gabled_roof(parent: Node3D, width: float, depth: float, wall_height: float, roof_material: Material, trim_material: Material) -> void:
	var rise := 1.7
	var half_depth := depth * 0.5
	var slope_length := sqrt(half_depth * half_depth + rise * rise)
	var angle := atan2(rise, half_depth)
	for side in [-1.0, 1.0]:
		var panel := _box_piece(parent, Vector3(width + 0.8, 0.28, slope_length), Vector3(0, wall_height + 0.34 + rise * 0.5, side * half_depth * 0.5), roof_material, false)
		panel.rotation.x = side * angle
	_add_box_piece(parent, Vector3(width + 1.0, 0.32, 0.42), Vector3(0, wall_height + rise + 0.34, 0), trim_material, false)


func _add_dome_roof(parent: Node3D, radius: float, wall_height: float, accent: Color, glass_dome: bool) -> void:
	var roof := MeshInstance3D.new()
	var mesh := SphereMesh.new()
	mesh.radius = radius * 0.98
	mesh.height = radius * 0.92
	mesh.radial_segments = 32
	mesh.rings = 12
	roof.mesh = mesh
	roof.position.y = wall_height + 0.12
	roof.scale.y = 0.6
	roof.material_override = _glass_material(Color(0.12, 0.3, 0.36, 0.78)) if glass_dome else _material(Color(0.11, 0.2, 0.25), 0.24, 0.74)
	parent.add_child(roof)
	for ring_index in range(2):
		var ring := MeshInstance3D.new()
		var ring_mesh := TorusMesh.new()
		ring_mesh.inner_radius = radius * (0.68 + ring_index * 0.16)
		ring_mesh.outer_radius = ring_mesh.inner_radius + 0.055
		ring.mesh = ring_mesh
		ring.position.y = wall_height + 0.18 + (1.0 - float(ring_index) * 0.35) * radius * 0.18
		ring.material_override = _emissive_material(accent)
		parent.add_child(ring)


func _add_sign(parent: Node3D, text: String, accent: Color, position: Vector3) -> void:
	var sign := Label3D.new()
	sign.text = text
	sign.position = position
	sign.font_size = 42
	sign.outline_size = 8
	sign.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	sign.modulate = accent
	parent.add_child(sign)


func _add_interior_furnishing(parent: Node3D, kind: String, size: Vector3, accent: Color) -> void:
	var counter_material := _material(Color(0.14, 0.2, 0.23), 0.3, 0.72)
	var back_z := -size.z * 0.24
	var counter_width := size.x * 0.38
	_add_box_piece(parent, Vector3(counter_width, 0.95, 0.72), Vector3(0, 0.82, back_z), counter_material, true)
	_add_box_piece(parent, Vector3(counter_width * 0.92, 0.08, 0.78), Vector3(0, 1.34, back_z), _emissive_material(accent.darkened(0.25)), false)
	for index in range(3):
		var screen := _box_piece(parent, Vector3(0.46, 0.4, 0.08), Vector3((float(index) - 1.0) * counter_width * 0.3, 1.61, back_z - 0.2), _emissive_material(accent), false)
		screen.rotation.x = deg_to_rad(-12.0)
	if kind == "zoo":
		for side in [-1.0, 1.0]:
			_add_box_piece(parent, Vector3(0.65, 1.35, 0.65), Vector3(side * size.x * 0.29, 0.85, 0), _material(Color(0.34, 0.38, 0.31), 0.9), true)
	var interior_light := OmniLight3D.new()
	interior_light.position = Vector3(0, size.y - 1.1, -0.6)
	interior_light.light_color = accent
	interior_light.light_energy = 1.6
	interior_light.omni_range = size.x * 0.75
	interior_light.shadow_enabled = false
	parent.add_child(interior_light)


func _add_building_light(parent: Node3D, height: float, accent: Color) -> void:
	_add_box_piece(parent, Vector3(1.5, 0.08, 0.08), Vector3(0, height, 0), _emissive_material(accent), false)


func _box_piece(parent: Node3D, size: Vector3, position: Vector3, material: Material, with_collision: bool) -> MeshInstance3D:
	var visual := MeshInstance3D.new()
	var mesh := BoxMesh.new()
	mesh.size = size
	visual.mesh = mesh
	visual.material_override = material
	if with_collision:
		var body := StaticBody3D.new()
		body.position = position
		var collision := CollisionShape3D.new()
		var shape := BoxShape3D.new()
		shape.size = size
		collision.shape = shape
		body.add_child(collision)
		parent.add_child(body)
		body.add_child(visual)
	else:
		visual.position = position
		parent.add_child(visual)
	return visual


func _add_box_piece(parent: Node3D, size: Vector3, position: Vector3, material: Material, with_collision: bool) -> void:
	_box_piece(parent, size, position, material, with_collision)


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
	_clear_instance_chunks()
	var quality := int(graphics_settings.get("vegetation_quality", 2))
	vegetation_quality = quality
	var scatter := RandomNumberGenerator.new()
	scatter.seed = seed + 8241
	var tree_counts := [260, 440, 680, 920]
	var shrub_counts := [220, 380, 600, 800]
	var rock_counts := [180, 300, 480, 640]
	var grass_counts := [800, 1700, 3300, 5600]
	for index in range(tree_counts[quality]):
		var point := _random_world_point(scatter, 17.0)
		if _in_clearing(point.x, point.y, 22.0):
			continue
		var height := scatter.randf_range(4.0, 7.2)
		var species := scatter.randi_range(0, 2)
		var width := scatter.randf_range(2.2, 3.6)
		var tree_position := Vector3(point.x, height_at(point.x, point.y), point.y)
		var tree_scale := Vector3(width, height, width)
		var tint := Color(scatter.randf_range(0.88, 1.08), scatter.randf_range(0.9, 1.08), scatter.randf_range(0.86, 1.04))
		_queue_instance("tree_%d" % species, tree_position, tree_scale, scatter.randf_range(0.0, TAU), tint)
		_queue_instance("tree_lod", tree_position, tree_scale, scatter.randf_range(0.0, TAU), tint)
	for index in range(shrub_counts[quality]):
		var point := _random_world_point(scatter, 17.0)
		if _in_clearing(point.x, point.y, 15.0):
			continue
		var size := scatter.randf_range(0.7, 1.45)
		_queue_instance("shrub", Vector3(point.x, height_at(point.x, point.y) + size * 0.25, point.y),
			Vector3(size * 1.3, size * 0.8, size), scatter.randf_range(0.0, TAU),
			Color(0.68, scatter.randf_range(0.82, 1.2), 0.62))
	for index in range(rock_counts[quality]):
		var point := _random_world_point(scatter, 17.0)
		if _in_clearing(point.x, point.y, 12.0):
			continue
		var size := scatter.randf_range(0.55, 1.8)
		var variant := scatter.randi_range(0, 3)
		_queue_instance("rock_%d" % variant, Vector3(point.x, height_at(point.x, point.y) + size * 0.38, point.y),
			Vector3(size * scatter.randf_range(1.0, 1.6), size * scatter.randf_range(0.7, 1.2), size * scatter.randf_range(0.9, 1.5)),
			scatter.randf_range(0.0, TAU),
			Color(scatter.randf_range(0.88, 1.12), scatter.randf_range(0.88, 1.1), scatter.randf_range(0.84, 1.06)))
	for index in range(grass_counts[quality]):
		var point := _random_world_point(scatter, 8.0)
		if _in_clearing(point.x, point.y, 12.0):
			continue
		var size := scatter.randf_range(0.6, 1.5)
		_queue_instance("grass", Vector3(point.x, height_at(point.x, point.y) + 0.02, point.y),
			Vector3(size, size, size), scatter.randf_range(0.0, TAU),
			Color(0.76, scatter.randf_range(0.8, 1.18), 0.66))
	_build_instance_chunks()


func _random_world_point(scatter: RandomNumberGenerator, margin: float) -> Vector2:
	return Vector2(
		scatter.randf_range(-WORLD_SIZE * 0.5 + margin, WORLD_SIZE * 0.5 - margin),
		scatter.randf_range(-WORLD_SIZE * 0.5 + margin, WORLD_SIZE * 0.5 - margin)
	)


func _queue_instance(kind: String, position: Vector3, scale_value: Vector3, rotation: float, color_value: Color) -> void:
	var chunk_x := floori((position.x + WORLD_SIZE * 0.5) / CHUNK_SIZE)
	var chunk_z := floori((position.z + WORLD_SIZE * 0.5) / CHUNK_SIZE)
	var chunk_id := Vector2i(chunk_x, chunk_z)
	if not instance_chunks.has(chunk_id):
		instance_chunks[chunk_id] = {}
	var chunk: Dictionary = instance_chunks[chunk_id]
	if not chunk.has(kind):
		chunk[kind] = []
	var origin := Vector3(
		-_chunk_size_half() + chunk_x * CHUNK_SIZE + CHUNK_SIZE * 0.5,
		0.0,
		-_chunk_size_half() + chunk_z * CHUNK_SIZE + CHUNK_SIZE * 0.5
	)
	var basis := Basis(Vector3.UP, rotation).scaled(scale_value)
	chunk[kind].append({"transform": Transform3D(basis, position - origin), "color": color_value})


func _chunk_size_half() -> float:
	return WORLD_SIZE * 0.5


func _build_instance_chunks() -> void:
	var leaf_material := _natural_vegetation_material(false)
	var rock_material := _natural_vegetation_material(true)
	var shrub_material := _instance_material(Color(0.17, 0.29, 0.12), true)
	var grass_material := _instance_material(Color(0.25, 0.43, 0.19), true)
	var mesh_by_kind := {
		"tree_0": _create_tree_mesh(0, true),
		"tree_1": _create_tree_mesh(1, true),
		"tree_2": _create_tree_mesh(2, true),
		"tree_lod": _create_tree_mesh(0, false),
		"shrub": _create_shrub_mesh(),
		"rock_0": _create_rock_mesh(1701),
		"rock_1": _create_rock_mesh(4807),
		"rock_2": _create_rock_mesh(9319),
		"rock_3": _create_rock_mesh(12491),
		"grass": _grass_blade_mesh(),
	}
	var material_by_kind := {}
	for kind: String in mesh_by_kind:
		material_by_kind[kind] = grass_material if kind == "grass" else shrub_material if kind == "shrub" else rock_material if kind.begins_with("rock_") else leaf_material
	var vegetation_distance := float([100.0, 165.0, 250.0, 370.0][int(graphics_settings.get("vegetation_distance", 2))])
	for chunk_value: Vector2i in instance_chunks:
		var chunk: Dictionary = instance_chunks[chunk_value]
		var center := Vector3(
			-_chunk_size_half() + chunk_value.x * CHUNK_SIZE + CHUNK_SIZE * 0.5,
			0.0,
			-_chunk_size_half() + chunk_value.y * CHUNK_SIZE + CHUNK_SIZE * 0.5
		)
		for kind: String in chunk:
			var items: Array = chunk[kind]
			var multimesh := MultiMesh.new()
			multimesh.transform_format = MultiMesh.TRANSFORM_3D
			multimesh.use_colors = true
			multimesh.mesh = mesh_by_kind[kind]
			multimesh.instance_count = items.size()
			for index in range(items.size()):
				multimesh.set_instance_transform(index, items[index]["transform"])
				multimesh.set_instance_color(index, items[index]["color"])
			var instance := MultiMeshInstance3D.new()
			instance.multimesh = multimesh
			instance.material_override = material_by_kind[kind]
			instance.position = center
			instance.set_meta("vegetation_kind", kind)
			vegetation_instances.append(instance)
			add_child(instance)
	_update_vegetation_ranges()


func _clear_instance_chunks() -> void:
	for child in get_children():
		if child is MultiMeshInstance3D:
			child.queue_free()
	instance_chunks.clear()
	vegetation_instances.clear()


func _update_vegetation_ranges() -> void:
	var distance_levels := [100.0, 165.0, 250.0, 370.0]
	var vegetation_distance := float(distance_levels[int(graphics_settings.get("vegetation_distance", 2))])
	for instance: MultiMeshInstance3D in vegetation_instances:
		var kind := str(instance.get_meta("vegetation_kind", ""))
		if kind == "tree_lod":
			instance.visibility_range_begin = vegetation_distance * 0.68
			instance.visibility_range_end = vegetation_distance
		elif kind.begins_with("tree_"):
			instance.visibility_range_end = vegetation_distance * 0.74
		else:
			instance.visibility_range_end = vegetation_distance
		instance.visibility_range_begin_margin = 18.0
		instance.visibility_range_end_margin = minf(24.0, vegetation_distance * 0.1)
		instance.visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF


func apply_graphics_settings(settings: Dictionary, camera: Camera3D = null) -> void:
	graphics_settings = settings.duplicate()
	var old_quality := vegetation_quality
	var render_levels := [160.0, 260.0, 410.0, 620.0]
	render_distance = render_levels[int(graphics_settings.get("render_distance", 2))]
	_update_terrain_ranges()
	if is_instance_valid(camera):
		camera.far = render_distance
	var detail_levels := [0.0, 0.6, 1.0, 1.5]
	if is_instance_valid(ground_material):
		ground_material.set_shader_parameter("detail_scale", detail_levels[int(graphics_settings.get("texture_quality", 2))])
	var shadow_quality := int(graphics_settings.get("shadow_quality", 2))
	if is_instance_valid(sun):
		sun.shadow_enabled = shadow_quality > 0
		sun.directional_shadow_max_distance = [45.0, 85.0, 145.0, 220.0][shadow_quality]
		sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_4_SPLITS if shadow_quality >= 2 else DirectionalLight3D.SHADOW_ORTHOGONAL
	var antialiasing := int(graphics_settings.get("anti_aliasing", 1))
	get_viewport().msaa_3d = [
		Viewport.MSAA_DISABLED,
		Viewport.MSAA_2X,
		Viewport.MSAA_4X,
		Viewport.MSAA_8X,
	][antialiasing]
	var fog_quality := int(graphics_settings.get("fog_quality", 2))
	if is_instance_valid(environment):
		environment.fog_enabled = fog_quality > 0
		environment.fog_light_color = Color(0.66, 0.77, 0.82)
		environment.fog_density = [0.0, 0.0016, 0.0011, 0.00072][fog_quality]
		environment.fog_sky_affect = 0.22
		if str(ProjectSettings.get_setting("rendering/renderer/rendering_method", "forward_plus")) == "forward_plus":
			environment.volumetric_fog_enabled = fog_quality >= 3
			environment.ssao_enabled = shadow_quality >= 2
			environment.ssil_enabled = shadow_quality >= 3
			environment.ssr_enabled = shadow_quality >= 3
	var building_levels := [120.0, 240.0, 390.0, 540.0]
	for building: Node3D in cullable_buildings:
		for visual: Node in building.find_children("*", "GeometryInstance3D", true, false):
			var geometry := visual as GeometryInstance3D
			geometry.visibility_range_end = building_levels[int(graphics_settings.get("building_distance", 3))]
			geometry.visibility_range_end_margin = 30.0
			geometry.visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF
	if old_quality != int(graphics_settings.get("vegetation_quality", 2)):
		_scatter_vegetation()
	else:
		_update_vegetation_ranges()


func _instance_material(color_value: Color, double_sided: bool = false) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color_value
	material.vertex_color_use_as_albedo = true
	material.roughness = 0.88
	if double_sided:
		material.cull_mode = BaseMaterial3D.CULL_DISABLED
	return material


func _natural_vegetation_material(is_rock: bool) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	var shader := Shader.new()
	shader.code = """
shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_schlick_ggx;

uniform bool rock_surface = false;
varying vec3 world_position;
varying vec3 world_normal;

float hash21(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash21(i), hash21(i + vec2(1.0, 0.0)), f.x),
		mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), f.x), f.y);
}

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}

void fragment() {
	float broad = value_noise(world_position.xz * (rock_surface ? 2.2 : 1.1) + vec2(world_position.y * 0.23));
	float fine = value_noise(world_position.xz * (rock_surface ? 13.0 : 6.0) + vec2(world_position.y * 1.7));
	if (rock_surface) {
		float strata = sin(world_position.y * 18.0 + broad * 5.0) * 0.5 + 0.5;
		float pits = smoothstep(0.68, 0.92, fine);
		ALBEDO = COLOR.rgb * (0.72 + broad * 0.22 + strata * 0.12 - pits * 0.12);
		ROUGHNESS = 0.94;
		SPECULAR = 0.18;
	} else {
		float bark = step(COLOR.g, COLOR.r);
		float bark_grain = sin(world_position.y * 24.0 + sin(world_position.x * 17.0 + world_position.z * 11.0) * 1.8) * 0.5 + 0.5;
		float foliage = 0.78 + broad * 0.26 + fine * 0.18;
		float bark_tone = 0.72 + bark_grain * 0.24 + broad * 0.1;
		ALBEDO = COLOR.rgb * mix(foliage, bark_tone, bark);
		ROUGHNESS = mix(0.86, 0.94, bark);
		SPECULAR = 0.16;
	}
}
"""
	material.shader = shader
	material.set_shader_parameter("rock_surface", is_rock)
	return material


func _create_tree_mesh(species: int, detailed: bool) -> ArrayMesh:
	var surface := SurfaceTool.new()
	surface.begin(Mesh.PRIMITIVE_TRIANGLES)
	var bark: Color = [Color(0.25, 0.145, 0.078), Color(0.31, 0.19, 0.10), Color(0.22, 0.16, 0.095)][species]
	var leaves: Array = [
		[Color(0.055, 0.16, 0.045), Color(0.085, 0.22, 0.055), Color(0.13, 0.28, 0.075)],
		[Color(0.045, 0.14, 0.07), Color(0.065, 0.19, 0.095), Color(0.10, 0.25, 0.12)],
		[Color(0.10, 0.17, 0.035), Color(0.15, 0.23, 0.05), Color(0.21, 0.29, 0.065)],
	][species]
	_append_tapered_branch(surface, Vector3.ZERO, Vector3(0, 0.82, 0), 0.095, 0.022, bark, 7)
	for root_index in range(5):
		var angle := TAU * float(root_index) / 5.0
		var direction := Vector3(cos(angle), 0, sin(angle))
		_append_tapered_branch(surface, direction * 0.16, Vector3(0, 0.09, 0), 0.024, 0.055, bark.darkened(0.08), 5)

	var branch_count := 4 if species == 1 else 7
	for branch_index in range(branch_count):
		var angle := TAU * float(branch_index) / float(branch_count) + float(species) * 0.7
		var level := 0.35 + float(branch_index % 4) * 0.105
		var branch_length := 0.27 if species == 1 else 0.4
		var branch_end := Vector3(cos(angle) * branch_length, level + 0.2, sin(angle) * branch_length)
		_append_tapered_branch(surface, Vector3(0, level, 0), branch_end, 0.035, 0.012, bark, 5)
		if detailed:
			var leaf_color: Color = leaves[branch_index % leaves.size()]
			_append_leaf_cluster(surface, branch_end + Vector3(0, 0.05, 0), Vector3(0.20, 0.15, 0.20), leaf_color, branch_index + species * 11)

	if species == 1:
		var tiers := 4 if detailed else 2
		for tier in range(tiers):
			var y := 0.38 + float(tier) * 0.17
			var tier_radius := 0.4 - float(tier) * 0.075
			_append_leaf_cluster(surface, Vector3(0, y, 0), Vector3(tier_radius, 0.24, tier_radius), leaves[tier % leaves.size()], tier + 9)
	else:
		var cluster_count := 9 if detailed else 4
		for cluster in range(cluster_count):
			var angle := TAU * float(cluster) / float(cluster_count)
			var radius := 0.23 if species == 0 else 0.19
			var center := Vector3(cos(angle) * radius, 0.76 + sin(float(cluster) * 2.1) * 0.16, sin(angle) * radius)
			var scale_value := Vector3(0.43, 0.3 if species == 0 else 0.36, 0.4)
			_append_leaf_cluster(surface, center, scale_value, leaves[cluster % leaves.size()], cluster + species * 13)
		_append_leaf_cluster(surface, Vector3(0, 0.94, 0), Vector3(0.37, 0.32, 0.36), leaves[1], species + 37)
	surface.generate_normals()
	return surface.commit()


func _append_tapered_branch(surface: SurfaceTool, start: Vector3, finish: Vector3, start_radius: float, end_radius: float, color_value: Color, sides: int) -> void:
	var direction := (finish - start).normalized()
	var axis_x := direction.cross(Vector3.FORWARD).normalized()
	if axis_x.length_squared() < 0.01:
		axis_x = direction.cross(Vector3.RIGHT).normalized()
	var axis_z := direction.cross(axis_x).normalized()
	for side in range(sides):
		var angle_a := TAU * float(side) / float(sides)
		var angle_b := TAU * float(side + 1) / float(sides)
		var offset_a := axis_x * cos(angle_a) + axis_z * sin(angle_a)
		var offset_b := axis_x * cos(angle_b) + axis_z * sin(angle_b)
		var a := start + offset_a * start_radius
		var b := start + offset_b * start_radius
		var c := finish + offset_a * end_radius
		var d := finish + offset_b * end_radius
		_add_colored_triangle(surface, a, b, c, color_value.darkened(0.08))
		_add_colored_triangle(surface, c, b, d, color_value.lightened(0.04))


func _append_leaf_cluster(surface: SurfaceTool, center: Vector3, scale_value: Vector3, base_color: Color, variant: int) -> void:
	const RINGS := 6
	const SEGMENTS := 10
	var vertices: Array[Vector3] = []
	var colors: Array[Color] = []
	for ring in range(RINGS + 1):
		var latitude := -PI * 0.5 + PI * float(ring) / float(RINGS)
		for segment in range(SEGMENTS):
			var longitude := TAU * float(segment) / float(SEGMENTS)
			var irregularity := 1.0 + 0.075 * sin(longitude * 3.0 + float(variant)) * cos(latitude * 4.0 + float(variant) * 0.7)
			var unit := Vector3(cos(latitude) * cos(longitude), sin(latitude), cos(latitude) * sin(longitude))
			vertices.append(center + Vector3(unit.x * scale_value.x, unit.y * scale_value.y, unit.z * scale_value.z) * irregularity)
			var shade := 0.88 + 0.18 * float((segment + ring + variant) % 4) / 3.0
			colors.append(base_color * shade)
	for ring in range(RINGS):
		for segment in range(SEGMENTS):
			var next_segment := (segment + 1) % SEGMENTS
			var a := ring * SEGMENTS + segment
			var b := ring * SEGMENTS + next_segment
			var c := (ring + 1) * SEGMENTS + segment
			var d := (ring + 1) * SEGMENTS + next_segment
			_add_colored_triangle(surface, vertices[a], vertices[c], vertices[b], colors[a])
			_add_colored_triangle(surface, vertices[b], vertices[c], vertices[d], colors[d])


func _create_shrub_mesh() -> ArrayMesh:
	var surface := SurfaceTool.new()
	surface.begin(Mesh.PRIMITIVE_TRIANGLES)
	for index in range(7):
		var angle := TAU * float(index) / 7.0
		var center := Vector3(cos(angle) * 0.21, 0.12 + float(index % 3) * 0.055, sin(angle) * 0.2)
		var size := Vector3(0.31 + float(index % 2) * 0.05, 0.27, 0.3)
		_append_leaf_cluster(surface, center, size, Color(0.18, 0.31, 0.12) * (0.82 + float(index % 3) * 0.12), index)
	surface.generate_normals()
	return surface.commit()


func _create_rock_mesh(mesh_seed: int) -> ArrayMesh:
	const RINGS := 7
	const SEGMENTS := 12
	var scatter := RandomNumberGenerator.new()
	scatter.seed = mesh_seed
	var ring_offsets: Array[float] = []
	for ring in range(RINGS + 1):
		ring_offsets.append(scatter.randf_range(0.84, 1.16))
	var surface := SurfaceTool.new()
	surface.begin(Mesh.PRIMITIVE_TRIANGLES)
	var vertices: Array[Vector3] = []
	var colors: Array[Color] = []
	for ring in range(RINGS + 1):
		var latitude := -PI * 0.5 + PI * float(ring) / float(RINGS)
		for segment in range(SEGMENTS):
			var longitude := TAU * float(segment) / float(SEGMENTS)
			var radial := cos(latitude) * ring_offsets[ring] * (0.9 + 0.1 * sin(longitude * 3.0 + float(mesh_seed)))
			var y := sin(latitude) * ring_offsets[ring]
			vertices.append(Vector3(cos(longitude) * radial, y, sin(longitude) * radial))
			var strata := 0.78 + 0.18 * float((ring * 3 + segment + mesh_seed) % 5) / 4.0
			colors.append(Color(0.43, 0.42, 0.37) * strata)
	for ring in range(RINGS):
		for segment in range(SEGMENTS):
			var next_segment := (segment + 1) % SEGMENTS
			var a := ring * SEGMENTS + segment
			var b := ring * SEGMENTS + next_segment
			var c := (ring + 1) * SEGMENTS + segment
			var d := (ring + 1) * SEGMENTS + next_segment
			_add_colored_triangle(surface, vertices[a], vertices[c], vertices[b], colors[a])
			_add_colored_triangle(surface, vertices[b], vertices[c], vertices[d], colors[d])
	surface.generate_normals()
	return surface.commit()


func _add_colored_triangle(surface: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, color_value: Color) -> void:
	surface.set_color(color_value)
	surface.add_vertex(a)
	surface.set_color(color_value)
	surface.add_vertex(b)
	surface.set_color(color_value)
	surface.add_vertex(c)


func _grass_blade_mesh() -> ArrayMesh:
	var surface := SurfaceTool.new()
	surface.begin(Mesh.PRIMITIVE_TRIANGLES)
	for vertex: Vector3 in [
		Vector3(-0.5, 0.0, 0.0), Vector3(0.5, 0.0, 0.0), Vector3(0.0, 1.0, 0.0),
		Vector3(0.0, 0.0, -0.5), Vector3(0.0, 0.0, 0.5), Vector3(0.0, 1.0, 0.0),
	]:
		surface.add_vertex(vertex)
	surface.generate_normals()
	return surface.commit()


func _add_path_lighting() -> void:
	for index in range(6):
		var z := -4.0 - index * 2.5
		_add_lamp(Vector3(-2.2, height_at(-2.2, z), z), NEON_CYAN)
		_add_lamp(Vector3(2.2, height_at(2.2, z), z), NEON_CYAN)
	for landmark: Vector2 in [Vector2(-18, -14), Vector2(0, -14), Vector2(18, -14)]:
		_add_lamp(Vector3(landmark.x - 6, height_at(landmark.x - 6, landmark.y), landmark.y), NEON_AMBER)
		_add_lamp(Vector3(landmark.x + 6, height_at(landmark.x + 6, landmark.y), landmark.y), NEON_AMBER)


func _add_lamp(position: Vector3, light_color: Color) -> void:
	var lamp := Node3D.new()
	lamp.position = position
	add_child(lamp)
	var post := MeshInstance3D.new()
	var post_mesh := CylinderMesh.new()
	post_mesh.top_radius = 0.055
	post_mesh.bottom_radius = 0.09
	post_mesh.height = 2.2
	post.mesh = post_mesh
	post.position.y = 1.1
	post.material_override = _material(Color(0.13, 0.2, 0.24), 0.28, 0.6)
	lamp.add_child(post)
	var lamp_head := MeshInstance3D.new()
	var head_mesh := SphereMesh.new()
	head_mesh.radius = 0.16
	head_mesh.height = 0.24
	lamp_head.mesh = head_mesh
	lamp_head.position.y = 2.25
	lamp_head.material_override = _emissive_material(light_color)
	lamp.add_child(lamp_head)
	var point_light := OmniLight3D.new()
	point_light.position.y = 2.2
	point_light.light_color = light_color
	point_light.light_energy = 0.65
	point_light.omni_range = 6.0
	point_light.shadow_enabled = false
	lamp.add_child(point_light)


func _spawn_fossils() -> void:
	for index in range(55):
		var x := rng.randf_range(-94, 94)
		var z := rng.randf_range(-94, 94)
		if _in_clearing(x, z, 24.0):
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
	for index in range(22):
		var x := rng.randf_range(-92, 92)
		var z := rng.randf_range(-92, 92)
		if _in_clearing(x, z, 18.0):
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


func _material(color_value: Color, roughness: float = 0.9, metallic: float = 0.0) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color_value
	material.roughness = roughness
	material.metallic = metallic
	return material


func _glass_material(color_value: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color_value
	material.metallic = 0.45
	material.roughness = 0.18
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.cull_mode = BaseMaterial3D.CULL_DISABLED
	return material


func _emissive_material(color_value: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color_value
	material.emission_enabled = true
	material.emission = color_value
	material.emission_energy_multiplier = 1.6
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	return material

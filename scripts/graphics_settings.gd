extends RefCounted

const OPTIONS := {
	"render_distance": ["Niedrig", "Mittel", "Hoch", "Ultra"],
	"texture_quality": ["Niedrig", "Mittel", "Hoch", "Ultra"],
	"shadow_quality": ["Niedrig", "Mittel", "Hoch", "Ultra"],
	"vegetation_quality": ["Niedrig", "Mittel", "Hoch", "Ultra"],
	"anti_aliasing": ["Niedrig", "Mittel", "Hoch", "Ultra"],
	"vegetation_distance": ["Niedrig", "Mittel", "Hoch", "Ultra"],
	"building_distance": ["Niedrig", "Mittel", "Hoch", "Ultra"],
	"fog_quality": ["Niedrig", "Mittel", "Hoch", "Ultra"],
}

const LABELS := {
	"render_distance": "Render Distance",
	"texture_quality": "Texturqualität",
	"shadow_quality": "Schatten",
	"vegetation_quality": "Vegetation",
	"anti_aliasing": "Anti-Aliasing",
	"vegetation_distance": "Vegetationssichtweite",
	"building_distance": "Gebäudesichtweite",
	"fog_quality": "Nebelqualität",
}


static func defaults() -> Dictionary:
	return {
		"render_distance": 3,
		"texture_quality": 3,
		"shadow_quality": 3,
		"vegetation_quality": 3,
		"anti_aliasing": 3,
		"vegetation_distance": 3,
		"building_distance": 3,
		"fog_quality": 3,
	}


static func sanitize(settings: Dictionary) -> Dictionary:
	var result := defaults()
	for setting: String in OPTIONS:
		var index := int(settings.get(setting, result[setting]))
		result[setting] = clampi(index, 0, OPTIONS[setting].size() - 1)
	return result


static func display_value(setting: String, settings: Dictionary) -> String:
	var index := int(settings.get(setting, 0))
	return str(OPTIONS[setting][clampi(index, 0, OPTIONS[setting].size() - 1)])

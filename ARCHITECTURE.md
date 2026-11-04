# Architektur

Das Spiel läuft vollständig in Godot 4. Die Module sind in GDScript getrennt:

- `scripts/game.gd`: setzt die Module zusammen und verwaltet Spielzustand, Fossilien, Werkzeuge, Forschung, Eier, Zoo-Einnahmen und Spielstände.
- `scripts/world.gd`: erzeugt ein 200 × 200 Meter großes, mehrstufiges Heightmap-Terrain mit HeightMap-Kollision und opakem, zweiseitigem PBR-Boden-Shader. Das Terrain liegt in 25 Chunks mit naher und vereinfachter ferner Geometrie. Markt, Labor und Zoo stehen beieinander, haben verschiedene Gebäudeformen, eingerichtete Innenräume und baulich offene, kollidierte Eingänge. Wiederholte Vegetation wird per MultiMesh in 40-Meter-Chunks instanziert; prozedurale PBR-Shader ergänzen Details auf den verästelten Baum-Meshes und unregelmäßigen Felsen. Vegetation und Gebäude haben separate Sichtweiten.
- `scripts/graphics_settings.gd`: definiert die vier Qualitätsstufen, Standardwerte und gespeicherten Grafikoptionen.
- `scripts/player.gd`: `CharacterBody3D`-First-Person-Steuerung mit Mausblick, physischen WASD-Tasten, Schwerkraft, Springen, Vorwärtssprint und Interaktions-Ray. E/I öffnen das Inventar, F benutzt anvisierte Objekte.
- `scripts/hud.gd`: HUD, Ausgrabungsfortschritt und kontextabhängige Menüs; während Menüs sind Bewegung und Kamerasteuerung pausiert.
- `scenes/main.tscn`: Einstiegsszene des Projekts.

## Grafikoptionen und Culling

Renderweite, Bodendetail, Schattenqualität, Vegetationsdichte, MSAA, Vegetationssichtweite, Gebäudesichtweite und Nebelqualität werden über das Pausenmenü eingestellt und im JSON-Spielstand gespeichert. Frustum Culling und distanzbasierte MultiMesh-Sichtweiten sind aktiv. Occlusion Culling ist nicht gebacken, da die Geometrie prozedural zur Laufzeit entsteht. Desktop nutzt Forward+ mit SSAO, SSIL, SSR und volumetrischem Nebel; mobile Geräte verwenden GL Compatibility.

## Speichern

Spielstände werden als JSON unter `user://dino_genesis_save.json` abgelegt. Das vermeidet zusätzliche Datenbank-Plugins und funktioniert direkt mit Godot. Fehlende oder ungültige Dateien starten ein neues Spiel, unbekannte Fossil- und Werkzeug-IDs werden beim Einlesen verworfen.

## Vereinfachungen

Die Welt nutzt ein endliches, prozedurales Mesh statt Voxel-Terrain. Gebäude sind einfache Geometrie, Besucher werden als regelmäßige Zoo-Einnahmen simuliert und der Dino ist ein geometrisches Platzhaltermodell. Die Kernschleife vom Fossilfund bis zum Zoo ist spielmechanisch verbunden.

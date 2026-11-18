# Architektur

Das Spiel läuft vollständig in Godot 4. Die Module sind in GDScript getrennt:

- `scripts/game.gd`: setzt die Module zusammen und verwaltet Spielzustand, Fossilien, Werkzeuge, Forschung, Eier, Zoo-Einnahmen und Spielstände.
- `scripts/world.gd`: erzeugt ein 280 × 280 Meter großes, abgeflachtes Heightmap-Terrain mit HeightMap-Kollision und opakem, zweiseitigem PBR-Boden-Shader. Das Terrain liegt in 49 Chunks mit naher und vereinfachter ferner Geometrie. Markt, Labor und Zoo stehen beieinander, haben verschiedene Gebäudeformen, eingerichtete Innenräume und ebenerdige Eingänge. Wiederholte Vegetation wird per MultiMesh in 40-Meter-Chunks instanziert; prozedurale PBR-Shader ergänzen Details auf den verästelten Baum-Meshes und unregelmäßigen Felsen. Vegetation und Gebäude haben separate Sichtweiten.
- `scripts/animal.gd`: prozedurale Vogel- und Dinosauriermodelle mit Gelenk-Animationen und begrenztem Wanderverhalten.
- `scripts/graphics_settings.gd`: definiert die vier Qualitätsstufen, Standardwerte und gespeicherten Grafikoptionen.
- `scripts/player.gd`: `CharacterBody3D`-First-Person-Steuerung mit Mausblick, physischen WASD-Tasten, Schwerkraft, kurzem Sprung, Vorwärtssprint, dezenter Laufkamera und Interaktions-Ray. E/I öffnen das Inventar, F benutzt anvisierte Objekte.
- `scripts/hud.gd`: HUD, Ausgrabungsfortschritt und kontextabhängige Menüs; während Menüs sind Bewegung und Kamerasteuerung pausiert.
- `scenes/main.tscn`: Einstiegsszene des Projekts.

## Grafikoptionen und Culling

Das initiale Ultra-Profil aktiviert 8× MSAA, FXAA, 1,35× 3D-Supersampling, SDFGI, SSAO, SSIL, SSR, volumetrischen Nebel und hochauflösende Schatten. Renderweite, Bodendetail, Vegetationsdichte und Objekt-Sichtweiten lassen sich über das Pausenmenü einstellen und speichern. Ältere Spielstände werden einmalig auf Ultra migriert; danach bleiben manuelle Einstellungen gespeichert. Frustum Culling und distanzbasierte MultiMesh-Sichtweiten sind aktiv. Occlusion Culling ist nicht gebacken, da die Geometrie prozedural zur Laufzeit entsteht. Mobile Geräte verwenden GL Compatibility und haben weniger Effekte.

## Speichern

Spielstände werden als JSON unter `user://dino_genesis_save.json` abgelegt. Welt-Seed und Zustand des Laborschalters werden mitgespeichert. Das vermeidet zusätzliche Datenbank-Plugins und funktioniert direkt mit Godot. Fehlende oder ungültige Dateien starten ein neues Spiel, unbekannte Fossil- und Werkzeug-IDs werden beim Einlesen verworfen.

## Vereinfachungen

Die Welt nutzt ein endliches, prozedurales Mesh statt Voxel-Terrain. Gebäude sind prozedurale Geometrie und Besucher werden als regelmäßige Zoo-Einnahmen simuliert. Fossilien und Tiere werden ebenfalls aus prozeduralen Meshes erzeugt.

# Architektur

Das Spiel läuft vollständig in Godot 4. Die Module sind in GDScript getrennt:

- `scripts/game.gd`: setzt die Module zusammen und verwaltet Spielzustand, Fossilien, Werkzeuge, Forschung, Eier, Zoo-Einnahmen und Spielstände.
- `scripts/world.gd`: erzeugt per `SurfaceTool` ein geglättetes, hügeliges Terrain mit Kollision. Derselbe Seed bestimmt Landschaft, Vegetation, Fossilien und Vögel. Gebäude und Wege liegen an festen Positionen.
- `scripts/player.gd`: `CharacterBody3D`-First-Person-Steuerung mit Mausblick, Schwerkraft, Springen, Sprinten und Interaktions-Ray.
- `scripts/hud.gd`: HUD, Ausgrabungsfortschritt und kontextabhängige Menüs; während Menüs sind Bewegung und Kamerasteuerung pausiert.
- `scenes/main.tscn`: Einstiegsszene des Projekts.

## Speichern

Spielstände werden als JSON unter `user://dino_genesis_save.json` abgelegt. Das vermeidet zusätzliche Datenbank-Plugins und funktioniert direkt mit Godot. Fehlende oder ungültige Dateien starten ein neues Spiel, unbekannte Fossil- und Werkzeug-IDs werden beim Einlesen verworfen.

## Vereinfachungen

Die Welt nutzt ein endliches, prozedurales Mesh statt Voxel-Terrain. Gebäude sind einfache Geometrie, Besucher werden als regelmäßige Zoo-Einnahmen simuliert und der Dino ist ein geometrisches Platzhaltermodell. Die Kernschleife vom Fossilfund bis zum Zoo ist spielmechanisch verbunden.

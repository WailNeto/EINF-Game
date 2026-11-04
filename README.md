# Dino Genesis

Dino Genesis ist ein **Godot-4-Projekt** und verwendet GDScript. Die prozedurale, begehbare Welt ist 200 × 200 Meter groß (6,25-mal die ursprüngliche Fläche) und verbindet Ebenen, Hügel und Bergrücken mit Wegen, Wäldern und variierenden Felsformationen. Markt, Labor und Zoo liegen als Gruppe beieinander. Die Gebäude haben unterschiedliche Formen, offene Eingänge und begehbare Innenräume. Geländeoberflächen werden mit einem opaken, zweiseitigen PBR-Shader schattiert. Vegetation wird in räumlichen Chunks mit GPU-Multimeshes gezeichnet; Bäume besitzen verästelte Stämme, mehrere Blätterkronen und eine vereinfachte Baum-LOD. Für Bäume und Felsen erzeugen PBR-Shader zusätzliche prozedurale Farb- und Oberflächendetails; dafür sind keine externen Assets erforderlich.

## Starten

1. Godot 4.3 oder neuer installieren.
2. Godot öffnen und **Import** wählen.
3. Die Datei `project.godot` in diesem Ordner auswählen.
4. Das Projekt starten (F6 oder F5).

Auch in VS Code kann das Projekt bearbeitet werden; zum Ausführen muss Godot installiert sein. Python, Ursina und pip sind für die Godot-Version nicht erforderlich. Für ältere Startanleitungen ist `main.py` als Kompatibilitäts-Launcher vorhanden: `python3 main.py` sucht Godot und öffnet dieses Projekt. Falls Godot nicht automatisch gefunden wird, Godot direkt starten oder `GODOT_EXECUTABLE` auf die ausführbare Datei setzen.

Die Steuerung lässt sich mit `godot --path . --script res://tests/input_smoke.gd` automatisiert prüfen.

## Steuerung

- **WASD**: bewegen (physische Tasten, unabhängig vom Tastaturlayout)
- **Maus**: Kamera bewegen (im Spiel gefangen, in Menüs sichtbar)
- **Shift + W**: vorwärts sprinten
- **Leertaste**: springen
- **E**: Inventar öffnen
- **F**: anvisiertes Objekt oder Gebäude benutzen
- **Linke Maustaste halten**: Fossil ausgraben
- **I**: Inventar jederzeit öffnen
- **Esc**: Menü / Pause schließen oder öffnen
- **Pausemenü → Grafikeinstellungen**: Renderweite, Texturdetails, Schatten, Vegetation, Anti-Aliasing, Vegetations- und Gebäudesichtweite sowie Nebel einstellen

## Grafik und Leistung

Die Grafikoptionen werden sofort angewendet und im Spielstand gespeichert. Renderweite, Sichtweiten, Vegetationsdichte, Schatten, MSAA und Nebel bieten jeweils vier Stufen. Die Welt nutzt Godots Frustum Culling sowie distanzbegrenzte, nach Bereichen gruppierte MultiMesh-Instanzen. Baked Occlusion Culling ist nicht aktiviert: Die Welt wird zur Laufzeit prozedural erzeugt und enthält keine gebackenen Occluder. Desktop nutzt Forward+ für SSAO, SSIL, SSR und volumetrischen Nebel; mobile Geräte verwenden weiterhin GL Compatibility.

## Spielablauf

- Fossilien an den goldenen Fundstellen ausgraben und im Inventar sammeln.
- Am Marktplatz Fossilien verkaufen und bessere Schaufeln kaufen.
- Im Labor Fossilien für DNA analysieren und mit DNA sowie einem gefangenen Vogel ein Dino-Ei herstellen.
- Im Zoo das Ei ausbrüten. Ein Velociraptor erscheint im Zoo; der Zoo erzeugt regelmäßig Einnahmen.
- Im Menü speichern. Spielstände liegen im Godot-Benutzerordner als `dino_genesis_save.json`.

## Projektstruktur

- `project.godot` – Godot-Projektkonfiguration und Startszene
- `scenes/main.tscn` – Hauptszene
- `scripts/game.gd` – Spielablauf, Aktionen und Speichern/Laden
- `scripts/world.gd` – prozedurale 3D-Landschaft, Gebäude, Pflanzen, Fundstellen und Vögel
- `scripts/player.gd` – First-Person-Bewegung und Kamera
- `scripts/hud.gd` – HUD und Menüs
- `ARCHITECTURE.md` – technische Übersicht
- `CHANGELOG.md` – Änderungen
- `TODO.md` – mögliche nächste Erweiterungen

Die Welt nutzt einen gespeicherten Seed und kann damit reproduzierbar erzeugt werden. Der Spielstand wird als JSON gespeichert, da Godot keine SQLite-Datenbank im Kern mitliefert und dieses Projekt keine zusätzlichen Datenbank-Plugins benötigt.

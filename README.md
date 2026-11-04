# Dino Genesis

Dino Genesis ist jetzt ein **Godot-4-Projekt** und verwendet GDScript. Die 3D-Welt wird direkt im Spiel aufgebaut: mit geglätteten Hügeln, Wald, Büschen, Felsen, Wegen und markierten Orten. Es werden keine externen 3D-Assets benötigt.

## Starten

1. Godot 4.3 oder neuer installieren.
2. Godot öffnen und **Import** wählen.
3. Die Datei `project.godot` in diesem Ordner auswählen.
4. Das Projekt starten (F6 oder F5).

Auch in VS Code kann das Projekt bearbeitet werden; zum Ausführen muss Godot installiert sein. Python, Ursina und pip sind für die Godot-Version nicht erforderlich. Für ältere Startanleitungen ist `main.py` als Kompatibilitäts-Launcher vorhanden: `python3 main.py` sucht Godot und öffnet dieses Projekt. Falls Godot nicht automatisch gefunden wird, Godot direkt starten oder `GODOT_EXECUTABLE` auf die ausführbare Datei setzen.

## Steuerung

- **WASD**: bewegen (physische Tasten, unabhängig vom Tastaturlayout)
- **Maus**: Kamera bewegen (im Spiel gefangen, in Menüs sichtbar)
- **Shift**: sprinten
- **Leertaste**: springen
- **E**: anvisiertes Objekt oder Gebäude benutzen
- **Linke Maustaste halten**: Fossil ausgraben
- **I**: Inventar öffnen
- **Esc**: Menü / Pause schließen oder öffnen

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

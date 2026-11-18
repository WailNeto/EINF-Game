# Dino Genesis

Dino Genesis wird als **Unity-Spiel mit C# und URP** entwickelt. Das aktive Unity-Projekt liegt unter [`UnityProject/`](./UnityProject/); `main.py` startet dieses Unity-Projekt. Die bisherige Godot-Fassung im Repository ist ein separater Prototyp und wird von diesem Launcher nicht verwendet.

## Starten

1. Unity 2022.3.17f1 über Unity Hub installieren.
2. Im Repository-Ordner `python3 main.py` ausführen. Der Launcher richtet das Unity-Projekt ein und öffnet es im Unity Editor.
3. Im Editor die Startszene `Assets/DinoGenesis/Scenes/Main.unity` verwenden und **Play** drücken.

Falls Unity nicht am Standardpfad installiert ist, `UNITY_EDITOR_PATH` auf die ausführbare Unity-Datei setzen. Details zum Projektstart und macOS-Build stehen in [`UnityProject/README.md`](./UnityProject/README.md).

**Hinweis:** Die nachfolgenden Abschnitte zu Steuerung, Grafik und Spielablauf beschreiben den vorhandenen Godot-Prototyp, nicht den aktuellen Unity-Stand. Unity wird phasenweise entwickelt; Insel, First-Person-Steuerung, Ausgrabung sowie Inventar und Markt gehören zu den Phasen 1 bis 3.

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
- **Pausemenü**: Seed kopieren oder eine neue Welt mit neuem Seed beginnen

## Grafik und Leistung

Das Spiel startet standardmäßig mit allen Grafikoptionen auf **Ultra** und hebt ältere Spielstände einmalig auf dieses Profil an. Forward+ nutzt dabei 8× MSAA plus FXAA, 1,35× 3D-Supersampling, hochauflösende Schatten, sky-basierte indirekte Beleuchtung, SDFGI, SSAO, SSIL, SSR und volumetrischen Nebel. Die Ultra-Sichtweite beträgt 760 m; Vegetation und Gelände nutzen größere Dichten und Sichtweiten. Diese Einstellung ist entsprechend anspruchsvoll und lässt sich im Grafikmenü jederzeit reduzieren. Die Welt nutzt Godots Frustum Culling sowie distanzbegrenzte, nach Bereichen gruppierte MultiMesh-Instanzen. Baked Occlusion Culling ist nicht aktiviert: Die Welt wird zur Laufzeit prozedural erzeugt und enthält keine gebackenen Occluder. Mobile Geräte verwenden weiterhin GL Compatibility und unterstützen nicht alle Forward+-Effekte.

## Spielablauf

- Fossilien an Fundstellen ausgraben und im Inventar sammeln. Zahn-, Krallen-, Fisch-, Blatt-, Feder- und Ammonitenfossilien besitzen eigene Formen.
- Am Marktplatz Fossilien verkaufen und bessere Schaufeln kaufen.
- Im Labor den wissenschaftlichen Schalter mit **F** aktivieren, Fossilien für DNA analysieren und mit DNA sowie einem gefangenen Vogel ein Dino-Ei herstellen.
- Im Zoo das Ei ausbrüten. Ein Velociraptor erscheint im Zoo; der Zoo erzeugt regelmäßig Einnahmen.
- Vögel und Dinosaurier bewegen sich eigenständig durch die Welt.
- Im Menü speichern. Spielstände liegen im Godot-Benutzerordner als `dino_genesis_save.json`.

## Projektstruktur

- `project.godot` – Godot-Projektkonfiguration und Startszene
- `scenes/main.tscn` – Hauptszene
- `scripts/game.gd` – Spielablauf, Aktionen und Speichern/Laden
- `scripts/world.gd` – prozedurale 3D-Landschaft, Gebäude, Pflanzen, Fundstellen, Tiere und Seed
- `scripts/animal.gd` – prozedurale Tiermodelle und Wanderbewegung
- `scripts/player.gd` – First-Person-Bewegung und Kamera
- `scripts/hud.gd` – HUD und Menüs
- `ARCHITECTURE.md` – technische Übersicht
- `CHANGELOG.md` – Änderungen
- `TODO.md` – mögliche nächste Erweiterungen

Die Welt nutzt einen gespeicherten Seed und kann damit reproduzierbar erzeugt werden. „Neue Welt“ setzt Welt und Spielfortschritt zurück; Grafikoptionen bleiben erhalten. Der Spielstand wird als JSON gespeichert, da Godot keine SQLite-Datenbank im Kern mitliefert und dieses Projekt keine zusätzlichen Datenbank-Plugins benötigt.

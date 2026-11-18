# Dino Genesis — Unity

Die Unity-Version liegt in diesem Ordner. Das bisherige Godot-Projekt im Repository-Stamm bleibt unverändert erhalten.

## Voraussetzungen

- Unity **2022.3.17f1** (die lokal installierte LTS-Version)
- macOS mit Apple-Silicon-Unterstützung; der installierte Editor läuft nativ auf `arm64`
- VS Code mit der Unity-Erweiterung und C# Dev Kit

Das Projekt verwendet URP **14.0.9** (die zum Editor gebündelte Version) und Input System **1.7.0**. Die macOS-Zielarchitektur ist ARM64, das Scripting Backend ist IL2CPP. Das Terrain erhält mehrskalige seed-basierte Höhenformen und eine prozedurale URP-Bodenalbedo-/Normalmap ohne Texturkacheln. Das Waldsystem hat sechs Formen je Laub-, Schirm- und Nadelbaumfamilie sowie sechs Busch- und sechs Unterwuchsvarianten. Einzelne Blattpolygone, dichte Waldcluster und zwei LOD-Stufen sorgen für Nahdetails bei kontrolliertem Aufwand. Hinzu kommen sechs facettierte, texturierte Felsformen und sechs Erscheinungsformen für Vögel mit einzelnen Flügelfedern, Augen und Schnäbeln. Kachelbare URP-Mikrooberflächen, gestaltete Landmarken, terrainfolgende Schotterwege sowie warmes Licht und Distanzdunst ergänzen die Welt. URP verwendet moderate Echtzeit-Schatten (35 m); aufwendige Echtzeit-GI- oder Post-Processing-Effekte sind nicht aktiviert. Die Grafik ist weiterhin prozeduraler Low-Poly-Stil und wurde nicht als visuell gleichwertig zu *Satisfactory* oder per M2-Framerate-Test verifiziert.

## Erststart im Editor

Im Terminal:

```sh
cd UnityProject
sh Tools/prepare-unity-project.sh
```

Das Skript stellt den Unity-Editor-Aufruf bereit, richtet die URP-Assets und die Startszene ein und öffnet anschließend den Editor. Bei einem anderen Installationspfad kann `UNITY_EDITOR_PATH` gesetzt werden. Danach kann das Projekt auch direkt in Unity Hub über `UnityProject/` geöffnet werden.

Der installierte Unity-Editor enthält in URP 14.0.9 eine fehlerhafte Import-Metadatei für `ShaderLibrary/Debug/DebugViewEnums.cs`; dadurch fehlen beim ersten Projektimport mehrere URP-Enums und Unity meldet Paket-Compilefehler. Das Startskript ergänzt ausschließlich die fehlenden Unity-Metadaten im generierten, ignorierten `Library/PackageCache`-Ordner. Es verändert weder den Editor noch den URP-Quellcode. Bei einer erneuten Paketauflösung kann das Skript erneut ausgeführt werden.

Die Startszene enthält eine reproduzierbare Insel, First-Person-Steuerung, Ausgrabungsstellen, Inventar, Markt, Forschungslabor, wild lebende Vögel und einen Zoo. Im Editor kann sie über **Play** gestartet werden. **WASD** bewegt, die **Maus** schaut, **Leertaste** springt, **Shift** sprintet, **I** öffnet das Inventar und **Esc** pausiert beziehungsweise schließt das aktuelle Menü. Halte die **linke Maustaste** auf einem amberfarbenen Marker, um auszugraben; der Fortschritt bleibt beim Loslassen erhalten. Jede Fundstelle liefert genau ein seed-bestimmtes Fossil. Im Inventar werden die Funde angezeigt. Ziele mit dem Fadenkreuz auf einen wilden Vogel und drücke **E**, um eine avianische DNA-Probe zu gewinnen. Am Markt kannst du nahe am Stand mit **E** das Handelsmenü öffnen und Fossilien nach einer Bestätigung verkaufen oder nacheinander eine Schaufel und verbesserte Schaufel kaufen. Im Labor öffnet **E** das Forschungsmenü; dort lassen sich Fossilien nach Bestätigung analysieren. Ein Fossil wird dabei aus dem verkäuflichen Inventar entfernt, sodass es nicht zugleich verkauft und analysiert werden kann. Die Analyse liefert je nach Fund 2, 7, 18 oder 45 DNA-Fragmente. Forschung schaltet Meilensteine bei 10, 30 und 65 DNA frei; bei 65 DNA wird das Velociraptor-Genom freigeschaltet. Nach dem Fang einer Vogel-DNA-Probe kannst du im Labor ein Ei synthetisieren. Es benötigt 30 Sekunden Spielzeit zum Schlüpfen und weitere 60 Sekunden bis zum ausgewachsenen Dinosaurier. Im Zoo öffnet **E** das Gehege-Menü. Ein Gehege ist anfangs frei; weitere kosten 250 beziehungsweise 600 Münzen. Nur ausgewachsene Dinosaurier können zugewiesen werden, je Gehege ist ein Tier möglich. Mit jedem Tier im Zoo kommen alle 12 Sekunden 1–3 simulierte Besucher, die jeweils 2 Münzen Eintritt zahlen. Die Hände graben mit 1x, die Schaufel mit 2x und die verbesserte Schaufel mit 3,5x Geschwindigkeit. Der Startkontostand beträgt 0 Münzen. Im Pausemenü (**Esc**) stehen **Save game** und **Load game** zur Verfügung. Beim Laden wird die gespeicherte Insel anhand ihres Seeds neu erzeugt und die persistenten Systeme werden wiederhergestellt.

## macOS-App bauen

1. `File > Build Settings…` öffnen und **macOS** als Ziel kontrollieren.
2. `Assets/DinoGenesis/Scenes/Main.unity` muss unter **Scenes In Build** aktiviert sein.
3. **Build** wählen und einen Ausgabeordner wie `Builds/macOS` angeben.
4. Die erzeugte `Dino Genesis.app` starten. Für eine Weitergabe außerhalb der lokalen Entwicklung sind Developer-ID-Signierung und Notarisierung separat einzurichten.

Ein eigenständiger macOS-Spielbuild wurde noch nicht erstellt oder ausgeführt.

## SQLite / Apple Silicon

SQLite wird über `IGameSaveRepository` abstrahiert; die konkrete Implementierung speichert einen versionierten JSON-Snapshot in einer SQLite-Transaktion unter `Application.persistentDataPath/dino-genesis.sqlite3`. Die Laufzeitsysteme kennen weder SQLite noch native SQLite-Typen. Gespeichert werden Seed, Münzen, Fossilien und Fundstellenstatus, Werkzeug, Transaktionsbuch, Forschung, gefangene Vögel/Proben, Eier und Dinosaurieralter sowie Gehege, Besucher und Einnahmen. Nicht unterstützte Save-Schemaversionen und inkonsistente Zustände werden explizit abgelehnt.

Das native Plugin `Assets/Plugins/macOS/arm64/libe_sqlite3.dylib` stammt aus `SQLitePCLRaw.lib.e_sqlite3` **2.1.11** (NuGet; Apache-2.0, siehe Lizenz-/Hinweisdateien neben dem Plugin). Es ist ausschließlich für macOS ARM64 als Editor- und Standalone-Plugin aktiviert. Der Unity-Editor-Smoke-Test hat SQLite nativ geöffnet und einen vollständigen Zustands-Roundtrip einschließlich Save-Datei ausgeführt. Ein macOS-ARM64-IL2CPP-Build wurde versucht, scheiterte aber beim Kompilieren von Unity 2022.3s gebündeltem zlib gegen das installierte macOS-26-Command-Line-Tools-SDK (`fdopen`-Makrokonflikt in `zutil.h`). Daher wurde keine eigenständige App erstellt und die SQLite-Ausführung im IL2CPP-Player bleibt unbestätigt.

## Struktur

- `Assets/DinoGenesis/Runtime/` — Laufzeitcode, nach Core, Player, World, Fossils, Economy, Research, Dinosaurs, Zoo, UI und Persistence gegliedert
- `Assets/DinoGenesis/Editor/` — Editor-Konfiguration und Setup
- `Assets/DinoGenesis/Settings/` — URP Pipeline- und Renderer-Assets
- `Assets/DinoGenesis/Scenes/` — Einstiegsszene
- `Packages/manifest.json` — versionierte Unity-Pakete
- `ProjectSettings/` — Unity-Version, Eingabe- und Plattformsettings
- `ARCHITECTURE.md`, `CHANGELOG.md`, `TODO.md` — Projektdokumentation

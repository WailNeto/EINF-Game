# Architektur — Unity-Version

## Projektbasis

Unity 2022.3.17f1 ist die festgelegte Editor-Version. Die gebündelte URP 14.0.9 wird verwendet, damit Editor und Render-Pipeline aufeinander abgestimmt bleiben. Input System 1.7.0 ist installiert und als aktive Eingabe-API konfiguriert. `DinoGenesis.Runtime` ist die Laufzeit-Assembly; Editor-Werkzeuge liegen getrennt in `DinoGenesis.Editor`.

Die Einstiegsszene ist `Assets/DinoGenesis/Scenes/Main.unity`. `ProjectBootstrap` konfiguriert Pipeline, macOS-Buildvorgaben und die Szenenobjekte. Laufzeitverhalten liegt ausschließlich in den Runtime-Komponenten.

## Phase 1

- `World/IslandWorld.cs` erzeugt ein 290-m-Raster-Terrain mit seedbasierter, mehrskaliger Perlin-Topografie und neun Hügelkörpern. Die Geländegeometrie ist ein Mesh mit 16.641 Vertices und eigener Terrain-Kollision. Die Bodenoberfläche wird seed-deterministisch als 256x256-URP-Albedo-/Normalmap ohne Kachelwiederholung erzeugt; weiche Gras-, Waldboden-, Erd- und Sandübergänge verwenden breit-/feinskalige Noise und die Geländeneigung.
- Inselobjekte erhalten aus dem Seed unabhängige `System.Random`-Sequenzen. Damit ändern Vegetation, Felsen und die sechs amberfarbenen Ausgrabungsmarker die Topografie nicht. Gebäude-Marker für Forschungsstation, Markt und Habitat stehen nahe dem Start.
- `World/ProceduralVegetationGenerator.cs` verteilt seed-deterministische, überlappende Waldcluster rund um eine freie Start-/Gebäudezone. Je sechs deutlich geformte Laub-, Schirm- und Nadelbaumvarianten verwenden sichtbare einzelne Blattpolygone, gefächerte Äste und gestaffelte Kronen. Sechs Busch- und sechs Farn-/Bodendeckerformen verdichten den Waldboden; höhere Kandidatendichte und leichte Mindestabstände halten die Verteilung natürlich.
- Vegetations-Sequenzen sind von Gelände, Felsen, Vögeln und Fundstellen unabhängig. Jedes Waldcluster kombiniert Nah-/Fernmesh-LOD über eine `LODGroup`; Pflanzen bleiben colliderfrei und im geprüften Mesh-/Vertexbudget. `World/ProceduralRockGenerator.cs` erstellt sechs unregelmäßige, facettierte Vulkansteinformen mit Mesh-Kollision und rauer Normalmap. Der Phase-1-Smoke-Test prüft die vollständige Variantenabdeckung, Felsgeometrie/-kollision, sattere Grasfarbwerte, Unterwuchs, LOD-Materialslots und das Vertexbudget. URP-Materialien und zusammengeführte Meshes werden bei Welt-Neugenerierung freigegeben.
- Die zehn `Dinosaurs/WildBird.cs`-Tiere variieren seed-basiert in sechs Farb-/Erscheinungsformen und besitzen einzeln modellierte Flügelfedern, Augen, Schnäbel und Füße. Materialien bleiben pro Vogel begrenzt und werden beim Zerstören des Vogels freigegeben; Fang-Raycasts und Einzelverwendung der DNA-Probe bleiben unverändert.
- `World/ProceduralWorldSurface.cs` erzeugt wiederholbare, nahtlos kachelbare 128x128-Albedo-/Normalmaps für Holz, Putz, Canvas, Metall, Schotter und Vulkanstein. Die Vulkanstein-Normalmap erhält zusätzliche hochfrequente Bruch- und Chipstrukturen; gemeinsame URP-Materialien halten Textur- und Speicheraufwand klein.
- `IslandWorld` verbindet Startbereich, Markt, Labor und Habitat durch ein Terrain-folgendes, zusammengefasstes Schotterweg-Mesh. Labor, Markt und Gehege haben thematisch eigene Details; ein Eingangstor hält den Habitat-Zugang lesbar. Ein sanfter Linear-Fog und warmes Soft-Light geben der Insel räumliche Tiefe.
- Der Phase-1-Smoke-Test prüft zusätzlich Weggeometrie, wiederholbare Oberflächenkarten, Landmarken-Details und Atmosphäreneinstellungen.
- `Player/FirstPersonController.cs` verwendet Unity Input System und `CharacterController` für WASD, Mausblick, Sprung und Sprint. Die UI sperrt Bewegung und Kameraeingabe über `SetInputEnabled`.
- `UI/ExplorationHud.cs` zeigt Seed, Startgeld, Steuerung und nahe Landmarken. Feldnotizen und Pausemenü lassen Maus und Tastatureingabe frei; der Seed-Dialog erzeugt dieselbe Insel reproduzierbar neu.
- `Core/GameBootstrap.cs` verbindet Welt, Spieler und HUD in der Startszene und setzt den Spieler auf die generierte Terrainhöhe.
- Die Phase-1-Editorprüfung erzeugt zweimal denselben Seed und vergleicht Terrain-Signatur und Objektzahl; ein anderer Seed muss die Signatur ändern. Sie prüft außerdem Vertex-/Objektgrenzen sowie nahe Landmarken.
- Der Terrain-Test prüft zusätzlich Texturgröße und Farbvariation sowie reproduzierbare und seed-abhängige Oberflächensignaturen.

## Phase 2

- `Fossils/FossilCatalog.cs` definiert vier Fossilprofile als serialisierte Katalogdaten: häufige Theropodenzähne, ungewöhnliche Hadrosaurierwirbel, seltene Triceratops-Hornfragmente und epische Tyrannosaurier-Kieferfragmente. Seltenheit, Gewicht, Verkaufswert und Forschungswert sind getrennt konfiguriert.
- `World/IslandWorld.cs` erzeugt sechs `ExcavationSite`-Komponenten mit eindeutiger Seed-/Fundstellen-ID und nicht blockierendem Trigger. Erforderliche Arbeitszeit ist für eine gegebene Welt und Fundstelle deterministisch.
- `Fossils/ExcavationSystem.cs` wertet einen zentrierten Kamerastrahl und gehaltene linke Maustaste aus. Fortschritt bleibt an der Fundstelle erhalten, eine Fundstelle kann nur einmal abgeschlossen werden und der Katalog wählt das Fossil deterministisch aus Seed und Fundstellenindex.
- `Fossils/ExcavationSite.cs` hält Laufzeitfortschritt und wechselt den Marker nach Abschluss sichtbar in den erledigten Zustand. Die Economy verwaltet gefundene Fossilien und beanspruchte Fundstellen.
- `UI/ExplorationHud.cs` zeigt Ziel, Fortschrittsbalken, Fundmeldung und ein Feldnotiz-Protokoll mit Seltenheit sowie Verkaufs-/Forschungswert.
- Der Phase-2-Smoke-Test prüft sechs valide Standorte, Trigger-Raycasts, teilweisen und vollständigen Fortschritt, deterministische Belohnungen und Schutz vor doppelten Fossilien.

## Phase 3

- `Economy/EconomySystem.cs` hält Münzen, Fossilhistorie, verkäufliches Inventar, bereits beanspruchte Fundstellen, Werkzeugstufe und Transaktionsbuchungen. Startwert ist 0; Verkäufe entfernen einen Fund und buchen dessen Wert in einem einzelnen validierten Vorgang. Eine einmal beanspruchte Fundstellen-ID kann auch nach Verkauf kein zweites Fossil ergeben.
- Werkzeuge sind Hände (1x), Schaufel (2x, 30 Münzen) und verbesserte Schaufel (3,5x, 90 Münzen). Upgrades sind sequenziell. Unbezahlbare oder bereits ausgeschöpfte Käufe ändern weder Guthaben noch Transaktionsbuch.
- `ExcavationSystem` überträgt neue Funde einmalig in die Wirtschaft und berücksichtigt das ausgerüstete Werkzeug. Bei Welt-Neugenerierung bleiben Münzen, Inventar und bereits ausgegrabene IDs erhalten; bekannte Fundstellen werden wieder als abgeschlossen angezeigt.
- Das Marktmenü wird mit E in 8 m Nähe zum Marktstand geöffnet. Verkauf und Werkzeugkauf erfordern eine Auswahl, eine ausdrückliche Bestätigung und sind abbrechbar. Inventar und HUD zeigen Geld, Werkzeug und Fossilien.
- Die Transaktionshistorie wird derzeit im Laufzeitzustand geführt, aber noch nicht über einen Neustart hinweg gespeichert. Save-/Load-Integration und dauerhafte Transaktionspersistenz sind Phase 7 mit SQLite.
- Der Phase-3-Smoke-Test prüft Startguthaben, Ablehnung unfinanzierter Käufe, Fundinventar, exakte Verkäufe, Einmaligkeit, Werkzeugpreise und Grabgeschwindigkeit, Transaktionssummen, Markt-Eingabesperre und Fortschritt über Seed-Neugenerierungen.

## Phase 4

- `Research/ResearchCatalog.cs` definiert streng aufsteigende DNA-Meilensteine für 10, 30 und 65 Fragmente; der letzte Meilenstein schaltet die Spezies-ID `velociraptor` frei.
- `Research/ResearchSystem.cs` vergibt beim Analysieren die im Fossilkatalog definierten Forschungswerte und protokolliert erreichte Meilensteine. `EconomySystem` entfernt analysierte Funde aus dem verkäuflichen Inventar und markiert sie separat, sodass ein Fossil nur verkauft oder analysiert werden kann.
- Das Labor wird nahe der Forschungsstation mit E geöffnet. Das HUD zeigt DNA, Meilensteinfortschritt, verfügbare Fossilien, Bestätigung/Abbruch der Analyse und Freischaltungen; geöffnete Menüs sperren Spieler- und Kameraeingabe.
- Forschungs- und Wirtschaftsdaten bleiben beim Neugenerieren der Insel erhalten, sind aber noch nicht dauerhaft gespeichert. Die Speziesfreischaltung stellt das Genom bereit, das Phase 5 für Ei-Synthese und Aufzucht nutzt.
- Der Phase-4-Smoke-Test analysiert 36 seed-generierte Funde, prüft exakte DNA-Vergabe und gegenseitigen Ausschluss mit Verkauf, alle Meilensteine und das Velociraptor-Genom sowie die Eingabesperre am Labor. Regressionstests der Phasen 1–3 laufen ebenfalls.

## Phase 5

- `World/IslandWorld.cs` erzeugt zehn seed-verteilte `WildBird`-Objekte mit sparsamen Primitive-Meshes und einfachen, begrenzten Bewegungszielen. Vögel sind mit dem Fadenkreuz per Raycast erfassbar; E deaktiviert einen gefangenen Vogel und vergibt genau eine avianische DNA-Probe.
- `Dinosaurs/DinosaurCatalog.cs` hält pro Spezies Forschungs-ID, benötigte Vogelproben, Inkubations-/Wachstumszeit und Farbe als austauschbare ScriptableObject-Daten. Der Standardkatalog enthält den kleinen Velociraptor.
- `Dinosaurs/DinosaurSystem.cs` erlaubt die Eiersynthese erst, wenn die Forschung die Spezies freigeschaltet hat und genügend Vogelproben vorhanden sind. Die Probe wird bei erfolgreicher Synthese verbraucht.
- `DinosaurRecord` durchläuft die Stadien Ei, Baby und Adult; der Inkubator benötigt 30 Sekunden und das Wachstum weitere 60 Sekunden Spielzeit. `DinosaurVisual` stellt Ei und Low-Poly-Dinosaurier dar und lässt Jungtier/Adult in einem kleinen Bereich um das Labor umherlaufen.
- Labor-HUD zeigt Proben, Synthetisieren-Bestätigung und Entwicklungsfortschritt. Fortschritt und Tiere bestehen bei Seed-Neugenerierung im Laufzeitzustand fort, werden aber bis Phase 7 noch nicht über Neustarts gespeichert.
- Der Phase-5-Smoke-Test prüft die generierte Vogelzahl, Kamera-Ray-Capture, doppelfreie Probenvergabe, Forschungs- und Proben-Gating sowie den vollständigen Ei-zu-Baby-zu-Adult-Übergang. Regressionstests der Phasen 1–4 laufen ebenfalls.

## Phase 6

- `Zoo/ZooSystem.cs` hält drei Gehegeplätze, anfangs ist der erste verfügbar. Die zwei Erweiterungen kosten 250 beziehungsweise 600 Münzen; Käufe werden als Economy-Transaktionen verbucht und unaffordable purchases hinterlassen Guthaben und Zoo unverändert.
- Ausgewachsene Dinosaurier können jeweils genau einem verfügbaren Gehege zugewiesen oder daraus entfernt werden. Die Visuals werden zur seedbasierten Gehegeposition verschoben und bewegen sich in einem begrenzten Umkreis.
- Das Zoo-Menü öffnet sich mit E am Habitat, sperrt währenddessen Bewegung/Kamera und zeigt Gehege, Zuweisungen, Besucher und Einnahmen. Jede belegte Anlage bringt alle 12 Sekunden 1–3 simulierte Besucher pro Gehege; jeder Eintritt kostet 2 Münzen.
- Eintrittserlöse werden als positive Zoo-Transaktion der Economy gutgeschrieben; leere Zoos erzeugen keine Besucher-/Einnahmen. Gehege, Zuweisungen und Einnahmen bleiben über Seed-Neugenerierung im Laufzeitzustand erhalten, aber noch nicht über Neustarts.
- Der Phase-6-Smoke-Test prüft Startfreischaltung, Schutz gegen Käufe ohne Guthaben, Kosten der beiden Erweiterungen, Erwachsenen-/Einmalzuweisung, exakte Besuchereinnahmen, keine Einnahmen ohne Tier und Zoo-Menü-Eingabesperre.

## Laufzeitmodule

Die Runtime bleibt modular und soll keine zentrale God-Script-Klasse erhalten:

- **Core:** gemeinsame IDs, Events, Spielzustand und Service-Komposition.
- **Player:** First-Person-Bewegung, Kamera, Eingaben und Interaktionsabstraktion.
- **World:** Seed, deterministische Inselgenerierung, Biome und Weltobjekte.
- **Fossils:** Fundstellen, Fossildefinitionen und Ausgrabung.
- **Inventory / Economy:** Inventar, Münzen, Markttransaktionen und Werkzeuge.
- **Research:** Labor, DNA-Fortschritt und Freischaltungen.
- **Dinosaurs:** Speziesdefinitionen, DNA-Verarbeitung, Eier und Lebensphasen.
- **Zoo:** `ZooSystem` für Gehege, Zuweisung, Besucher-Simulation und Eintrittserlöse.
- **UI:** HUD und Menüs; UI ruft Anwendungsdienste auf statt Spiellogik zu duplizieren.
- **Persistence:** Save-Repository und SQLite-Adapter, getrennt von den Domänenmodulen.

Konfigurationen und Content-Definitionen kommen in ScriptableObjects; Laufzeitzustand bleibt in normalen C#-Modellen. Feature-Ordner und Namespaces werden beim Hinzufügen ihrer Systeme angelegt. Assembly-Grenzen werden bei Bedarf entlang stabiler Schnittstellen weiter aufgeteilt.

## Phase 7

- `Persistence/IGameSaveRepository.cs` entkoppelt den Spielfortschritt vom Speicherformat. `SqliteGameSaveRepository` nutzt SQLite mit vorbereiteten Statements, atomarer Schreibtransaktion und einer `schema_version`-Spalte; unbekannte Versionen werden abgewiesen.
- Der Save-Payload ist ein versionierter `GameSaveData`-Snapshot. Er enthält Welt-Seed, Wirtschaft/Fossilien/beanspruchte Fundstellen/Transaktionen, Forschungs-DNA, Vogelproben und Fangindizes, Dinosaurieridentität/Spezies/Alter/Gehegezuweisung sowie Zoogehege, Besucher und Einnahmen.
- `GameBootstrap` erzeugt die Insel beim Laden zunächst aus dem gespeicherten Seed und restauriert danach die Domänenzustände. Fundstellen, Vogel-Fangzustände, Dinosaurier-Visuals und Gehege werden anschließend aus den gespeicherten Daten rekonstruiert.
- Die native ARM64-`libe_sqlite3.dylib` aus SQLitePCLRaw 2.1.11 wird nur für macOS Editor/Standalone importiert. Die Datenbankdatei liegt unter `Application.persistentDataPath`; das Runtime-Repository ist die einzige Laufzeitkomponente mit nativen SQLite-Aufrufen.
- Der Phase-7-Editor-Smoke-Test öffnet die native SQLite-Bibliothek, schreibt und liest einen Snapshot zurück und prüft Economy-Ledger, Fossilstatus, Forschung, Vogel-Fangzustand, ausgewachsenen Dinosaurier, Zoofortschritt und erledigte Fundstellen. Der macOS-ARM64-IL2CPP-Build wurde versucht, scheitert aber zuvor in Unity 2022.3s mitgeliefertem zlib beim Kompilieren gegen das macOS-26-Command-Line-Tools-SDK (`fdopen`-Makrokonflikt); der Player-Build bleibt ungeprüft.

## Plattform und Performance

Der Standalone-Build ist auf macOS ARM64 und IL2CPP konfiguriert; Unitys native Metal-Unterstützung wird verwendet. URP nutzt kurze Schattenreichweite und wenige Echtzeit-Effekte als konservative Ausgangsbasis für M2. Culling und begrenzte Objektzahlen sind aktiv; zusätzliche LOD-Stufen und Pooling folgen, sobald Laufzeitdaten dies rechtfertigen.

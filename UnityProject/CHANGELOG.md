# Changelog — Unity-Version

## Graphics / Variationen und Fauna

- Baumgenerierung auf sechs unterscheidbare Varianten je Laub-, Schirm- und Nadelbaumfamilie erweitert; einzelne Blattpolygone ergänzen die Nahansicht.
- Unterwuchs auf sechs Busch- und sechs Pflanzenformen erweitert und Waldkandidaten erhöht; Vegetation bleibt in Cluster-Meshes mit zwei LOD-Stufen gebündelt.
- 72 seed-platzierte, facettierte Felsen mit sechs Geometrieformen, Mesh-Kollision und stärker strukturierter Vulkanstein-Normalmap ergänzt.
- Wildvögel erhalten sechs Gefiederfarbvarianten, modellierte Flügelfedern, Augen, Schnäbel und Füße; bestehende Fang- und DNA-Mechanik bleibt erhalten.
- Grasfarbpalette gesättigt; Phase-1- und Phase-5-Smoke-Tests prüfen Farben, Varianten, Fels- und Vogeldetails sowie Mesh-/Objektbudgets.

## Graphics / Forest — Referenzpass

- Waldverteilung auf ein dichtes, seed-deterministisches Clusterfeld erweitert, ohne die freie Start- und Gebäudezone zu bepflanzen.
- Hohe schlanke Stämme mit Wurzelansätzen, sichtbaren Astfächern, strukturierten Laub-/Schirmkronen und gestaffelten Nadelbaumkronen ergänzt.
- Büsche und größere Farnwedel als dichter Unterwuchs ergänzt; Baumabstände reduzieren übermäßige Stammüberlappung.
- Vegetation bleibt in kombinierten Meshes und zwei LOD-Stufen; Seed-, Arten-, Objekt- und Vertexbudgets werden im Phase-1-Smoke-Test validiert.

## Graphics / Worldbuilding — Abschnitt 4

- Eigene nahtlos kachelbare URP-Albedo-/Normalmaps für verwittertes Holz, Putz, Canvas, Metall, Schotter und Vulkanstein ergänzt.
- Labor, Markt und Habitat um Fenster, Solarpanel, Lüfter, Marktstanddetails, Beschattung und ein gut erkennbares Eingangstor erweitert.
- Vier terrainfolgende Zugangswege zu Markt, Labor und Habitat als einzelnes Schotter-Mesh ergänzt.
- Warmes Soft-Light, aktivierte URP-Soft-Shadows, abgestufte Umgebungsfarben und sanfter linearer Dunst über die Inselkonfiguration eingerichtet.
- Phase-1-Smoke-Test um Texturvariation, Weg-Mesh, Landmarken-Details und Atmosphäre erweitert.

## Graphics / Vegetation — Abschnitt 3

- Primitive Einzelbäume und Einzelbüsche durch acht seed-deterministische, natürlich verstreute Waldcluster mit freier Startzone ersetzt.
- Low-Poly-Laub- und Nadelbaumvarianten, Büsche, doppelseitige Farne und vereinzelte Totholzstämme als kombinierte Material-Meshes ergänzt.
- Zwei Distanzstufen pro Cluster ergänzt; Meshes und Materialien werden bei Welt-Neugenerierung und Zerstörung freigegeben, Vegetation bleibt colliderfrei.
- Phase-1-Smoke-Test erweitert: Seed-Wiederholbarkeit der Vegetationsverteilung, Objekt-/Vertexbudget, LOD-Geometrie und Materialzuordnung.

## Graphics / Landscape — Abschnitt 1

- Inselhöhen mit großräumigen Erhebungen, rollenden Übergängen und feineren Geländedetails erweitert, ohne Seed-Reproduzierbarkeit oder Gameplay-Interaktionspunkte zu verändern.
- Bisherige flache Gras-/Sandflächen durch eine prozedurale, nicht gekachelte 256x256-URP-Bodenalbedo mit organischen Gras-, Walderde-, Erdboden- und Küsten-Sandvariationen ersetzt.
- Eine aus der tatsächlichen Terrainhöhe erzeugte Normalmap ergänzt; Terrain-UVs, Tangenten und eine einzelne Material-Submesh-Belegung eingerichtet.
- Phase-1-Smoke-Test um Texturvariation sowie Terrain-/Oberflächen-Seed-Reproduzierbarkeit erweitert.

## Phase 7

- Versioniertes SQLite-Save-Repository und `Application.persistentDataPath`-Speicherziel integriert; Schreibvorgänge laufen atomar in einer SQLite-Transaktion.
- Save-/Load-Aktionen im Pausemenü ergänzt und Laden mit Seed-Regenerierung sowie Wiederherstellung von Economy, Forschung, Vögeln, Dinosauriern, Fundstellen und Zoo verbunden.
- SQLite-Systembibliothek für macOS ARM64 als Unity Editor-/Standalone-Plugin eingebunden; Paketlizenz und Herkunft dokumentiert.
- Phase-7-Smoke-Test für nativen SQLite-Zugriff und vollständigen Gameplay-Snapshot-Roundtrip ergänzt.

## Phase 6

- Zoo-System mit einem Startgehege und zwei käuflichen Erweiterungen (250/600 Münzen) ergänzt.
- Voll ausgewachsene Dinosaurier können je genau einem Gehege zugewiesen und wieder entfernt werden; einfache Umherbewegung bleibt innerhalb der Gehegezone.
- Zoo-Menü mit Eingabesperre, Gehegeverwaltung, Besucher- und Einnahmenübersicht am Zoo-Standort integriert.
- Besucher simuliert: alle 12 Sekunden 1–3 Besucher pro belegtem Gehege, 2 Münzen Eintritt pro Besucher; Einnahmen und Käufe werden im Economy-Transaktionsbuch verbucht.
- Phase-6-Editor-Smoke-Test für Freischaltung, Erwachsenen-Zuweisung, Eintrittserlöse und Zoo-Interaktion ergänzt.

## Phase 5

- Zehn seed-platzierte, niedrig-poly Wildvögel mit einfacher Bewegung und E-Fang per Fadenkreuz-Ray ergänzt.
- Erfassung vergibt eine avianische DNA-Probe genau einmal pro Vogel.
- Spezieskatalog und Labor-Synthese ergänzen die Forschungsfreischaltung als Voraussetzung sowie den Verbrauch einer Probe pro Velociraptor-Ei.
- Ei-, Baby- und Adultstadien mit 30 Sekunden Inkubation, 60 Sekunden Wachstum, einfacher Low-Poly-Darstellung und begrenzter Bewegung ergänzt.
- HUD um Vogelproben, Ei-Synthese-Bestätigung und Dinosaurier-Entwicklungsfortschritt erweitert.
- Phase-5-Editor-Smoke-Test und Regressionstests der Phasen 1–4 ergänzt.

## Phase 4

- Forschungskatalog mit DNA-Meilensteinen bei 10, 30 und 65 Punkten sowie Velociraptor-Genomfreischaltung ergänzt.
- Forschungslabor mit Fossilauswahl, DNA-Fortschritt, Analysebestätigung und Menü-Eingabesperre integriert.
- Fossilanalyse verbraucht das Fossil und verbucht den Forschungsertrag; verkaufte oder analysierte Funde können nicht ein zweites Mal verwertet werden.
- Unity-Editor-Smoke-Test für DNA-Erträge, Analyse-/Verkaufs-Ausschluss, Forschungsstufen, Speziesfreischaltung und Laboreingabe ergänzt.

## Phase 3

- Inventar und Economy-System mit 0 Startmünzen, Fossilhistorie, verkäuflichen Funden, beanspruchten Fundstellen und Transaktionsbuch ergänzt.
- Markthandel mit Verkaufs- und Werkzeugkaufbestätigung sowie Schutz vor mehrfachen Verkäufen und unbezahlbaren Käufen ergänzt.
- Schaufel (2x) und verbesserte Schaufel (3,5x) als aufeinanderfolgende Upgrades eingeführt; Ausgrabung übernimmt den Geschwindigkeitsfaktor des Werkzeugs.
- HUD um Geld, Inventar, Werkzeug, Marktinteraktion mit E und Handelsmenü erweitert.
- Seed-Neugenerierung erhält Wirtschaftsdaten und bereits ausgegrabene Fundstellen.
- Unity-Editor-Smoke-Test für Economy-Invarianten, Verkäufe, Upgrade-Kosten, Transaktionsbuch, Marktbereich und Seed-Erhalt ergänzt.

## Phase 2

- Fossilkatalog mit vier Seltenheitsstufen und unterschiedlichen Entdeckungsgewichten, Verkaufswerten und Forschungswerten ergänzt.
- Sechs seed-bestimmte Fundstellen mit Fortschritt, Kamera-Zielerfassung und Ausgrabung bei gehaltener linker Maustaste verbunden.
- Ein Fossil wird erst nach abgeschlossener Ausgrabung entdeckt; abgeschlossene Standorte können nicht erneut Fossilien vergeben.
- HUD um Fundstellen-Fortschritt, Fundmeldungen und ein Fossilprotokoll erweitert.
- Unity-Editor-Smoke-Test für Fundstellen, Fortschrittszustände, Katalogbelohnungen, Raycast-Erreichbarkeit, Duplikatschutz und Seed-Reset ergänzt.

## Phase 1

- Seed-basierte Low-Poly-Insel mit hügeligem Mesh-Terrain, Strand, Küstenwasser, Vegetation, Felsen und markierten künftigen Ausgrabungsstellen ergänzt.
- Forschungsstation, Markt und zukünftiges Zoo-Habitat nahe dem Startpunkt platziert.
- First-Person-Steuerung mit Input System, `CharacterController`, Mausblick, Sprung und Sprint ergänzt.
- Erkundungs-HUD mit Seed, Steuerung, Landmarkenhinweis, Inventar-/Feldnotizen-Menü, Pausemenü und Seed-Regeneration ergänzt.
- Batchmode-Smoke-Test für identische Seed-Ausgaben, Seed-Variation, Weltgröße und Landmarken ergänzt.

## Phase 0

- Eigenständiges Unity-Projekt unter `UnityProject/` erstellt; vorhandene Godot-Dateien bleiben unberührt.
- Unity 2022.3.17f1 mit URP 14.0.9 und Input System 1.7.0 festgelegt.
- URP-Startassets, Einstiegsszene, Laufzeit-/Editor-Assemblies und macOS-ARM64/IL2CPP-Einstellungen ergänzt.
- macOS- und Apple-Silicon-Runtime-Assets von SQLitePCLRaw 2.1.11 geprüft; keine SQLite-Bibliothek vor Phase 7 eingebunden.
- Erststart-Helfer ergänzt, der fehlende Import-Metadaten des mitgelieferten URP-Pakets im ignorierten Unity-Cache ergänzt.

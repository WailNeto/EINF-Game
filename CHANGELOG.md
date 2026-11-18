# Changelog

## Godot migration

- Ersetzt Ursina/Python als Spieleinstieg durch ein natives Godot-4-Projekt.
- Fügt First-Person-Bewegung, Maussteuerung, Raycast-Interaktion und Menüs in GDScript hinzu.
- Erzeugt ein beleuchtetes, hügeliges 3D-Terrain mit Seed, Wegen, Gebäuden, Bäumen, Büschen, Felsen, Fossilien und Vögeln.
- Verbessert First-Person-Eingaben (WASD, Leertaste, Shift+W, E-Inventar, F-Interaktion) und ergänzt Grasdetails, Wegbeleuchtung sowie futuristische Rundgebäude mit Glas und Leuchtelementen.
- Übernimmt die grundlegende Fossil-, Markt-, Werkzeug-, Forschungs-, Ei- und Zoo-Schleife.
- Speichert den Fortschritt im Godot-Benutzerordner in JSON.
- Vergrößert die prozedurale Landschaft von 80 × 80 auf 200 × 200 Meter und ergänzt Ebenen, Hügel und Bergrücken.
- Gruppiert Wälder, Büsche, Felsen und Gras mit MultiMesh-Instanzen in Chunks; Bäume besitzen eine vereinfachte Fern-LOD und distanzabhängige Sichtbarkeit.
- Ergänzt prozedurales PBR-Terrain, atmosphärischen Nebel, Qualitätsoptionen für Renderweite, Texturdetails, Schatten, Vegetation, MSAA und Gebäudesichtweite; Optionen werden gespeichert.
- Behebt durchsichtig gerendertes Terrain durch opakes zweiseitiges Rendering und eindeutige Terrain-LOD-Sichtweiten.
- Ersetzt Baum- und Fels-Platzhalter durch variierte MultiMesh-Modelle mit prozeduralen PBR-Oberflächendetails und gruppiert Markt, Labor und Zoo als unterschiedliche, betretbare Gebäude mit freiem Eingang und eingerichtetem Innenraum.
- Vergrößert die Welt auf 280 × 280 Meter, glättet das Gelände und gruppiert Terrain in 49 LOD-Chunks.
- Ergänzt im Pausemenü einen kopierbaren Welt-Seed sowie eine Aktion zum Erzeugen einer neuen Welt mit zurückgesetztem Fortschritt.
- Ergänzt animierte, wandernde Vögel und Dinosaurier sowie eigene Ammoniten-, Fisch-, Blatt-, Feder-, Zahn- und Krallenmodelle.
- Ergänzt einen bedienbaren wissenschaftlichen Laborschalter als Voraussetzung zur Erzeugung von Dino-Eiern.
- Verkürzt den Sprung und ergänzt dezentes Kamerawippen beim Laufen sowie eine Landebewegung.
- Setzt das Grafikprofil auf Ultra: 8× MSAA, FXAA, 1,35× 3D-Supersampling, SDFGI, SSAO, SSIL, SSR, volumetrischer Nebel, 8K-Schattenatlas sowie erhöhte Sichtweiten und Vegetationsdichte.
- Migriert vorhandene Spielstände einmalig zum Ultra-Profil, ohne spätere manuelle Grafikeinstellungen zu überschreiben.

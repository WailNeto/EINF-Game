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

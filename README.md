Kernfunktionen
Das Programm bietet eine Integration der GTFS-Spezifikation und unterstützt das Einlesen aller 17 offiziellen GTFS-Tabellen (u.a. stops.txt, routes.txt, trips.txt, stop_times.txt, shapes.txt).

1. Geodaten-Export (GTFS to Shapefile)
Interaktive Karte: Visualisierung von Linienverläufen auf Basis von OpenStreetMap.
Shapefile-Export: Exportiert Liniengeometrien als ESRI Shapefiles (.shp) inklusive Attributdaten wie Liniennummer und Betreiber.
Bild-Export: Erstellung von PNG-Karten inklusive Legende der gewählten Linie.

2. Fahrplan-Management & Analyse
Fahrplan-Matrix: Generierung von klassischen Tabellenfahrplänen für spezifische Linien und Kalendertage.
PDF-Export: Export der generierten Fahrpläne als PDF/HTML-Dokument.
Live-Abfahrtstafel: Anzeige aller Abfahrten an einer gewählten Haltestelle für einen bestimmten Zeitpunkt, inkl. Filterung nach Linien und Zielen.
Erkennung von Sonderfahrten: Automatische Identifikation von Schulfahrten oder Ausnahmetagen im Kalender.

3. Haltestellen-Explorer & Erreichbarkeit
Globale Suche: Schnelle Suche über tausende Haltestellen mit detaillierter Ansicht von Attributen (Barrierefreiheit, Tarifzonen, Umsteigemöglichkeiten).
Frequenzanalyse: Automatische Berechnung der Abfahrten pro Woche und pro Tag (Mo-So) für jede Linie an einer Haltestelle.
Einzugsgebiets-Analyse: Visualisierung von Erreichbarkeits-Radien (z. B. 300m/600m) um Haltestellen zur Analyse der Netzabdeckung.

Technische Details
Framework: .NET 10.0 Windows (WPF & WinForms Interop).
Bibliotheken:
CsvHelper: Einlesen der GTFS-Textdateien.
GMap.NET: Karten-Engine für die Visualisierung.
NetTopologySuite: Verarbeitung und Export von geografischen Vektordaten (Shapefiles).
Architektur: Zentraler GtfsStore für den Zugriff auf den geladenen Datensatz über alle Module hinweg.

Installation & Nutzung
Projekt klonen: Repository herunterladen und in Visual Studio öffnen.
Abhängigkeiten: NuGet-Pakete (CsvHelper, GMap.NET, NetTopologySuite) werden beim Build automatisch wiederhergestellt.
Daten laden: Nach dem Start einen Ordner auswählen, der die GTFS-Textdateien enthält. Das Programm lädt die Daten automatisch in den Speicher und aktiviert die Analysemodule.


Core Features
The application provides deep integration of the GTFS specification and supports the ingestion of all 17 official GTFS tables (including stops.txt, routes.txt, trips.txt, stop_times.txt, and shapes.txt).

1. Geospatial Data Export (GTFS to Shapefile)
Interactive Map: Visualization of line routes based on OpenStreetMap.
Shapefile Export: Exports line geometries as ESRI Shapefiles (.shp) including attribute data such as line numbers and operators.
Image Export: Creation of PNG maps including an automated legend for the selected line.

2. Timetable Management & Analysis
Timetable Matrix: Generation of classic tabular schedules for specific lines and calendar dates.
PDF Export: Export of generated timetables as PDF or HTML documents.
Live Departure Board: Real-time display of all departures at a selected stop for a specific time, including filters for lines and destinations.
Special Service Detection: Automatic identification of school-only runs or calendar exceptions.

3. Stop Explorer & Accessibility
Global Search: Quick search through thousands of stops with detailed views of attributes like accessibility (wheelchair boarding), fare zones, and transfer options.
Frequency Analysis: Automatic calculation of departures per week and per day (Mon–Sun) for every line at a stop.
Catchment Area Analysis: Visualization of accessibility radii (e.g., 300m/600m) around stops to analyze network coverage.

Technical Details
Framework: .NET 10.0 Windows (WPF & WinForms Interop).
Libraries:
CsvHelper: High-performance parsing of GTFS text files.
GMap.NET: Map engine for visualization.
NetTopologySuite: Processing and exporting of geospatial vector data (Shapefiles).
Architecture: Centralized GtfsStore for efficient access to the loaded dataset across all modules.

Installation & Usage
Clone Project: Download the repository and open it in Visual Studio.
Dependencies: NuGet packages (CsvHelper, GMap.NET, NetTopologySuite) are automatically restored during build.
Load Data: After starting the app, select the folder containing your GTFS text files. The program will automatically load the data into memory and activate the analysis modules.

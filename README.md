# 🚉 GTFS-Data Analyzer & Visualizer

![.NET Version](https://img.shields.io/badge/.NET-10.0-blue?style=for-the-badge&logo=dotnet)
![WPF](https://img.shields.io/badge/UI-WPF-orange?style=for-the-badge)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)

A powerful, high-performance desktop application designed for deep analysis, visualization, and processing of **GTFS (General Transit Feed Specification)** datasets. Built with a modern C# stack to handle complex transit networks with ease.

---

## 🚀 Key Features

### 🗺️ Geospatial Data & Export (GTFS to Shapefile)
* **Interactive Mapping:** Visualize complete transit lines using an integrated OpenStreetMap engine.
* **Vector Export:** Convert GTFS shapes into **ESRI Shapefiles (.shp)** including rich attribute data like line numbers and agency info.
* **Visual Dumps:** Create high-resolution PNG snapshots of routes with automatically generated legends.
* **Smart Cleaning:** Built-in gap detection to filter out erroneous "straight-line" artifacts in transit geometries.

### 📅 Timetable Management & Analysis
* **Schedule Matrix:** Generate classic tabular timetables for any line and specific calendar dates.
* **Professional Export:** Save your schedules as clean **PDF/HTML documents** for distribution.
* **Live Departure Board:** Real-time-style departure monitors for any station with advanced filtering.
* **Service Intelligence:** Automatic detection of school-runs, holiday exceptions, and special service patterns.

### 🚉 Stop Explorer & Accessibility
* **Global Station Search:** Instant access to thousands of stops with detailed metadata (Accessibility, Fare Zones, Transfers).
* **Frequency Analysis:** Automated calculation of departure counts per week and per day to identify high-frequency corridors.
* **Catchment Area Analysis:** Visualize accessibility radii (e.g., 300m/600m) to evaluate local network coverage.

---

## 🛠️ Technical Details

| Component | Technology |
| :--- | :--- |
| **Framework** | .NET 10.0 Windows (WPF & WinForms Interop) |
| **Parsing** | `CsvHelper` for high-speed GTFS ingestion |
| **Map Engine** | `GMap.NET` for interactive geospatial visualization |
| **GIS Engine** | `NetTopologySuite` for professional Shapefile processing |
| **Architecture** | Centralized `GtfsStore` for efficient cross-module data access |

The application supports all **17 official GTFS tables**, ensuring full compatibility with standard transit feeds.

---

## 📖 Installation & Usage

### 1. Prerequisites
* Visual Studio 2022 (or newer)
* .NET 10 SDK

### 2. Setup
1.  **Clone the repository:**
    ```bash
    git clone [https://github.com/your-username/GTFS-Data-Analyzer.git](https://github.com/your-username/GTFS-Data-Analyzer.git)
    ```
2.  **Open the Solution:** Load the `.sln` file in Visual Studio.
3.  **Restore Packages:** NuGet dependencies (`CsvHelper`, `GMap.NET`, `NetTopologySuite`) will restore automatically on build.

### 3. Loading Data
Upon launching the application, use the **"Load GTFS"** button to select a folder containing your `.txt` feed files. The system will initialize the `GtfsStore` and unlock all analysis modules.

---

## 🇩🇪 Deutsche Kurzbeschreibung

Dieses Tool ermöglicht die tiefgehende Analyse von GTFS-Verkehrsdaten. 
* **Export:** Konvertierung von GTFS-Daten in ESRI Shapefiles (.shp).
* **Analyse:** Erstellung von Fahrplan-Matrizen und PDF-Exporten.
* **Visualisierung:** Darstellung von Haltestellen-Erreichbarkeiten und Frequenzanalysen auf einer interaktiven Karte.
* **Technik:** Basiert auf .NET 10 und WPF für maximale Performance.

---
*Developed as a professional tool for transit planners and GIS enthusiasts.*

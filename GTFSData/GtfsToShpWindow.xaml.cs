using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GTFSData
{
    public partial class GtfsToShpWindow : Window
    {
        private List<LineItemDisplay> availableLineNames = [];
        private string currentSelectedLine = "";
        private string currentRouteLongName = "";
        private string currentBetreiberName = "";
        private List<List<PointLatLng>> currentLineVariants = [];
        private List<GMapRoute> currentMapRoutes = [];

        public GtfsToShpWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                GMapProvider.WebProxy = null;
                GMaps.Instance.Mode = AccessMode.ServerAndCache;
                MainMap.MapProvider = OpenStreetMapProvider.Instance;
                MainMap.Position = new PointLatLng(49.1426, 9.2108);
                MainMap.Zoom = 10;
                MainMap.DragButton = MouseButton.Left;
                MainMap.ShowCenter = false;

                availableLineNames = GtfsStore.Routes
                    .Where(r => !string.IsNullOrWhiteSpace(r.route_short_name))
                    .GroupBy(r => r.route_short_name!)
                    .Select(g => new LineItemDisplay
                    {
                        ShortName = g.Key,
                        LongName = g.First().route_long_name ?? "Keine Streckenbeschreibung hinterlegt"
                    })
                    .OrderBy(n => n.ShortName)
                    .ToList();

                UpdateSearchList("");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler beim Starten der Karte: " + ex.Message);
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSearchList(TxtSearch.Text);
        }

        private void UpdateSearchList(string query)
        {
            if (availableLineNames == null) return;

            var filtered = string.IsNullOrWhiteSpace(query)
                ? availableLineNames
                : availableLineNames.Where(n =>
                    n.ShortName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    n.LongName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            ListLines.ItemsSource = filtered;
        }

        private void ListLines_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListLines.SelectedItem is LineItemDisplay selectedLine)
            {
                ShowLineVariantsOnMap(selectedLine.ShortName);
            }
        }

        // Berechnet die Entfernung, um kaputte GTFS-Shapes (riesige Luftlinien) abzuschneiden
        private double CalculateDistanceMeters(PointLatLng p1, PointLatLng p2)
        {
            var R = 6371000;
            var dLat = (p2.Lat - p1.Lat) * Math.PI / 180;
            var dLon = (p2.Lng - p1.Lng) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(p1.Lat * Math.PI / 180) * Math.Cos(p2.Lat * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private void ShowLineVariantsOnMap(string lineName)
        {
            var matchingRoutes = GtfsStore.Routes.Where(r => r.route_short_name == lineName).ToList();
            if (!matchingRoutes.Any()) return;

            var routeIds = matchingRoutes.Select(r => r.route_id).ToHashSet();
            currentRouteLongName = matchingRoutes.FirstOrDefault(r => !string.IsNullOrEmpty(r.route_long_name))?.route_long_name ?? "Unbekannt";
            currentSelectedLine = lineName;

            string rawAgencyId = matchingRoutes.FirstOrDefault(r => !string.IsNullOrEmpty(r.agency_id))?.agency_id ?? "";
            currentBetreiberName = GtfsStore.Agencies.FirstOrDefault(a => a.agency_id == rawAgencyId)?.agency_name ?? "Unbekannt";

            var relevantTrips = GtfsStore.Trips.Where(t => t.route_id != null && routeIds.Contains(t.route_id)).ToList();

            var shapeIdsForLine = relevantTrips
                .Where(t => !string.IsNullOrEmpty(t.shape_id))
                .Select(t => t.shape_id)
                .Distinct()
                .ToList();

            if (!shapeIdsForLine.Any())
            {
                TxtStatus.Text = $"Linie {lineName} hat keine Geometriedaten.";
                BtnExport.IsEnabled = false;
                BtnExportPng.IsEnabled = false;
                return;
            }

            currentLineVariants.Clear();
            foreach (var sId in shapeIdsForLine)
            {
                var points = GtfsStore.Shapes
                    .Where(s => s.shape_id == sId)
                    .OrderBy(s => s.shape_pt_sequence)
                    .Select(sp => new PointLatLng(sp.shape_pt_lat, sp.shape_pt_lon))
                    .ToList();

                if (points.Count > 1)
                {
                    var currentSegment = new List<PointLatLng> { points[0] };
                    for (int i = 1; i < points.Count; i++)
                    {
                        // Lückenerkennung (8km)
                        if (CalculateDistanceMeters(points[i - 1], points[i]) > 8000)
                        {
                            if (currentSegment.Count > 1) currentLineVariants.Add(currentSegment);
                            currentSegment = [];
                        }
                        currentSegment.Add(points[i]);
                    }
                    if (currentSegment.Count > 1) currentLineVariants.Add(currentSegment);
                }
            }

            DrawVariantsOnMap();
            TxtStatus.Text = $"Linie {lineName} geladen.";
            BtnExport.IsEnabled = true;
            BtnExportPng.IsEnabled = true;
        }

        private void DrawVariantsOnMap()
        {
            foreach (var route in currentMapRoutes) MainMap.Markers.Remove(route);
            currentMapRoutes.Clear();

            foreach (var variantPoints in currentLineVariants)
            {
                var mapRoute = new GMapRoute(variantPoints)
                {
                    Shape = new System.Windows.Shapes.Path { Stroke = new SolidColorBrush(Color.FromRgb(220, 53, 69)), StrokeThickness = 4, Opacity = 0.85 }
                };
                currentMapRoutes.Add(mapRoute);
                MainMap.Markers.Add(mapRoute);
            }

            if (currentLineVariants.Any() && currentLineVariants.First().Any())
            {
                MainMap.Position = currentLineVariants.First()[currentLineVariants.First().Count / 2];
                MainMap.Zoom = 12;
            }
        }

        private void BtnExportShape_Click(object sender, RoutedEventArgs e)
        {
            if (!currentLineVariants.Any()) return;

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "ESRI Shapefile (*.shp)|*.shp",
                FileName = $"Linie_{currentSelectedLine}_Komplett.shp"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var factory = new GeometryFactory();
                    var lineStrings = new List<LineString>();

                    foreach (var variant in currentLineVariants)
                    {
                        var coords = variant.Select(p => new Coordinate(p.Lng, p.Lat)).ToArray();
                        if (coords.Length > 1) lineStrings.Add(factory.CreateLineString(coords));
                    }

                    var multiLine = factory.CreateMultiLineString(lineStrings.ToArray());
                    var attributes = new AttributesTable();
                    attributes.Add("Liniennr", currentSelectedLine);
                    attributes.Add("Name", currentRouteLongName);
                    attributes.Add("Betreiber", currentBetreiberName);

                    var feature = new Feature(multiLine, attributes);
                    var features = new List<IFeature> { feature };

                    var header = ShapefileDataWriter.GetHeader(features[0], features.Count);
                    var writer = new ShapefileDataWriter(sfd.FileName, factory) { Header = header };
                    writer.Write(features);

                    string prjPath = System.IO.Path.ChangeExtension(sfd.FileName, ".prj");
                    string wgs84Wkt = "GEOGCS[\"GCS_WGS_1984\",DATUM[\"D_WGS_1984\",SPHEROID[\"WGS_1984\",6378137.0,298.257223563]],PRIMEM[\"Greenwich\",0.0],UNIT[\"Degree\",0.0174532925199433]]";
                    File.WriteAllText(prjPath, wgs84Wkt);

                    MessageBox.Show("Shapefile erfolgreich exportiert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Fehler beim Exportieren:\n" + ex.Message);
                }
            }
        }

        private void BtnExportPng_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG Bild (*.png)|*.png",
                FileName = $"Linie_{currentSelectedLine}_MapDump.png"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    int width = (int)MainMap.ActualWidth;
                    int height = (int)MainMap.ActualHeight;
                    RenderTargetBitmap rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(MainMap);

                    DrawingVisual dv = new DrawingVisual();
                    using (DrawingContext dc = dv.RenderOpen())
                    {
                        dc.DrawImage(rtb, new Rect(0, 0, width, height));

                        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(230, 24, 24, 24)),
                                         new Pen(new SolidColorBrush(Color.FromRgb(0, 120, 215)), 2),
                                         new Rect(20, 20, 250, 60));

                        FormattedText lineText = new FormattedText(
                            $"Linie {currentSelectedLine}",
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                            32, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);

                        // PRO-FIX: Eindeutige Zuweisung zu System.Windows.Point
                        dc.DrawText(lineText, new System.Windows.Point(35, 25));
                    }

                    RenderTargetBitmap finalImage = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                    finalImage.Render(dv);

                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(finalImage));

                    using (var fs = File.OpenWrite(sfd.FileName))
                    {
                        encoder.Save(fs);
                    }

                    MessageBox.Show("High-Res PNG erfolgreich erstellt!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Fehler beim Erstellen des Bildes:\n" + ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    public class LineItemDisplay
    {
        public string ShortName { get; set; } = "";
        public string LongName { get; set; } = "";
        public string DisplayName => $"Linie {ShortName}";
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;

namespace GTFSData
{
    public partial class AccessibilityWindow : Window
    {
        // .NET 10 Collection Expressions (schneller und sauberer)
        private List<GtfsStop> allStops = [];
        private List<GMapMarker> trainMarkers = [];
        private List<GMapMarker> busMarkers = [];
        private Dictionary<string, int> stopToType = [];
        private int currentRenderedState = 0;

        public AccessibilityWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MainMap.MapProvider = OpenStreetMapProvider.Instance;
            MainMap.Position = new PointLatLng(49.1426, 9.2108);
            MainMap.Zoom = 11;
            MainMap.OnMapZoomChanged += UpdateMapVisibility;

            allStops = GtfsStore.Stops;
            await PrepareMarkersAsync();
            UpdateSearchList("");
            UpdateMapVisibility();
        }

        private async Task PrepareMarkersAsync()
        {
            await Task.Run(() =>
            {
                var tripToType = new Dictionary<string, string>();
                foreach (var trip in GtfsStore.Trips)
                {
                    if (string.IsNullOrEmpty(trip.trip_id)) continue;
                    var route = GtfsStore.Routes.FirstOrDefault(r => r.route_id == trip.route_id);
                    tripToType[trip.trip_id] = route?.route_type ?? "3";
                }

                foreach (var st in GtfsStore.StopTimes)
                {
                    if (string.IsNullOrEmpty(st.stop_id) || string.IsNullOrEmpty(st.trip_id)) continue;
                    if (!tripToType.TryGetValue(st.trip_id, out string? rType) || rType == null) continue;

                    bool isTrain = (rType == "0" || rType == "1" || rType == "2" || rType == "100" || rType == "400");

                    if (!stopToType.ContainsKey(st.stop_id)) stopToType[st.stop_id] = isTrain ? 0 : 3;
                    else if (isTrain) stopToType[st.stop_id] = 0;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var stop in allStops)
                    {
                        if (string.IsNullOrEmpty(stop.stop_id)) continue;

                        int type = stopToType.GetValueOrDefault(stop.stop_id, 3);
                        var marker = new GMapMarker(new PointLatLng(stop.stop_lat, stop.stop_lon));

                        var circle = new Ellipse
                        {
                            Width = 10,
                            Height = 10,
                            Fill = type == 0 ? new SolidColorBrush(Color.FromRgb(220, 53, 69)) : new SolidColorBrush(Color.FromRgb(30, 144, 255)),
                            Stroke = Brushes.White,
                            StrokeThickness = 1.5,
                            Cursor = Cursors.Hand
                        };

                        circle.MouseLeftButtonDown += (s, a) => { DrawRadius(stop); a.Handled = true; };
                        marker.Shape = circle;
                        marker.Offset = new System.Windows.Point(-5, -5);

                        if (type == 0) trainMarkers.Add(marker); else busMarkers.Add(marker);

                        stop.DisplayStopName = stop.stop_name + (string.IsNullOrEmpty(stop.platform_code) ? "" : $" (Steig {stop.platform_code})");
                    }
                });
            });
        }

        private void UpdateMapVisibility()
        {
            int state = MainMap.Zoom >= 14 ? 2 : (MainMap.Zoom >= 11 ? 1 : 0);
            if (currentRenderedState != state)
            {
                // Sichert die gezeichneten Radien (Polygone), bevor die Marker geleert werden
                var polygons = MainMap.Markers.Where(m => m is GMapPolygon).ToList();
                MainMap.Markers.Clear();

                foreach (var p in polygons) MainMap.Markers.Add(p);
                if (state >= 1) foreach (var m in trainMarkers) MainMap.Markers.Add(m);
                if (state >= 2) foreach (var m in busMarkers) MainMap.Markers.Add(m);

                currentRenderedState = state;

                if (state == 0) TxtMapInfo.Text = "Zoom näher heran (ab Stufe 11).";
                else if (state == 1) TxtMapInfo.Text = "Bahnhöfe sichtbar. (Näher für Busse)";
                else TxtMapInfo.Text = "Alle Haltestellen sichtbar.";
            }
        }

        private void DrawRadius(GtfsStop stop)
        {
            if (string.IsNullOrEmpty(stop.stop_id)) return;

            int type = stopToType.GetValueOrDefault(stop.stop_id, 3);

            // Sicheres .NET 10 Parsing: Verhindert Abstürze durch Falscheingaben
            double radius = 300;
            if (type == 0)
            {
                if (!double.TryParse(TxtTrainRadius.Text, out radius)) radius = 600;
            }
            else
            {
                if (!double.TryParse(TxtBusRadius.Text, out radius)) radius = 300;
            }

            List<PointLatLng> points = [];
            for (int i = 0; i <= 360; i += 10)
            {
                double angle = i * Math.PI / 180;
                double latOffset = (radius / 6371000.0) * (180.0 / Math.PI);
                double lngOffset = latOffset / Math.Cos(stop.stop_lat * Math.PI / 180.0);
                points.Add(new PointLatLng(stop.stop_lat + latOffset * Math.Sin(angle), stop.stop_lon + lngOffset * Math.Cos(angle)));
            }

            Color fillColor = type == 0 ? Color.FromArgb(60, 220, 53, 69) : Color.FromArgb(60, 30, 144, 255);
            Brush strokeBrush = type == 0 ? new SolidColorBrush(Color.FromRgb(220, 53, 69)) : new SolidColorBrush(Color.FromRgb(30, 144, 255));

            var poly = new GMapPolygon(points)
            {
                Shape = new System.Windows.Shapes.Path
                {
                    Fill = new SolidColorBrush(fillColor),
                    Stroke = strokeBrush,
                    StrokeThickness = 1
                }
            };

            MainMap.Markers.Add(poly);
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSearchList(TxtSearch.Text);
        }

        private void UpdateSearchList(string query)
        {
            var filtered = string.IsNullOrWhiteSpace(query)
                ? allStops.Take(40).ToList()
                : allStops.Where(s => s.stop_name != null && s.stop_name.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(40).ToList();
            ListStops.ItemsSource = filtered;
        }

        private void ListStops_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListStops.SelectedItem is GtfsStop selectedStop)
            {
                MainMap.Position = new PointLatLng(selectedStop.stop_lat, selectedStop.stop_lon);
                MainMap.Zoom = 15;
                DrawRadius(selectedStop);
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            var nonPolygons = MainMap.Markers.Where(m => !(m is GMapPolygon)).ToList();
            MainMap.Markers.Clear();
            foreach (var m in nonPolygons) MainMap.Markers.Add(m);
        }
    }
}
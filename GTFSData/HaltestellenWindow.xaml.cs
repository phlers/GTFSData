using System;
using System.Collections.Generic;
using System.Globalization;
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
    public partial class HaltestellenWindow : Window
    {
        private List<GtfsStop> allStops = [];
        private List<GMapMarker> trainMarkers = [];
        private List<GMapMarker> busMarkers = [];
        private int currentRenderedState = -1; // -1 erzwingt initiales Update

        public HaltestellenWindow()
        {
            InitializeComponent();
        }

        // --- EIGENE FENSTER-LEISTE STEUERUNG ---
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) BtnMaximize_Click(sender, e);
            else DragMove();
        }
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnMaximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();


        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            GMapProvider.WebProxy = null;
            GMaps.Instance.Mode = AccessMode.ServerAndCache;
            MainMap.MapProvider = OpenStreetMapProvider.Instance;
            MainMap.Position = new PointLatLng(49.1426, 9.2108);
            MainMap.Zoom = 10;
            MainMap.DragButton = MouseButton.Left;
            MainMap.OnMapZoomChanged += UpdateMapVisibility;

            allStops = GtfsStore.Stops;

            foreach (var stop in allStops)
            {
                stop.DisplayStopName = stop.stop_name + (string.IsNullOrEmpty(stop.platform_code) ? "" : $" (Steig {stop.platform_code})");
            }

            UpdateSearchList("");
            await PrepareMarkersAsync();
            UpdateMapVisibility();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => UpdateSearchList(TxtSearch.Text);

        private void UpdateSearchList(string query)
        {
            var filtered = string.IsNullOrWhiteSpace(query)
                ? allStops.Take(50).ToList()
                : allStops.Where(s => s.stop_name != null && s.stop_name.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(50).ToList();

            ListStops.ItemsSource = filtered;
        }

        private void ListStops_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListStops.SelectedItem is GtfsStop s)
            {
                MainMap.Position = new PointLatLng(s.stop_lat, s.stop_lon);
                MainMap.Zoom = 16;
                ShowStopDetails(s);
            }
        }

        private async Task PrepareMarkersAsync()
        {
            await Task.Run(() =>
            {
                var tripToRouteType = new Dictionary<string, string>();
                var tripToAgency = new Dictionary<string, string>();

                foreach (var trip in GtfsStore.Trips)
                {
                    if (string.IsNullOrEmpty(trip.trip_id)) continue;
                    var route = GtfsStore.Routes.FirstOrDefault(r => r.route_id == trip.route_id);
                    tripToRouteType[trip.trip_id] = route?.route_type ?? "3";

                    var agency = GtfsStore.Agencies.FirstOrDefault(a => a.agency_id == route?.agency_id);
                    tripToAgency[trip.trip_id] = agency?.agency_name?.ToLower() ?? "";
                }

                var stopToType = new Dictionary<string, int>();
                foreach (var st in GtfsStore.StopTimes)
                {
                    if (string.IsNullOrEmpty(st.stop_id) || string.IsNullOrEmpty(st.trip_id)) continue;
                    if (!tripToRouteType.TryGetValue(st.trip_id, out string? rType) || rType == null) continue;

                    tripToAgency.TryGetValue(st.trip_id, out string? aName);
                    aName ??= "";

                    bool isTrain = (rType == "0" || rType == "1" || rType == "2" || rType == "100" || rType == "400");
                    bool isBuerger = aName.Contains("bürgerbus");

                    int currentType = isTrain ? 0 : (isBuerger ? 2 : 1);

                    if (!stopToType.ContainsKey(st.stop_id)) stopToType[st.stop_id] = currentType;
                    else if (currentType < stopToType[st.stop_id]) stopToType[st.stop_id] = currentType;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    trainMarkers.Clear(); busMarkers.Clear();
                    foreach (var stop in allStops)
                    {
                        if (string.IsNullOrEmpty(stop.stop_id)) continue;

                        int type = stopToType.GetValueOrDefault(stop.stop_id, 1);
                        Brush fill = type == 0 ? Brushes.Red : (type == 2 ? Brushes.Gray : Brushes.DodgerBlue);

                        var marker = new GMapMarker(new PointLatLng(stop.stop_lat, stop.stop_lon));

                        var circle = new Ellipse { Fill = fill, Stroke = Brushes.White, StrokeThickness = 1.5, Cursor = Cursors.Hand };

                        // --- PRO-FIX: HOVER ANIMATION ---
                        var scaleTransform = new ScaleTransform(1, 1);
                        circle.RenderTransform = scaleTransform;
                        circle.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);

                        circle.MouseEnter += (s, a) =>
                        {
                            scaleTransform.ScaleX = 1.5;
                            scaleTransform.ScaleY = 1.5;
                            circle.Stroke = Brushes.Yellow;
                            circle.StrokeThickness = 2.5;
                            Panel.SetZIndex(circle, 9999);
                        };

                        circle.MouseLeave += (s, a) =>
                        {
                            scaleTransform.ScaleX = 1.0;
                            scaleTransform.ScaleY = 1.0;
                            circle.Stroke = Brushes.White;
                            circle.StrokeThickness = 1.5;
                            Panel.SetZIndex(circle, 0);
                        };

                        circle.MouseLeftButtonDown += (s, a) => { ShowStopDetails(stop); a.Handled = true; };

                        marker.Shape = circle;
                        marker.ZIndex = type == 0 ? 10 : 1;

                        if (type == 0) trainMarkers.Add(marker); else busMarkers.Add(marker);
                    }
                });
            });
        }

        private void UpdateMapVisibility()
        {
            int state = MainMap.Zoom >= 14 ? 2 : (MainMap.Zoom >= 11 ? 1 : 0);

            // --- PRO-FIX: DYNAMISCHER ZOOM FÜR HALTESTELLEN ---
            double zoomScale = MainMap.Zoom >= 16 ? 1.6 : (MainMap.Zoom >= 14 ? 1.3 : 1.0);

            foreach (var m in trainMarkers)
            {
                if (m.Shape is Ellipse e)
                {
                    double s = 14 * zoomScale;
                    e.Width = s; e.Height = s;
                    m.Offset = new System.Windows.Point(-s / 2, -s / 2);
                }
            }

            foreach (var m in busMarkers)
            {
                if (m.Shape is Ellipse e)
                {
                    double s = 10 * zoomScale;
                    e.Width = s; e.Height = s;
                    m.Offset = new System.Windows.Point(-s / 2, -s / 2);
                }
            }

            if (currentRenderedState != state)
            {
                MainMap.Markers.Clear();
                if (state >= 1) foreach (var m in trainMarkers) MainMap.Markers.Add(m);
                if (state >= 2) foreach (var m in busMarkers) MainMap.Markers.Add(m);
                currentRenderedState = state;

                if (state == 0) TxtMapInfo.Text = "Zoom näher heran.";
                else if (state == 1) TxtMapInfo.Text = "Bahnhöfe sichtbar.";
                else TxtMapInfo.Text = "Alle Haltestellen sichtbar.";
            }
        }

        private async void ShowStopDetails(GtfsStop stop)
        {
            TxtName.Text = stop.DisplayStopName;

            string typeStr = "0 - Haltepunkt / Bahnsteig";
            if (stop.location_type == "1") typeStr = "1 - Station / Bahnhof";
            else if (stop.location_type == "2") typeStr = "2 - Eingang / Ausgang";
            else if (stop.location_type == "3") typeStr = "3 - Knotenpunkt (Node)";
            else if (stop.location_type == "4") typeStr = "4 - Einsteigebereich";
            TxtType.Text = typeStr;

            string pStation = "Keine";
            if (!string.IsNullOrWhiteSpace(stop.parent_station))
            {
                var parent = GtfsStore.Stops.FirstOrDefault(s => s.stop_id == stop.parent_station);
                pStation = parent?.stop_name ?? stop.parent_station;
            }
            TxtParent.Text = pStation;

            TxtWheelchair.Text = stop.wheelchair_boarding == "1" ? "Ja, möglich" : (stop.wheelchair_boarding == "2" ? "Nein, nicht möglich" : "Unbekannt");
            TxtZone.Text = string.IsNullOrWhiteSpace(stop.zone_id) ? "Keine Tarifzone definiert" : stop.zone_id;

            if (!string.IsNullOrWhiteSpace(stop.level_id))
            {
                var level = GtfsStore.Levels.FirstOrDefault(l => l.level_id == stop.level_id);
                TxtLevel.Text = level != null ? $"{level.level_name ?? level.level_id} (Index: {level.level_index})" : stop.level_id;
            }
            else TxtLevel.Text = "Nicht definiert";

            var pathwaysCount = GtfsStore.Pathways.Count(p => p.from_stop_id == stop.stop_id || p.to_stop_id == stop.stop_id);
            TxtPathways.Text = pathwaysCount > 0 ? $"{pathwaysCount} verknüpfte Wegelemente im System" : "Keine Detailwege erfasst";

            var transfers = GtfsStore.Transfers.Where(t => t.from_stop_id == stop.stop_id).ToList();
            if (transfers.Any())
            {
                var toStopIds = transfers.Select(t => t.to_stop_id).Distinct().ToList();
                var toStops = GtfsStore.Stops.Where(s => s.stop_id != null && toStopIds.Contains(s.stop_id)).Select(s => s.stop_name).Distinct().ToList();
                TxtTransfers.Text = toStops.Any() ? string.Join(", ", toStops) : "Keine direkten Verbindungen.";
            }
            else TxtTransfers.Text = "Keine garantierten Umstiege erfasst.";

            ListDepartures.ItemsSource = null;
            TxtLoadingDepartures.Visibility = Visibility.Visible;

            var departureData = await Task.Run(() => CalculateDepartures(stop.stop_id));

            TxtLoadingDepartures.Visibility = Visibility.Collapsed;
            ListDepartures.ItemsSource = departureData;
        }

        private int ParseGtfsTimeToSeconds(string timeStr)
        {
            var parts = timeStr.Split(':');
            if (parts.Length == 3 && int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int m) && int.TryParse(parts[2], out int s))
            {
                return h * 3600 + m * 60 + s;
            }
            return 0;
        }

        private List<DepartureInfo> CalculateDepartures(string? stopId)
        {
            if (string.IsNullOrEmpty(stopId)) return [];

            var tripIdsAtStop = GtfsStore.StopTimes
                .Where(st => st.stop_id == stopId && !string.IsNullOrEmpty(st.trip_id))
                .Select(st => st.trip_id!)
                .ToHashSet();

            List<DepartureInfo> results = [];
            var relevantTrips = GtfsStore.Trips.Where(t => t.trip_id != null && tripIdsAtStop.Contains(t.trip_id)).ToList();

            foreach (var routeGroup in relevantTrips.GroupBy(t => t.route_id))
            {
                var route = GtfsStore.Routes.FirstOrDefault(r => r.route_id == routeGroup.Key);
                if (route == null) continue;

                string agencyName = GtfsStore.Agencies.FirstOrDefault(a => a.agency_id == route.agency_id)?.agency_name ?? "Unbekannt";

                int[] dailyDepartures = new int[7];
                bool isTaktverkehr = false;

                foreach (var trip in routeGroup)
                {
                    int tripOccurrencesPerDay = 1;

                    var freqs = GtfsStore.Frequencies.Where(f => f.trip_id == trip.trip_id).ToList();
                    if (freqs.Any())
                    {
                        isTaktverkehr = true;
                        tripOccurrencesPerDay = 0;
                        foreach (var f in freqs)
                        {
                            if (int.TryParse(f.headway_secs, out int headway) && headway > 0 &&
                                !string.IsNullOrEmpty(f.start_time) && !string.IsNullOrEmpty(f.end_time))
                            {
                                int start = ParseGtfsTimeToSeconds(f.start_time);
                                int end = ParseGtfsTimeToSeconds(f.end_time);
                                if (end > start) tripOccurrencesPerDay += (end - start) / headway;
                            }
                        }
                        if (tripOccurrencesPerDay == 0) tripOccurrencesPerDay = 1;
                    }

                    bool[] operatesOn = new bool[7];

                    var cal = GtfsStore.Calendar.FirstOrDefault(c => c.service_id == trip.service_id);
                    if (cal != null)
                    {
                        if (cal.monday?.Trim() == "1") operatesOn[0] = true;
                        if (cal.tuesday?.Trim() == "1") operatesOn[1] = true;
                        if (cal.wednesday?.Trim() == "1") operatesOn[2] = true;
                        if (cal.thursday?.Trim() == "1") operatesOn[3] = true;
                        if (cal.friday?.Trim() == "1") operatesOn[4] = true;
                        if (cal.saturday?.Trim() == "1") operatesOn[5] = true;
                        if (cal.sunday?.Trim() == "1") operatesOn[6] = true;
                    }

                    var dates = GtfsStore.CalendarDates.Where(cd => cd.service_id == trip.service_id && cd.exception_type?.Trim() == "1").ToList();
                    foreach (var d in dates)
                    {
                        if (DateTime.TryParseExact(d.date?.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        {
                            int dayIdx = dt.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)dt.DayOfWeek - 1;
                            operatesOn[dayIdx] = true;
                        }
                    }

                    for (int i = 0; i < 7; i++)
                    {
                        if (operatesOn[i]) dailyDepartures[i] += tripOccurrencesPerDay;
                    }
                }

                int totalWeeklyDepartures = dailyDepartures.Sum();

                string depLabel = $"Σ {totalWeeklyDepartures} Abfahrten/Woche";
                if (isTaktverkehr) depLabel += " (Taktverkehr!)";

                string daysStr = $"Mo: {dailyDepartures[0]}   Di: {dailyDepartures[1]}   Mi: {dailyDepartures[2]}   Do: {dailyDepartures[3]}   Fr: {dailyDepartures[4]}   Sa: {dailyDepartures[5]}   So: {dailyDepartures[6]}";

                string rColor = string.IsNullOrEmpty(route.route_color) ? "0078D7" : route.route_color;
                string tColor = string.IsNullOrEmpty(route.route_text_color) ? "FFFFFF" : route.route_text_color;
                if (!rColor.StartsWith('#')) rColor = "#" + rColor;
                if (!tColor.StartsWith('#')) tColor = "#" + tColor;

                SolidColorBrush bg, txt;
                try { bg = (SolidColorBrush)new BrushConverter().ConvertFromString(rColor)!; bg.Freeze(); }
                catch { bg = Brushes.DodgerBlue; }

                try { txt = (SolidColorBrush)new BrushConverter().ConvertFromString(tColor)!; txt.Freeze(); }
                catch { txt = Brushes.White; }

                results.Add(new DepartureInfo
                {
                    LineName = route.route_short_name ?? "Linie",
                    RouteName = route.route_long_name ?? "Keine Streckenbeschreibung",
                    AgencyName = agencyName,
                    TotalDeparturesLabel = depLabel,
                    DeparturesPerDayString = daysStr,
                    BgBrush = bg,
                    TextBrush = txt
                });
            }
            return results.OrderBy(r => r.LineName).ToList();
        }
    }

    public class DepartureInfo
    {
        public string LineName { get; set; } = "";
        public string RouteName { get; set; } = "";
        public string AgencyName { get; set; } = "";
        public string TotalDeparturesLabel { get; set; } = "";
        public string DeparturesPerDayString { get; set; } = "";
        public SolidColorBrush BgBrush { get; set; } = Brushes.DodgerBlue;
        public SolidColorBrush TextBrush { get; set; } = Brushes.White;
    }
}
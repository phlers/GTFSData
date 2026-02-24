using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GTFSData
{
    public partial class FahrplanWindow : Window
    {
        private List<LineItemDisplay> availableLines = [];
        private List<GtfsStop> availableStops = [];
        private List<TimetableViewModel> currentTimetables = [];

        private bool isUpdatingComboBox = false;

        public FahrplanWindow()
        {
            InitializeComponent();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) BtnMaximize_Click(sender, e);
            else DragMove();
        }
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnMaximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            availableLines = GtfsStore.Routes
                .Where(r => !string.IsNullOrWhiteSpace(r.route_short_name))
                .GroupBy(r => r.route_short_name!)
                .Select(g => new LineItemDisplay
                {
                    ShortName = g.Key ?? "Unbekannt",
                    LongName = g.First().route_long_name ?? "Unbekannt"
                })
                .OrderBy(n => n.ShortName)
                .ToList();

            CboLines.ItemsSource = availableLines;
            CboLines.DisplayMemberPath = "DisplayName";

            availableStops = GtfsStore.Stops.Where(s => s.location_type == "0" || string.IsNullOrEmpty(s.location_type)).ToList();
            foreach (var s in availableStops)
            {
                s.DisplayStopName = s.stop_name + (string.IsNullOrEmpty(s.platform_code) ? "" : $" ({s.platform_code})");
            }

            UpdateStopSearch("");
            SetCalendarBounds();
        }

        private void SetCalendarBounds()
        {
            DateTime minDate = DateTime.MaxValue;
            DateTime maxDate = DateTime.MinValue;

            foreach (var cal in GtfsStore.Calendar)
            {
                if (DateTime.TryParseExact(cal.start_date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
                    if (start < minDate) minDate = start;
                if (DateTime.TryParseExact(cal.end_date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
                    if (end > maxDate) maxDate = end;
            }

            foreach (var cd in GtfsStore.CalendarDates)
            {
                if (DateTime.TryParseExact(cd.date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                {
                    if (d < minDate) minDate = d;
                    if (d > maxDate) maxDate = d;
                }
            }

            if (minDate != DateTime.MaxValue && maxDate != DateTime.MinValue)
            {
                DpDate.DisplayDateStart = minDate;
                DpDate.DisplayDateEnd = maxDate;
                DpStopDate.DisplayDateStart = minDate;
                DpStopDate.DisplayDateEnd = maxDate;

                DateTime today = DateTime.Today;
                DpDate.SelectedDate = (today >= minDate && today <= maxDate) ? today : minDate;
                DpStopDate.SelectedDate = DpDate.SelectedDate;
            }
        }

        private bool RunsOnDate(string serviceId, DateTime date)
        {
            string dateStr = date.ToString("yyyyMMdd");

            var exception = GtfsStore.CalendarDates.FirstOrDefault(cd => cd.service_id == serviceId && cd.date == dateStr);
            if (exception != null)
            {
                if (exception.exception_type == "1") return true;
                if (exception.exception_type == "2") return false;
            }

            var cal = GtfsStore.Calendar.FirstOrDefault(c => c.service_id == serviceId);
            if (cal != null)
            {
                if (string.Compare(dateStr, cal.start_date) >= 0 && string.Compare(dateStr, cal.end_date) <= 0)
                {
                    if (date.DayOfWeek == DayOfWeek.Monday && cal.monday == "1") return true;
                    if (date.DayOfWeek == DayOfWeek.Tuesday && cal.tuesday == "1") return true;
                    if (date.DayOfWeek == DayOfWeek.Wednesday && cal.wednesday == "1") return true;
                    if (date.DayOfWeek == DayOfWeek.Thursday && cal.thursday == "1") return true;
                    if (date.DayOfWeek == DayOfWeek.Friday && cal.friday == "1") return true;
                    if (date.DayOfWeek == DayOfWeek.Saturday && cal.saturday == "1") return true;
                    if (date.DayOfWeek == DayOfWeek.Sunday && cal.sunday == "1") return true;
                }
            }
            return false;
        }

        // --- NEU: ERKENNT SCHULFAHRTEN, WOCHENTAGE UND AUSNAHMEN ---
        private string GetServiceDays(GtfsTrip trip)
        {
            var cal = GtfsStore.Calendar.FirstOrDefault(c => c.service_id == trip.service_id);
            var exceptions = GtfsStore.CalendarDates.Where(cd => cd.service_id == trip.service_id).ToList();

            if (cal != null)
            {
                bool mo = cal.monday?.Trim() == "1";
                bool di = cal.tuesday?.Trim() == "1";
                bool mi = cal.wednesday?.Trim() == "1";
                bool do_ = cal.thursday?.Trim() == "1";
                bool fr = cal.friday?.Trim() == "1";
                bool sa = cal.saturday?.Trim() == "1";
                bool so = cal.sunday?.Trim() == "1";

                if (mo && di && mi && do_ && fr && !sa && !so) return "Mo-Fr";
                if (mo && di && mi && do_ && fr && sa && so) return "Täglich";
                if (!mo && !di && !mi && !do_ && !fr && sa && !so) return "Nur Sa";
                if (!mo && !di && !mi && !do_ && !fr && !sa && so) return "Nur So";
                if (!mo && !di && !mi && !do_ && !fr && sa && so) return "Sa+So";

                // Falls ein wilder Mix angegeben ist
                if (mo || di || mi || do_ || fr || sa || so) return "Bestimmte Tage";
            }

            // Wenn es keinen regulären Kalender gibt, aber Ausnahmetage (oft Schulfahrten oder Feiertags-Specials)
            if (exceptions.Any(d => d.exception_type == "1")) return "Sonderfahrt / Ausnahmen";

            return "-";
        }

        private string FormatTime(string gtfsTime)
        {
            if (string.IsNullOrEmpty(gtfsTime)) return "-";
            var parts = gtfsTime.Split(':');
            if (parts.Length >= 2) return $"{parts[0].PadLeft(2, '0')}:{parts[1].PadLeft(2, '0')}";
            return gtfsTime;
        }

        // ==========================================================
        // TAB 1: FAHRPLAN MATRIX 
        // ==========================================================

        private void CboLines_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) BtnGenerateMatrix_Click(null!, null!);
        }

        private void UI_OptionsChanged(object sender, RoutedEventArgs e)
        {
            if (CboDirection != null) CboDirection.IsEnabled = (ChkKombiFahrplan.IsChecked == false);
            if (CboLines.SelectedItem != null) BtnGenerateMatrix_Click(null!, null!);
        }

        private void CboLines_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CboLines.SelectedItem is LineItemDisplay line)
            {
                isUpdatingComboBox = true;

                var routeIds = GtfsStore.Routes.Where(r => r.route_short_name == line.ShortName).Select(r => r.route_id).ToHashSet();
                var directions = GtfsStore.Trips.Where(t => t.route_id != null && routeIds.Contains(t.route_id))
                                        .Select(t => t.trip_headsign ?? "Richtung " + t.direction_id)
                                        .Where(d => !string.IsNullOrEmpty(d))
                                        .Distinct()
                                        .OrderBy(d => d)
                                        .ToList();

                directions.Insert(0, "Alle Richtungen");
                CboDirection.ItemsSource = directions;
                CboDirection.SelectedIndex = 0;

                isUpdatingComboBox = false;

                if (DpDate.SelectedDate != null) BtnGenerateMatrix_Click(null!, null!);
            }
        }

        private void CboDirection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isUpdatingComboBox && CboDirection.SelectedItem != null && ChkKombiFahrplan.IsChecked == false)
                BtnGenerateMatrix_Click(null!, null!);
        }

        private void DpDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CboLines.SelectedItem != null) BtnGenerateMatrix_Click(null!, null!);
        }

        private void BtnGenerateMatrix_Click(object sender, RoutedEventArgs e)
        {
            currentTimetables.Clear();
            TimetableItemsControl.ItemsSource = null;

            string lineSearch = CboLines.Text;
            if (CboLines.SelectedItem is LineItemDisplay selectedLineItem) lineSearch = selectedLineItem.ShortName;

            if (string.IsNullOrWhiteSpace(lineSearch) || DpDate.SelectedDate == null) return;

            DateTime selectedDate = DpDate.SelectedDate.Value;
            string selectedDirection = CboDirection.SelectedItem as string ?? "Alle Richtungen";
            bool isKombi = ChkKombiFahrplan.IsChecked == true;
            bool showDetails = ChkShowDetails.IsChecked == true;

            var routeIds = GtfsStore.Routes.Where(r => r.route_short_name != null && r.route_short_name.Equals(lineSearch, StringComparison.OrdinalIgnoreCase)).Select(r => r.route_id).ToHashSet();

            if (!routeIds.Any())
            {
                MessageBox.Show("Linie nicht gefunden!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var allActiveTrips = GtfsStore.Trips
                .Where(t => t.route_id != null && routeIds.Contains(t.route_id) &&
                            t.service_id != null && RunsOnDate(t.service_id, selectedDate))
                .ToList();

            if (!allActiveTrips.Any())
            {
                MessageBox.Show($"Keine Fahrten für Linie {lineSearch} am {selectedDate.ToString("dd.MM.yyyy")} gefunden.", "Kein Betrieb", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            List<IGrouping<string, GtfsTrip>> directionGroups;

            if (isKombi)
            {
                directionGroups = allActiveTrips.GroupBy(t => t.direction_id == "1" ? "Rückfahrten (Gesamt)" : "Hinfahrten (Gesamt)").ToList();
            }
            else
            {
                var tempGroups = allActiveTrips.GroupBy(t => t.trip_headsign ?? "Richtung " + t.direction_id).ToList();
                if (selectedDirection != "Alle Richtungen") tempGroups = tempGroups.Where(g => g.Key == selectedDirection).ToList();
                directionGroups = tempGroups;
            }

            foreach (var group in directionGroups)
            {
                var tripsInDirection = group.ToList();
                var activeTripIds = tripsInDirection.Select(t => t.trip_id).ToHashSet();
                var stopTimes = GtfsStore.StopTimes.Where(st => st.trip_id != null && activeTripIds.Contains(st.trip_id)).ToList();

                var masterStops = stopTimes
                    .Where(st => st.stop_id != null)
                    .GroupBy(st => st.stop_id!)
                    .Select(g => new {
                        StopId = g.Key,
                        AvgSeq = g.Average(st => int.TryParse(st.stop_sequence, out int seq) ? seq : 0)
                    })
                    .OrderBy(x => x.AvgSeq)
                    .Select(x => x.StopId)
                    .ToList();

                if (!masterStops.Any()) continue;

                DataTable dt = new DataTable();
                // Die Spalte heißt IMMER "Haltestelle" - So vergisst man sie nicht!
                dt.Columns.Add("Haltestelle", typeof(string));

                var sortedTrips = tripsInDirection.OrderBy(t => stopTimes.Where(st => st.trip_id == t.trip_id).Min(st => st.departure_time)).ToList();

                for (int i = 0; i < sortedTrips.Count; i++) dt.Columns.Add($"Fahrt {i + 1}", typeof(string));

                // --- HINWEISE / FAHRTDETAILS ---
                if (showDetails)
                {
                    DataRow rDays = dt.NewRow();
                    rDays[0] = "Verkehrstage / Art";
                    for (int i = 0; i < sortedTrips.Count; i++) rDays[i + 1] = GetServiceDays(sortedTrips[i]);
                    dt.Rows.Add(rDays);

                    if (sortedTrips.Any(t => !string.IsNullOrEmpty(t.trip_short_name)))
                    {
                        DataRow rName = dt.NewRow();
                        rName[0] = "Fahrt-/Zugnummer";
                        for (int i = 0; i < sortedTrips.Count; i++) rName[i + 1] = sortedTrips[i].trip_short_name ?? "";
                        dt.Rows.Add(rName);
                    }

                    if (isKombi && sortedTrips.Any(t => !string.IsNullOrEmpty(t.trip_headsign)))
                    {
                        DataRow rDest = dt.NewRow();
                        rDest[0] = "Fahrtziel";
                        for (int i = 0; i < sortedTrips.Count; i++) rDest[i + 1] = sortedTrips[i].trip_headsign ?? "";
                        dt.Rows.Add(rDest);
                    }

                    if (sortedTrips.Any(t => t.wheelchair_accessible == "1" || t.wheelchair_accessible == "2"))
                    {
                        DataRow rWheel = dt.NewRow();
                        rWheel[0] = "Barrierefrei";
                        for (int i = 0; i < sortedTrips.Count; i++)
                            rWheel[i + 1] = sortedTrips[i].wheelchair_accessible == "1" ? "♿ Ja" : (sortedTrips[i].wheelchair_accessible == "2" ? "Nein" : "-");
                        dt.Rows.Add(rWheel);
                    }

                    if (sortedTrips.Any(t => t.bikes_allowed == "1" || t.bikes_allowed == "2"))
                    {
                        DataRow rBike = dt.NewRow();
                        rBike[0] = "Fahrradmitnahme";
                        for (int i = 0; i < sortedTrips.Count; i++)
                            rBike[i + 1] = sortedTrips[i].bikes_allowed == "1" ? "🚲 Ja" : (sortedTrips[i].bikes_allowed == "2" ? "Nein" : "-");
                        dt.Rows.Add(rBike);
                    }

                    DataRow rSep = dt.NewRow();
                    rSep[0] = "▼ Haltestellen & Abfahrten ▼";
                    for (int i = 0; i < sortedTrips.Count; i++) rSep[i + 1] = "↓";
                    dt.Rows.Add(rSep);
                }

                // --- ECHTE HALTESTELLEN ---
                foreach (var stopId in masterStops)
                {
                    DataRow row = dt.NewRow();
                    var stopInfo = GtfsStore.Stops.FirstOrDefault(s => s.stop_id == stopId);
                    row[0] = stopInfo?.stop_name ?? stopId;

                    for (int i = 0; i < sortedTrips.Count; i++)
                    {
                        var tripId = sortedTrips[i].trip_id;
                        var st = stopTimes.FirstOrDefault(x => x.trip_id == tripId && x.stop_id == stopId);
                        row[i + 1] = st != null ? FormatTime(st.departure_time ?? "") : "|";
                    }
                    dt.Rows.Add(row);
                }

                currentTimetables.Add(new TimetableViewModel { Title = $"▶ {group.Key} ({sortedTrips.Count} Fahrten)", TableView = dt.DefaultView, RawTable = dt });
            }

            TimetableItemsControl.ItemsSource = currentTimetables;
        }

        private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (!currentTimetables.Any()) return;

            string lineName = CboLines.Text;
            string dateStr = DpDate.SelectedDate?.ToString("dd.MM.yyyy") ?? "";

            StringBuilder html = new StringBuilder();
            html.AppendLine("<html><head><meta charset='utf-8'><style>");
            html.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; }");
            html.AppendLine("h1 { color: #0078D7; }");
            html.AppendLine("h2 { color: #333; margin-top: 40px; border-bottom: 2px solid #0078D7; padding-bottom: 5px; }");
            html.AppendLine("table { border-collapse: collapse; width: 100%; font-size: 11px; margin-bottom: 30px; }");
            html.AppendLine("th, td { border: 1px solid #ddd; padding: 5px; text-align: center; }");
            html.AppendLine("th { background-color: #0078D7; color: #fff; font-weight: bold; }");
            html.AppendLine("td:first-child { text-align: left; font-weight: bold; background-color: #fafafa; }");
            html.AppendLine("</style></head><body>");

            html.AppendLine($"<h1>Gesamtfahrplan Linie {lineName}</h1>");
            html.AppendLine($"<p><strong>Gültig am:</strong> {dateStr}</p>");

            foreach (var timetable in currentTimetables)
            {
                html.AppendLine($"<h2>{timetable.Title}</h2>");
                html.AppendLine("<table><tr>");
                foreach (DataColumn col in timetable.RawTable.Columns) html.AppendLine($"<th>{col.ColumnName}</th>");
                html.AppendLine("</tr>");

                foreach (DataRow row in timetable.RawTable.Rows)
                {
                    html.AppendLine("<tr>");
                    foreach (var item in row.ItemArray) html.AppendLine($"<td>{item}</td>");
                    html.AppendLine("</tr>");
                }
                html.AppendLine("</table>");
            }

            html.AppendLine("</body></html>");

            string filePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Fahrplan_Linie_{lineName}_{dateStr}.html");
            File.WriteAllText(filePath, html.ToString());

            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }

        // ==========================================================
        // TAB 2: LIVE ABFAHRTEN
        // ==========================================================

        private void TxtStopSearch_TextChanged(object sender, TextChangedEventArgs e) => UpdateStopSearch(TxtStopSearch.Text);

        private void UpdateStopSearch(string query)
        {
            var filtered = string.IsNullOrWhiteSpace(query)
                ? availableStops.Take(50).ToList()
                : availableStops.Where(s => s.stop_name != null && s.stop_name.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(50).ToList();
            ListStops.ItemsSource = filtered;
            ListStops.DisplayMemberPath = "DisplayStopName";
        }

        private void ListStops_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListStops.SelectedItem is GtfsStop stop)
            {
                var stopTimesHere = GtfsStore.StopTimes.Where(st => st.stop_id == stop.stop_id).ToList();
                var tripIdsHere = stopTimesHere.Select(st => st.trip_id).ToHashSet();
                var routeIdsHere = GtfsStore.Trips.Where(t => t.trip_id != null && tripIdsHere.Contains(t.trip_id)).Select(t => t.route_id).ToHashSet();

                var linesAtStop = GtfsStore.Routes.Where(r => r.route_id != null && routeIdsHere.Contains(r.route_id))
                                                  .Select(r => r.route_short_name)
                                                  .Where(n => !string.IsNullOrWhiteSpace(n))
                                                  .Distinct()
                                                  .OrderBy(n => n)
                                                  .ToList();

                linesAtStop.Insert(0, "Alle Linien zeigen");
                CboFilterLine.ItemsSource = linesAtStop;
                CboFilterLine.SelectedIndex = 0;
            }

            if (DpStopDate.SelectedDate != null) BtnShowDepartures_Click(null!, null!);
        }

        private void BtnShowDepartures_Click(object sender, RoutedEventArgs e)
        {
            if (ListStops.SelectedItem is not GtfsStop stop || DpStopDate.SelectedDate == null) return;

            DateTime selectedDate = DpStopDate.SelectedDate.Value;
            bool wholeDay = ChkWholeDay.IsChecked == true;
            string inputTime = TxtStopTime.Text.PadLeft(5, '0');

            string filterLine = CboFilterLine.Text.Trim();
            if (filterLine == "Alle Linien zeigen") filterLine = "";

            string filterDest = TxtFilterDest.Text.Trim();

            var stopTimesAtStop = GtfsStore.StopTimes.Where(st => st.stop_id == stop.stop_id).ToList();
            var activeTripIds = stopTimesAtStop.Select(st => st.trip_id).ToHashSet();

            var activeTrips = GtfsStore.Trips.Where(t => t.trip_id != null && activeTripIds.Contains(t.trip_id) &&
                                                    t.service_id != null && RunsOnDate(t.service_id, selectedDate)).ToList();

            List<DepartureBoardItem> departures = [];

            foreach (var trip in activeTrips)
            {
                var st = stopTimesAtStop.First(x => x.trip_id == trip.trip_id);
                var route = GtfsStore.Routes.FirstOrDefault(r => r.route_id == trip.route_id);

                string depTime = st.departure_time ?? st.arrival_time ?? "00:00:00";
                string lineName = route?.route_short_name ?? "?";
                string destination = trip.trip_headsign ?? route?.route_long_name ?? "Unbekannt";

                if (!wholeDay && string.Compare(depTime, inputTime + ":00") < 0) continue;
                if (!string.IsNullOrEmpty(filterLine) && !lineName.Equals(filterLine, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(filterDest) && !destination.Contains(filterDest, StringComparison.OrdinalIgnoreCase)) continue;

                departures.Add(new DepartureBoardItem
                {
                    Time = FormatTime(depTime),
                    RawTime = depTime,
                    Line = lineName,
                    Destination = destination
                });
            }

            ListDepartures.ItemsSource = departures.OrderBy(d => d.RawTime).ToList();
        }
    }

    // --- HILFSKLASSEN ---

    public class TimetableViewModel
    {
        public string Title { get; set; } = "";
        public DataView? TableView { get; set; }
        public DataTable RawTable { get; set; } = new();
    }

    public class DepartureBoardItem
    {
        public string Time { get; set; } = "";
        public string RawTime { get; set; } = "";
        public string Line { get; set; } = "";
        public string Destination { get; set; } = "";
    }
}
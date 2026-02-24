using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CsvHelper;
using CsvHelper.Configuration;

namespace GTFSData
{
    public partial class MainWindow : Window
    {
        private readonly string configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "last_gtfs_path.txt");

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += async (s, e) => await CheckLastUsedGtfs();
        }

        private async Task CheckLastUsedGtfs()
        {
            if (File.Exists(configPath))
            {
                string savedPath = File.ReadAllText(configPath);
                if (Directory.Exists(savedPath)) await LoadGtfsDataAsync(savedPath);
                else { TxtGtfsStatus.Text = "Bereit zum Laden"; TxtGtfsDetails.Text = "Der letzte Pfad existiert nicht mehr."; }
            }
            else { TxtGtfsStatus.Text = "Willkommen!"; TxtGtfsDetails.Text = "Bitte lade oben rechts einen GTFS-Ordner."; }
        }

        private async void BtnLoadGtfs_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Wähle den Ordner mit den GTFS-Dateien aus";
                dialog.UseDescriptionForTitle = true;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    File.WriteAllText(configPath, dialog.SelectedPath);
                    await LoadGtfsDataAsync(dialog.SelectedPath);
                }
            }
        }

        private async Task LoadGtfsDataAsync(string path)
        {
            ProgBar.Visibility = Visibility.Visible;
            ProgBar.IsIndeterminate = true;
            TxtGtfsStatus.Text = "Lese komplettes GTFS-Universum...";

            var progressText = new Progress<string>(text => TxtGtfsDetails.Text = text);

            try
            {
                await Task.Run(() =>
                {
                    var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        HasHeaderRecord = true,
                        IgnoreBlankLines = true,
                        MissingFieldFound = null,
                        HeaderValidated = null,
                        PrepareHeaderForMatch = args => args.Header.ToLower()
                    };

                    List<T> ReadCsv<T>(string filename, string statusMessage)
                    {
                        ((IProgress<string>)progressText).Report(statusMessage);
                        string fullPath = System.IO.Path.Combine(path, filename);
                        if (!File.Exists(fullPath)) return [];
                        using (var reader = new StreamReader(fullPath))
                        using (var csv = new CsvReader(reader, config))
                        {
                            return csv.GetRecords<T>().ToList();
                        }
                    }

                    // --- DIE ULTIMATIVE GTFS LESE-ROUTINE ---
                    GtfsStore.FeedInfo = ReadCsv<GtfsFeedInfo>("feed_info.txt", "Metadaten des Datensatzes...");
                    GtfsStore.Agencies = ReadCsv<GtfsAgency>("agency.txt", "Betreibergesellschaften...");
                    GtfsStore.Stops = ReadCsv<GtfsStop>("stops.txt", "Haltestellen & Bahnhöfe...");
                    GtfsStore.Routes = ReadCsv<GtfsRoute>("routes.txt", "Linien und Farbgebungen...");
                    GtfsStore.Trips = ReadCsv<GtfsTrip>("trips.txt", "Fahrtvarianten...");
                    GtfsStore.StopTimes = ReadCsv<GtfsStopTime>("stop_times.txt", "Detaillierte Fahrpläne...");
                    GtfsStore.Calendar = ReadCsv<GtfsCalendar>("calendar.txt", "Reguläre Betriebstage...");
                    GtfsStore.CalendarDates = ReadCsv<GtfsCalendarDate>("calendar_dates.txt", "Ausnahmetage (Feiertage)...");
                    GtfsStore.FareAttributes = ReadCsv<GtfsFareAttribute>("fare_attributes.txt", "Tarifpreise...");
                    GtfsStore.FareRules = ReadCsv<GtfsFareRule>("fare_rules.txt", "Tarifzonen-Regeln...");
                    GtfsStore.Shapes = ReadCsv<GtfsShape>("shapes.txt", "Geografische Linienwege...");
                    GtfsStore.Frequencies = ReadCsv<GtfsFrequency>("frequencies.txt", "Taktverkehre & U-Bahnen...");
                    GtfsStore.Transfers = ReadCsv<GtfsTransfer>("transfers.txt", "Offizielle Umsteigeverbindungen...");
                    GtfsStore.Pathways = ReadCsv<GtfsPathway>("pathways.txt", "Fußwege in Bahnhöfen...");
                    GtfsStore.Levels = ReadCsv<GtfsLevel>("levels.txt", "Ebenen & Stockwerke...");
                    GtfsStore.Translations = ReadCsv<GtfsTranslation>("translations.txt", "Sprach-Übersetzungen...");
                    GtfsStore.Attributions = ReadCsv<GtfsAttribution>("attributions.txt", "Lizenzrechte...");
                });

                GtfsStore.IsLoaded = GtfsStore.Routes.Any();
                TxtGtfsStatus.Text = "✅ SYSTEM BEREIT (VOLLE TIEFE)";
                TxtGtfsDetails.Text = $"Ordner: {System.IO.Path.GetFileName(path)} | {GtfsStore.Stops.Count} Haltestellen geladen.";
            }
            catch (Exception ex)
            {
                TxtGtfsStatus.Text = "❌ KRITISCHER FEHLER";
                TxtGtfsDetails.Text = ex.Message;
                GtfsStore.IsLoaded = false;
            }
            finally
            {
                ProgBar.Visibility = Visibility.Collapsed;
                ProgBar.IsIndeterminate = false;
            }
        }

        // --- HIER IST DAS UPDATE: Kacheln öffnen jetzt die Module! ---
        private void TileGtfsToShp_Click(object sender, RoutedEventArgs e)
        {
            if (GtfsStore.IsLoaded) new GtfsToShpWindow().Show();
            else ShowLoadWarning();
        }

        private void TileHaltestellen_Click(object sender, RoutedEventArgs e)
        {
            if (GtfsStore.IsLoaded) new HaltestellenWindow().Show();
            else ShowLoadWarning();
        }

        private void TileAccessibility_Click(object sender, RoutedEventArgs e)
        {
            if (GtfsStore.IsLoaded) new AccessibilityWindow().Show();
            else ShowLoadWarning();
        }

        private void TileFahrplan_Click(object sender, RoutedEventArgs e)
        {
            if (GtfsStore.IsLoaded) new FahrplanWindow().Show();
            else ShowLoadWarning();
        }

        private void ShowLoadWarning()
        {
            System.Windows.MessageBox.Show("Bitte lade zuerst einen GTFS-Datensatz (oben rechts)!", "Keine Daten", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // --- DER GLOBALE SPEICHER FÜR DAS GESAMTE GTFS-UNIVERSUM ---
    public static class GtfsStore
    {
        public static List<GtfsFeedInfo> FeedInfo { get; set; } = [];
        public static List<GtfsAgency> Agencies { get; set; } = [];
        public static List<GtfsStop> Stops { get; set; } = [];
        public static List<GtfsRoute> Routes { get; set; } = [];
        public static List<GtfsTrip> Trips { get; set; } = [];
        public static List<GtfsStopTime> StopTimes { get; set; } = [];
        public static List<GtfsCalendar> Calendar { get; set; } = [];
        public static List<GtfsCalendarDate> CalendarDates { get; set; } = [];
        public static List<GtfsFareAttribute> FareAttributes { get; set; } = [];
        public static List<GtfsFareRule> FareRules { get; set; } = [];
        public static List<GtfsShape> Shapes { get; set; } = [];
        public static List<GtfsFrequency> Frequencies { get; set; } = [];
        public static List<GtfsTransfer> Transfers { get; set; } = [];
        public static List<GtfsPathway> Pathways { get; set; } = [];
        public static List<GtfsLevel> Levels { get; set; } = [];
        public static List<GtfsTranslation> Translations { get; set; } = [];
        public static List<GtfsAttribution> Attributions { get; set; } = [];
        public static bool IsLoaded { get; set; } = false;
    }

    // --- ALLE 17 OFFIZIELLEN GTFS MODELLE ---
    public class GtfsFeedInfo { public string? feed_publisher_name { get; set; } public string? feed_publisher_url { get; set; } public string? feed_lang { get; set; } public string? feed_start_date { get; set; } public string? feed_end_date { get; set; } public string? feed_version { get; set; } }
    public class GtfsAgency { public string? agency_id { get; set; } public string? agency_name { get; set; } public string? agency_url { get; set; } public string? agency_timezone { get; set; } public string? agency_lang { get; set; } public string? agency_phone { get; set; } }
    public class GtfsStop { public string? stop_id { get; set; } public string? stop_code { get; set; } public string? stop_name { get; set; } public string? stop_desc { get; set; } public double stop_lat { get; set; } public double stop_lon { get; set; } public string? zone_id { get; set; } public string? stop_url { get; set; } public string? location_type { get; set; } public string? parent_station { get; set; } public string? stop_timezone { get; set; } public string? wheelchair_boarding { get; set; } public string? level_id { get; set; } public string? platform_code { get; set; } public string DisplayStopName { get; set; } = ""; }
    public class GtfsRoute { public string? route_id { get; set; } public string? agency_id { get; set; } public string? route_short_name { get; set; } public string? route_long_name { get; set; } public string? route_desc { get; set; } public string? route_type { get; set; } public string? route_url { get; set; } public string? route_color { get; set; } public string? route_text_color { get; set; } }
    public class GtfsTrip { public string? route_id { get; set; } public string? service_id { get; set; } public string? trip_id { get; set; } public string? trip_headsign { get; set; } public string? trip_short_name { get; set; } public string? direction_id { get; set; } public string? block_id { get; set; } public string? shape_id { get; set; } public string? wheelchair_accessible { get; set; } public string? bikes_allowed { get; set; } }
    public class GtfsStopTime { public string? trip_id { get; set; } public string? arrival_time { get; set; } public string? departure_time { get; set; } public string? stop_id { get; set; } public string? stop_sequence { get; set; } public string? stop_headsign { get; set; } public string? pickup_type { get; set; } public string? drop_off_type { get; set; } public string? shape_dist_traveled { get; set; } }
    public class GtfsCalendar { public string? service_id { get; set; } public string? monday { get; set; } public string? tuesday { get; set; } public string? wednesday { get; set; } public string? thursday { get; set; } public string? friday { get; set; } public string? saturday { get; set; } public string? sunday { get; set; } public string? start_date { get; set; } public string? end_date { get; set; } }
    public class GtfsCalendarDate { public string? service_id { get; set; } public string? date { get; set; } public string? exception_type { get; set; } }
    public class GtfsFareAttribute { public string? fare_id { get; set; } public string? price { get; set; } public string? currency_type { get; set; } public string? payment_method { get; set; } public string? transfers { get; set; } public string? agency_id { get; set; } public string? transfer_duration { get; set; } }
    public class GtfsFareRule { public string? fare_id { get; set; } public string? route_id { get; set; } public string? origin_id { get; set; } public string? destination_id { get; set; } public string? contains_id { get; set; } }
    public class GtfsShape { public string? shape_id { get; set; } public double shape_pt_lat { get; set; } public double shape_pt_lon { get; set; } public int shape_pt_sequence { get; set; } public double? shape_dist_traveled { get; set; } }
    public class GtfsFrequency { public string? trip_id { get; set; } public string? start_time { get; set; } public string? end_time { get; set; } public string? headway_secs { get; set; } public string? exact_times { get; set; } }
    public class GtfsTransfer { public string? from_stop_id { get; set; } public string? to_stop_id { get; set; } public string? transfer_type { get; set; } public string? min_transfer_time { get; set; } }
    public class GtfsPathway { public string? pathway_id { get; set; } public string? from_stop_id { get; set; } public string? to_stop_id { get; set; } public string? pathway_mode { get; set; } public string? is_bidirectional { get; set; } public string? length { get; set; } public string? traversal_time { get; set; } public string? stair_count { get; set; } public string? max_slope { get; set; } public string? min_width { get; set; } public string? signposted_as { get; set; } }
    public class GtfsLevel { public string? level_id { get; set; } public string? level_index { get; set; } public string? level_name { get; set; } }
    public class GtfsTranslation { public string? table_name { get; set; } public string? field_name { get; set; } public string? language { get; set; } public string? translation { get; set; } public string? record_id { get; set; } public string? record_sub_id { get; set; } public string? field_value { get; set; } }
    public class GtfsAttribution { public string? attribution_id { get; set; } public string? agency_id { get; set; } public string? route_id { get; set; } public string? trip_id { get; set; } public string? organization_name { get; set; } public string? is_producer { get; set; } public string? is_operator { get; set; } public string? is_authority { get; set; } public string? attribution_url { get; set; } public string? attribution_email { get; set; } public string? attribution_phone { get; set; } }
}
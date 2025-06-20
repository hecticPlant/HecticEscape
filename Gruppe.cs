using Org.BouncyCastle.Asn1.Mozilla;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HecticEscape
{
    public class Gruppe
    {
        public bool Aktiv { get; set; }
        public string Name { get; set; }
        public List<AppHE> Apps { get; set; }
        public List<Website> Websites { get; set; }
        public long DailyTimeMs { get; set; }
        public List<Log> Logs { get; set; }
    }

    public class Log
    {
        public DateOnly Date { get; set; }

        public long TimeMs { get; set; }
    }

    public class AppHE
    {
        public string Name { get; set; }
        public long DailyTimeMs { get; set; }
        public List<Log> Logs { get; set; }
    }

    public class Website
    {
        public string Name { get; set; }
    }

    public class Config
    {
        public Dictionary<string, Gruppe> Gruppen { get; set; }
        public bool EnableWebsiteBlocking { get; set; }
        public bool EnableAppBlocking { get; set; }
        public bool StartTimerAtStartup { get; set; }
        public int IntervalFreeMs { get; set; }
        public int IntervalBreakMs { get; set; }
        public int IntervalCheckMs { get; set; }
        public bool EnableDebugMode { get; set; }
        public bool EnableShowTimeInOverlay { get; set; }
        public bool EnableVerboseMode { get; set; }
        public bool EnableOverlay { get; set; }
        public string ActiveLanguageNameString { get; set; }
        public bool EnableUpdateCheck { get; set; }
        public bool EnableStartOnWindowsStartup { get; set; }
        public bool EnableShowAppTimeInOverlay { get; set; }
        public bool EnableShowProcessesWithWindowOnly { get; set; }
        public bool EnableIncludeFoundGames { get; set; }
        public bool EnableGroupBlocking { get; set; }
        public bool EnabbleScanForNewApps { get; set; }

        // Pause-Timer Farben & Opacity
        public string PauseTimerForegroundColorHex { get; set; }
        public string PauseTimerBackgroundColorHex { get; set; }
        public double PauseTimerForegroundOpacity { get; set; }
        public double PauseTimerBackgroundOpacity { get; set; }

        // App-Timer Farben & Opacity
        public string AppTimerForegroundColorHex { get; set; }
        public string AppTimerBackgroundColorHex { get; set; }
        public double AppTimerForegroundOpacity { get; set; }
        public double AppTimerBackgroundOpacity { get; set; }

        // Message-Text Einstellungen
        public string MessageForegroundColorHex { get; set; }
        public string MessageBackgroundColorHex { get; set; }
        public double MessageForegroundOpacity { get; set; }
        public double MessageBackgroundOpacity { get; set; }

    }


    public class LanguageFile
    {
        public Dictionary<string, LanguageData> Sprachen { get; set; }
    }

    public class  ProcessFile
    {
        public Dictionary<string, ProcessData> Prozesse { get; set; }
    }
    public class ProcessData
    {
        public string Name { get; set; }
    }

    public class LanguageData
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        [JsonPropertyName("Content")]
        public Dictionary<string, MainWindowSection> Content { get; set; }
    }

    public class MainWindowSection
    {
        [JsonPropertyName("Timer-Tab")]
        public Dictionary<string, string> TimerTab { get; set; }

        [JsonPropertyName("WebsitesTab")]
        public Dictionary<string, string> WebsitesTab { get; set; }

        [JsonPropertyName("ProzesseTab")]
        public Dictionary<string, string> ProzesseTab { get; set; }

        [JsonPropertyName("GruppenTab")]
        public Dictionary<string, string> GruppenTab { get; set; }

        [JsonPropertyName("SteuerungTab")]
        public Dictionary<string, string> SteuerungTab { get; set; }

        [JsonPropertyName("StatusBar")]
        public Dictionary<string, string> StatusBar { get; set; }

        [JsonPropertyName("Overlay")]
        public Dictionary<string, string> Overlay { get; set; }

        [JsonPropertyName("ErrorMessages")]
        public Dictionary<string, string> ErrorMessages { get; set; }

        [JsonPropertyName("Misc")]
        public Dictionary<string, string> Misc { get; set; }
    }
}
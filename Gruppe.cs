using System.Collections.Generic;

namespace ScreenZen
{
    public class Gruppe
    {
        public bool Aktiv { get; set; }
        public string Name { get; set; }
        public List<AppSZ> Apps { get; set; }
        public List<Website> Websites { get; set; }
    }

    public class Log
    {
        public DateOnly Date { get; set; }

        public long TimeMs { get; set; }
    }

    public class AppSZ
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
        public bool EnableDebugMode { get; set; } = false;
        public bool EnableShowTimeInOverlay { get; set; } = true;
        public bool EnableVerboseMode { get; set; } = false;
        public bool EnableOverlay { get; set; } = true;
    }
}
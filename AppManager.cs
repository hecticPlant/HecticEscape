using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Automation.Peers;
using System.Windows.Forms;

namespace HecticEscape
{
    /// <summary>
    /// Verwaltet die Anwendungen (Apps)
    /// </summary>
    public class AppManager : AManager
    {
        private bool _disposed = false;
        private Process[] _runningProcesses;
        private readonly GroupManager _groupManager;

        public AppManager(ConfigReader configReader, GroupManager groupManager) 
            : base(configReader)
        {
            _groupManager = groupManager;
            Initialize();
        }

        public override void Initialize()
        {
            UpdateRunningProcesses();
            Logger.Instance.Log("AppManager initialisiert.", LogLevel.Info);
        }

        private void UpdateRunningProcesses()
        {
            _runningProcesses = Process.GetProcesses();
        }

        public Process[] GetRunningProcesses()
        {
            UpdateRunningProcesses();
            return _runningProcesses;
        }

        public void AddAppToGroup(Gruppe group, string processName)
        {
            if (group == null || string.IsNullOrWhiteSpace(processName))
                return;

            string cleanedName = CleanProcessName(processName);
            var app = new AppHE
            {
                Name = cleanedName,
                DailyTimeMs = 7200000, // Standard: 2 Stunden
                Logs = new List<Log>()
            };

            if (!group.Apps.Any(a => a.Name.Equals(app.Name, StringComparison.OrdinalIgnoreCase)))
            {
                group.Apps.Add(app);
                _configReader.SaveConfig();
            }
        }

        public bool RemoveAppFromGroup(Gruppe group, AppHE app)
        {
            if (group == null || app == null)
                return false;

            var existingApp = group.Apps.FirstOrDefault(a => a == app);
            if (existingApp != null)
            {
                group.Apps.Remove(existingApp);
                _configReader.SaveConfig();
                return true;
            }
            return false;
        }

        public List<AppHE> GetAllApps()
        {
            return _groupManager.GetAllGroups()
                .SelectMany(g => g.Apps)
                .ToList();
        }

        public List<AppHE> GetAppsFromGroup(Gruppe group)
        {
            return group?.Apps ?? new List<AppHE>();
        }

        public AppHE? GetAppByName(Gruppe group, string appName)
        {
            return group?.Apps.FirstOrDefault(a => a.Name.Equals(appName, StringComparison.OrdinalIgnoreCase));
        }

        public void SetDailyTimeMs(Gruppe group, AppHE app, long dailyTimeMs)
        {
            if (group == null || app == null) return;
            var existingApp = group.Apps.FirstOrDefault(a => a == app);
            if (existingApp != null)
            {
                existingApp.DailyTimeMs = dailyTimeMs;
                _configReader.SaveConfig();
            }
        }

        public long GetDailyTimeLeft(Gruppe group, AppHE app, DateOnly date)
        {
            if (group == null || app == null) return 0;
            var log = app.Logs?.FirstOrDefault(l => l.Date == date);
            long used = log?.TimeMs ?? 0;
            return app.DailyTimeMs - used;
        }

        public void AddTimeToLog(AppHE app, DateOnly date, long timeMs)
        {
            if (app == null || date == default || timeMs < 0)
            {
                Logger.Instance.Log("Ungültige Parameter für AddTimeToLog. App, Datum oder Zeit sind ungültig.", LogLevel.Error);
                return;
            }
            var log = app.Logs.FirstOrDefault(l => l.Date == date);
            if (log != null)
            {
                log.TimeMs += timeMs;
                _configReader.SaveConfig();
            }
        }

        public void RemoveAllAppsFromGroup(Gruppe group)
        {
            if (group == null) return;
            group.Apps.Clear();
            _configReader.SaveConfig();
        }

        private string CleanProcessName(string processName)
        {
            string pattern = @"\s?\(ID: \d+\)$";
            string cleanedName = Regex.Replace(processName, pattern, string.Empty).Trim();
            Logger.Instance.Log($"'{processName}' wurde zu '{cleanedName}' bereinigt", LogLevel.Verbose);
            return cleanedName;
        }

        public void HandleAppBlocking(int intervalCheckMs, bool isBreakActive)
        {
            var activeGroups = _groupManager.GetAllActiveGroups()
                .Where(g => !string.IsNullOrEmpty(g.Name))
                .ToList();

            Logger.Instance.Log($"Starte App-Blocking Handler {intervalCheckMs} {isBreakActive} mit {activeGroups.Count} aktiven Gruppen", LogLevel.Verbose);

            if (!_configReader.GetAppBlockingEnabled())
            {
                Logger.Instance.Log("App-Blocking ist deaktiviert. Es werden keine Apps beendet.", LogLevel.Verbose);
                return;
            }

            DateOnly today = DateOnly.FromDateTime(DateTime.Now);
            foreach (var group in activeGroups)
            {
                if (!group.Aktiv)
                {
                    Logger.Instance.Log($"Gruppe {group.Name} ist nicht aktiv. Überspringe App-Blockierung.", LogLevel.Verbose);
                    continue;
                }
                Logger.Instance.Log($"Verarbeite Gruppe: {group.Name}, aktiv: {group.Aktiv}", LogLevel.Verbose);
                foreach (var app in group.Apps)
                {
                    if (app.Logs == null)
                    {
                        Logger.Instance.Log($"Erstelle neues Log für {app.Name} in Gruppe {group.Name}, da Logs null sind.", LogLevel.Error);
                        app.Logs = new List<Log>();
                    }
                    var log = app.Logs.FirstOrDefault(l => l.Date == today);
                    if (log == null)
                    {
                        log = new Log { Date = today, TimeMs = 0 };
                        app.Logs.Add(log);
                        Logger.Instance.Log($"Neues Log für {app.Name} am {today:yyyy-MM-dd} erstellt.", LogLevel.Debug);
                    }
                    if (isBreakActive)
                    {
                        var processesForPause = Process.GetProcessesByName(app.Name);
                        if (processesForPause.Length > 0)
                        {
                            Logger.Instance.Log($"Beende App {app.Name} aus Gruppe {group.Name} wegen Pause.", LogLevel.Debug);
                            TerminateProcesses(app.Name, processesForPause, group.Name, "Pausenmodus");
                        }
                        else
                        {
                            Logger.Instance.Log($"Keine laufenden Prozesse für {app.Name} gefunden (Pausenmodus).", LogLevel.Verbose);
                        }
                        continue;
                    }
                    var runningAppProcesses = Process.GetProcessesByName(app.Name);
                    if (runningAppProcesses.Length == 0)
                    {
                        Logger.Instance.Log($"Keine laufenden Prozesse für {app.Name} gefunden.", LogLevel.Verbose);
                        continue;
                    }
                    Logger.Instance.Log($"Gefundene Prozesse für {app.Name}: {runningAppProcesses.Length}", LogLevel.Verbose);
                    AddTimeToLog(app, today, intervalCheckMs);
                    Logger.Instance.Log($"Zeit zum Log für {app.Name} am {today:yyyy-MM-dd} hinzugefügt: {intervalCheckMs}ms (bisher: {log.TimeMs}ms)", LogLevel.Verbose);
                    if (log.TimeMs >= app.DailyTimeMs)
                    {
                        Logger.Instance.Log($"Beende App {app.Name}, da tägliche Zeit erreicht ist: {log.TimeMs}ms >= {app.DailyTimeMs}ms", LogLevel.Warn);
                        TerminateProcesses(app.Name, runningAppProcesses, group.Name, "Tageslimit überschritten");
                    }
                }
            }
            Logger.Instance.Log("App-Blocking Handler abgeschlossen.", LogLevel.Verbose);
        }

        private void TerminateProcesses(string appName, Process[] processes, string groupName, string reason)
        {
            foreach (var process in processes)
            {
                try
                {
                    process.Kill();
                    Logger.Instance.Log($"Prozess {appName} (PID {process.Id}) in Gruppe {groupName} beendet: {reason}.", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Fehler beim Beenden von {appName} (PID {process.Id}) in Gruppe {groupName} ({reason}): {ex.Message}", LogLevel.Error);
                }
            }
        }

        private List<string> GetAllActiveApps()
        {
            List<string> appList = new List<string>();
            foreach (var group in _groupManager.GetAllActiveGroups())
            {
                foreach (var app in group.Apps)
                {
                    appList.Add(app.Name);

                    Logger.Instance.Log($"Verarbeite Gruppe: {group.Name}, aktiv: {group.Aktiv}", LogLevel.Verbose);
                }
            }
            return appList;
        }
        
        public void ListAllActiveApps()
        {
            var activeApps = GetAllActiveApps();
            if (activeApps.Count == 0)
            {
                Logger.Instance.Log("Keine aktiven Apps gefunden.", LogLevel.Debug);
                return;
            }
            string message = $"Aktive Apps ({activeApps.Count}):";
            foreach (var app in activeApps)
            {
                message += $"\n- {app}";
            }
            Logger.Instance.Log($"{message}", LogLevel.Debug);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_runningProcesses != null)
                    {
                        foreach (var process in _runningProcesses)
                        {
                            process.Dispose();
                        }
                        _runningProcesses = null;
                    }
                    Logger.Instance.Log("AppManager wurde disposed.", LogLevel.Info);
                }
                base.Dispose(disposing);
            }
        }
    }
}
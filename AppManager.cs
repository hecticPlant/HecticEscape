using System.Diagnostics;
using System.Runtime.CompilerServices;
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
            Logger.Instance.Log("Aktualisiere laufende Prozesse", LogLevel.Verbose);
            if (_configReader.GetEnableShowProcessesWithWindowOnly())
            {
                Logger.Instance.Log("Zeige nur Prozesse mit Fenster.", LogLevel.Verbose);
                _runningProcesses = Process.GetProcesses().Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(p.MainWindowTitle)).ToArray();
            }
            else
            {
                Logger.Instance.Log("Zeige alle laufenden Prozesse.", LogLevel.Verbose);
                _runningProcesses = Process.GetProcesses();
            }
        }

        public Process[] GetRunningProcesses()
        {
            Logger.Instance.Log("Hole laufende Prozesse", LogLevel.Verbose);
            UpdateRunningProcesses();
            return _runningProcesses;
        }

        public void AddAppToGroup(Gruppe group, string processName)
        {
            Logger.Instance.Log($"Versuche, App '{processName}' zur Gruppe '{group?.Name}' hinzuzufügen.", LogLevel.Verbose);
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
            }
            _configReader.SetSaveConfigFlag();
        }

        public bool RemoveAppFromGroup(Gruppe group, AppHE app)
        {
            Logger.Instance.Log($"Versuche, App '{app?.Name}' aus Gruppe '{group?.Name}' zu entfernen.", LogLevel.Verbose);
            if (group == null || app == null)
                return false;

            var existingApp = group.Apps.FirstOrDefault(a => a == app);
            if (existingApp != null)
            {
                group.Apps.Remove(existingApp);
                _configReader.SetSaveConfigFlag();
                return true;
            }
            return false;
        }

        public List<AppHE> GetAllApps()
        {
            Logger.Instance.Log("Hole alle Apps aus allen Gruppen.", LogLevel.Verbose);
            return _groupManager.GetAllGroups()
                .SelectMany(g => g.Apps)
                .ToList();
        }

        public List<AppHE> GetAppsFromGroup(Gruppe group)
        {
            Logger.Instance.Log($"Hole Apps aus Gruppe '{group?.Name}'.", LogLevel.Verbose);
            return group?.Apps ?? new List<AppHE>();
        }

        public AppHE? GetAppByName(Gruppe group, string appName)
        {
            Logger.Instance.Log($"Hole App '{appName}' aus Gruppe '{group?.Name}'.", LogLevel.Verbose);
            return group?.Apps.FirstOrDefault(a => a.Name.Equals(appName, StringComparison.OrdinalIgnoreCase));
        }

        public void SetDailyTimeMs(Gruppe group, AppHE app, long dailyTimeMs)
        {
            if (group == null || app == null) return;
            var existingApp = group.Apps.FirstOrDefault(a => a == app);
            if (existingApp != null)
            {
                existingApp.DailyTimeMs = dailyTimeMs;
                Logger.Instance.Log($"DailyTime von {existingApp.Name} aus Gruppe {group.Name} auf {dailyTimeMs} gesetzts", LogLevel.Verbose);
            }
            _configReader.SetSaveConfigFlag();
        }

        public void SetTimeMS(Gruppe group, AppHE app, long timeMs)
        {
            if (group == null || app == null) return;
            var existingApp = group.Apps.FirstOrDefault(a => a == app);
            DateOnly date = DateOnly.FromDateTime(DateTime.Now);
            if (existingApp != null)
            {
                Log? log = existingApp.Logs.FirstOrDefault(l => l.Date == date);
                if(log == null)
                {
                    log = new Log { Date = date, TimeMs = 0 };
                    existingApp.Logs.Add(log);
                    Logger.Instance.Log($"Neues Log für {existingApp.Name} am {date:yyyy-MM-dd} erstellt.", LogLevel.Debug);
                }
                else
                {
                    log.TimeMs = timeMs;
                    Logger.Instance.Log($"DailyTime von {existingApp.Name} aus Gruppe {group.Name} auf {timeMs} gesetzts", LogLevel.Verbose);
                }
            }
            _configReader.SetSaveConfigFlag(); 
        }

        public long GetDailyTimeLeft(Gruppe group, AppHE app, DateOnly date)
        {
            Logger.Instance.Log($"Berechne verbleibende Zeit für {app?.Name} in Gruppe {group?.Name} am {date:yyyy-MM-dd}.", LogLevel.Verbose);
            if (group == null || app == null) return 0;
            var log = app.Logs?.FirstOrDefault(l => l.Date == date);
            long used = log?.TimeMs ?? 0;
            return app.DailyTimeMs - used;
        }
        public void AddTimeToLog(AppHE app, DateOnly date, long timeMs)
        {
            Logger.Instance.Log($"Füge {timeMs}ms zu Log von {app?.Name} am {date:yyyy-MM-dd} hinzu.", LogLevel.Verbose);
            if (app == null || date == default || timeMs < 0)
            {
                Logger.Instance.Log("Ungültige Parameter für AddTimeToLog. App, Datum oder Zeit sind ungültig.", LogLevel.Error);
                return;
            }
            var log = app.Logs.FirstOrDefault(l => l.Date == date);
            if (log != null)
            {
                log.TimeMs += timeMs;
            }
        }

        public void RemoveAllAppsFromGroup(Gruppe group)
        {
            Logger.Instance.Log($"Entferne alle Apps aus Gruppe '{group?.Name}'.", LogLevel.Verbose);
            if (group == null) return;
            group.Apps.Clear();
            _configReader.SetSaveConfigFlag();
        }

        private string CleanProcessName(string processName)
        {
            string pattern = @"\s?\(ID: \d+\)$";
            string cleanedName = Regex.Replace(processName, pattern, string.Empty).Trim();
            Logger.Instance.Log($"'{processName}' wurde zu '{cleanedName}' bereinigt", LogLevel.Verbose);
            return cleanedName;
        }

        public async Task HandleAppBlocking(int intervalCheckMs, bool isBreakActive)
        {
            var activeGroups = _groupManager.GetAllActiveGroups().Where(g => !string.IsNullOrEmpty(g.Name)).ToList();

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
                        Logger.Instance.Log($"Neues Log für {app.Name} am {today:yyyy-MM-dd} erstellt.", LogLevel.Info);
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
            Logger.Instance.Log($"{message}", LogLevel.Verbose);
        }

        public List<(Gruppe, AppHE)> WarnIfDailyTimeIsLow()
        {
            Logger.Instance.Log("Überprüfe, ob tägliche Zeit für Apps niedrig ist.", LogLevel.Verbose);
            DateOnly date = DateOnly.FromDateTime(DateTime.Now);
            List<Gruppe> activeGroups = _groupManager.GetAllActiveGroups().Where(g => g.Aktiv && g.Apps.Any(a => a.DailyTimeMs > 0)).ToList();
            List<(Gruppe, AppHE)> lowTimeGroups = new List<(Gruppe, AppHE)>();
            if (activeGroups.Count == 0)
            {
                Logger.Instance.Log("Keine aktiven Gruppen mit Apps gefunden.", LogLevel.Debug);
                return new List<(Gruppe, AppHE)>();
            }
            else
            {
                foreach (var group in activeGroups)
                {
                    foreach (var app in group.Apps)
                    {
                        var processesForPause = Process.GetProcessesByName(app.Name);
                        if (processesForPause.Length > 0)
                        {
                            Logger.Instance.Log($"App {app.Name} zu lowTimeGroup hinzugefügt.", LogLevel.Verbose);
                            lowTimeGroups.Add((group, app));
                        }

                    }
                }
            }
            return lowTimeGroups;
        }

        public void SaveConfig()
        {
            Logger.Instance.Log("Speichere Konfiguration.", LogLevel.Verbose);
            _configReader.SaveConfig();
        }

        public void Dispose()
        {
            Logger.Instance.Log("AppManager wird disposed.", LogLevel.Info);
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing)
        {
            Logger.Instance.Log("Dispose-Methode des AppManagers aufgerufen.", LogLevel.Verbose);
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
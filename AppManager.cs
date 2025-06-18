using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
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
        public readonly GameManager GameManager;

        public AppManager(ConfigReader configReader, GroupManager groupManager, GameManager gameManager)
            : base(configReader)
        {
            _groupManager = groupManager;
            GameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
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

        private List<SteamGame> GetInstalledGames()
        {
            Logger.Instance.Log("Hole installierte Spiele.", LogLevel.Verbose);
            return GameManager.GetInstalledGames();
        }

        public void AddFoundGamesToConfig(Gruppe group)
        {
            if (group == null)
            {
                Logger.Instance.Log("Gruppe ist null. Kann keine Spiele hinzufügen.", LogLevel.Error);
                return;
            }
            List<SteamGame> foundGames = GetInstalledGames();
            Logger.Instance.Log($"Füge {foundGames.Count} gefundene Spiele zur Konfiguration hinzu.", LogLevel.Verbose);
            if (foundGames == null || foundGames.Count == 0)
            {
                Logger.Instance.Log("Keine Spiele zum Hinzufügen gefunden.", LogLevel.Warn);
                return;
            }
            foreach(var game in foundGames)
            {
                Logger.Instance.Log($"Füge Spiel '{game.Name}' in Gruppe {group.Name}' hinzu.", LogLevel.Verbose);
                AddAppToGroup(group, game.Name);
            }
            _configReader.SetSaveConfigFlag();
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
            if (log == null)
            {
                log = new Log { Date = date, TimeMs = 0 };
                app.Logs.Add(log);
                Logger.Instance.Log($"Neues Log für {app.Name} am {date:yyyy-MM-dd} erstellt.", LogLevel.Info);
            }
            log.TimeMs += timeMs;
            _configReader.SetSaveConfigFlag();
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

        public async Task BlockHandler(int intervalCheckMs, bool isBreakActive)
        {
            var activeGroups = _groupManager.GetAllActiveGroups();
            Logger.Instance.Log($"Starte App-Blocking Handler {intervalCheckMs} {isBreakActive} mit {activeGroups.Count} aktiven Gruppen", LogLevel.Verbose);

            if (!_configReader.GetAppBlockingEnabled())
            {
                Logger.Instance.Log("App-Blocking ist deaktiviert. Es werden keine Apps beendet.", LogLevel.Verbose);
                return;
            }
            DateOnly today = DateOnly.FromDateTime(DateTime.Now);
            foreach (var group in activeGroups)
            {
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
                        var processesForPause = IsAppRunning(app.Name);
                        if (processesForPause != null && processesForPause.Length > 0)
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
                    var runningAppProcesses = IsAppRunning(app.Name);
                    if (runningAppProcesses != null && runningAppProcesses.Length > 0)
                    {
                        AddTimeToLog(app, today, intervalCheckMs);
                        _groupManager.AddTimeToLog(group, today, intervalCheckMs);
                        Logger.Instance.Log($"Zeit zum Log für {app.Name} am {today:yyyy-MM-dd} hinzugefügt: {intervalCheckMs}ms (bisher: {log.TimeMs}ms)", LogLevel.Debug);
                        Logger.Instance.Log($"Zeit zum Log für Gruppe {group.Name} am {today:yyyy-MM-dd} hinzugefügt: {intervalCheckMs}ms (bisher: {group.Logs.FirstOrDefault(l => l.Date == today)?.TimeMs}ms)", LogLevel.Debug);
                        
                        if (log.TimeMs >= app.DailyTimeMs)
                        {
                            Logger.Instance.Log($"Beende App {app.Name}, da tägliche Zeit erreicht ist: {log.TimeMs}ms >= {app.DailyTimeMs}ms", LogLevel.Warn);
                            if (runningAppProcesses != null)
                                TerminateProcesses(app.Name, runningAppProcesses, group.Name, "Tageslimit überschritten");
                        }
                    }
                }
            }

            if (EnableGroupBlocking)
            {
                foreach (var group in activeGroups)
                {
                    var log = group.Logs.FirstOrDefault(l => l.Date == today);
                    if (log == null)
                    {
                        log = new Log { Date = today, TimeMs = 0 };
                        group.Logs.Add(log);
                        Logger.Instance.Log($"Neues Log für {group.Name} am {today:yyyy-MM-dd} erstellt.", LogLevel.Info);
                    }
                    if (log.TimeMs >= group.DailyTimeMs)
                    {
                        Logger.Instance.Log($"Tageslimit von Gruppe {group.Name} überschritten: {group.DailyTimeMs}", LogLevel.Verbose);
                        foreach (var app in group.Apps)
                        {
                            TerminateProcesses(app.Name, Process.GetProcessesByName(app.Name), group.Name, "Tageslimit Gruppe überschritten");
                        }
                    }
                }
            }
            Logger.Instance.Log("App-Blocking Handler abgeschlossen.", LogLevel.Verbose);
        }

        private Process[] IsAppRunning(string appName)
        {
            Process[] runningAppProcesses = Process.GetProcessesByName(appName);

            if (runningAppProcesses.Length == 0)
            {
                Logger.Instance.Log($"Keine laufenden Prozesse für {appName} gefunden. {runningAppProcesses.Length}", LogLevel.Debug);
                return Array.Empty<Process>();
            }

            return runningAppProcesses;
        }

        public TimeSpan GetLowestTimeRemaining() 
        {
            List<(Gruppe group, AppHE app)> lowTimeApps = GetAppsWithLowDailyTimeLeft();
            Logger.Instance.Log($"LowTimeApps: {lowTimeApps?.Count ?? 0}", LogLevel.Verbose);
            List<Gruppe> lowTimeGroups = _groupManager.GetGroupsWithLowDailyTimeLeft();
            Logger.Instance.Log($"LowTimeGroups: {lowTimeGroups?.Count ?? 0}", LogLevel.Verbose);
            TimeSpan lowestTimeSpan = TimeSpan.MaxValue;
            DateOnly date = DateOnly.FromDateTime(DateTime.Now);

            if (EnableGroupBlocking)
            {
                if (lowTimeGroups != null && lowTimeGroups.Count > 0)
                {
                    Gruppe? lowestGroup = null;
                    long minGroupRemainingMs = long.MaxValue;

                    foreach (var group in lowTimeGroups)
                    {
                        if (group == null)
                            continue;

                        long remainingMs;
                        try
                        {
                            remainingMs = _groupManager.GetDailyTimeLeft(group, date);
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.Log($"Fehler beim Abrufen der verbleibenden Tageszeit für Gruppe '{group.Name}': {ex}", LogLevel.Error);
                            continue;
                        }
                        if (remainingMs < minGroupRemainingMs)
                        {
                            minGroupRemainingMs = remainingMs;
                            lowestGroup = group;
                        }
                    }

                    if (lowestGroup != null)
                    {
                        bool hasGroupRunningAppFlag = false;
                        foreach (var app in lowestGroup.Apps)
                        {
                           if(IsAppRunning(app.Name).Length > 0)
                                hasGroupRunningAppFlag = true;
                        }
                        if (hasGroupRunningAppFlag)
                        {
                            TimeSpan timeSpan;
                            try
                            {
                                timeSpan = TimeSpan.FromMilliseconds(_groupManager.GetDailyTimeLeft(lowestGroup, date));
                            }
                            catch (Exception ex)
                            {
                                Logger.Instance.Log($"Fehler beim Konvertieren der verbleibenden Zeit in TimeSpan für Gruppe '{lowestGroup.Name}': {ex}", LogLevel.Error);
                                return lowestTimeSpan;
                            }
                            lowestTimeSpan = timeSpan;
                        }
                    }
                }
            }

            if (lowTimeApps != null && lowTimeApps.Count > 0)
            {
                AppHE? lowestApp = null;
                Gruppe? groupOfLowestApp = null;
                long minAppRemainingMs = long.MaxValue;

                foreach (var (group, app) in lowTimeApps)
                {
                    if (group == null || app == null)
                        continue;

                    long remainingMs;
                    try
                    {
                        remainingMs = GetDailyTimeLeft(group, app, date);
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Log($"Fehler beim Abrufen der verbleibenden Tageszeit für App '{app.Name}' in Gruppe '{group.Name}': {ex}", LogLevel.Error);
                        continue;
                    }

                    if (remainingMs < minAppRemainingMs)
                    {
                        minAppRemainingMs = remainingMs;
                        lowestApp = app;
                        groupOfLowestApp = group;
                    }
                }

                if (lowestApp != null)
                {
                    TimeSpan timeSpan;
                    try
                    {
                        timeSpan = TimeSpan.FromMilliseconds(GetDailyTimeLeft(groupOfLowestApp!, lowestApp, date));
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Log($"Fehler beim Konvertieren der verbleibenden Zeit in TimeSpan für App '{lowestApp.Name}': {ex}", LogLevel.Error);
                        return lowestTimeSpan;
                    }
                    if (timeSpan < lowestTimeSpan)
                        lowestTimeSpan = timeSpan;
                }
            }
            return lowestTimeSpan; 
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

        public List<(Gruppe, AppHE)> GetAppsWithLowDailyTimeLeft()
        {
            Logger.Instance.Log("Überprüfe, ob tägliche Zeit für Apps niedrig ist.", LogLevel.Verbose);
            DateOnly date = DateOnly.FromDateTime(DateTime.Now);
            List<Gruppe> activeGroups = _groupManager.GetAllActiveGroups().ToList();
            List<(Gruppe, AppHE)> lowTimeGroups = new List<(Gruppe, AppHE)>();
            if (activeGroups.Count == 0)
            {
                Logger.Instance.Log("Keine aktiven Gruppen mit Apps gefunden.", LogLevel.Verbose);
                return new List<(Gruppe, AppHE)>();
            }
            else
            {
                foreach (var group in activeGroups)
                {
                    foreach (var app in group.Apps)
                    {
                        var processesForPause = IsAppRunning(app.Name);
                        if (processesForPause != null && processesForPause.Length > 0)
                        {
                            if (GetDailyTimeLeft(group, app, date) < 65 * 60 * 1000)
                            {
                                Logger.Instance.Log($"App {app.Name} zu lowTimeGroup hinzugefügt.", LogLevel.Verbose);
                                lowTimeGroups.Add((group, app));
                            }
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
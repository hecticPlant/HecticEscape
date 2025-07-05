using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Automation.Peers;
using System.Windows.Forms;
using System.IO;
using System.Windows.Threading;
using System.Windows;

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
            try
            {
                if (_configReader != null)
                {
                    _configReader.NewProcessFileCreated -= OnNewProcessFileCreated;
                    _configReader.NewProcessFileCreated += OnNewProcessFileCreated;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Initialisieren des AppManagers: {ex.Message}", LogLevel.Error);
                throw new InvalidOperationException("AppManager konnte nicht initialisiert werden.", ex);
            }
            Initialize();
        }


        public override void Initialize()
        {
            UpdateRunningProcesses();
            Logger.Instance.Log("AppManager initialisiert.", LogLevel.Info);
        }

        private void OnNewProcessFileCreated()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Logger.Instance.Log("Neue Prozessdatei erstellt. Fülle Prozessliste", LogLevel.Verbose);
                for (int i = 0; i < 20; i++)
                {
                    ScanForNewProcesses();
                }
            });
        }

        private void UpdateRunningProcesses()
        {
            if (_configReader.GetEnableShowProcessesWithWindowOnly())
            {
                _runningProcesses = Process.GetProcesses().Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(p.MainWindowTitle)).ToArray();
            }
            else
            {
                _runningProcesses = Process.GetProcesses();
            }
        }

        private List<SteamGame> GetInstalledGames()
        {
            return GameManager.GetInstalledGames();
        }

        public string ScanForNewProcesses()
        {
            Process[] runningProcesses = Process.GetProcesses().Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(p.MainWindowTitle)).ToArray();
            if (runningProcesses == null || runningProcesses.Length == 0)
            {
                Logger.Instance.Log("Keine laufenden Prozesse gefunden.", LogLevel.Warn);
                return String.Empty;
            }
            foreach (var process in runningProcesses)
            {
                if (process == null || string.IsNullOrWhiteSpace(process.ProcessName))
                {
                    Logger.Instance.Log("Ungültiger Prozess gefunden. Überspringe.", LogLevel.Warn);
                    continue;
                }
                string cleanedName = CleanProcessName(process.ProcessName);
                if (!GetAllActiveApps().Contains(cleanedName, StringComparer.OrdinalIgnoreCase))
                {
                    if (!IsProcessInProcessFile(cleanedName))
                    {
                        Logger.Instance.Log($"Neuer Prozess gefunden: {cleanedName}. Füge zur Konfiguration hinzu.", LogLevel.Verbose);
                        AddProcessToFile(cleanedName);
                        _configReader.SetSaveProcessListFlag();
                        return cleanedName;
                    }
                }
            }
            return String.Empty;
        }

        private bool IsProcessInProcessFile(string processName)
        {
            var allProcesses = GetAllProcessesFromFile();
            return allProcesses.Any(p => p.Name.Equals(processName, StringComparison.OrdinalIgnoreCase));
        }

        private List<ProcessData> GetAllProcessesFromFile()
        {
           return _configReader.ProcessFile.Prozesse.Values.Where(p => !string.IsNullOrWhiteSpace(p.Name)).ToList();
        }

        private void AddProcessToFile(string processName)
        {
            Logger.Instance.Log($"Füge Prozess '{processName}' zur Prozessdatei hinzu.", LogLevel.Verbose);
            if (string.IsNullOrWhiteSpace(processName)) return;
            var processData = new ProcessData
            {
                Name = CleanProcessName(processName),
            };
            _configReader.ProcessFile.Prozesse[processData.Name] = processData;
            _configReader.SetSaveConfigFlag();
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
            UpdateRunningProcesses();
            return _runningProcesses;
        }

        public void AddAppToGroup(Gruppe group, string processName)
        {
            Logger.Instance.Log($"App '{processName}' wird zur Gruppe '{group?.Name}' hinzuzufügt.", LogLevel.Info);
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
                }
            }
            _configReader.SetSaveConfigFlag(); 
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
            return cleanedName;
        }

        public async Task BlockHandler(int intervalCheckMs, bool isBreakActive)
        {
            var activeGroups = _groupManager.GetAllActiveGroups();

            if (!_configReader.GetAppBlockingEnabled())
            {
                return;
            }
            DateOnly today = DateOnly.FromDateTime(DateTime.Now);
            foreach (var group in activeGroups)
            {
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
                        continue;
                    }
                    var runningAppProcesses = IsAppRunning(app.Name);
                    if (runningAppProcesses != null && runningAppProcesses.Length > 0)
                    {
                        AddTimeToLog(app, today, intervalCheckMs);
                        _groupManager.AddTimeToLog(group, today, intervalCheckMs);
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
                        foreach (var app in group.Apps)
                        {
                            TerminateProcesses(app.Name, Process.GetProcessesByName(app.Name), group.Name, "Tageslimit Gruppe überschritten");
                        }
                    }
                }
            }
        }

        private Process[] IsAppRunning(string appName)
        {
            Process[] runningAppProcesses = Process.GetProcessesByName(appName);

            if (runningAppProcesses.Length == 0)
            {
                return Array.Empty<Process>();
            }

            return runningAppProcesses;
        }

        public TimeSpan GetLowestTimeRemaining()
        {
            List<(Gruppe group, AppHE app)> lowTimeApps = GetAppsWithLowDailyTimeLeft();
            List<Gruppe> lowTimeGroups = _groupManager.GetGroupsWithLowDailyTimeLeft();
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
        }

        public List<(Gruppe, AppHE)> GetAppsWithLowDailyTimeLeft()
        {
            DateOnly date = DateOnly.FromDateTime(DateTime.Now);
            List<Gruppe> activeGroups = _groupManager.GetAllActiveGroups().ToList();
            List<(Gruppe, AppHE)> lowTimeGroups = new List<(Gruppe, AppHE)>();
            if (activeGroups.Count == 0)
            {
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
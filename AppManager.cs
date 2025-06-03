using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Automation.Peers;
using System.Windows.Forms;

namespace HecticEscape
{
    /// <summary>
    /// Verwaltet die Apps
    /// </summary>
    public class AppManager : IDisposable
    {
        private bool disposed = false;
        private Process[] runningProcesses;
        /// <summary>
        /// Liste mit allen Apps
        /// </summary>
        private ConfigReader configReader;

        public AppManager(ConfigReader configReader)
        {
            this.configReader = configReader;
            UpdateRunningProcesses();
            Logger.Instance.Log("Initialisiert", LogLevel.Info);
        }

        /// <summary>
        /// Aktualisiert die laufende Prozesse
        /// </summary>
        private void UpdateRunningProcesses()
        {
            runningProcesses = Process.GetProcesses();
        }

        /// <summary>
        /// Aktualisiert die Liste der laufenden Prozesse und gibt sie zurück
        /// </summary>
        /// <returns>Process[] ded laufende Prozesse</returns>
        public Process[] GetRunningProcesses()
        {
            UpdateRunningProcesses();
            // Nur Prozesse mit sichtbarem Hauptfenster (typische Apps)
            return runningProcesses
                /*.Where(p =>
                {
                    try
                    {
                        return p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(p.MainWindowTitle);
                    }
                    catch
                    {
                        // Zugriff verweigert oder Prozess bereits beendet
                        return false;
                    }
                })
                */.ToArray();
        }

        /// <summary>
        /// Speichert einen Prozess
        /// </summary>
        /// <param name="selectedGroup">Gruppen Name</param>
        /// <param name="processName">Prozess Name</param>
        public void SaveSelectedProcessesToFile(Gruppe selectedGroup, string processName)
        {
            string newProcessName = CleanProcessName(processName);
            AppHE app = new AppHE
            {
                Name = newProcessName,
                DailyTimeMs = 7200000, // Standardmäßig 2 Stunden (7200000 ms)
                Logs = new List<Log>()
            }; 
            configReader.AddAppToGroup(selectedGroup, app);
        }

        /// <summary>
        /// Bereinigt den Namen des Prozesses
        /// </summary>
        /// <param name="processName"></param>
        /// <returns></returns>
        private string CleanProcessName(string processName)
        {
            // Entfernt alles nach dem ersten Auftreten von " (ID: <Zahl>)"
            string pattern = @"\s?\(ID: \d+\)$";
            string newProcessName = Regex.Replace(processName, pattern, string.Empty).Trim();
            Logger.Instance.Log($"'{processName}' wurde zu '{newProcessName}' bereinigt", LogLevel.Verbose);
            return newProcessName;
        }

        /// <summary>
        /// Blockiert Apps basierend auf den aktiven Gruppen und dem Pausenstatus.
        /// </summary>
        /// <param name="intervalCheckMs"></param>
        /// <param name="breakActive"></param>
        public void BlockHandler(int intervalCheckMs, bool breakActive)
        {
            List<Gruppe> activeGroupList = configReader.GetAllActiveGroups();
            Logger.Instance.Log($"Starte App-Blocking Handler {intervalCheckMs} {breakActive} mit {activeGroupList.Count} aktiven Gruppen", LogLevel.Verbose);

            if (!configReader.GetAppBlockingEnabled())
            {
                Logger.Instance.Log("App-Blocking ist deaktiviert. Es werden keine Apps beendet.", LogLevel.Verbose);
                return;
            }
            DateOnly today = DateOnly.FromDateTime(DateTime.Now);
            foreach (var group in configReader.GetAllActiveGroups())
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
                    if (breakActive)
                    {
                        var processesForPause = Process.GetProcessesByName(app.Name);
                        if (processesForPause.Length > 0)
                        {
                            Logger.Instance.Log($"Beende App {app.Name} aus Gruppe {group.Name} wegen Pause.", LogLevel.Debug);

                            TerminateProcesses(appName: app.Name, processes: processesForPause, groupName: group.Name, reason: "Pausenmodus");
                        }
                        else
                        {
                            Logger.Instance.Log($"Keine laufenden Prozesse für {app.Name} gefunden (Pausenmodus).", LogLevel.Verbose);
                        }
                        continue;
                    }
                    var runningProcesses = Process.GetProcessesByName(app.Name);
                    if (runningProcesses.Length == 0)
                    {
                        Logger.Instance.Log($"Keine laufenden Prozesse für {app.Name} gefunden.", LogLevel.Verbose);
                        continue;
                    }
                    Logger.Instance.Log($"Gefundene Prozesse für {app.Name}: {runningProcesses.Length}", LogLevel.Verbose);
                    AddTimeToLog(app, today, intervalCheckMs);
                    Logger.Instance.Log($"Zeit zum Log für {app.Name} am {today:yyyy-MM-dd} hinzugefügt: {intervalCheckMs}ms (bisher: {log.TimeMs}ms)", LogLevel.Verbose);
                    if (log.TimeMs >= app.DailyTimeMs)
                    {
                        Logger.Instance.Log($"Beende App {app.Name}, da tägliche Zeit erreicht ist: {log.TimeMs}ms >= {app.DailyTimeMs}ms", LogLevel.Warn);
                        TerminateProcesses(appName: app.Name, processes: runningProcesses, groupName: group.Name, reason: "Tageslimit überschritten");
                    }
                }
            }
        configReader.SaveConfig();
        Logger.Instance.Log("App-Blocking Handler abgeschlossen.", LogLevel.Verbose);
        }

        /// <summary>
        /// Hilfsmethode, die alle Prozesse in <paramref name="processes"/> killt und
        /// entsprechende Log-Einträge schreibt. 
        /// </summary>
        /// <param name="appName">Name der App (Prozessname ohne .exe).</param>
        /// <param name="processes">Array der Process-Instanzen, die beendet werden sollen.</param>
        /// <param name="groupName">Name der Gruppe, zu der die App gehört (für Logging).</param>
        /// <param name="reason">Kurztext für den Grund des Killens (z.B. "Pausenmodus", "Tageslimit").</param>
        private void TerminateProcesses(
            string appName,
            Process[] processes,
            string groupName,
            string reason)
        {
            foreach (var proc in processes)
            {
                try
                {
                    proc.Kill();
                    Logger.Instance.Log($"Prozess {appName} (PID {proc.Id}) in Gruppe {groupName} beendet: {reason}.", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Fehler beim Beenden von {appName} (PID {proc.Id}) in Gruppe {groupName} ({reason}): {ex.Message}", LogLevel.Error);
                }
            }
        }

        ///<summary>Löscht ein App von der Liste der geblockten Apps</summary> 
        ///<param name="processName">Name der App</param>
        ///<param name="selectedGroup">Name der Gruppe</param>
        public void RemoveSelectedProcessesFromFile(string selectedGroup, string processName)
        {
            Gruppe? group = configReader.GetGroupByName(selectedGroup);
            if (group != null)
            {
                AppHE? app = configReader.GetAppFromGroup(group, processName);
                configReader.DeleteAppFromGroup(group, app);
            }
        }

        public bool GetGroupActivity(Gruppe group)
        {
            return configReader.GetGroupActiveStatus(group);
        }

        public void SetGroupActivity(Gruppe group, bool isActive)
        {
            configReader.SetGroupActiveStatus(group, isActive);
        }

        public long GetDailyTimeMs(Gruppe gruppe, AppHE app)
        {
            return configReader.GetDailyAppTime(gruppe, app);
        }

        public void SetDailyTimeMs(Gruppe group, AppHE app, long dailyTimeMs)
        {
            configReader.SetDailyAppTime(group, app, dailyTimeMs);
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
            }
        }

        public void SetAppDateTime(Gruppe group, AppHE app, DateOnly date, long timeMs)
        {
            configReader.SetAppDateTimeMs(group, app, date, timeMs);
        }

        public long GetDailyTimeLeft(Gruppe group, AppHE app, DateOnly date)
        {
            if (group == null || app == null)
            {
                Logger.Instance.Log("Gruppe oder App ist null. Kann die tägliche Zeit nicht abrufen.", LogLevel.Error);
                return 0;
            }
            long timeLeftMs = configReader.GetDailyAppTime(group, app) - configReader.GetAppDateTimeMs(group, app, date);
            return timeLeftMs;
        }

        public void SetDaílyTimeMs(Gruppe group, AppHE app, DateOnly date, long timeMs)
        {
            if (group == null || app == null)
            {
                Logger.Instance.Log("Gruppe oder App ist null. Kann die Zeit nicht setzen.", LogLevel.Error);
                return;
            }
            configReader.SetAppDateTimeMs(group, app, date, timeMs);
        }

        public AppHE GetAppByNameFromGroup(Gruppe group, string appName)
        {
            return configReader.GetAppByNameFromGroup(group, appName);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                Logger.Instance.Log("AppManager wurde disposed.", LogLevel.Info);
            }
            disposed = true;
        }
    }
}
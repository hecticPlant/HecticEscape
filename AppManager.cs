using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ScreenZen
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
        private List<string> blockedApps = new List<string>();
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
        public void SaveSelectedProcessesToFile(string selectedGroup, string processName)
        {
            string newProcessName = CleanProcessName(processName);
            configReader.AddAppToGroup(selectedGroup, CleanProcessName(processName));
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
            Logger.Instance.Log($"'{processName}' wurde zu '{newProcessName}' bereinigt", LogLevel.Debug);
            return newProcessName;
        }

        /// <summary>
        /// Schreibt alle aktiven Apps in blockedApps
        /// </summary>
        private void UpdateAppList()
        {
            blockedApps.Clear(); // Liste leeren!
            List<string> activeApps = configReader.GetActiveGroupsApps();

            if (activeApps.Any())
            {
                blockedApps.AddRange(activeApps);
                Logger.Instance.Log($"Aktive Apps wurden hinzugefügt: {string.Join(", ", activeApps)}", LogLevel.Debug);
            }
            else
            {
                Logger.Instance.Log("Keine aktiven Apps gefunden.", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Beendet alle Apps einer Gruppe
        /// </summary>
        public void BlockHandler()
        {
            if (!configReader.GetAppBlockingEnabled())
            {
                Logger.Instance.Log("App-Blocking ist deaktiviert. Es werden keine Apps beendet.", LogLevel.Debug);
                return;
            }
            UpdateAppList();
            foreach (var app in blockedApps)
            {
                EndApp(app);
            }
        }

        /// <summary>
        /// Beendet einen Prozess
        /// </summary>
        /// <param name="appName">Name der App</param>
        private void EndApp(string appName)
        {
            try
            {
                // Hole die Prozesse, die den Namen des appName haben
                Process[] processes = Process.GetProcessesByName(appName);

                foreach (var process in processes)
                {
                    // Beende den Prozess
                    process.Kill();
                    Logger.Instance.Log($"Prozess {process} wurde beendet", LogLevel.Warn);
                }

                // Falls keine Prozesse mit diesem Namen gefunden wurden, gebe eine Nachricht aus
                if (processes.Length == 0)
                {
                    //Logger.Instance.Log($"Der Prozess {processes} wurde nicht gefunden.");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Beenden des Prozesses '{appName}': {ex.Message}", LogLevel.Error);
            }
        }

        ///<summary>Löscht ein App von der Liste der geblockten Apps</summary> 
        ///<param name="processName">Name der App</param>
        ///<param name="selectedGroup">Name der Gruppe</param>
        public void RemoveSelectedProcessesFromFile(string selectedGroup, string processName)
        {
            configReader.DeleteAppFromGroup(selectedGroup, processName);
        }

        public bool GetGroupActivity(string groupName)
        {
            return configReader.ReadActiveStatus(groupName);
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
                // Hier ggf. weitere Ressourcen freigeben
                blockedApps.Clear();
                Logger.Instance.Log("AppManager wurde disposed.", LogLevel.Info);
            }
            disposed = true;
        }
    }
}
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ScreenZen
{
    /// <summary>
    /// Verwaltet die Apps
    /// </summary>
    public class AppManager
    {

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
            return runningProcesses;
        }

        /// <summary>
        /// Speichert einen Prozess
        /// </summary>
        /// <param name="selectedGroup">Gruppen Name</param>
        /// <param name="processName">Prozess Name</param>
        public void SaveSelectedProcessesToFile(string selectedGroup, string processName)
        {
            configReader.AddAppToGroup(selectedGroup, processName);
        }

        /// <summary>
        /// Bereinigt den Namen des Prozesses
        /// </summary>
        /// <param name="processName"></param>
        /// <returns></returns>
        private string CleanProcessName(string processName)
        {
            // Entfernt alles nach dem ersten Auftreten von " (ID: <Zahl>)"
            string pattern = @"\s?\(ID: \d+\)$";  // Sucht nach der Form " (ID: <Zahl>)" am Ende des Strings
            return Regex.Replace(processName, pattern, string.Empty).Trim();  // Entfernt die ID und gibt den bereinigten Namen zurück
        }

        /// <summary>
        /// Schreibt alle aktiven Apps in blockedApps
        /// </summary>
        private void UpdateAppList()
        {
            JsonNode jsonNode = configReader.GetActiveGroups();
            if (jsonNode is JsonArray jsonArray)
            {
                string[] apps = jsonArray.Select(node => node.ToString()).ToArray();
                blockedApps.AddRange(apps); // Richtige Methode für das Hinzufügen einer Liste
            }
        }

        /// <summary>
        /// Beendet alle Apps einer Gruppe
        /// </summary>
        /// <param name="groupId"></param>
        public void BlockHandler()
        {
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
                    Logger.Instance.Log($"Prozess {process} wurde beendet");
                }

                // Falls keine Prozesse mit diesem Namen gefunden wurden, gebe eine Nachricht aus
                if (processes.Length == 0)
                {
                    //Logger.Instance.Log($"Der Prozess {processes} wurde nicht gefunden.");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Beenden des Prozesses '{appName}': {ex.Message}");
            }
        }

        ///<summary>Löscht ein App von der Liste der geblockten Apps</summary> 
        ///<param name="processName">Name der App</param>
        ///<param name="selectedGroup">Name der Gruppe</param>
        public void RemoveSelectedProcessesFromFile(string selectedGroup, string processName)
        {
            configReader.DeleteAppFromGroup(selectedGroup, processName);
        }
    }
}
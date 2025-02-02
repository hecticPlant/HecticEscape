using System.IO;
using System.Diagnostics;
using System.Windows;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace ScreenZen
{
    public class ProcessManager
    {
        private Process[] runningProcesses;
        private List<string> blockedApps = new List<string>();

        public ProcessManager()
        {
            UpdateRunningProcesses();
        }

        // Methode, um die laufenden Prozesse zu aktualisieren
        public void UpdateRunningProcesses()
        {
            runningProcesses = Process.GetProcesses();
        }

        // Methode, um das aktuelle Array der laufenden Prozesse abzurufen
        public Process[] GetRunningProcesses()
        {
            return runningProcesses;
        }

        //JSON 
        public void SaveSelectedProcessesToFile(string selectedGroup, string processName)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Config.json"); // JSON-Datei mit den Gruppen

            try
            {
                // Überprüfe, ob die Datei existiert
                if (!File.Exists(filePath))
                {
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Die Datei '{filePath}' wurde nicht gefunden.");
                    return;
                }

                // Lese den Inhalt der JSON-Datei
                string jsonContent = File.ReadAllText(filePath);
                JObject jsonObject = JObject.Parse(jsonContent);

                // Überprüfen, ob die angegebene Gruppe existiert
                if (!jsonObject.ContainsKey(selectedGroup))
                {
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Die Gruppe '{selectedGroup}' existiert nicht.");
                    return;
                }

                // Hole die "Apps"-Liste der ausgewählten Gruppe
                JObject selectedGroupObject = (JObject)jsonObject[selectedGroup];
                JArray apps = (JArray)selectedGroupObject["Apps"];

                // Bereinige den Prozessnamen
                string cleanedProcessName = CleanProcessName(processName).Trim();

                // Überprüfe, ob der Prozess bereits existiert
                bool processExists = apps.Any(app => app["Name"].ToString().Trim() == cleanedProcessName);

                if (processExists)
                {
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole($"'{cleanedProcessName}' existiert bereits in der Gruppe '{selectedGroup}'.");
                    return;
                }

                // Erstelle ein neues App-Objekt gemäß neuer JSON-Struktur
                JObject newApp = new JObject
                {
                    ["Name"] = cleanedProcessName,
                    ["HeuteOeffnungen"] = false // Standardwert
                };

                // Füge das neue App-Objekt zur "Apps"-Liste hinzu
                apps.Add(newApp);

                // Speichere die Änderungen in der JSON-Datei
                File.WriteAllText(filePath, jsonObject.ToString());

                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Prozess '{cleanedProcessName}' wurde erfolgreich in der Gruppe '{selectedGroup}' gespeichert.");
            }
            catch (Exception ex)
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler beim Speichern der Datei: {ex.Message}");
            }
        }

        // Methode zum Bereinigen des Prozessnamens (Entfernt die ID)
        private string CleanProcessName(string processName)
        {
            // Entfernt alles nach dem ersten Auftreten von " (ID: <Zahl>)"
            string pattern = @"\s?\(ID: \d+\)$";  // Sucht nach der Form " (ID: <Zahl>)" am Ende des Strings
            return Regex.Replace(processName, pattern, string.Empty).Trim();  // Entfernt die ID und gibt den bereinigten Namen zurück
        }

        //JSON 
        public void BlockAppsFromGroup(string selectedGroup)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Config.json");  // Der Pfad zur Konfigurationsdatei

            if (File.Exists(filePath))
            {
                try
                {
                    // Lese die JSON-Datei und parse sie
                    string jsonContent = File.ReadAllText(filePath);
                    JObject jsonObject = JObject.Parse(jsonContent);

                    if (jsonObject.ContainsKey(selectedGroup))
                    {
                        // Hole die "Apps"-Liste der ausgewählten Gruppe
                        JObject selectedGroupObject = (JObject)jsonObject[selectedGroup];
                        JArray apps = (JArray)selectedGroupObject["Apps"];

                        foreach (var app in apps)
                        {
                            // Extrahiere den "Name"-Wert jeder App
                            string appName = app["Name"].ToString();

                            // Blockiere die App basierend auf dem Namen
                            BlockApp(appName);
                        }

                        ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Apps aus Gruppe '{selectedGroup}' wurden erfolgreich blockiert.");
                    }
                    else
                    {
                        ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Die Gruppe '{selectedGroup}' existiert nicht.");
                    }
                }
                catch (Exception ex)
                {
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler beim Blockieren der Apps: {ex.Message}");
                }
            }
            else
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole("Config.json nicht gefunden.");
            }
        }


        // Methode zum Blockieren der App (Beenden des Prozesses)
        private void BlockApp(string appName)
        {
            try
            {
                // Hole die Prozesse, die den Namen des appName haben
                Process[] processes = Process.GetProcessesByName(appName);

                foreach (var process in processes)
                {
                    // Beende den Prozess
                    process.Kill();
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Prozess '{appName}' wurde beendet.");
                }

                // Falls keine Prozesse mit diesem Namen gefunden wurden, gebe eine Nachricht aus
                if (processes.Length == 0)
                {
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Kein Prozess mit dem Namen '{appName}' gefunden.");
                }
            }
            catch (Exception ex)
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler beim Beenden des Prozesses '{appName}': {ex.Message}");
            }
        }

        //JSON 
        public void RemoveSelectedProcessesFromFile(string selectedGroup, string processName)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Config.json");  // Die Datei, in der die Gruppen gespeichert sind

            try
            {
                // Überprüfen, ob die Datei existiert
                if (File.Exists(filePath))
                {
                    // Lese den Inhalt der JSON-Datei
                    string jsonContent = File.ReadAllText(filePath);
                    JObject jsonObject = JObject.Parse(jsonContent);

                    // Überprüfen, ob die angegebene Gruppe existiert
                    if (jsonObject.ContainsKey(selectedGroup))
                    {
                        // Hole die "Apps"-Liste der ausgewählten Gruppe
                        JObject selectedGroupObject = (JObject)jsonObject[selectedGroup];
                        JArray apps = (JArray)selectedGroupObject["Apps"];

                        // Bereinige den Prozessnamen
                        string cleanedProcessName = CleanProcessName(processName).Trim();

                        // Suche nach der App, die entfernt werden soll, indem der "Name"-Wert verglichen wird
                        var appToRemove = apps.FirstOrDefault(app => app["Name"].ToString().Trim() == cleanedProcessName);

                        if (appToRemove != null)
                        {
                            // Entferne die App aus der Liste
                            apps.Remove(appToRemove);
                            ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Prozess '{cleanedProcessName}' wurde erfolgreich aus der Gruppe '{selectedGroup}' entfernt.");
                        }
                        else
                        {
                            ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Prozess '{cleanedProcessName}' wurde in der Gruppe '{selectedGroup}' nicht gefunden.");
                        }

                        // Speichere die Änderungen in der JSON-Datei
                        File.WriteAllText(filePath, jsonObject.ToString());
                    }
                    else
                    {
                        ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Die Gruppe '{selectedGroup}' existiert nicht.");
                    }
                }
                else
                {
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Die Datei '{filePath}' wurde nicht gefunden.");
                }
            }
            catch (Exception ex)
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler beim Entfernen des Prozesses: {ex.Message}");
            }
        }

        //JSON
        public void UpdateAppBlocking(string selectedGroup)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Config.json");  // Die Datei, in der die Gruppen gespeichert sind

            try
            {
                // Überprüfen, ob die Datei existiert
                if (File.Exists(filePath))
                {
                    // Lese den Inhalt der JSON-Datei
                    string jsonContent = File.ReadAllText(filePath);
                    JObject jsonObject = JObject.Parse(jsonContent);

                    // Überprüfen, ob die angegebene Gruppe existiert
                    if (jsonObject.ContainsKey(selectedGroup))
                    {
                        // Hole die "Apps"-Liste und anderen Daten der ausgewählten Gruppe
                        JObject selectedGroupObject = (JObject)jsonObject[selectedGroup];
                        JArray apps = (JArray)selectedGroupObject["Apps"];

                        foreach (JObject app in apps) // apps enthält eine Liste von Objekten
                        {
                            // Hier fügen wir die Namen der Apps zu blockedApps hinzu
                            string appName = app["Name"]?.ToString();  // Optional: Weiterer Zugriff auf App-Daten
                            if (!string.IsNullOrWhiteSpace(appName))
                            {
                                blockedApps.Add(appName);  // Nur den Namen hinzufügen
                            }
                        }

                        // Optional: Bestätigung, dass Apps geladen wurden
                        ((MainWindow)Application.Current.MainWindow).AppendToConsole("Apps aus der Gruppe wurden erfolgreich geladen.");
                    }
                    else
                    {
                        // Ausgabe, wenn die Gruppe nicht existiert
                        ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Die Gruppe '{selectedGroup}' existiert nicht.");
                    }
                }
                else
                {
                    // Datei nicht gefunden, also Fehlermeldung anzeigen
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Die Datei '{filePath}' wurde nicht gefunden.");
                }
            }
            catch (Exception ex)
            {
                // Fehlerbehandlung
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler beim Bearbeiten der App-Blockierung: {ex.Message}");
            }
        }

    }
}
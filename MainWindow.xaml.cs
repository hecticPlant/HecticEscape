using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Text.Json.Nodes;

namespace ScreenZen
{
    public partial class MainWindow : Window
    {
        private TimeManagement timeManager;
        private AppManager appManager;
        private WebManager webManager;
        private Overlay overlay;
        private ConfigReader configReader;
        private readonly TimeManagement _timeManagement;

        // Der Konstruktor nimmt die Abhängigkeiten entgegen:
        public MainWindow(TimeManagement timeManager, AppManager appManager, WebManager webManager, Overlay overlay, ConfigReader configReader)
        {
            InitializeComponent();

            this.timeManager = timeManager;
            this.appManager = appManager;
            this.webManager = webManager;
            this.overlay = overlay;
            this.configReader = configReader;

            this.timeManager.StatusChanged += OnStatusChanged;
            LoadGroups();
            this.overlay = overlay;

            _timeManagement = timeManager;
            _timeManagement.OverlayToggleRequested += ToggleOverlay;
        }

        /// <summary>
        /// Aktualisiert den Status-Text im UI-Thread.
        /// </summary>
        /// <param name="newStatus"></param>
        private void OnStatusChanged(string newStatus)
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                // UI-Aktualisierungen hier, z. B.:
                StatusPauseTextBlock.Text = newStatus;
            }));
        }
        
        /// <summary>
        /// DEBUG: Startet den Proxy
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartProxy_Click(object sender, RoutedEventArgs e)
        {
           timeManager.Start();
        }

        /// <summary>
        /// DEBUG: stoppt den Proxy
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StopProxy_Click(object sender, RoutedEventArgs e)
        {
            timeManager.Stop(); 
        }

        /// <summary>
        /// Läd die laufenden Prozesse in ProcessListBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListProcessButton_Click(object sender, RoutedEventArgs e)
        {
            Process[] processes = appManager.GetRunningProcesses();

            ProcessListBox.Items.Clear(); // Vorherige Einträge löschen

            foreach (var process in processes)
            {
                ProcessListBox.Items.Add($"{process.ProcessName} (ID: {process.Id})");
            }
        }

        /// <summary>
        /// Lädt alle Gruppen in die GroupComboBox.
        /// </summary>
        private void LoadGroups()
        {
            JsonNode allGroups = configReader.ReadConfig();

            // Prüfe, ob das gelesene JSON ein Objekt ist
            if (allGroups is JsonObject groupsObject)
            {
                foreach (var groupProperty in groupsObject)
                {
                    GroupComboBox.Items.Add(groupProperty.Key);
                }
            }
        }

        /// <summary>
        /// Erstellt eine neue Gruppe
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CreateNewGroupButton_Click(object sender, RoutedEventArgs e)
        {
            configReader.CreateNewGroup();
            LoadGroups();
        }

        /// <summary>
        /// Löscht eine ausgewählte Gruppe
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteGroupButton_Click(object sender, RoutedEventArgs e)
        {
            string groupNameToDelete = GroupComboBox.SelectedItem as string;
            groupNameToDelete = CleanGroupName(groupNameToDelete);
            configReader.RemoveFromConfig(groupNameToDelete, "g", null);
            LoadGroups();

        }

        /// <summary>
        /// Speichert den ausgewähten Prozess in die Config
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveProcessButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedGroup = GroupComboBox.SelectedItem as string;
            selectedGroup = CleanGroupName(selectedGroup);
            string selectedProcess = ProcessListBox.SelectedItem as string;
            configReader.AppendToConfig(selectedGroup, "a", selectedProcess);    
        }

        /// <summary>
        /// Löscht den ausgwählten Prozess aus der Config
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveProcessFromFile_Click(object sender, RoutedEventArgs e)
        {
            string selectedGroup = GroupComboBox.SelectedItem as string;
            selectedGroup = CleanGroupName(selectedGroup);
            string selectedProcess = ProcessListBox.SelectedItem as string;
            appManager.RemoveSelectedProcessesFromFile(selectedGroup, selectedProcess);
        }

        /// <summary>
        /// Löscht die ausgewählte Domain von der Liste
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveDomainFromFile_Click(object sender, RoutedEventArgs e)
        {
            string selectedGroup = GroupComboBox.SelectedItem as string;
            selectedGroup = CleanGroupName(selectedGroup);
            string selectedProcess = ProcessListBox.SelectedItem as string;
            webManager.RemoveSelectedWebsiteFromFile(selectedGroup, selectedProcess);
        }

        /// <summary>
        /// DEBUG: Startet die Timer timerCheck und timerFree
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartTimer_Click(object sender, RoutedEventArgs e)
        {
            timeManager.Start();
        }

        /// <summary>
        /// DEBUG: Stoppt alle Timer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StopTimer_Click(object sender, RoutedEventArgs e)
        {
            timeManager.Stop();
        }

        /// <summary>
        /// DEBUG: Erzwingt eine Paus
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ForceBreak_Click(object sender, RoutedEventArgs e)
        {
            timeManager.ForceBreak();
        }

        /// <summary>
        /// DEBUG: Beendet eine Paus
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EndBreak_Click(object sender, RoutedEventArgs e)
        {
            timeManager.EndBreak();
        }

        /// <summary>
        /// Listet alle Blockierten Apps in GroupComboBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>        
        private async void ListBlockedApps_Click(object sender, RoutedEventArgs e)
         {      
            string groupID = GroupComboBox.SelectedItem as string;
            JsonNode apps= configReader.ReadConfig(groupID, "a");
            ProcessListBox.Items.Clear(); // Vorherige Einträge löschen

            if (apps is JsonObject groupsObject)
            {
                foreach (var groupProperty in groupsObject)
                {
                    GroupComboBox.Items.Add(groupProperty.Key);
                }
            }
        }

        //JASON 
        private async void ListBlockedDomains_Click(object sender, RoutedEventArgs e)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Config.json");

            // Überprüfen, ob die Datei existiert
            if (File.Exists(filePath))
            {
                try
                {
                    // Lese den Inhalt der JSON-Datei asynchron
                    string jsonContent = await File.ReadAllTextAsync(filePath);
                    JObject jsonObject = JObject.Parse(jsonContent);

                    // Wenn der Benutzer eine Gruppe auswählt
                    string selectedGroup = GroupComboBox.SelectedItem as string;
                    selectedGroup = CleanGroupName(selectedGroup);

                    if (string.IsNullOrEmpty(selectedGroup))
                    {
                        ((MainWindow)Application.Current.MainWindow).AppendToConsole("Bitte wählen Sie eine Gruppe aus.");
                        return;
                    }

                    // Überprüfen, ob die Gruppe in der JSON-Datei existiert
                    if (jsonObject.TryGetValue(selectedGroup, out JToken groupToken) && groupToken is JObject selectedGroupObject)
                    {
                        // Hole die "Websites"-Liste der ausgewählten Gruppe
                        if (selectedGroupObject.TryGetValue("Websites", out JToken websitesToken) && websitesToken is JArray websites)
                        {
                            // Vorherige Einträge löschen
                            ProcessListBox.Items.Clear();

                            foreach (JObject website in websites)
                            {
                                if (website.TryGetValue("Name", out JToken nameToken))
                                {
                                    ProcessListBox.Items.Add(nameToken.ToString());
                                }
                            }

                            if (ProcessListBox.Items.Count == 0)
                            {
                                ((MainWindow)Application.Current.MainWindow).AppendToConsole("Es wurden keine blockierten Websites gefunden.");
                            }
                        }
                        else
                        {
                            ((MainWindow)Application.Current.MainWindow).AppendToConsole("Die Gruppe enthält keine Websites.");
                        }
                    }
                    else
                    {
                        ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Die Gruppe '{selectedGroup}' existiert nicht.");
                    }
                }
                catch (Exception ex)
                {
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler beim Laden der Datei: {ex.Message}");
                }
            }
            else
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Die Datei '{filePath}' wurde nicht gefunden.");
            }
        }

        private void AddWebsiteToBlocklist_Click(object sender, RoutedEventArgs e)
        {
            string websiteName = WebsiteTextBox.Text.Trim();
            string selectedGroup = GroupComboBox.SelectedItem as string;
            selectedGroup = CleanGroupName(selectedGroup);

            if (string.IsNullOrWhiteSpace(websiteName))
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole("Bitte geben Sie eine gültige Website an.");
                return;
            }

            try
            {
                // Aufruf der Methode ohne die zusätzliche Klammer
                webManager.SaveSelectedWebsiteToFile(selectedGroup, websiteName);
            }
            catch (Exception ex)
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler: {ex.Message}");
            }

            // Leere das Textfeld nach dem Hinzufügen
            WebsiteTextBox.Clear();
        }

        private readonly Queue<string> logQueue = new Queue<string>();
        private bool isProcessing = false;

        public void AppendToConsole(string message)
        {
            logQueue.Enqueue(message);

            if (!isProcessing)
            {
                isProcessing = true;

                Task.Run(() =>
                {
                    while (logQueue.Any())
                    {
                        string logMessage = logQueue.Dequeue();

                        Dispatcher.Invoke(() =>
                        {
                            if (ConsoleOutputTextBox.Text.Length > 5000)
                                ConsoleOutputTextBox.Clear();

                            ConsoleOutputTextBox.AppendText(logMessage + Environment.NewLine);
                            Logger.Instance.Log(logMessage);
                            ConsoleOutputTextBox.ScrollToEnd();
                        });
                    }

                    isProcessing = false;
                });
            }
        }

        private bool isOvelay = false;
        public void ToggleOverlayClick(object sender, RoutedEventArgs e)
        {
            ToggleOverlay();
        }

        public void ToggleOverlay()
        {
            if (!isOvelay)
            {
                overlay.Show();
                isOvelay = true;
            }
            else
            {
                overlay.Hide();
                isOvelay = false;
            }
        }

        private void ConsoleOutputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void ToggleGroupButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedGroup = GroupComboBox.SelectedItem as string;
            selectedGroup = CleanGroupName(selectedGroup);
            if (selectedGroup != null)
            {
                ToggleGroup(selectedGroup);
            }
        }

        public string CleanGroupName(string input)
        {
            if (input.EndsWith(" ( True)", StringComparison.OrdinalIgnoreCase))
            {
                return input.Substring(0, input.Length - 7).Trim(); ; // Länge von " (True)" ist 8
            }
            else if (input.EndsWith(" ( False)", StringComparison.OrdinalIgnoreCase))
            {
                return input.Substring(0, input.Length - 8).Trim(); // Länge von " (False)" ist 9
            }
            else
            {
                Logger.Instance.Log($"Es wurde nichts entfernt von {input}");
                return input; // Rückgabe des Original-Strings, wenn kein Suffix gefunden wurde
            }
        }

        public void ToggleGroup(string selectedGroup)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Config.json");  // Die Datei, in der die Gruppen gespeichert sind

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
                else
                {
                    JObject selectedGroupObject = (JObject)jsonObject[selectedGroup];
                    bool aktiv = selectedGroupObject.Value<bool>("Aktiv");
                    selectedGroupObject["Aktiv"] = !aktiv;
                    File.WriteAllText(filePath, jsonObject.ToString());
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole($"{selectedGroup} wurde auf {!aktiv} geändert\"");
                    LoadGroups();
                }
            }
            catch (Exception ex)
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler beim aktiviern/deaktiviern der Grupe: {ex.Message}");
            }
        }
        
        public string getConfig(string key, int num)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Config.json"); // JSON-Datei mit den Gruppen

            try
            {
                // Überprüfe, ob die Datei existiert
                if (!File.Exists(filePath))
                {
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Die Datei '{filePath}' wurde nicht gefunden.");
                    return null;
                }

                // Lese den Inhalt der JSON-Datei
                string jsonContent = File.ReadAllText(filePath);

                switch (key)
                {
                    case ("c"):
                        return jsonContent;
                    case ("a"):
                        return "";
                    default:
                        return "";
                }
            }
            catch (Exception ex)
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler beim Speichern der Datei: {ex.Message}");
                return "";
            }
        }
    }
}

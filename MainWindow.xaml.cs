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
            GroupComboBox.Items.Clear();  

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

        /// <summary>
        /// Listet alle blockierten Websites in GroupComboBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ListBlockedDomains_Click(object sender, RoutedEventArgs e)
        {
            string domain = GroupComboBox.SelectedItem as string;
            JsonNode domains = configReader.ReadConfig(domain, "w");
            ProcessListBox.Items.Clear(); // Vorherige Einträge löschen

            if (domains is JsonObject groupsObject)
            {
                foreach (var groupProperty in groupsObject)
                {
                    GroupComboBox.Items.Add(groupProperty.Key);
                }
            }
        }

        /// <summary>
        /// Fügt die ausgewählte Website hinzu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddWebsiteToBlocklist_Click(object sender, RoutedEventArgs e)
        {
            string websiteName = WebsiteTextBox.Text.Trim();
            string selectedGroup = GroupComboBox.SelectedItem as string;

            configReader.AppendToConfig(selectedGroup, "w", websiteName);
            // Leere das Textfeld nach dem Hinzufügen
            WebsiteTextBox.Clear();
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

        private void ToggleGroupButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedGroup = GroupComboBox.SelectedItem as string;
            configReader.ToggleGroup(selectedGroup);
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
        
        public string getConfig(string key, int num)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Config.json"); // JSON-Datei mit den Gruppen

            try
            {
                // Überprüfe, ob die Datei existiert
                if (!File.Exists(filePath))
                {
                    Logger.Instance.Log($"Die Datei '{filePath}' wurde nicht gefunden.");
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
                Logger.Instance.Log($"Fehler beim Speichern der Datei: {ex.Message}");
                return "";
            }
        }
    }
}

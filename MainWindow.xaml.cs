using System.Diagnostics;
using System.IO;
using System.Windows;

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
            // Holen der Gruppennamen als durch Kommas getrennte Liste
            string allGroups = configReader.GetAllGroups();
            GroupComboBox.Items.Clear();

            // Sicherstellen, dass Gruppen vorhanden sind
            if (!string.IsNullOrEmpty(allGroups))
            {
                // Gruppen in die ComboBox hinzufügen (nehmen wir an, dass allGroups eine durch Kommas getrennte Liste ist)
                var groups = allGroups.Split(new[] { ", " }, StringSplitOptions.None);
                foreach (var group in groups)
                {
                    GroupComboBox.Items.Add(group);
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
            configReader.CreateGroup();
            LoadGroups();
        }

        /// <summary>
        /// Löscht eine ausgewählte Gruppe
        /// </summary>
        private void DeleteGroupButton_Click(object sender, RoutedEventArgs e)
        {
            string groupNameToDelete = GroupComboBox.SelectedItem as string;

            if (string.IsNullOrEmpty(groupNameToDelete))
            {
                MessageBox.Show("Bitte wählen Sie eine Gruppe zum Löschen aus.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            configReader.DeleteGroup(groupNameToDelete);
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
            configReader.AddAppToGroup(selectedGroup, selectedProcess);
            ListBlockedApps_Click(sender, e);
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
            ListBlockedApps_Click(sender, e);
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
            ListBlockedDomains_Click(sender, e);
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
        /// Listet alle blockierten Apps in ProcessListBox
        /// </summary>
        private void ListBlockedApps_Click(object sender, RoutedEventArgs e)
        {
            string groupID = GroupComboBox.SelectedItem as string;
            if (string.IsNullOrEmpty(groupID)) return;

            string apps = configReader.GetAppsFromGroup(groupID);
            ProcessListBox.Items.Clear(); // Vorherige Einträge löschen

            if (!string.IsNullOrEmpty(apps))
            {
                foreach (var app in apps.Split(", "))
                {
                    ProcessListBox.Items.Add(app);
                }
            }
        }

        /// <summary>
        /// Listet alle blockierten Websites in ProcessListBox
        /// </summary>
        private void ListBlockedDomains_Click(object sender, RoutedEventArgs e)
        {
            string groupID = GroupComboBox.SelectedItem as string;
            if (string.IsNullOrEmpty(groupID)) return;

            string domains = configReader.GetWebsitesFromGroup(groupID);
            ProcessListBox.Items.Clear(); // Vorherige Einträge löschen

            if (!string.IsNullOrEmpty(domains))
            {
                foreach (var domain in domains.Split(", "))
                {
                    ProcessListBox.Items.Add(domain);
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

            configReader.AddWebsiteToGroup(selectedGroup, websiteName);
            // Leere das Textfeld nach dem Hinzufügen
            WebsiteTextBox.Clear();
            ListBlockedDomains_Click(sender, e);
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

        private void ActivateGroupButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedGroup = GroupComboBox.SelectedItem as string;

            if (string.IsNullOrEmpty(selectedGroup))
            {
                MessageBox.Show("Bitte wählen Sie eine Gruppe zum Aktivieren aus.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            configReader.SetActiveStatus(selectedGroup, true);
            MessageBox.Show($"Die Gruppe '{selectedGroup}' wurde aktiviert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeactivateGroupButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedGroup = GroupComboBox.SelectedItem as string;

            if (string.IsNullOrEmpty(selectedGroup))
            {
                MessageBox.Show("Bitte wählen Sie eine Gruppe zum Deaktivieren aus.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            configReader.SetActiveStatus(selectedGroup, false);
            MessageBox.Show($"Die Gruppe '{selectedGroup}' wurde deaktiviert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
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

using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json.Linq;

namespace ScreenZen
{
    public partial class MainWindow : Window
    {
        private TimeManagement timeManager;
        private ProcessManager processManager;
        private WebManagerSZ webManager;
        private WebProxySZ webProxy;

        // Der Konstruktor nimmt die Abhängigkeiten entgegen:
        public MainWindow(TimeManagement timeManager, ProcessManager processManager, WebManagerSZ webManager, WebProxySZ webProxy)
        {
            InitializeComponent();

            this.timeManager = timeManager;
            this.processManager = processManager;
            this.webManager = webManager;
            this.webProxy = webProxy;

            this.timeManager.StatusChanged += OnStatusChanged;
            LoadGroups();
        }

        private void OnStatusChanged(string newStatus)
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                // UI-Aktualisierungen hier, z. B.:
                StatusPauseTextBlock.Text = newStatus;
            }));
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Startet den Proxy
                await webProxy.StartProxy();
                StatusProxyTextBlock.Text = "Status: Proxy aktiv";
            }
            catch (Exception ex)
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler beim Starten des Proxys: {ex.Message}");
            }
        }

        private void ListProcessButton_Click(object sender, RoutedEventArgs e)
        {
            processManager.UpdateRunningProcesses();
            Process[] processes = processManager.GetRunningProcesses();

            ProcessListBox.Items.Clear(); // Vorherige Einträge löschen

            foreach (var process in processes)
            {
                ProcessListBox.Items.Add($"{process.ProcessName} (ID: {process.Id})");
            }
        }

        // Event-Handler für den Stop-Button (falls du ihn auch hinzufügst)
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                webProxy.StopProxy();
                StatusProxyTextBlock.Text = "Status: Proxy inaktiv";
            }
            catch (Exception ex)
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler beim Stoppen des Proxys: {ex.Message}");
            }
        }

        //JSON 
        //Methode zum Laden der bestehenden Gruppen aus der JSON-Datei
        private void LoadGroups()
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Config.json");

            // Überprüfen, ob die Datei existiert
            if (File.Exists(filePath))
            {
                try
                {
                    // Lese den Inhalt der JSON-Datei
                    string jsonContent = File.ReadAllText(filePath);
                    JObject jsonObject = JObject.Parse(jsonContent);

                    // Falls die JSON-Datei gültig ist und Gruppen enthält
                    if (jsonObject.Count > 0)
                    {
                        GroupComboBox.Items.Clear();

                        foreach (var group in jsonObject.Properties())
                        {
                            GroupComboBox.Items.Add(group.Name); // Gruppenname hinzufügen
                        }

                        // Falls keine Gruppen gefunden wurden
                        if (GroupComboBox.Items.Count == 0)
                        {
                            ((MainWindow)Application.Current.MainWindow).AppendToConsole("Keine Gruppen in der JSON-Datei gefunden.");
                        }
                    }
                    else
                    {
                        ((MainWindow)Application.Current.MainWindow).AppendToConsole("Das JSON-Dokument ist leer oder ungültig.");
                    }
                }
                catch (Exception ex)
                {
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler beim Laden der Gruppen: {ex.Message}");
                }
            }
            else
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Die Datei '{filePath}' wurde nicht gefunden.");
            }
        }

        //Methode zum Erstellen einer neuen Gruppe
        private void CreateNewGroupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Pfad zur JSON-Datei
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Config.json");

                // Falls die Datei nicht existiert, erstelle sie
                if (!File.Exists(filePath))
                {
                    File.WriteAllText(filePath, "{}"); // Leere JSON-Struktur erstellen
                }

                // Lade die bestehende JSON-Datei
                string jsonContent = File.ReadAllText(filePath);
                JObject jsonObject = string.IsNullOrEmpty(jsonContent) ? new JObject() : JObject.Parse(jsonContent);

                // Finde einen eindeutigen Gruppennamen
                int groupNumber = 1;
                string newGroupName;
                do
                {
                    newGroupName = "Gruppe " + groupNumber;
                    groupNumber++;
                } while (jsonObject.ContainsKey(newGroupName));

                // Neue Gruppe erstellen mit aktueller Struktur
                JObject newGroup = new JObject
                {
                    ["Date"] = DateTime.Now.ToString("yyyy-MM-dd"), // Setzt das heutige Datum
                    ["Apps"] = new JArray(), // Leere App-Liste
                    ["Websites"] = new JArray() // Leere Website-Liste
                };

                // Neue Gruppe zum JSON hinzufügen
                jsonObject[newGroupName] = newGroup;

                // Aktualisierte JSON-Datei speichern
                File.WriteAllText(filePath, jsonObject.ToString());

                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Neue Gruppe '{newGroupName}' wurde erfolgreich erstellt.");
            }
            catch (Exception ex)
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler beim Erstellen der neuen Gruppe: {ex.Message}");
            }
            LoadGroups();
        }

        private void DeleteGroupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Pfad zur JSON-Datei
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Config.json");

                // Falls die Datei nicht existiert, kann keine Gruppe gelöscht werden
                if (!File.Exists(filePath))
                {
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole("Die Konfigurationsdatei existiert nicht.");
                    return;
                }

                // Lade die bestehende JSON-Datei
                string jsonContent = File.ReadAllText(filePath);
                JObject jsonObject = string.IsNullOrEmpty(jsonContent) ? new JObject() : JObject.Parse(jsonContent);

                string groupNameToDelete = GroupComboBox.SelectedItem as string;

                // Überprüfen, ob die Gruppe existiert
                if (jsonObject.ContainsKey(groupNameToDelete))
                {
                    // Gruppe aus dem JSON-Objekt entfernen
                    jsonObject.Remove(groupNameToDelete);

                    // Aktualisierte JSON-Datei speichern
                    File.WriteAllText(filePath, jsonObject.ToString());

                    ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Gruppe '{groupNameToDelete}' wurde erfolgreich gelöscht.");
                }
                else
                {
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Die Gruppe '{groupNameToDelete}' existiert nicht.");
                }
            }
            catch (Exception ex)
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler beim Löschen der Gruppe: {ex.Message}");
            }
            LoadGroups();
        }

        private void SaveProcessButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string selectedGroup = GroupComboBox.SelectedItem as string;
                string selectedProcess = ProcessListBox.SelectedItem as string;

                if (string.IsNullOrEmpty(selectedGroup) || string.IsNullOrEmpty(selectedProcess))
                {
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole("Bitte wählen Sie eine Gruppe aus.");
                    return;
                }
                processManager.SaveSelectedProcessesToFile(selectedGroup, selectedProcess);
            }
            catch (Exception ex)
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler: {ex.Message}");
            }
        }

        private void BlockApps_Click(object sender, RoutedEventArgs e)
        {
            string selectedGroup = GroupComboBox.SelectedItem as string;
            processManager.BlockAppsFromGroup(selectedGroup);
            StatusPauseTextBlock.Text = "Momentan Pause";
        }

        private void RemoveProcessFromFile_Click(object sender, RoutedEventArgs e)
        {
            string selectedGroup = GroupComboBox.SelectedItem as string;
            string selectedProcess = ProcessListBox.SelectedItem as string;
            processManager.RemoveSelectedProcessesFromFile(selectedGroup, selectedProcess);
        }

        private void RemoveDomainFromFile_Click(object sender, RoutedEventArgs e)
        {
            string selectedGroup = GroupComboBox.SelectedItem as string;
            string selectedProcess = ProcessListBox.SelectedItem as string;
            webManager.RemoveSelectedWebsiteFromFile(selectedGroup, selectedProcess);
        }

        private void StartTimer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                timeManager.Start(); // Starte den Timer
                StatuTimersTextBlock.Text = "Status: Timer läuft";
            }
            catch (Exception ex)
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler beim Starten des Timers: {ex.Message}");
            }
        }

        private void StopTimer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                timeManager.Stop(); // Starte den Timer
                StatuTimersTextBlock.Text = "Status: Timer inaktiv";
            }
            catch (Exception ex)
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler beim Starten des Timers: {ex.Message}");
            }
        }

        private void ForceBreak_Click(object sender, RoutedEventArgs e)
        {
            timeManager.ForceBreak();
        }

        private void EndBreak_Click(object sender, RoutedEventArgs e)
        {
            timeManager.EndBreak();
        }

        //JASON 
        private async void ListBlockedApps_Click(object sender, RoutedEventArgs e)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Config.json");

            // Überprüfen, ob die Datei existiert
            if (File.Exists(filePath))
            {
                try
                {
                    // Lese den Inhalt der JSON-Datei
                    string jsonContent = await File.ReadAllTextAsync(filePath);
                    JObject jsonObject = JObject.Parse(jsonContent);

                    // Wenn der Benutzer eine Gruppe auswählt
                    string selectedGroup = GroupComboBox.SelectedItem as string;

                    if (string.IsNullOrEmpty(selectedGroup))
                    {
                        ((MainWindow)Application.Current.MainWindow).AppendToConsole("Bitte wählen Sie eine Gruppe aus.");
                        return;
                    }

                    // Überprüfen, ob die Gruppe in der JSON-Datei existiert
                    if (jsonObject.TryGetValue(selectedGroup, out JToken groupToken) && groupToken is JObject selectedGroupObject)
                    {
                        // Hole die "Apps"-Liste der ausgewählten Gruppe
                        if (selectedGroupObject.TryGetValue("Apps", out JToken appsToken) && appsToken is JArray apps)
                        {
                            // Wenn Apps vorhanden sind, zeige nur die Namen an
                            ProcessListBox.Items.Clear(); // Vorherige Einträge löschen

                            foreach (JObject app in apps)
                            {
                                if (app.TryGetValue("Name", out JToken nameToken))
                                {
                                    ProcessListBox.Items.Add(nameToken.ToString());
                                }
                            }

                            if (ProcessListBox.Items.Count == 0)
                            {
                                ((MainWindow)Application.Current.MainWindow).AppendToConsole("Es wurden keine blockierten Apps gefunden.");
                            }
                        }
                        else
                        {
                            ((MainWindow)Application.Current.MainWindow).AppendToConsole("Die Gruppe enthält keine Apps.");
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
                            ConsoleOutputTextBox.ScrollToEnd();
                        });
                    }

                    isProcessing = false;
                });
            }
        }

        public void timerCheckalive_Click(object sender, RoutedEventArgs e)
        {
            //timeManager.isTimerRuning();
        }

        private void ConsoleOutputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}

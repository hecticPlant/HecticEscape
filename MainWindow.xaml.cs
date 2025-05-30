using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using MessageBox = System.Windows.MessageBox;

namespace ScreenZen
{
    public partial class MainWindow : Window
    {
        private readonly TimeManagement _timeManagement;
        private readonly AppManager _appManager;
        private readonly WebManager _webManager;
        private readonly Overlay _overlay;
        private readonly ConfigReader _configReader;
        private readonly DispatcherTimer statusUpdateTimer = new();
        private NotifyIcon _notifyIcon;
        private bool _closeToTray = true; // Optional: Verhalten steuern

        // Parameterloser Konstruktor (XAML)
        public MainWindow() : this(
            ResolveDependency<TimeManagement>("TimeManagement"),
            ResolveDependency<AppManager>("AppManager"),
            ResolveDependency<WebManager>("WebManager"),
            ResolveDependency<Overlay>("Overlay"),
            ResolveDependency<ConfigReader>("ConfigReader"))
        {
            Logger.Instance.Log("MainWindow Konstruktor (XAML) aufgerufen", LogLevel.Debug);
            UpdateStatusTextBlocks();
        }

        private static T ResolveDependency<T>(string dependencyName) where T : class
        {
            var dependency = (App.Current as App)?.Services.GetService<T>();
            if (dependency == null)
                throw new InvalidOperationException($"{dependencyName} darf nicht null sein.");
            return dependency;
        }

        // DI-Konstruktor
        public MainWindow(
            TimeManagement timeManagement,
            AppManager appManager,
            WebManager webManager,
            Overlay overlay,
            ConfigReader configReader)
        {
            Logger.Instance.Log("MainWindow DI-Konstruktor aufgerufen", LogLevel.Info);

            _timeManagement = timeManagement;
            _appManager = appManager;
            _webManager = webManager;
            _overlay = overlay;
            _configReader = configReader;

            InitializeComponent();
            Logger.Instance.Log("MainWindow initialisiert", LogLevel.Info);

            // Null-Checks für Abhängigkeiten
            if (_timeManagement == null)
            {
                Logger.Instance.Log("TimeManagement ist null!", LogLevel.Error);
                MessageBox.Show("Fehler: TimeManagement konnte nicht geladen werden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (_appManager == null)
            {
                Logger.Instance.Log("AppManager ist null!", LogLevel.Error);
            }
            if (_webManager == null)
            {
                Logger.Instance.Log("WebManager ist null!", LogLevel.Error);
            }
            if (_overlay == null)
            {
                Logger.Instance.Log("Overlay ist null!", LogLevel.Error);
            }
            if (_configReader == null)
            {
                Logger.Instance.Log("ConfigReader ist null!", LogLevel.Error);
            }

            try
            {
                _timeManagement.StatusChanged += OnStatusChanged;
            }
            catch { Logger.Instance.Log("StatusChanged-Event konnte nicht abonniert werden.", LogLevel.Error); }

            try { LoadGroups(); } catch { Logger.Instance.Log("LoadGroups() fehlgeschlagen.", LogLevel.Error); }
            try { ListTimers(); } catch { Logger.Instance.Log("ListTimers() fehlgeschlagen.", LogLevel.Error); }

            try
            {
                _timeManagement.OverlayToggleRequested += ToggleOverlay;
            }
            catch { Logger.Instance.Log("OverlayToggleRequested-Event konnte nicht abonniert werden.", LogLevel.Error); }

            statusUpdateTimer.Interval = TimeSpan.FromSeconds(1);
            statusUpdateTimer.Tick += (s, e) => UpdateStatusTextBlocks();
            statusUpdateTimer.Start();
            Logger.Instance.Log("Initialisiert");

            WebsiteBlockingCheckBox.IsChecked = _configReader.GetWebsiteBlockingEnabled();
            AppBlockingCheckBox.IsChecked = _configReader.GetAppBlockingEnabled();

            WebsitesTab.IsEnabled = _configReader.GetWebsiteBlockingEnabled();
            ProzesseTab.IsEnabled = _configReader.GetAppBlockingEnabled();

            StartTimerAtStartupCheckBox.IsChecked = _configReader.GetStartTimerAtStartup();
            ShowTimerInOverlay.IsChecked = _configReader.GetShowTimeInOverlayEnable();

            Closing += MainWindow_Closing;

            _notifyIcon = new NotifyIcon();
            try
            {
                _notifyIcon.Icon = new System.Drawing.Icon("app.ico");
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Fehler beim Laden des Tray-Icons: " + ex.Message, LogLevel.Error);
                // Optional: Fallback-Icon oder kein Icon setzen
            }
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "ScreenZen läuft im Hintergrund";
            _notifyIcon.DoubleClick += (s, e) =>
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
            };

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add("Öffnen", null, (s, e) => { this.Show(); this.WindowState = WindowState.Normal; });
            contextMenu.Items.Add("Beenden", null, (s, e) =>
            {
                _closeToTray = false;
                this.Close();
                System.Windows.Application.Current.Shutdown(); // Anwendung wirklich beenden
            });
            _notifyIcon.ContextMenuStrip = contextMenu;

            this.StateChanged += (s, e) =>
            {
                if (WindowState == WindowState.Minimized)
                {
                    this.Hide();
                    _notifyIcon.BalloonTipTitle = "ScreenZen";
                    _notifyIcon.BalloonTipText = "ScreenZen läuft im Hintergrund.";
                    _notifyIcon.ShowBalloonTip(1000);
                }
            };
        }

        // -------------------- Status & UI --------------------

        private void OnStatusChanged(string newStatus)
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                if (PauseStatusTextBlock != null)
                    PauseStatusTextBlock.Text = newStatus ?? "";
            }));
        }

        private void UpdateStatusTextBlocks()
        {
            if (_timeManagement == null || _webManager == null)
                return;

            // Free-Timer
            if (FreeTimerStatusTextBlock != null)
            {
                if (_timeManagement.IsWorkTimerRunning())
                {
                    FreeTimerStatusTextBlock.Text = "Free-Timer aktiv";
                    FreeTimerStatusTextBlock.Foreground = Brushes.Green;
                }
                else
                {
                    FreeTimerStatusTextBlock.Text = "Free-Timer inaktiv";
                    FreeTimerStatusTextBlock.Foreground = Brushes.Gray;
                }
            }

            // Break-Timer
            if (BreakTimerStatusTextBlock != null)
            {
                if (_timeManagement.IsBreakTimerRunning())
                {
                    BreakTimerStatusTextBlock.Text = "Break-Timer aktiv";
                    BreakTimerStatusTextBlock.Foreground = Brushes.Green;
                }
                else
                {
                    BreakTimerStatusTextBlock.Text = "Break-Timer inaktiv";
                    BreakTimerStatusTextBlock.Foreground = Brushes.Gray;
                }
            }

            // Check-Timer
            if (CheckTimerStatusTextBlock != null)
            {
                if (_timeManagement.IsCheckTimerRunning())
                {
                    CheckTimerStatusTextBlock.Text = "Check-Timer aktiv";
                    CheckTimerStatusTextBlock.Foreground = Brushes.Green;
                }
                else
                {
                    CheckTimerStatusTextBlock.Text = "Check-Timer inaktiv";
                    CheckTimerStatusTextBlock.Foreground = Brushes.Gray;
                }
            }

            // Timer-Status
            if (TimerStatusTextBlock != null)
            {
                TimeSpan? remaining = null;
                string timerName = "";

                if (_timeManagement.IsWorkTimerRunning())
                {
                    remaining = _timeManagement.GetRemainingWorkTime();
                    timerName = "Free-Timer";
                }
                else if (_timeManagement.IsBreakTimerRunning())
                {
                    remaining = _timeManagement.GetRemainingBreakTime();
                    timerName = "Break-Timer";
                }
                else if (_timeManagement.IsCheckTimerRunning())
                {
                    remaining = _timeManagement.GetRemainingCheckTime();
                    timerName = "Check-Timer";
                }

                if (remaining.HasValue && remaining.Value.TotalSeconds > 0)
                {
                    TimerStatusTextBlock.Text = $"{timerName}: {remaining.Value.Hours:D2}:{remaining.Value.Minutes:D2}:{remaining.Value.Seconds:D2} verbleibend";
                }
                else
                {
                    TimerStatusTextBlock.Text = "Timer: --:--";
                }
            }

            // Pause-Status
            if (PauseStatusTextBlock != null)
            {
                if (_timeManagement.IsBreakActive())
                {
                    PauseStatusTextBlock.Text = "Pause aktiv";
                    PauseStatusTextBlock.Foreground = Brushes.Green;
                }
                else
                {
                    PauseStatusTextBlock.Text = "Momentan keine Pause";
                    PauseStatusTextBlock.Foreground = Brushes.Black;
                }
            }

            // Proxy-Status
            if (ProxyStatusTextBlock != null)
            {
                if (_webManager.IsProxyRunning)
                {
                    ProxyStatusTextBlock.Text = "Proxy aktiv";
                    ProxyStatusTextBlock.Foreground = Brushes.Green;
                }
                else
                {
                    ProxyStatusTextBlock.Text = "Proxy inaktiv";
                    ProxyStatusTextBlock.Foreground = Brushes.Red;
                }
            }

            // Debug-Status
            if (DebugStatusTextBlock != null)
            {
                if (_configReader.GetEnableDebugMode())
                {
                    DebugStatusTextBlock.Text = "Debug an";
                    DebugStatusTextBlock.Foreground = Brushes.Red;
                }
                else
                {
                    DebugStatusTextBlock.Text = "Debug aus";
                    DebugStatusTextBlock.Foreground = Brushes.Gray;
                }
            }
        }

        // -------------------- Gruppen-Tab --------------------

        private void LoadGroups()
        {
            if (_configReader == null)
            {
                Logger.Instance.Log("LoadGroups: ConfigReader ist null!", LogLevel.Error);
                return;
            }
            string allGroups = _configReader.GetAllGroups();
            GroupSelectionComboBox?.Items.Clear();

            if (!string.IsNullOrEmpty(allGroups))
            {
                var groups = allGroups.Split(new[] { ", " }, System.StringSplitOptions.None);
                foreach (var group in groups)
                {
                    GroupSelectionComboBox?.Items.Add(group);
                }
            }

            if (GroupSelectionComboBox != null)
            {
                GroupSelectionComboBox.SelectedItem = "Gruppe 1";
                GroupSelectionComboBox.SelectedIndex = GroupSelectionComboBox.Items.IndexOf("Gruppe 1");
            }
        }

        private void CreateGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (_configReader == null) return;
            _configReader.CreateGroup();
            LoadGroups();
        }

        private void DeleteGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (_configReader == null) return;
            string? groupNameToDelete = GroupSelectionComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(groupNameToDelete))
            {
                MessageBox.Show("Bitte wählen Sie eine Gruppe zum Löschen aus.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _configReader.DeleteGroup(groupNameToDelete);
            LoadGroups();
        }

        private void ActivateGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (_configReader == null) return;
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup))
            {
                MessageBox.Show("Bitte wählen Sie eine Gruppe zum Aktivieren aus.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _configReader.SetActiveStatus(selectedGroup, true);
            MessageBox.Show($"Die Gruppe '{selectedGroup}' wurde aktiviert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            UpdateGroupActivityTextBox();
        }

        private void DeactivateGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (_configReader == null) return;
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup))
            {
                MessageBox.Show("Bitte wählen Sie eine Gruppe zum Deaktivieren aus.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _configReader.SetActiveStatus(selectedGroup, false);
            MessageBox.Show($"Die Gruppe '{selectedGroup}' wurde deaktiviert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            UpdateGroupActivityTextBox();
        }

        private void GroupSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateGroupActivityTextBox();
        }

        private void UpdateGroupActivityTextBox()
        {
            if (_configReader == null) return;
            if (GroupSelectionComboBox?.SelectedItem is string selectedGroup && !string.IsNullOrEmpty(selectedGroup))
            {
                bool isActive = false;
                try
                {
                    isActive = _configReader.GetGroupActiveStatus(selectedGroup);
                }
                catch
                {
                    Logger.Instance.Log($"Fehler beim Lesen des Aktiv-Status für Gruppe '{selectedGroup}'", LogLevel.Error);
                }
                if (GroupActivityTextBox != null)
                    GroupActivityTextBox.Text = isActive ? "aktiv" : "nicht aktiv";
            }
            else
            {
                if (GroupActivityTextBox != null)
                    GroupActivityTextBox.Text = "";
            }
        }

        // -------------------- Prozesse-Tab --------------------

        private void SaveProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (_appManager == null) return;
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            string? selectedProcess = ProcessListBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup) || string.IsNullOrEmpty(selectedProcess)) return;
            _appManager.SaveSelectedProcessesToFile(selectedGroup, selectedProcess);
        }

        private void DeleteProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (_appManager == null) return;
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            string? selectedProcess = ProcessListBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup) || string.IsNullOrEmpty(selectedProcess)) return;
            _appManager.RemoveSelectedProcessesFromFile(selectedGroup, selectedProcess);
            ShowBlockedAppsButton_Click(sender, e);
        }

        private void ShowBlockedAppsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_configReader == null) return;
            string? groupID = GroupSelectionComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(groupID)) return;

            string apps = _configReader.GetAppsFromGroup(groupID);
            ProcessListBox?.Items.Clear();
            if (!string.IsNullOrEmpty(apps))
            {
                foreach (var app in apps.Split(", "))
                    ProcessListBox?.Items.Add(app);
            }
        }

        private void ShowRunningProcessesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_appManager == null) return;
            Process[] processes = _appManager.GetRunningProcesses();
            ProcessListBox?.Items.Clear();
            if (processes != null)
            {
                foreach (var process in processes)
                    ProcessListBox?.Items.Add($"{process.ProcessName} (ID: {process.Id})");
            }
        }

        // -------------------- Websites-Tab --------------------

        private void ShowBlockedWebsitesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_configReader == null) return;
            string? groupID = GroupSelectionComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(groupID)) return;

            string domains = _configReader.GetWebsitesFromGroup(groupID);
            WebsiteListBox?.Items.Clear();
            if (!string.IsNullOrEmpty(domains))
            {
                foreach (var domain in domains.Split(", "))
                    WebsiteListBox?.Items.Add(domain);
            }
        }

        private void SaveWebsiteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_configReader == null) return;
            string? websiteName = WebsiteInputTextBox?.Text?.Trim();
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup) || string.IsNullOrEmpty(websiteName)) return;
            _configReader.AddWebsiteToGroup(selectedGroup, websiteName);
            WebsiteInputTextBox?.Clear();
            ShowBlockedWebsitesButton_Click(sender, e);
        }

        private void DeleteWebsiteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_webManager == null) return;
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            string? selectedWebsite = WebsiteListBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup) || string.IsNullOrEmpty(selectedWebsite)) return;
            _webManager.RemoveSelectedWebsiteFromFile(selectedGroup, selectedWebsite);
            ShowBlockedWebsitesButton_Click(sender, e);
        }

        // -------------------- Timer-Tab --------------------

        private void ListTimers()
        {
            TimerTypeComboBox?.Items.Clear();
            TimerTypeComboBox?.Items.Add("Freizeit");
            TimerTypeComboBox?.Items.Add("Pause");
            TimerTypeComboBox?.Items.Add("Check");
            if (TimerTypeComboBox != null)
            {
                TimerTypeComboBox.SelectedIndex = 0; // Intervalltimer als Standard
                UpdateTimerDurationTextBox(); // Direkt beim Start anzeigen
            }
        }

        private void SetTimerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_timeManagement == null) return;
            string? selectedTimer = TimerTypeComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedTimer))
            {
                MessageBox.Show("Bitte wählen sie einen Timer aus.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                Logger.Instance.Log($"Kein Timer gewählt");
                return;
            }

            // Eingabe im Format hh:mm:ss parsen
            if (!TimeSpan.TryParse(TimerCurrentTimeMaskedBox?.Text, out var timeSpan))
            {
                MessageBox.Show("Bitte geben Sie die Zeit im Format hh:mm:ss ein.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                Logger.Instance.Log($"Ungültiges Zeitformat: '{TimerCurrentTimeMaskedBox?.Text}'");
                return;
            }
            int timeInSeconds = (int)timeSpan.TotalSeconds;

            switch (selectedTimer)
            {
                case "Freizeit":
                    _timeManagement.SetTimerTime("i", timeInSeconds);
                    break;
                case "Pause":
                    _timeManagement.SetTimerTime("p", timeInSeconds);
                    break;
                case "Check":
                    _timeManagement.SetTimerTime("c", timeInSeconds);
                    break;
                default:
                    Logger.Instance.Log($"Ungültiger Timer: '{selectedTimer}'");
                    break;
            }

            // Nach dem Setzen des Timers
            if (TimerDurationTextBox != null)
                TimerDurationTextBox.Text = timeSpan.ToString(@"hh\:mm\:ss");
        }

        private void StartTimerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_timeManagement == null) return;
            string? selectedTimer = TimerTypeComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedTimer))
            {
                MessageBox.Show("Bitte wählen sie einen Timer aus.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                Logger.Instance.Log($"Kein Timer gewählt");
                return;
            }
            switch (selectedTimer)
            {
                case "Timer Intervall":
                    _timeManagement.StartTimer("i");
                    break;
                case "Timer Pause":
                    _timeManagement.StartTimer("p");
                    break;
                case "Timer Check":
                    _timeManagement.StartTimer("c");
                    break;
                default:
                    Logger.Instance.Log($"Ungültiger Timer: '{selectedTimer}'");
                    break;
            }
        }

        private void StopTimerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_timeManagement == null) return;
            string? selectedTimer = TimerTypeComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedTimer))
            {
                MessageBox.Show("Bitte wählen sie einen Timer aus.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                Logger.Instance.Log($"Kein Timer gewählt");
                return;
            }
            switch (selectedTimer)
            {
                case "Timer Intervall":
                    _timeManagement.StopTimer("i");
                    break;
                case "Timer Pause":
                    _timeManagement.StopTimer("p");
                    break;
                case "Timer Check":
                    _timeManagement.StopTimer("c");
                    break;
                default:
                    Logger.Instance.Log($"Ungültiger Timer: '{selectedTimer}'");
                    break;
            }
        }

        private void TimerTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTimerDurationTextBox();
        }

        private void UpdateTimerDurationTextBox()
        {
            if (_timeManagement == null)
            {
                if (TimerDurationTextBox != null)
                    TimerDurationTextBox.Text = "Error: Timer management not initialized.";
                return;
            }

            if (TimerTypeComboBox?.SelectedItem is string selectedTimer)
            {
                int seconds = 0;
                switch (selectedTimer)
                {
                    case "Freizeit":
                        seconds = _timeManagement.GetIntervalFree();
                        break;
                    case "Pause":
                        seconds = _timeManagement.GetIntervalBreak();
                        break;
                    case "Check":
                        seconds = _timeManagement.GetIntervalCheck();
                        break;
                    default:
                        seconds = 0;
                        break;
                }
                if (TimerDurationTextBox != null)
                    TimerDurationTextBox.Text = TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
            }
        }

        private void StartTimerWithDuration()
        {
            int minutes = 0;
            if (int.TryParse(TimerDurationTextBox.Text, out minutes))
            {
                int durationInSeconds = minutes * 60;
                // Timer mit durationInSeconds starten
            }
        }

        // -------------------- Steuerung-Tab --------------------

        private void StartProxyButton_Click(object sender, RoutedEventArgs e)
        {
            _webManager?.StartProxy();
        }
        private void StopProxyButton_Click(object sender, RoutedEventArgs e)
        {
            _webManager?.StopProxy();
        }
        private void StopAllTimersButton_Click(object sender, RoutedEventArgs e)
        {
            _timeManagement?.Stop();
        }
        private void ForceBreakButton_Click(object sender, RoutedEventArgs e)
        {
            _timeManagement?.ForceBreak();
        }
        private void EndBreakButton_Click(object sender, RoutedEventArgs e)
        {
            _timeManagement?.EndBreak();
        }
        private void ToggleOverlayButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleOverlay();
        }
        private void DebugButton_Click(object sender, RoutedEventArgs e)
        {
            
            _configReader.SetEnableDebugMode(!_configReader.GetEnableDebugMode());
            _configReader.SaveConfig();

            if (_configReader.GetEnableDebugMode())
            {
                _timeManagement?.SetTimerTime("p", 45);
                _timeManagement?.SetTimerTime("i", 15);
                Logger.Instance.Log("Debug-Modus aktiviert: Pause = 45s, Intervall = 15s", LogLevel.Info);
            }
            UpdateStatusTextBlocks();
        }
        private void WebsiteBlockingCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _configReader.SetWebsiteBlockingEnabled(true);
            WebsitesTab.IsEnabled = true;
        }

        private void WebsiteBlockingCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _configReader.SetWebsiteBlockingEnabled(false);
            WebsitesTab.IsEnabled = false;
            // Optional: Proxy stoppen, falls aktiv
        }

        private void AppBlockingCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _configReader.SetAppBlockingEnabled(true);
            ProzesseTab.IsEnabled = true;
        }

        private void AppBlockingCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _configReader.SetAppBlockingEnabled(false);
            ProzesseTab.IsEnabled = false;
        }

        private void StartBlockingButton_Click(object sender, RoutedEventArgs e)
        {
            // Arbeitszeit-Timer starten (freie Zeit)
            _timeManagement?.StartTimer("i");
            Logger.Instance.Log("Blockier-/Pausenlogik wurde gestartet.", LogLevel.Info);
        }

        private void StartTimerAtStartupCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _configReader.SetStartTimerAtStartup(true);
        }

        private void StartTimerAtStartupCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _configReader.SetStartTimerAtStartup(false);

        }

        private void UpdateTimerIntervals(int freeMs, int breakMs, int checkMs)
        {
            _configReader.SetIntervalFreeMs(freeMs);
            _configReader.SetIntervalBreakMs(breakMs);
            _configReader.SetIntervalCheckMs(checkMs);
        }

        // -------------------- Overlay --------------------

        private bool isOvelay = false;
        public void ToggleOverlay()
        {
            if (_overlay == null) return;
            if (!isOvelay)
            {
                _overlay.Show();
                isOvelay = true;
            }
            else
            {
                _overlay.Hide();
                isOvelay = false;
            }
        }

        private void ShowTimerInOverlay_Checked(object sender, RoutedEventArgs e)
        {
            _configReader.SetShowTimeInOverlayEnable(true);
            _overlay.SetShowTimer(true);
        }

        private void ShowTimerInOverlay_Unchecked(object sender, RoutedEventArgs e)
        {
            _configReader.SetShowTimeInOverlayEnable(false);
            _overlay.SetShowTimer(false);
        }

        // -------------------- Hilfsmethoden --------------------

        public string CleanGroupName(string input)
        {
            if (input == null) return "";
            if (input.EndsWith(" ( True)", System.StringComparison.OrdinalIgnoreCase))
                return input.Substring(0, input.Length - 7).Trim();
            else if (input.EndsWith(" ( False)", System.StringComparison.OrdinalIgnoreCase))
                return input.Substring(0, input.Length - 8).Trim();
            else
            {
                Logger.Instance.Log($"Es wurde nichts entfernt von {input}");
                return input;
            }
        }

        public string? getConfig(string key, int num)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Config.json");
            try
            {
                if (!File.Exists(filePath))
                {
                    Logger.Instance.Log($"Die Datei '{filePath}' wurde nicht gefunden.");
                    return null; // Rückgabewert bleibt nullable
                }
                string jsonContent = File.ReadAllText(filePath);
                switch (key)
                {
                    case ("c"):
                        return jsonContent;
                    case ("a"):
                        return string.Empty; // Rückgabe eines leeren Strings statt null
                    default:
                        return string.Empty; // Rückgabe eines leeren Strings statt null
                }
            }
            catch (System.Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Speichern der Datei: {ex.Message}");
                return string.Empty; // Rückgabe eines leeren Strings statt null
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_closeToTray)
            {
                e.Cancel = true;
                this.Hide();
                _notifyIcon.BalloonTipTitle = "ScreenZen";
                _notifyIcon.BalloonTipText = "ScreenZen läuft weiter im Hintergrund.";
                _notifyIcon.ShowBalloonTip(1000);
            }
            else
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        }
    }
}
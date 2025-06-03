using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;
using Microsoft.Win32;
using System.Reflection;
using System.Windows.Interop;

namespace HecticEscape
{
    public partial class MainWindow : Window
    {
        private readonly TimeManagement _timeManagement;
        private readonly AppManager _appManager;
        private readonly WebManager _webManager;
        private readonly Overlay _overlay;
        private readonly ConfigReader _configReader;
        private readonly LanguageManager _languageManager;
        private readonly DispatcherTimer statusUpdateTimer = new();
        private NotifyIcon? _notifyIcon;
        private bool _closeToTray = true;
        private readonly UpdateManager _updateService = new UpdateManager();
        private readonly UpdateManager _updateManager;
        private bool _isInitialized = false;

        // Parameterloser Konstruktor (XAML)
        public MainWindow() : this(
            ResolveDependency<TimeManagement>("TimeManagement"),
            ResolveDependency<AppManager>("AppManager"),
            ResolveDependency<WebManager>("WebManager"),
            ResolveDependency<Overlay>("Overlay"),
            ResolveDependency<ConfigReader>("ConfigReader"),
            ResolveDependency<LanguageManager>("LanguageManager"),
            ResolveDependency<UpdateManager>("UpdateManager"))
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
            ConfigReader configReader,
            LanguageManager languageManager,
            UpdateManager updateManager)
        {
            Logger.Instance.Log("MainWindow DI-Konstruktor aufgerufen", LogLevel.Info);

            _timeManagement = timeManagement;
            _appManager = appManager;
            _webManager = webManager;
            _overlay = overlay;
            _configReader = configReader;
            _languageManager = languageManager;
            _updateManager = updateManager;

            InitializeComponent();
            Logger.Instance.Log("MainWindow initialisiert", LogLevel.Info);

            // Null-Checks für Abhängigkeiten
            if (_timeManagement == null)
                Logger.Instance.Log("TimeManagement ist null!", LogLevel.Error);
            if (_appManager == null)
                Logger.Instance.Log("AppManager ist null!", LogLevel.Error);
            if (_webManager == null)
                Logger.Instance.Log("WebManager ist null!", LogLevel.Error);
            if (_overlay == null)
                Logger.Instance.Log("Overlay ist null!", LogLevel.Error);
            if (_configReader == null)
                Logger.Instance.Log("ConfigReader ist null!", LogLevel.Error);
            if (_languageManager == null)
                Logger.Instance.Log("LanguageManager ist null!", LogLevel.Error);
            try
            {
                InitializeTexts();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler in InitializeTexts(): {ex.Message}", LogLevel.Error);
            }

            try
            {
                _timeManagement.StatusChanged += OnStatusChanged;
            }
            catch
            {
                Logger.Instance.Log("StatusChanged-Event konnte nicht abonniert werden.", LogLevel.Error);
            }

            try { LoadGroups(); }
            catch { Logger.Instance.Log("LoadGroups() fehlgeschlagen.", LogLevel.Error); }
            try { LoadLanguages(); }
            catch { Logger.Instance.Log("LoadLanguages() fehlgeschlagen.", LogLevel.Error); }
            try { GetCurrentLanguage(); }
            catch { Logger.Instance.Log("GetCurrentLanguage() fehlgeschlagen.", LogLevel.Error); }

            try { ListTimers(); }
            catch { Logger.Instance.Log("ListTimers() fehlgeschlagen.", LogLevel.Error); }

            try
            {
                _timeManagement.OverlayToggleRequested += ToggleEnableOverlay;
            }
            catch
            {
                Logger.Instance.Log("OverlayToggleRequested-Event konnte nicht abonniert werden.", LogLevel.Error);
            }

            statusUpdateTimer.Interval = TimeSpan.FromSeconds(1);
            statusUpdateTimer.Tick += (s, e) => UpdateStatusTextBlocks();
            statusUpdateTimer.Start();
            Logger.Instance.Log("Initialisiert");

            try
            {
                if (_configReader == null)
                {
                    Logger.Instance.Log("ConfigReader ist null!", LogLevel.Error);
                    return;
                }

                WebsiteBlockingCheckBox.IsChecked = _configReader.GetWebsiteBlockingEnabled();
                AppBlockingCheckBox.IsChecked = _configReader.GetAppBlockingEnabled();
                EnableStartOnWindowsStartupCheckBox.IsChecked = _configReader.GetEnableStartOnWindowsStartup();

                WebsitesTab.IsEnabled = _configReader.GetWebsiteBlockingEnabled();
                ProzesseTab.IsEnabled = _configReader.GetAppBlockingEnabled();

                StartTimerAtStartupCheckBox.IsChecked = _configReader.GetStartTimerAtStartup();
                ShowTimerInOverlay.IsChecked = _configReader.GetShowTimeInOverlayEnable();
                if(_overlay != null)
                    _overlay.SetShowTimer(_configReader.GetShowTimeInOverlayEnable());
                EnableUpdateCheckBox.IsChecked = _configReader.GetEnableUpdateCheck();
                EnableStartOnWindowsStartupCheckBox.IsChecked = _configReader.GetEnableStartOnWindowsStartup();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Fehler beim Laden der Konfiguration: " + ex.Message, LogLevel.Error);
            }

            Closing += MainWindow_Closing;
            this.StateChanged += (s, e) =>
            {
                if (WindowState == WindowState.Minimized && _notifyIcon != null)
                {
                    this.Hide();
                    _notifyIcon.BalloonTipTitle = "HecticEscape";
                    _notifyIcon.BalloonTipText = "HecticEscape läuft im Hintergrund.";
                    _notifyIcon.ShowBalloonTip(1000);
                }
            };
            if (_configReader != null)
            {
                ResetDailyTimeButton.Visibility = _configReader.GetEnableDebugMode() ? Visibility.Visible : Visibility.Collapsed;
            }
            try
            {
                Logger.Instance.Log("Prüfe auf Updates...", LogLevel.Info);
                CheckForUpdatesAndApply();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler bei der Update-Prüfung: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            Logger.Instance.Log("OnSourceInitialized wird ausgeführt", LogLevel.Info);
            
            InitializeNotifyIcon();
            var source = PresentationSource.FromVisual(this) as HwndSource;
            if (source != null)
            {
                source.AddHook(WndProc);
                Logger.Instance.Log("Windows Message Hook installiert", LogLevel.Info);
            }

            _isInitialized = true;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_TASKBARCREATED = 0x8000; // System Tray wurde erstellt
            
            if (msg == WM_TASKBARCREATED && _isInitialized)
            {
                // System Tray ist jetzt verfügbar
                InitializeNotifyIcon();
                handled = true;
            }
            
            return IntPtr.Zero;
        }

        private void InitializeNotifyIcon()
        {
            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }

                _notifyIcon = new NotifyIcon
                {
                    Visible = false,
                    Text = "HecticEscape läuft im Hintergrund"
                };

                try
                {
                    _notifyIcon.Icon = new System.Drawing.Icon("app.ico");
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Fehler beim Laden des Tray-Icons: {ex.Message}", LogLevel.Error);
                    return;
                }

                // Tray Settings
                _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;

                var contextMenu = new ContextMenuStrip();
                contextMenu.Items.Add("Öffnen", null, NotifyIcon_OpenClick);
                contextMenu.Items.Add("Beenden", null, NotifyIcon_ExitClick);
                _notifyIcon.ContextMenuStrip = contextMenu;

                _notifyIcon.Visible = true;

                Logger.Instance.Log("NotifyIcon erfolgreich initialisiert", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler bei NotifyIcon Initialisierung: {ex.Message}", LogLevel.Error);
            }
        }

        private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
        {
            ShowMainWindow();
        }

        private void NotifyIcon_OpenClick(object? sender, EventArgs e)
        {
            ShowMainWindow();
        }

        private void NotifyIcon_ExitClick(object? sender, EventArgs e)
        {
            _closeToTray = false;
            Close();
            System.Windows.Application.Current.Shutdown();
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private async void CheckForUpdatesAndApply()
        {
            if (!_configReader.GetEnableUpdateCheck())
            {
                Logger.Instance.Log("Update-Prüfung deaktiviert.", LogLevel.Info);
                return;
            }
            try
            {
                string? latestVersion = await _updateService.GetLatestVersionAsync();
                string currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";

                if (latestVersion != null && latestVersion != currentVersion)
                {
                    // Beispiel: Asset-Name und Zielpfad anpassen!
                    string assetName = "HecticEscapeInstaller.exe";
                    string downloadPath = Path.Combine(Path.GetTempPath(), assetName);

                    await _updateService.DownloadLatestReleaseAssetAsync(assetName, downloadPath);

                    MessageBox.Show("Update gefunden! Die Anwendung wird jetzt aktualisiert.", "Update", MessageBoxButton.OK, MessageBoxImage.Information);

                    _updateService.ApplyUpdate(downloadPath);
                }
                else
                {
                    MessageBox.Show("Keine Updates verfügbar.", "Update", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler bei der Update-Prüfung: {ex.Message}", "Update", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            // Verbose-Status
            if (VerboseStatusTextBlock != null)
            {
                if (_configReader.GetEnableVerboseMode())
                {
                    VerboseStatusTextBlock.Visibility = Visibility.Visible; // Sichtbar machen, wenn aktiv
                    VerboseStatusTextBlock.Text = "Verbose";
                    VerboseStatusTextBlock.Foreground = Brushes.Red;
                }
                else
                {
                    VerboseStatusTextBlock.Text = "";
                    VerboseStatusTextBlock.Visibility = Visibility.Collapsed; // Ausblenden, wenn nicht aktiv
                }
            }

            // Overlay-Status
            if (OverlayStatusTextBlock != null)
            {
                if (_configReader.GetEnableOverlay())
                {
                    OverlayStatusTextBlock.Text = "Overlay aktiv";
                    OverlayStatusTextBlock.Foreground = Brushes.Green;
                }
                else
                {
                    OverlayStatusTextBlock.Text = "Overlay inaktiv";
                    OverlayStatusTextBlock.Foreground = Brushes.Gray;
                }
            }
            UpdateDailyTimeLeftTextBox();
        }

        // -------------------- Sprachdatei --------------------

        private void InitializeTexts()
        {
            // --- Tab-Header ---
            TimerTab.Header = _languageManager.Get("Timer-Tab.Header");
            WebsitesTab.Header = _languageManager.Get("WebsitesTab.Header");
            ProzesseTab.Header = _languageManager.Get("ProzesseTab.Header");
            GruppenTab.Header = _languageManager.Get("GruppenTab.Header");
            SteuerungTab.Header = _languageManager.Get("SteuerungTab.Header");

            // --- Timer-Tab ---
            TimerSteuerungTextBox.Text = _languageManager.Get("Timer-Tab.TimerSteuerungTextBox");
            SetTimerButton.Content = _languageManager.Get("Timer-Tab.SetTimerButton");
            StartTimerButton.Content = _languageManager.Get("Timer-Tab.StartTimerButton");
            StopTimerButton.Content = _languageManager.Get("Timer-Tab.StopTimerButton");

            // --- Websites-Tab ---
            WebseitenVerwaltungTextBlock.Text = _languageManager.Get("WebsitesTab.WebseitenVerwaltungText");
            ShowBlockedWebsitesButton.Content = _languageManager.Get("WebsitesTab.ShowBlockedWebsitesButton");
            SaveWebsiteButton.Content = _languageManager.Get("WebsitesTab.SaveWebsiteButton");
            DeleteWebsiteButton.Content = _languageManager.Get("WebsitesTab.DeleteWebsiteButton");

            // --- Prozesse-Tab ---
            ProzessVerwaltungTextBlock.Text = _languageManager.Get("ProzesseTab.ProzessVerwaltungText");
            ShowBlockedAppsButton.Content = _languageManager.Get("ProzesseTab.ShowBlockedAppsButton");
            SaveProcessButton.Content = _languageManager.Get("ProzesseTab.SaveProcessButton");
            ShowRunningProcessesButton.Content = _languageManager.Get("ProzesseTab.ShowRunningProcessesButton");
            DeleteProcessButton.Content = _languageManager.Get("ProzesseTab.DeleteProcessButton");
            DailyTimesTextBlock.Text = _languageManager.Get("ProzesseTab.DailyTimesText");
            SaveDailyTimeButton.Content = _languageManager.Get("ProzesseTab.SaveDailyTimeButton");
            HeuteVerbliebenLabel.Text = _languageManager.Get("ProzesseTab.HeuteVerbliebenLabel");
            ResetDailyTimeButton.Content = _languageManager.Get("ProzesseTab.ResetDailyTimeButton");

            // --- Gruppen-Tab ---
            GruppenVerwaltungTextBlock.Text = _languageManager.Get("GruppenTab.GruppenVerwaltungText");
            CreateGroupButton.Content = _languageManager.Get("GruppenTab.CreateGroupButton");
            DeleteGroupButton.Content = _languageManager.Get("GruppenTab.DeleteGroupButton");
            ActivateGroupButton.Content = _languageManager.Get("GruppenTab.ActivateGroupButton");
            DeactivateGroupButton.Content = _languageManager.Get("GruppenTab.DeactivateGroupButton");

            // --- Steuerung-Tab ---
            AllgemeinTextBlock.Text = _languageManager.Get("SteuerungTab.AllgemeinText");
            StartBlockingButton.Content = _languageManager.Get("SteuerungTab.StartBlockingButton");
            DebugButton.Content = _languageManager.Get("SteuerungTab.DebugButton");
            VerboseButton.Content = _languageManager.Get("SteuerungTab.VerboseButton");
            ToggleOverlayButton.Content = _languageManager.Get("SteuerungTab.ToggleOverlayButton");

            TimerTextBlock.Text = _languageManager.Get("SteuerungTab.TimerText");
            StopAllTimersButton.Content = _languageManager.Get("SteuerungTab.StopAllTimersButton");
            ForceBreakButton.Content = _languageManager.Get("SteuerungTab.ForceBreakButton");
            EndBreakButton.Content = _languageManager.Get("SteuerungTab.EndBreakButton");
            StartTimerAtStartupCheckBox.Content = _languageManager.Get("SteuerungTab.StartTimerAtStartupCheckBox");
            ShowTimerInOverlay.Content = _languageManager.Get("SteuerungTab.ShowTimerInOverlay");
            EnableStartOnWindowsStartupCheckBox.Content = _languageManager.Get("SteuerungTab.EnableStartOnWindowsStartupCheckBox");

            ProzesseTextBlock.Text = _languageManager.Get("SteuerungTab.ProzesseText");
            AppBlockingCheckBox.Content = _languageManager.Get("SteuerungTab.AppBlockingCheckBox");

            WebTextBlock.Text = _languageManager.Get("SteuerungTab.WebText");
            StartProxyButton.Content = _languageManager.Get("SteuerungTab.StartProxyButton");
            StopProxyButton.Content = _languageManager.Get("SteuerungTab.StopProxyButton");
            WebsiteBlockingCheckBox.Content = _languageManager.Get("SteuerungTab.WebsiteBlockingCheckBox");

            LanguageTextBlock.Text = _languageManager.Get("SteuerungTab.LanguageText");
            ChangeLanguageButton.Content = _languageManager.Get("SteuerungTab.ChangeLanguageButton");
            AktuelleSpracheLabel.Text = _languageManager.Get("SteuerungTab.AktuelleSpracheLabel");

            // --- StatusBar ---
            ProxyStatusTextBlock.Text = _languageManager.Get("StatusBar.ProxyStatusTextBlock");
            PauseStatusTextBlock.Text = _languageManager.Get("StatusBar.PauseStatusTextBlock");
            FreeTimerStatusTextBlock.Text = _languageManager.Get("StatusBar.FreeTimerStatusTextBlock");
            BreakTimerStatusTextBlock.Text = _languageManager.Get("StatusBar.BreakTimerStatusTextBlock");
            CheckTimerStatusTextBlock.Text = _languageManager.Get("StatusBar.CheckTimerStatusTextBlock");
            TimerStatusTextBlock.Text = _languageManager.Get("StatusBar.TimerStatusTextBlock");

            OverlayStatusTextBlock.Text = _languageManager.Get("StatusBar.OverlayStatusTextBlock");
            DebugStatusTextBlock.Text = _languageManager.Get("StatusBar.DebugStatusTextBlock");
            VerboseStatusTextBlock.Text = _languageManager.Get("StatusBar.VerboseStatusTextBlock");
        }


        // -------------------- Gruppen-Tab --------------------

        private void LoadGroups()
        {
            if (_configReader == null)
            {
                Logger.Instance.Log("LoadGroups: ConfigReader ist null!", LogLevel.Error);
                return;
            }
            List<String> allGroups = _configReader.GetAllGroupNamesString();
            GroupSelectionComboBox?.Items.Clear();

            if (allGroups != null)
            {
                foreach (var group in allGroups)
                {
                    if (!string.IsNullOrEmpty(group))
                    {
                        GroupSelectionComboBox?.Items.Add(group);
                    }
                }
            }
            else
            {
                Logger.Instance.Log("LoadGroups: Keine Gruppen gefunden.", LogLevel.Warn);
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
            if (string.IsNullOrEmpty(groupNameToDelete)) return;
            Gruppe? group = _configReader.GetGroupByName(groupNameToDelete);
            if (group == null) return;
            _configReader.DeleteGroup(group);
            LoadGroups();
        }

        private void ActivateGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (_configReader == null) return;
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup)) return;
            Gruppe? group = _configReader.GetGroupByName(selectedGroup);
            if (group == null) return;
            _configReader.SetGroupActiveStatus(group, true);
            UpdateGroupActivityTextBox();
        }

        private void DeactivateGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (_configReader == null) return;
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup)) return;
            Gruppe? group = _configReader.GetGroupByName(selectedGroup);
            if (group == null) return;
            _configReader.SetGroupActiveStatus(group, false);
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
                    Gruppe? group = _configReader.GetGroupByName(selectedGroup);
                    if (group == null)
                    {
                        Logger.Instance.Log($"Gruppe '{selectedGroup}' nicht gefunden.", LogLevel.Warn);
                        return;
                    }
                    isActive = _configReader.GetGroupActiveStatus(group);
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
            Gruppe? group = _configReader.GetGroupByName(selectedGroup);
            if (group == null) return;
            _appManager.SaveSelectedProcessesToFile(group, selectedProcess);
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

            Gruppe? group = _configReader.GetGroupByName(groupID);
            if (group == null) return;
            List<string> apps = _configReader.GetAppNamesFromGroupString(group);
            ProcessListBox?.Items.Clear();
            foreach (var app in apps)
            {
                if (!string.IsNullOrEmpty(app))
                {
                    ProcessListBox?.Items.Add(app);
                }
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

        private void SaveDailyTimeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_appManager == null) return;
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            string? selectedProcess = ProcessListBox?.SelectedItem as string;
            string? dailyTimeMs = DailyTimeTextBox?.Text?.Trim();
            if (string.IsNullOrEmpty(selectedGroup) || string.IsNullOrEmpty(selectedProcess) || string.IsNullOrEmpty(dailyTimeMs)) return;

            // Eingabe im Format hh:mm:ss parsen
            if (!TimeSpan.TryParse(DailyTimeMaskedBox?.Text, out var timeSpan))
            {
                MessageBox.Show($"{_languageManager.Get("ErrorMessages.InvalidTimeFormat")}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                Logger.Instance.Log($"Ungültiges Zeitformat: '{DailyTimeMaskedBox?.Text}'");
                return;
            }
            Logger.Instance.Log($"Tägliche Zeit für App'{selectedProcess}' in Gruppe '{selectedGroup}' wird auf {dailyTimeMs} gesetzt.", LogLevel.Info);

            int timeInSeconds = (int)timeSpan.TotalSeconds;
            // Umwandlung in Millisekunden
            long dailyTimeMsValue = timeInSeconds * 1000; // Umwandlung in Millisekunden

            if (selectedGroup == null || selectedProcess == null) return;
            Gruppe? group = _configReader.GetGroupByName(selectedGroup);
            if (group == null) return;
            AppHE app = _configReader.GetAppFromGroup(group, selectedProcess);

            _appManager.SetDailyTimeMs(group, app, dailyTimeMsValue);
            UpdateDailyTimeTextBox();
        }

        private void ProcessListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDailyTimeTextBox();
            UpdateDailyTimeLeftTextBox();
        }

        private void UpdateDailyTimeTextBox()
        {
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            string? selectedProcess = ProcessListBox?.SelectedItem as string;
            if (_appManager == null || (string.IsNullOrEmpty(selectedGroup)) || (string.IsNullOrEmpty(selectedProcess))) return;
            Gruppe? group = _configReader.GetGroupByName(selectedGroup);
            if (group == null) return;
            AppHE? app = _configReader.GetAppFromGroup(group, selectedProcess);
            if (app == null)
            {
                if (DailyTimeTextBox != null)
                    DailyTimeTextBox.Text = "00:00:00"; // Standardwert, falls keine Zeit gesetzt ist
                return;
            }
            string? dailyTimeMs = _appManager.GetDailyTimeMs(group, app).ToString();
            if (string.IsNullOrEmpty(dailyTimeMs))
            {
                if (DailyTimeTextBox != null)
                    DailyTimeTextBox.Text = "00:00:00"; // Standardwert, falls keine Zeit gesetzt ist
                return;
            }
            if (DailyTimeTextBox != null)
            {
                long timeMs = long.Parse(dailyTimeMs);
                DailyTimeTextBox.Text = TimeSpan.FromMilliseconds(timeMs).ToString(@"hh\:mm\:ss");
            }
        }

        private void UpdateDailyTimeLeftTextBox()
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.Now);
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            string? selectedProcess = ProcessListBox?.SelectedItem as string;
            if (_appManager == null || (string.IsNullOrEmpty(selectedGroup)) || (string.IsNullOrEmpty(selectedProcess))) return;
            Gruppe? group = _configReader.GetGroupByName(selectedGroup);
            if (group == null) return;
            AppHE? app = _configReader.GetAppFromGroup(group, selectedProcess);
            if (app == null)
            {
                if (DailyTimeLeftTextBox != null)
                    DailyTimeLeftTextBox.Text = "00:00:00"; // Standardwert, falls keine Zeit gesetzt ist
                return;
            }
            long timeLeftMs = _appManager.GetDailyTimeLeft(group, app, today);
            if (DailyTimeLeftTextBox != null)
            {
                DailyTimeLeftTextBox.Text = TimeSpan.FromMilliseconds(timeLeftMs).ToString(@"hh\:mm\:ss");
            }

        }

        private void ResetDailyTimeButton_Click(object sender, EventArgs e)
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.Now);
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            string? selectedProcess = ProcessListBox?.SelectedItem as string;
            if (_appManager == null || (string.IsNullOrEmpty(selectedGroup)) || (string.IsNullOrEmpty(selectedProcess))) return;
            Gruppe? group = _configReader.GetGroupByName(selectedGroup);
            if (group == null) return;
            AppHE? app = _configReader.GetAppFromGroup(group, selectedProcess);
            if (app == null) return;
            _appManager.SetDaílyTimeMs(group, app, today, 0);
            UpdateDailyTimeTextBox();
            UpdateDailyTimeLeftTextBox();
        }

        private void ProcessTabOpend(object sender, RoutedEventArgs e)
        {
            // Beim Öffnen des Tabs die Liste der Prozesse aktualisieren
            ShowBlockedAppsButton_Click(sender, e);
        }

        // -------------------- Websites-Tab --------------------

        private void ShowBlockedWebsitesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_configReader == null) return;
            string? groupID = GroupSelectionComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(groupID)) return;

            Gruppe? group = _configReader.GetGroupByName(groupID);
            if (group == null) return;
            List<string> domains = _configReader.GetWebsiteNamesFromGroupString(group);
            WebsiteListBox?.Items.Clear();
            if (domains.Count > 0)
            {
                foreach (var domain in domains)
                {
                    if (!string.IsNullOrEmpty(domain))
                    {
                        WebsiteListBox?.Items.Add(domain);
                    }
                }
            }
        }

        private void SaveWebsiteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_configReader == null) return;
            string? websiteName = WebsiteInputTextBox?.Text?.Trim();
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup) || string.IsNullOrEmpty(websiteName)) return;
            Gruppe? group = _configReader.GetGroupByName(selectedGroup);
            if (group == null) return;
            Website? website = _configReader.GetWebsiteFromGroup(group, websiteName);
            if (website != null) return;
            _configReader.AddWebsiteToGroup(group, website);
            WebsiteInputTextBox?.Clear();
            ShowBlockedWebsitesButton_Click(sender, e);
        }

        private void DeleteWebsiteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_webManager == null) return;
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            string? selectedWebsite = WebsiteListBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup) || string.IsNullOrEmpty(selectedWebsite)) return;
            Gruppe? group = _configReader.GetGroupByName(selectedGroup);
            if (group == null) return;
            Website? website = _configReader.GetWebsiteFromGroup(group, selectedWebsite);
            if (website == null) return;
            _webManager.RemoveSelectedWebsiteFromFile(group, website);
            ShowBlockedWebsitesButton_Click(sender, e);
        }

        private void WebsiteTabOpend(object sender, RoutedEventArgs e)
        {
            // Beim Öffnen des Tabs die Liste der Websites aktualisieren
            ShowBlockedWebsitesButton_Click(sender, e);
        }

        // -------------------- Timer-Tab --------------------

        private void ListTimers()
        {
            TimerTypeComboBox?.Items.Clear();
            TimerTypeComboBox?.Items.Add("Freizeit");
            TimerTypeComboBox?.Items.Add("Pause");
            if (_configReader.GetEnableDebugMode())
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
                MessageBox.Show($"{_languageManager.Get("ErrorMessages.NoTimerSelected")}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                Logger.Instance.Log($"Kein Timer gewählt");
                return;
            }

            // Eingabe im Format hh:mm:ss parsen
            if (!TimeSpan.TryParse(TimerCurrentTimeMaskedBox?.Text, out var timeSpan))
            {
                MessageBox.Show($"{_languageManager.Get("ErrorMessages.InvalidTimeFormat")}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show($"{_languageManager.Get("ErrorMessages.NoTimerSelected")}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show($"{_languageManager.Get("ErrorMessages.NoTimerSelected")}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            ToggleEnableOverlay();
        }
        private void DebugButton_Click(object sender, RoutedEventArgs e)
        {
            _configReader.SetEnableDebugMode(!_configReader.GetEnableDebugMode());

            if (_configReader.GetEnableDebugMode())
            {
                _timeManagement?.SetTimerTime("p", 45);
                _timeManagement?.SetTimerTime("i", 15);
                Logger.Instance.Log("Debug-Modus aktiviert: Pause = 45s, Intervall = 15s", LogLevel.Info);
            }
            UpdateStatusTextBlocks();
            ListTimers();
            ResetDailyTimeButton.Visibility = _configReader.GetEnableDebugMode() ? Visibility.Visible : Visibility.Collapsed;
        }

        private void VerboseButton_Click(object sender, RoutedEventArgs e)
        {
            _configReader.SetEnablVerbosetMode(!_configReader.GetEnableVerboseMode());
            _configReader.SaveConfig();
            UpdateStatusTextBlocks();
            Logger.Instance.Log($"Verbose {(_configReader.GetEnableVerboseMode() ? "aktiviert" : "deaktiviert")}", LogLevel.Info);
        }

        private void WebsiteBlockingCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            MessageBox.Show($"{_languageManager.Get("ErrorMessages.WebsiteBlockingMissing")}", $"{_languageManager.Get("Misc.Error")}", MessageBoxButton.OK, MessageBoxImage.Information);
            WebsiteBlockingCheckBox.IsChecked = false; // Checkbox deaktivieren
            return; // Temporär deaktiviert, bis Proxy implementiert ist
            Logger.Instance.Log("Website-Blocking aktiviert", LogLevel.Info);
            _configReader.SetWebsiteBlockingEnabled(true);
            WebsitesTab.IsEnabled = true;
        }

        private void WebsiteBlockingCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            return; // Temporär deaktiviert, bis Proxy implementiert ist
            Logger.Instance.Log("Website-Blocking deaktiviert", LogLevel.Info);
            _configReader.SetWebsiteBlockingEnabled(false);
            WebsitesTab.IsEnabled = false;
            // Optional: Proxy stoppen, falls aktiv
        }

        private void AppBlockingCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            Logger.Instance.Log("App-Blocking aktiviert", LogLevel.Info);
            _configReader.SetAppBlockingEnabled(true);
            ProzesseTab.IsEnabled = true;
        }

        private void AppBlockingCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            Logger.Instance.Log("App-Blocking deaktiviert", LogLevel.Info);
            _configReader.SetAppBlockingEnabled(false);
            ProzesseTab.IsEnabled = false;
        }

        private void StartBlockingButton_Click(object sender, RoutedEventArgs e)
        {
            // Arbeitszeit-Timer starten (freie Zeit)
            _timeManagement?.StartTimer("i");
            _timeManagement?.StartTimer("c");
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

        private void LanguageSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }
        private void GetCurrentLanguage()
        {
            if (_configReader == null) return;
            string? currentLanguage = _configReader.GetActiveLanguage();
            if (currentLanguage != null && LanguageSelectionCombobox != null)
            {
                AktuelleSpracheTextblock.Text = currentLanguage;
            }
            else
            {
                AktuelleSpracheTextblock.Text = "Error";
                Logger.Instance.Log("Aktuelle Sprache konnte nicht geladen werden.", LogLevel.Warn);
            }
        }

        private void LoadLanguages()
        {
            if (_configReader == null)
            {
                Logger.Instance.Log("LoadGroups: ConfigReader ist null!", LogLevel.Error);
                return;
            }
            List<LanguageData> allLanguages = _configReader.GetAllLanguages();
            LanguageSelectionCombobox?.Items.Clear();

            if (allLanguages != null)
            {
                foreach (var language in allLanguages)
                {
                    if (language != null)
                    {
                        LanguageSelectionCombobox?.Items.Add(language.Name);
                    }
                }
            }
            else
            {
                Logger.Instance.Log("LoadGroups: Keine Sprachen gefunden.", LogLevel.Warn);
            }
        }

        private void ChangeLanguageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_configReader == null) return;
            string? selectedLanguage = LanguageSelectionCombobox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedLanguage)) return;
            _configReader.SetActiveLanguage(selectedLanguage);
            MessageBox.Show($"{_languageManager.Get("ErrorMessages.LanguageChanged")}", $"{_languageManager.Get("ErrorMessages.LanguageChangedHeader")}", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void EnableUpdateCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _configReader.SetEnableUpdateCheck(true);
            Logger.Instance.Log("Update-Check aktiviert", LogLevel.Info);
        }

        private void EnableUpdateCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _configReader.SetEnableUpdateCheck(false);
            Logger.Instance.Log("Update-Check deaktiviert", LogLevel.Info);
        }

        private void EnableStartOnWindowsStartupCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _configReader.SetEnableStartOnWindowsStartup(true);
            Logger.Instance.Log("Autostart aktiviert", LogLevel.Info);
            StartOnWindowsStartup(true);
        }

        private void EnableStartOnWindowsStartupCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _configReader.SetEnableStartOnWindowsStartup(false);
            Logger.Instance.Log("Autostart deaktiviert", LogLevel.Info);
            StartOnWindowsStartup(false);
        }

        // -------------------- Overlay --------------------

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

        private void ToggleEnableOverlay()
        {
            if (_overlay == null) return;
            if (_configReader.GetEnableOverlay())
            {
                _overlay.Show();
                _overlay.DisableOverlay();
                _configReader.SetEnableOverlay(false);
                Logger.Instance.Log("Overlay deaktiviert", LogLevel.Info);
            }
            else
            {
                _overlay.Hide();
                _overlay.EnableOverlay();
                _configReader.SetEnableOverlay(true);
                Logger.Instance.Log("Overlay aktiviert", LogLevel.Info);
            }
            UpdateStatusTextBlocks();
        }

        // -------------------- Hilfsmethoden --------------------

        public void StartOnWindowsStartup(bool enable)
        {
            try
            {
                // Pfad zum PowerShell-Skript relativ zum Anwendungsverzeichnis
                string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Skripts", "SetAutostart.ps1");

                if (!File.Exists(scriptPath))
                {
                    Logger.Instance.Log($"PowerShell-Skript nicht gefunden: {scriptPath}", LogLevel.Error);
                    throw new FileNotFoundException("PowerShell-Skript nicht gefunden", scriptPath);
                }

                if (enable)
                {
                    // PowerShell-Prozess mit erhöhten Rechten starten
                    using (Process process = new Process())
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-ExecutionPolicy Bypass -NoProfile -File \"{scriptPath}\" -Enable $true -ApplicationPath \"{Assembly.GetExecutingAssembly().Location}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        };

                        process.StartInfo = startInfo;
                        process.Start();
                        process.WaitForExit();

                        if (process.ExitCode == 0)
                        {
                            Logger.Instance.Log("Anwendung erfolgreich zum Windows Autostart hinzugefügt", LogLevel.Info);
                        }
                        else
                        {
                            Logger.Instance.Log($"Fehler beim Ausführen des PowerShell-Skripts. Exit Code: {process.ExitCode}", LogLevel.Error);
                            throw new Exception($"PowerShell-Skript fehlgeschlagen mit Exit Code {process.ExitCode}");
                        }
                    }
                }
                else
                {
                    // Autostart-Eintrag entfernen
                    using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                        true))
                    {
                        if (key != null)
                        {
                            key.DeleteValue("HecticEscape", false);
                            Logger.Instance.Log("Anwendung aus Windows Autostart entfernt", LogLevel.Info);
                        }
                        else
                        {
                            Logger.Instance.Log("Registry-Schlüssel konnte nicht geöffnet werden", LogLevel.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Setzen des Autostarts: {ex.Message}", LogLevel.Error);
            }
        }

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
                Hide();
                
                if (_notifyIcon != null)
                {
                    _notifyIcon.BalloonTipTitle = "HecticEscape";
                    _notifyIcon.BalloonTipText = "HecticEscape läuft weiter im Hintergrund.";
                    _notifyIcon.ShowBalloonTip(1000);
                }
            }
            else if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
    }
}
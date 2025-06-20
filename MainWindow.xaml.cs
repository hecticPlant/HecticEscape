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
using System.Windows.Interop;
using MessageBox = System.Windows.MessageBox;

namespace HecticEscape
{
    public partial class MainWindow : Window
    {
        private readonly WindowManager _windowManager;
        private readonly LanguageManager _languageManager;
        private readonly DispatcherTimer _statusUpdateTimer = new();
        private NotifyIcon? _notifyIcon;
        private string _notifyIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
        private bool _closeToTray = true;
        private readonly UpdateManager _updateManager = new UpdateManager();
        private bool _isInitialized = false;
        private readonly HashSet<int> _alreadyAnnouncedMinutes = new();

        private static T ResolveDependency<T>(string dependencyName) where T : class
        {
            var dependency = (App.Current as App)?.Services.GetService<T>();
            if (dependency == null)
                throw new InvalidOperationException($"{dependencyName} darf nicht null sein.");
            return dependency;
        }

        // DI-Konstruktor
        public MainWindow(
            LanguageManager languageManager,
            UpdateManager updateManager)
        {
            Logger.Instance.Log("MainWindow Konstruktor: Start", LogLevel.Info);

            _languageManager = languageManager ?? throw new ArgumentNullException(nameof(languageManager));
            _updateManager = updateManager ?? throw new ArgumentNullException(nameof(updateManager));
            _windowManager = (App.Current as App)?.Services.GetRequiredService<WindowManager>();

            InitializeComponent();

            try { InitializeTexts(); } catch (Exception ex) { Logger.Instance.Log($"Fehler in InitializeTexts(): {ex.Message}", LogLevel.Error); }
            try { _windowManager.TimeManager.StatusChanged += OnStatusChanged; } catch { Logger.Instance.Log("StatusChanged-Event konnte nicht abonniert werden.", LogLevel.Error); }
            try { LoadGroups(); } catch { Logger.Instance.Log("LoadGroups() fehlgeschlagen.", LogLevel.Error); }
            try { LoadLanguages(); } catch { Logger.Instance.Log("LoadLanguages() fehlgeschlagen.", LogLevel.Error); }
            try { GetCurrentLanguage(); } catch { Logger.Instance.Log("GetCurrentLanguage() fehlgeschlagen.", LogLevel.Error); }
            try { ListTimers(); } catch { Logger.Instance.Log("ListTimers() fehlgeschlagen.", LogLevel.Error); }
            try { _windowManager.TimeManager.OverlayToggleRequested += ToggleEnableOverlay; } catch { Logger.Instance.Log("OverlayToggleRequested-Event konnte nicht abonniert werden.", LogLevel.Error); }

            _statusUpdateTimer.Interval = TimeSpan.FromSeconds(1);
            _statusUpdateTimer.Tick += (s, e) => UpdateStatusTextBlocks();
            _statusUpdateTimer.Start();
            Logger.Instance.Log("Initialisiert");

            try
            {
                WebsiteBlockingCheckBox.IsChecked = _windowManager.EnableWebsiteBlocking;
                AppBlockingCheckBox.IsChecked = _windowManager.EnableAppBlocking;
                EnableStartOnWindowsStartupCheckBox.IsChecked = _windowManager.EnableStartOnWindowsStartup;

                WebsitesTab.IsEnabled = _windowManager.EnableWebsiteBlocking;
                ProzesseTab.IsEnabled = _windowManager.EnableAppBlocking;

                StartTimerAtStartupCheckBox.IsChecked = _windowManager.StartTimerAtStartup;
                ShowTimerInOverlayCheckBox.IsChecked = _windowManager.EnableShowTimeInOverlay;
                ShowAppTimerInOverlayCheckBox.IsChecked = _windowManager.EnableShowAppTimeInOverlay;
                EnableUpdateCheckBox.IsChecked = _windowManager.EnableUpdateCheck;
                EnableStartOnWindowsStartupCheckBox.IsChecked = _windowManager.EnableStartOnWindowsStartup;
                ShowProcessesWithWindowOnlyCheckBox.IsChecked = _windowManager.EnableShowProcessesWithWindowOnly;
                //IncludeFoundGanesCheckBox.IsChecked = _windowManager.AppManager.EnableIncludeFoundGames;
                EnableGroupBlockingCheckBox.IsChecked = _windowManager.EnableGroupBlocking;
                ScanForNewAppsCheckBox.IsChecked = _windowManager.AppManager.EnableScanForNewApps;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Fehler beim Laden der Konfiguration: " + ex.Message, LogLevel.Error);
            }

            Closing += MainWindow_Closing;
            StateChanged += (s, e) =>
            {
                if (WindowState == WindowState.Minimized && _notifyIcon != null)
                {
                    Hide();
                    _notifyIcon.BalloonTipTitle = "HecticEscape";
                    _notifyIcon.BalloonTipText = "HecticEscape läuft im Hintergrund.";
                    _notifyIcon.ShowBalloonTip(1000);
                }
            };

            ResetDailyTimeButton.Visibility = _windowManager.EnableDebugMode ? Visibility.Visible : Visibility.Collapsed;
            ResetDailyTimeGroupButton.Visibility = _windowManager.EnableDebugMode ? Visibility.Visible : Visibility.Collapsed;  

            try
            {
                Logger.Instance.Log("Prüfe auf Updates...", LogLevel.Info);
                CheckForUpdatesAndApply();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler bei der Update-Prüfung: {ex.Message}", LogLevel.Error);
            }
            try
            {
                if (_windowManager != null && _windowManager.OverlayManager != null)
                {
                    _windowManager.OverlayManager.AttachToTimeManager(_windowManager.TimeManager);
                    _windowManager.TimeManager.TimerTicked += remaining =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            TimerStatusTextBlock.Text = remaining.TotalSeconds > 0
                                ? $"Timer: {remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2} verbleibend"
                                : "Timer: --:--";
                        });
                    };
                    _windowManager.TimeManager.NewProcessDetected += (processName) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _windowManager.ShowGroupSelectionWindow(processName);
                        });
                    };
                }
                else
                {
                    Logger.Instance.Log("WindowManager oder OverlayManager ist null.", LogLevel.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Initialisieren des WindowManagers: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
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
            const int WM_TASKBARCREATED = 0x8000;
            if (msg == WM_TASKBARCREATED && _isInitialized)
            {
                InitializeNotifyIcon();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void InitializeNotifyIcon()
        {
            Logger.Instance.Log("Initialisiere NotifyIcon...", LogLevel.Info);
            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
                else
                {
                    Logger.Instance.Log("NotifyIcon ist null, initialisiere neu.", LogLevel.Warn);
                }
                _notifyIcon = new NotifyIcon
                {
                    Visible = false
                };

                try
                {
                    _notifyIcon.Icon = new System.Drawing.Icon(_notifyIconPath);
                    Logger.Instance.Log($"Tray-Icon gesetzt: {_notifyIcon.Icon}", LogLevel.Verbose);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Fehler beim Laden des Tray-Icons: {ex.Message}", LogLevel.Error);
                    return;
                }

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
            _windowManager.ShowMainWindow();
        }

        private void NotifyIcon_OpenClick(object? sender, EventArgs e)
        {
            _windowManager.ShowMainWindow();
        }

        private void NotifyIcon_ExitClick(object? sender, EventArgs e)
        {
            _closeToTray = false;
            Close();
            _windowManager.AppManager.SaveConfig();
            System.Windows.Application.Current.Shutdown();
        }

        private async void CheckForUpdatesAndApply()
        {
            try
            {
                string? latestVersionStr = await _updateManager.GetLatestVersionAsync();
                string? currentVersionStr = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

                if (latestVersionStr != null && currentVersionStr != null)
                {
                    var latestVersion = new SemVersion(latestVersionStr);
                    var currentVersion = new SemVersion(currentVersionStr);

                    if (latestVersion.CompareTo(currentVersion) > 0)
                    {
                        Logger.Instance.Log($"Update verfügbar: {latestVersion} (aktuell: {currentVersion})", LogLevel.Info);
                        MessageBox.Show($"Es ist ein Update verfügbar: {latestVersion}", $"Update {currentVersion}",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        Logger.Instance.Log("Keine neuere Version verfügbar.", LogLevel.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler bei der Update-Prüfung: {ex.Message}", "Update", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Logger.Instance.Log($"Fehler bei der Update-Prüfung: {ex.Message}", LogLevel.Error);
            }
        }

        // -------------------- Status & UI --------------------
        private void OnStatusChanged(string newStatus)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (PauseStatusTextBlock != null)
                    PauseStatusTextBlock.Text = newStatus;
            }));
        }

        private void UpdateStatusTextBlocks()
        {
            if (_windowManager.TimeManager == null) return;

            // Free-Timer
            if (FreeTimerStatusTextBlock != null)
            {
                if (_windowManager.TimeManager.IsWorkTimerRunning())
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
                if (_windowManager.TimeManager.IsBreakTimerRunning())
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
                if (_windowManager.TimeManager.IsCheckTimerRunning())
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

            // Pause-Status
            if (PauseStatusTextBlock != null)
            {
                if (_windowManager.TimeManager.IsBreakActive())
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
                if (_windowManager.WebManager.IsProxyRunning)
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
                if (_windowManager.EnableDebugMode)
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
                if (_windowManager.EnableVerboseMode)
                {
                    VerboseStatusTextBlock.Visibility = Visibility.Visible;
                    VerboseStatusTextBlock.Text = "Verbose";
                    VerboseStatusTextBlock.Foreground = Brushes.Red;
                }
                else
                {
                    VerboseStatusTextBlock.Text = "";
                    VerboseStatusTextBlock.Visibility = Visibility.Collapsed;
                }
            }

            // Overlay-Status
            if (OverlayStatusTextBlock != null)
            {
                if (_windowManager.EnableOverlay)
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
            UpdateDailyTimeLeftGroupTextBox();
        }

        // -------------------- Sprachdatei --------------------
        private void InitializeTexts()
        {
            Logger.Instance.Log("Initialisiere Texte", LogLevel.Verbose);
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
            ShowTimerInOverlayCheckBox.Content = _languageManager.Get("SteuerungTab.ShowTimerInOverlay");
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
            var allGroups = _windowManager.GroupManager.GetAllGroupNames();
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
            _windowManager.GroupManager.CreateGroup();
            LoadGroups();
        }

        private void DeleteGroupButton_Click(object sender, RoutedEventArgs e)
        {
            string? groupNameToDelete = GroupSelectionComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(groupNameToDelete)) return;
            var group = _windowManager.GroupManager.GetGroupByName(groupNameToDelete);
            if (group == null || string.IsNullOrEmpty(group.Name)) return;
            _windowManager.GroupManager.DeleteGroup(group);
            LoadGroups();
        }

        private void ActivateGroupButton_Click(object sender, RoutedEventArgs e)
        {
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup)) return;
            var group = _windowManager.GroupManager.GetGroupByName(selectedGroup);
            if (group == null || string.IsNullOrEmpty(group.Name)) return;
            _windowManager.GroupManager.SetGroupActiveStatus(group, true);
            UpdateGroupActivityTextBox();
        }

        private void DeactivateGroupButton_Click(object sender, RoutedEventArgs e)
        {
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup)) return;
            var group = _windowManager.GroupManager.GetGroupByName(selectedGroup);
            if (group == null || string.IsNullOrEmpty(group.Name)) return;
            _windowManager.GroupManager.SetGroupActiveStatus(group, false);
            UpdateGroupActivityTextBox();
        }

        private void GroupSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateGroupActivityTextBox();
            UpdateDailyTimeGroupTextBox();
        }

        private void SaveDailyTimeGroupButton_Click(object sender, RoutedEventArgs e)
        {
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            string? dailyTimeMs = DailyTimeGroupTextBox?.Text?.Trim();
            if (string.IsNullOrEmpty(selectedGroup) || string.IsNullOrEmpty(dailyTimeMs)) return;

            if (!TimeSpan.TryParse(DailyTimeGroupMaskedBox?.Text, out var timeSpan))
            {
                MessageBox.Show($"{_languageManager.Get("ErrorMessages.InvalidTimeFormat")}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                Logger.Instance.Log($"Ungültiges Zeitformat: '{DailyTimeGroupMaskedBox?.Text}'");
                return;
            }
            Logger.Instance.Log($"Tägliche Zeit für Gruppe '{selectedGroup}' wird auf {dailyTimeMs} gesetzt.", LogLevel.Info);

            int timeInSeconds = (int)timeSpan.TotalSeconds;
            long dailyTimeMsValue = timeInSeconds * 1000;

            var group = _windowManager.GroupManager.GetGroupByName(selectedGroup);
            if (group == null) return;

            _windowManager.GroupManager.SetDailyTimeMs(group, dailyTimeMsValue);
            UpdateDailyTimeGroupTextBox();
        }

        private void UpdateDailyTimeGroupTextBox()
        {
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup)) return;
            var group = _windowManager.GroupManager.GetGroupByName(selectedGroup);
            if (group == null) return;
            long dailyTimeMs = group.DailyTimeMs;
            if (DailyTimeGroupTextBox != null)
            {
                DailyTimeGroupTextBox.Text = TimeSpan.FromMilliseconds(dailyTimeMs).ToString(@"hh\:mm\:ss");
            }
        }

        private void UpdateDailyTimeLeftGroupTextBox()
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.Now);
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup)) return;
            var group = _windowManager.GroupManager.GetGroupByName(selectedGroup);
            if (group == null) return;
            long timeLeftMs = _windowManager.GroupManager.GetDailyTimeLeft(group, today);
            if (DailyTimeLeftGroupTextBox != null)
            {
                DailyTimeLeftGroupTextBox.Text = TimeSpan.FromMilliseconds(timeLeftMs).ToString(@"hh\:mm\:ss");
            }
        }

        private void ResetDailyTimeGroupButton_Click(object sender, RoutedEventArgs e)
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.Now);
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup)) return;
            var group = _windowManager.GroupManager.GetGroupByName(selectedGroup);
            if (group == null) return;
            _windowManager.GroupManager.SetTimeMS(group, 0);
            UpdateDailyTimeGroupTextBox();
            UpdateDailyTimeLeftGroupTextBox();
            Logger.Instance.Log($"Tägliche Zeit für Gruppe '{group.Name}' wurde zurückgesetzt." +
                $"Neue Zeit:  {_windowManager.GroupManager.GetDailyTimeLeft(group, today)}", LogLevel.Debug);
        }

        private void EnableGroupBlockingCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableGroupBlocking(true);
        }

        private void EnableGroupBlockingCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableGroupBlocking(false);
        }

        private void UpdateGroupActivityTextBox()
        {
            if (GroupSelectionComboBox?.SelectedItem is string selectedGroup && !string.IsNullOrEmpty(selectedGroup))
            {
                try
                {
                    var group = _windowManager.GroupManager.GetGroupByName(selectedGroup);
                    if (group == null || string.IsNullOrEmpty(group.Name))
                    {
                        Logger.Instance.Log($"Gruppe '{selectedGroup}' nicht gefunden.", LogLevel.Warn);
                        if (GroupActivityTextBox != null)
                            GroupActivityTextBox.Text = "nicht verfügbar";
                        return;
                    }
                    bool isActive = group.Aktiv;
                    if (GroupActivityTextBox != null)
                        GroupActivityTextBox.Text = isActive ? "aktiv" : "nicht aktiv";
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Fehler beim Lesen des Aktiv-Status für Gruppe '{selectedGroup}': {ex.Message}", LogLevel.Error);
                    if (GroupActivityTextBox != null)
                        GroupActivityTextBox.Text = "Fehler";
                }
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
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            string? selectedProcess = ProcessListBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup) || string.IsNullOrEmpty(selectedProcess)) return;
            var group = _windowManager.GroupManager.GetGroupByName(selectedGroup);
            if (group == null || string.IsNullOrEmpty(group.Name)) return;
            _windowManager.AppManager.AddAppToGroup(group, selectedProcess);
        }

        private void DeleteProcessButton_Click(object sender, RoutedEventArgs e)
        {
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            string? selectedProcess = ProcessListBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup) || string.IsNullOrEmpty(selectedProcess)) return;
            var group = _windowManager.GroupManager.GetGroupByName(selectedGroup);
            var app = _windowManager.AppManager.GetAppByName(group, selectedProcess);
            _windowManager.AppManager.RemoveAppFromGroup(group, app);
            ShowBlockedAppsButton_Click(sender, e);
        }

        private void ShowBlockedAppsButton_Click(object sender, RoutedEventArgs e)
        {
            string? groupID = GroupSelectionComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(groupID)) return;

            var group = _windowManager.GroupManager.GetGroupByName(groupID);
            if (group == null || string.IsNullOrEmpty(group.Name))
            {
                Logger.Instance.Log($"Gruppe '{groupID}' nicht gefunden.", LogLevel.Warn);
                return;
            }

            var apps = _windowManager.AppManager.GetAppsFromGroup(group)
                .Select(a => a.Name)
                .ToList();

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
            var processes = _windowManager.AppManager.GetRunningProcesses();
            ProcessListBox?.Items.Clear();
            if (processes != null)
            {
                foreach (var process in processes)
                    ProcessListBox?.Items.Add($"{process.ProcessName} (ID: {process.Id})");
            }
        }

        private void SaveDailyTimeButton_Click(object sender, RoutedEventArgs e)
        {
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            string? selectedProcess = ProcessListBox?.SelectedItem as string;
            string? dailyTimeMs = DailyTimeTextBox?.Text?.Trim();
            if (string.IsNullOrEmpty(selectedGroup) || string.IsNullOrEmpty(selectedProcess) || string.IsNullOrEmpty(dailyTimeMs)) return;

            if (!TimeSpan.TryParse(DailyTimeMaskedBox?.Text, out var timeSpan))
            {
                MessageBox.Show($"{_languageManager.Get("ErrorMessages.InvalidTimeFormat")}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                Logger.Instance.Log($"Ungültiges Zeitformat: '{DailyTimeMaskedBox?.Text}'");
                return;
            }
            Logger.Instance.Log($"Tägliche Zeit für App'{selectedProcess}' in Gruppe '{selectedGroup}' wird auf {dailyTimeMs} gesetzt.", LogLevel.Info);

            int timeInSeconds = (int)timeSpan.TotalSeconds;
            long dailyTimeMsValue = timeInSeconds * 1000;

            var group = _windowManager.GroupManager.GetGroupByName(selectedGroup);
            if (group == null) return;
            var app = _windowManager.AppManager.GetAppByName(group, selectedProcess);

            _windowManager.AppManager.SetDailyTimeMs(group, app, dailyTimeMsValue);
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
            if (string.IsNullOrEmpty(selectedGroup) || string.IsNullOrEmpty(selectedProcess)) return;
            var group = _windowManager.GroupManager.GetGroupByName(selectedGroup);
            if (group == null) return;
            var app = _windowManager.AppManager.GetAppByName(group, selectedProcess);
            if (app == null)
            {
                if (DailyTimeTextBox != null)
                    DailyTimeTextBox.Text = "00:00:00";
                return;
            }
            long dailyTimeMs = app.DailyTimeMs;
            if (DailyTimeTextBox != null)
            {
                DailyTimeTextBox.Text = TimeSpan.FromMilliseconds(dailyTimeMs).ToString(@"hh\:mm\:ss");
            }
        }

        private void UpdateDailyTimeLeftTextBox()
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.Now);
            string? selectedGroup = GroupSelectionComboBox?.SelectedItem as string;
            string? selectedProcess = ProcessListBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup) || string.IsNullOrEmpty(selectedProcess)) return;
            var group = _windowManager.GroupManager.GetGroupByName(selectedGroup);
            if (group == null) return;
            var app = _windowManager.AppManager.GetAppByName(group, selectedProcess);
            if (app == null)
            {
                if (DailyTimeLeftTextBox != null)
                    DailyTimeLeftTextBox.Text = "00:00:00";
                return;
            }
            long timeLeftMs = _windowManager.AppManager.GetDailyTimeLeft(group, app, today);
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
            if (string.IsNullOrEmpty(selectedGroup) || string.IsNullOrEmpty(selectedProcess)) return;
            var group = _windowManager.GroupManager.GetGroupByName(selectedGroup);
            var app = _windowManager.AppManager.GetAppByName(group, selectedProcess);
            if (app == null) return;
            _windowManager.AppManager.SetTimeMS(group, app, 0);
            UpdateDailyTimeTextBox();
            UpdateDailyTimeLeftTextBox();
            Logger.Instance.Log($"Tägliche Zeit für App '{app.Name}' in Gruppe '{group.Name}' wurde zurückgesetzt." +
                $"Neue Zeit:  {_windowManager.AppManager.GetDailyTimeLeft(group, app, today)}", LogLevel.Debug);
        }

        private void ProcessTabOpend(object sender, RoutedEventArgs e)
        {
            ShowBlockedAppsButton_Click(sender, e);
        }

        private void ShowProcessesWithWindowOnlyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableShowProcessesWithWindowOnly(true);
        }
        private void ShowProcessesWithWindowOnlyCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableShowProcessesWithWindowOnly(false);
        }

        private void IncludeFoundGanesCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            IncludeFoundGames(true);


        }
        private void IncludeFoundGanesCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableIncludeFoundGames(false);
        }

        private void IncludeFoundGames(bool enable)
        {
            if (enable)
            {
                string? groupName = GroupSelectionComboBox?.SelectedItem as string;
                if (!string.IsNullOrEmpty(groupName))
                {
                    Gruppe? group = _windowManager.GroupManager.GetGroupByName(groupName);
                    if (group != null && !string.IsNullOrEmpty(group.Name))
                    {
                        _windowManager.AppManager.AddFoundGamesToConfig(group);
                    }
                    else
                    {
                        Logger.Instance.Log($"Gruppe '{groupName}' nicht gefunden.", LogLevel.Warn);
                    }
                }
            }
        }

        private void FindeGamesButton_Click(object sender, RoutedEventArgs e)
        {
            _windowManager.AppManager.GameManager.ScanAndSaveGames();
            IncludeFoundGames(true);
            ShowBlockedAppsButton_Click(sender, e);
        }

        // -------------------- Websites-Tab --------------------
        private void ShowBlockedWebsitesButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void SaveWebsiteButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void DeleteWebsiteButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void WebsiteTabOpend(object sender, RoutedEventArgs e)
        {
            ShowBlockedWebsitesButton_Click(sender, e);
        }

        // -------------------- Timer-Tab --------------------
        private void ListTimers()
        {
            TimerTypeComboBox?.Items.Clear();
            TimerTypeComboBox?.Items.Add("Freizeit");
            TimerTypeComboBox?.Items.Add("Pause");
            if (_windowManager.EnableDebugMode)
                TimerTypeComboBox?.Items.Add("Check");
            if (TimerTypeComboBox != null)
            {
                TimerTypeComboBox.SelectedIndex = 0;
                UpdateTimerDurationTextBox();
            }
        }

        private void SetTimerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_windowManager.TimeManager == null) return;
            string? selectedTimer = TimerTypeComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedTimer))
            {
                MessageBox.Show($"{_languageManager.Get("ErrorMessages.NoTimerSelected")}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                Logger.Instance.Log($"Kein Timer gewählt");
                return;
            }

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
                    _windowManager.TimeManager.SetTimerInterval(TimerType.Work, timeInSeconds);
                    break;
                case "Pause":
                    _windowManager.TimeManager.SetTimerInterval(TimerType.Break, timeInSeconds);
                    break;
                case "Check":
                    _windowManager.TimeManager.SetTimerInterval(TimerType.Check, timeInSeconds);
                    break;
                default:
                    Logger.Instance.Log($"Ungültiger Timer: '{selectedTimer}'");
                    break;
            }

            if (TimerDurationTextBox != null)
                TimerDurationTextBox.Text = timeSpan.ToString(@"hh\:mm\:ss");
        }

        private void StartTimerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_windowManager.TimeManager == null) return;
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
                    _windowManager.TimeManager.StartTimer(TimerType.Work);
                    break;
                case "Timer Pause":
                    _windowManager.TimeManager.StartTimer(TimerType.Break);
                    break;
                case "Timer Check":
                    _windowManager.TimeManager.StartTimer(TimerType.Check);
                    break;
                default:
                    Logger.Instance.Log($"Ungültiger Timer: '{selectedTimer}'");
                    break;
            }
        }

        private void StopTimerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_windowManager.TimeManager == null) return;
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
                    _windowManager.TimeManager.StopTimer(TimerType.Work);
                    break;
                case "Timer Pause":
                    _windowManager.TimeManager.StopTimer(TimerType.Break);
                    break;
                case "Timer Check":
                    _windowManager.TimeManager.StopTimer(TimerType.Check);
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
            if (_windowManager.TimeManager == null)
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
                        seconds = _windowManager.TimeManager.GetWorkIntervalSeconds();
                        break;
                    case "Pause":
                        seconds = _windowManager.TimeManager.GetBreakIntervalSeconds();
                        break;
                    case "Check":
                        seconds = _windowManager.TimeManager.GetCheckIntervalSeconds();
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
        }

        private void StopProxyButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void StopAllTimersButton_Click(object sender, RoutedEventArgs e)
        {
            _windowManager.TimeManager?.StopAllTimers();
        }

        private void ForceBreakButton_Click(object sender, RoutedEventArgs e)
        {
            _windowManager.TimeManager?.ForceBreak();
        }

        private void EndBreakButton_Click(object sender, RoutedEventArgs e)
        {
            _windowManager.TimeManager?.EndBreak();
        }

        private void ToggleOverlayButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleEnableOverlay();
            UpdateStatusTextBlocks();
        }

        private void DebugButton_Click(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableDebugMode(!_windowManager.EnableDebugMode);

            if (_windowManager.EnableDebugMode)
            {
                Logger.Instance.Log("Debug-Modus aktiviert.", LogLevel.Info);
            }
            UpdateStatusTextBlocks();
            ListTimers();
            ResetDailyTimeButton.Visibility = _windowManager.EnableDebugMode ? Visibility.Visible : Visibility.Collapsed;
        }

        private void VerboseButton_Click(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableVerboseMode(!_windowManager.EnableVerboseMode);
            UpdateStatusTextBlocks();
            Logger.Instance.Log($"Verbose {(_windowManager.EnableVerboseMode ? "aktiviert" : "deaktiviert")}", LogLevel.Info);
        }

        private void WebsiteBlockingCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            MessageBox.Show($"{_languageManager.Get("ErrorMessages.WebsiteBlockingMissing")}", $"{_languageManager.Get("Misc.Error")}", MessageBoxButton.OK, MessageBoxImage.Information);
            WebsiteBlockingCheckBox.IsChecked = false;
            return;
        }

        private void WebsiteBlockingCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            return;
        }

        private void AppBlockingCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableAppBlocking(true);
            ProzesseTab.IsEnabled = true;
        }

        private void AppBlockingCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableAppBlocking(false);
            ProzesseTab.IsEnabled = false;
        }

        private void StartBlockingButton_Click(object sender, RoutedEventArgs e)
        {
            _windowManager.TimeManager.StartTimer(TimerType.Work);
            _windowManager.TimeManager.StartTimer(TimerType.Check);
        }

        private void StartTimerAtStartupCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetStartTimerAtStartup(true);
        }

        private void StartTimerAtStartupCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetStartTimerAtStartup(false);
        }

        private void LanguageSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void GetCurrentLanguage()
        {
            string? currentLanguage = _windowManager.LanguageManager.GetCurrentLanguageString();
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
            var allLanguages = _windowManager.LanguageManager.GetAllLanguages();
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
            string? selectedLanguage = LanguageSelectionCombobox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedLanguage)) return;
            _windowManager.LanguageManager.SetCurrentLanguageString(selectedLanguage);
            MessageBox.Show($"{_languageManager.Get("ErrorMessages.LanguageChanged")}", $"{_languageManager.Get("ErrorMessages.LanguageChangedHeader")}", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void EnableUpdateCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableUpdateCheck(true);
        }

        private void EnableUpdateCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableUpdateCheck(false);
        }

        private async void EnableStartOnWindowsStartupCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableStartOnWindowsStartup(true);
            await StartOnWindowsStartupAsync(true);
        }

        private async void EnableStartOnWindowsStartupCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableStartOnWindowsStartup(false);
            await StartOnWindowsStartupAsync(false);
        }

        private void CustomizeButton_Click(object sender, RoutedEventArgs e)
        {
            _windowManager.ShowCustomizerWindow();
        }

        // -------------------- Overlay --------------------
        private void ShowTimerInOverlay_Checked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableShowTimeInOverlay(true);
        }

        private void ShowTimerInOverlay_Unchecked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableShowTimeInOverlay(false);
        }

        private void ShowAppTimerInOverlay_Checked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableShowAppTimeInOverlay(true);
        }
        private void ShowAppTimerInOverlay_Unchecked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableShowAppTimeInOverlay(false);
        }

        private void ToggleEnableOverlay()
        {
            _windowManager.OverlayManager.ToggleOverlayVisibility();
            UpdateStatusTextBlocks();
        }

        // -------------------- Hilfsmethoden --------------------
        public async Task StartOnWindowsStartupAsync(bool enable)
        {
            try
            {
                // Pfad zum Skript (relativ zum Programmverzeichnis)
                string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Skripts", "SetAutostart.ps1");

                if (!File.Exists(scriptPath))
                {
                    Logger.Instance.Log($"PowerShell-Skript nicht gefunden: {scriptPath}", LogLevel.Error);
                    throw new FileNotFoundException("PowerShell-Skript nicht gefunden", scriptPath);
                }

                if (enable)
                {
                    await Task.Run(() =>
                    {
                        string pwshPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                            "System32",
                            "WindowsPowerShell",
                            "v1.0",
                            "powershell.exe"
                        );

                        string appPath = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".exe");
                        string enableValue = enable ? "True" : "False";

                        var startInfo = new ProcessStartInfo
                        {
                            FileName = pwshPath,
                            Arguments = $"-ExecutionPolicy Bypass -NoProfile -File \"{scriptPath}\" -Enable {enableValue} -ApplicationPath \"{appPath}\" -Verbose",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };

                        using (var process = new Process { StartInfo = startInfo })
                        {
                            Logger.Instance.Log($"Starte PowerShell: \"{pwshPath}\" {startInfo.Arguments}", LogLevel.Debug);
                            process.Start();

                            string output = process.StandardOutput.ReadToEnd();
                            string error = process.StandardError.ReadToEnd();

                            process.WaitForExit();
                            if (!string.IsNullOrWhiteSpace(output))
                            {
                                Logger.Instance.Log($"PowerShell-Output:\n{output}", LogLevel.Info);
                            }

                            if (!string.IsNullOrWhiteSpace(error))
                            {
                                Logger.Instance.Log($"PowerShell-Error:\n{error}", LogLevel.Error);
                            }

                            if (process.ExitCode == 0)
                            {
                                Logger.Instance.Log("Anwendung erfolgreich zum Windows-Autostart hinzugefügt", LogLevel.Info);
                            }
                            else
                            {
                                Logger.Instance.Log($"PowerShell-Skript fehlgeschlagen. ExitCode: {process.ExitCode}", LogLevel.Error);
                                throw new Exception($"PowerShell-Skript fehlgeschlagen mit ExitCode {process.ExitCode}");
                            }
                        }
                    });
                }
                else
                {
                    await Task.Run(() =>
                    {
                        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                            writable: true))
                        {
                            if (key != null)
                            {
                                key.DeleteValue("HecticEscape", throwOnMissingValue: false);
                                Logger.Instance.Log("Anwendung aus Windows-Autostart entfernt", LogLevel.Info);
                            }
                            else
                            {
                                Logger.Instance.Log("Registry-Schlüssel konnte nicht geöffnet werden", LogLevel.Error);
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Setzen des Autostarts: {ex.Message}", LogLevel.Error);
            }
        }


        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_closeToTray)
            {
                try
                {
                    e.Cancel = true;
                    Hide();
                    if (_notifyIcon != null)
                    {
                        _notifyIcon.BalloonTipTitle = "HecticEscape";
                        _notifyIcon.ShowBalloonTip(1000);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log("Fehler beim Minimieren in den Systemtray: " + ex.Message, LogLevel.Warn);
                }
            }
            else
            {
                try
                {
                    _windowManager.OverlayManager?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log("Fehler beim Schließen des Overlays: " + ex.Message, LogLevel.Warn);
                }

                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }
            }
        }

        private void ScanForNewAppsCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableScanForNewApps(true);
        }
        private void ScanForNewAppsCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _windowManager.SetEnableScanForNewApps(false);
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using TimersTimer = System.Timers.Timer;

namespace HecticEscape
{
    /// <summary>
    /// Diese Klasse verwaltet das Laden und Speichern der Config.json und Lang.json.
    /// Sie enthält keine Geschäftslogik für Gruppen, Apps oder Websites.
    /// </summary>
    public class ConfigReader
    {
        public Config Config { get; private set; }
        public LanguageFile LanguageFile { get; private set; }
        public LanguageData CurrentLanguage { get; private set; }
        public ProcessFile ProcessFile { get; set; }

        public event Action? NewProcessFileCreated;

        private readonly string _filePathConfig = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HecticEscape",
            "Config.json");
        private readonly string _langFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Lang.json");
        private readonly string _processListFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HecticEscape",
            "ProcessList.json");

        private TimersTimer _saveConfigTimer = null!;
        private bool _saveConfigFlag = false;
        private bool _saveProcessListFlag = false;

        public ConfigReader()
        {
            Logger.Instance.Log("Starte Initialisierung des ConfigReaders.", LogLevel.Info);

            Directory.CreateDirectory(Path.GetDirectoryName(_filePathConfig)!);

            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HecticEscape");
            Directory.CreateDirectory(appDataPath);

            SetupConfig();
            LoadLanguages();
            SetupConfigTimer();
            LoadProcessList();

            Logger.Instance.Log("ConfigReader initialisiert.", LogLevel.Info);

        }
        private void SetupConfigTimer()
        {
            Logger.Instance.Log("Initialisiere SaveConfigTimer.", LogLevel.Info);
            _saveConfigTimer = new TimersTimer(500);
            _saveConfigTimer.Elapsed += (sender, e) =>
            {
                if (_saveConfigFlag)
                {
                    SaveConfig();
                    _saveConfigFlag = false;
                }
                if (_saveProcessListFlag)
                {
                    SaveProcessList();
                    _saveProcessListFlag = false;
                }
            };
            _saveConfigTimer.AutoReset = true;
            _saveConfigTimer.Start();
        }

        private void SetupConfig()
        {
            if (File.Exists(_filePathConfig))
            {
                Logger.Instance.Log($"{_filePathConfig} existiert.", LogLevel.Info);
                try
                {
                    string json = File.ReadAllText(_filePathConfig);
                    Logger.Instance.Log($"Config-Inhalt geladen: {json}", LogLevel.Info);
                    Config = JsonSerializer.Deserialize<Config>(json)
                              ?? new Config();

                    EnsureValidConfigStructure();

                    if (Config.IntervalFreeMs <= 0) Config.IntervalFreeMs = 2 * 3600 * 1000;
                    if (Config.IntervalBreakMs <= 0) Config.IntervalBreakMs = 15 * 60 * 1000;
                    if (Config.IntervalCheckMs <= 0) Config.IntervalCheckMs = 1000;

                    if (Config.Gruppen == null || Config.Gruppen.Count == 0)
                    {
                        Logger.Instance.Log("Ungültige oder leere Konfigurationsdatei. Eine neue Datei wird erstellt.", LogLevel.Warn);
                        CreateDefaultConfig();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Fehler beim Laden oder Deserialisieren der Datei: {ex.Message}", LogLevel.Error);
                    CreateDefaultConfig();
                }
            }
            else
            {
                Logger.Instance.Log("Datei nicht gefunden. Erstelle eine neue Konfiguration.", LogLevel.Warn);
                CreateDefaultConfig();
            }
            if (Config != null)
            {
                try
                {
                    Logger.Instance.IsDebugEnabled = Config.EnableDebugMode;
                    Logger.Instance.IsVerboseEnabled = Config.EnableVerboseMode;
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Fehler beim Setzen des Debug-Modus: {ex.Message}", LogLevel.Error);
                }
            }
        }

        private void EnsureValidConfigStructure()
        {
            if (Config.Gruppen == null)
            {
                Config.Gruppen = new Dictionary<string, Gruppe>();
            }

            foreach (var gruppe in Config.Gruppen.Values)
            {
                if (gruppe.Apps == null)
                {
                    gruppe.Apps = new List<AppHE>();
                }
                else
                {
                    foreach (var app in gruppe.Apps)
                    {
                        if (app.Logs == null)
                        {
                            app.Logs = new List<Log>();
                        }
                        if (app.Name == null)
                        {
                            app.Name = string.Empty;
                        }
                    }
                }

                if (gruppe.Websites == null)
                {
                    gruppe.Websites = new List<Website>();
                }
                else
                {
                    foreach (var site in gruppe.Websites)
                    {
                        if (site.Name == null)
                        {
                            site.Name = string.Empty;
                        }
                        if(site.Logs == null)
                        {
                            site.Logs = new List<Log>();
                        }
                    }
                }

                if (gruppe.Logs == null)
                {
                    gruppe.Logs = new List<Log>();
                }

                if (gruppe.Name == null)
                {
                    gruppe.Name = string.Empty;
                }
            }

            Config.ActiveLanguageNameString ??= string.Empty;

            Config.PauseTimerForegroundColorHex ??= "#FFFFFF";
            Config.PauseTimerBackgroundColorHex ??= "#000000";
            Config.PauseTimerForegroundOpacity = Clamp(Config.PauseTimerForegroundOpacity, 0.0, 1.0);
            Config.PauseTimerBackgroundOpacity = Clamp(Config.PauseTimerBackgroundOpacity, 0.0, 1.0);
            Config.PauseTimerStrokeThickness = ClampInt(Config.PauseTimerStrokeThickness, 0, 5);

            Config.AppTimerForegroundColorHex ??= "#FF0000";
            Config.AppTimerBackgroundColorHex ??= "#000000";
            Config.AppTimerForegroundOpacity = Clamp(Config.AppTimerForegroundOpacity, 0.0, 1.0);
            Config.AppTimerBackgroundOpacity = Clamp(Config.AppTimerBackgroundOpacity, 0.0, 1.0);
            Config.AppTimerStrokeThickness = ClampInt(Config.AppTimerStrokeThickness, 0, 5);

            Config.MessageForegroundColorHex ??= "#FFFFFF";
            Config.MessageBackgroundColorHex ??= "#000000";
            Config.MessageForegroundOpacity = Clamp(Config.MessageForegroundOpacity, 0.0, 1.0);
            Config.MessageBackgroundOpacity = Clamp(Config.MessageBackgroundOpacity, 0.0, 1.0);
        }

        /// <summary>
        /// Klemmt einen Wert zwischen min und max ein.
        /// </summary>
        private double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private int ClampInt(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private void LoadProcessList()
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HecticEscape");

            if (!File.Exists(_processListFilePath))
            {
                Logger.Instance.Log($"{_processListFilePath} nicht gefunden. Erstelle neue.", LogLevel.Warn);
                Directory.CreateDirectory(appDataPath);
                ProcessFile = new ProcessFile
                {
                    Prozesse = new Dictionary<string, ProcessData>()
                };

                string json = JsonSerializer.Serialize(ProcessFile, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_processListFilePath, json);
                Logger.Instance.Log("Leere Prozessliste erstellt.", LogLevel.Info);
                OnNewProcessFileCreated();
                return;
            }
            Logger.Instance.Log($"{_processListFilePath} gefunden. Lese Prozessliste ein.", LogLevel.Verbose);
            try
            {
                var json = File.ReadAllText(_processListFilePath);
                ProcessFile = JsonSerializer.Deserialize<ProcessFile>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                }) ?? new ProcessFile { Prozesse = new Dictionary<string, ProcessData>() };

                Logger.Instance.Log($"Prozessliste geladen: {string.Join(", ", ProcessFile.Prozesse.Keys)}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Laden der Prozessliste: {ex.Message}", LogLevel.Error);
            }
        }
        private void SaveProcessList()
        {
            try
            {
                if (ProcessFile == null)
                {
                    Logger.Instance.Log("Keine Prozessdaten zum Speichern vorhanden.", LogLevel.Warn);
                    return;
                }

                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HecticEscape");

                // Ordner zur Sicherheit nochmal erstellen, falls nicht vorhanden
                Directory.CreateDirectory(appDataPath);

                string json = JsonSerializer.Serialize(ProcessFile, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_processListFilePath, json);
                Logger.Instance.Log("Prozessliste gespeichert.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Speichern der Prozessliste: {ex.Message}", LogLevel.Error);
            }
        }
        private void LoadLanguages()
        {
            if (!File.Exists(_langFilePath))
            {
                Logger.Instance.Log($"{_langFilePath} nicht gefunden. Erstelle leeres Sprach-Setup.", LogLevel.Warn);
                LanguageFile = new LanguageFile { Sprachen = new Dictionary<string, LanguageData>() };
                CurrentLanguage = new LanguageData
                {
                    Name = "Deutsch",
                    Content = new Dictionary<string, MainWindowSection>
                    {
                        ["MainWindow"] = new MainWindowSection
                        {
                            TimerTab = new Dictionary<string, string>(),
                            WebsitesTab = new Dictionary<string, string>(),
                            ProzesseTab = new Dictionary<string, string>(),
                            GruppenTab = new Dictionary<string, string>(),
                            SteuerungTab = new Dictionary<string, string>(),
                            StatusBar = new Dictionary<string, string>(),
                            Overlay = new Dictionary<string, string>(),
                            ErrorMessages = new Dictionary<string, string>(),
                            Misc = new Dictionary<string, string>()
                        }
                    }
                };
                return;
            }
            Logger.Instance.Log($"{_langFilePath} gefunden. Lese Sprachdatei ein.", LogLevel.Info);
            var json = File.ReadAllText(_langFilePath);
            LanguageFile = JsonSerializer.Deserialize<LanguageFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) ?? new LanguageFile { Sprachen = new Dictionary<string, LanguageData>() };
            if (LanguageFile.Sprachen != null && LanguageFile.Sprachen.TryGetValue(GetCurrentLanguageString(), out var langData))
            {
                CurrentLanguage = langData;
                Logger.Instance.Log($"Aktive Sprache '{CurrentLanguage.Name}' geladen.", LogLevel.Info);
            }
            else
            {
                CurrentLanguage = LanguageFile.Sprachen?.Values.FirstOrDefault()
                                   ?? new LanguageData
                                   {
                                       Name = "Deutsch",
                                       Content = new Dictionary<string, MainWindowSection>()
                                   };
            }
            if (CurrentLanguage.Content == null || !CurrentLanguage.Content.ContainsKey("MainWindow"))
            {
                Logger.Instance.Log(
                    "LanguageData.Content enthält keinen Key \"MainWindow\" – lege leere Sektion an.",
                    LogLevel.Warn);

                CurrentLanguage.Content = new Dictionary<string, MainWindowSection>
                {
                    ["MainWindow"] = new MainWindowSection
                    {
                        TimerTab = new Dictionary<string, string>(),
                        WebsitesTab = new Dictionary<string, string>(),
                        ProzesseTab = new Dictionary<string, string>(),
                        GruppenTab = new Dictionary<string, string>(),
                        SteuerungTab = new Dictionary<string, string>(),
                        StatusBar = new Dictionary<string, string>()
                    }
                };
            }
        }

        private void CreateDefaultConfig()
        {
            Logger.Instance.Log("Erstelle Default-Konfiguration.", LogLevel.Info);

            Config = new Config
            {
                Gruppen = new Dictionary<string, Gruppe>(),
                EnableWebsiteBlocking = false,
                EnableAppBlocking = false,
                EnableDebugMode = false,
                EnableVerboseMode = false,
                EnableShowTimeInOverlay = false,
                EnableShowAppTimeInOverlay = false,
                StartTimerAtStartup = false,
                IntervalFreeMs = 2 * 3600 * 1000,
                IntervalBreakMs = 15 * 60 * 1000,
                IntervalCheckMs = 1000,
                EnableOverlay = true,
                ActiveLanguageNameString = "Deutsch",
                EnableUpdateCheck = false,
                EnableStartOnWindowsStartup = false,
                EnableShowProcessesWithWindowOnly = true,
                EnableIncludeFoundGames = false,
                EnableGroupBlocking = false,
                EnabbleScanForNewApps = true,
                PauseTimerForegroundColorHex = "#FFFFFFFF",
                PauseTimerBackgroundColorHex = "#00000000",
                PauseTimerForegroundOpacity = 1.0,
                PauseTimerBackgroundOpacity = 1.0,
                AppTimerForegroundColorHex = "#FF0000",
                AppTimerBackgroundColorHex = "#00000000",
                AppTimerForegroundOpacity = 1.0,
                AppTimerBackgroundOpacity = 1.0,
                MessageForegroundColorHex = "#FFFFFFFF",
                MessageBackgroundColorHex = "#00000000",
                MessageForegroundOpacity = 1.0,
                MessageBackgroundOpacity = 1.0,
            };

            var defaultGroup = new Gruppe
            {
                Name = "Gruppe 1",
                Aktiv = true,
                Apps = new List<AppHE>(),
                Websites = new List<Website>(),
                Logs = new List<Log>(),
                DailyTimeMs = 7200000,
            };
            Config.Gruppen.Add(defaultGroup.Name, defaultGroup);

            SaveConfig();

            Logger.Instance.Log("Eine neue Konfigurationsdatei mit Standard-Gruppe wurde erstellt.", LogLevel.Info);
            string json = File.ReadAllText(_filePathConfig);
            Logger.Instance.Log($"Neue Config-Inhalte: {json}", LogLevel.Info);
        }

        public void SetSaveConfigFlag()
        {
            if (!_saveConfigFlag)
            {
                _saveConfigFlag = true;
            }
            else
            {
                return;
            }
        }

        public void SetSaveProcessListFlag()
        {
            if (!_saveProcessListFlag)
            {
                _saveProcessListFlag = true;
            }
            else
            {
                return;
            }
        }

        public void SaveConfig()
        {
            try
            {
                string json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePathConfig, json);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Speichern der Konfiguration: {ex.Message}", LogLevel.Error);
            }
        }

        // --- Sprachverwaltung und globale Settings bleiben erhalten ---
        public LanguageData GetCurrentLanguage()
        {
            if (CurrentLanguage != null)
            {
                return CurrentLanguage;
            }
            Logger.Instance.Log("Keine aktuelle Sprache gesetzt.", LogLevel.Warn);
            return new LanguageData { Name = "ERROR:LANGUAGE" };
        }
        public string GetCurrentLanguageString()
        {
            if (!string.IsNullOrEmpty(Config.ActiveLanguageNameString))
            {
                return Config.ActiveLanguageNameString;
            }
            return "ERROR:LANGUAGE";
        }

        public void SetCurrentLanguage(LanguageData language)
        {
            if (language != null && LanguageFile.Sprachen != null && LanguageFile.Sprachen.ContainsKey(language.Name))
            {
                Config.ActiveLanguageNameString = language.Name;
                CurrentLanguage = language;
                Logger.Instance.Log($"Aktive Sprache auf '{language.Name}' gesetzt.", LogLevel.Info);
                SetSaveConfigFlag();
            }
            else
            {
                Logger.Instance.Log($"SetCurrentLanguage: Sprache '{language?.Name}' nicht gefunden.", LogLevel.Warn);
            }
        }
        public void SetCurrentLanguageString(string languageName)
        {
            if (LanguageFile.Sprachen != null && LanguageFile.Sprachen.ContainsKey(languageName))
            {
                Config.ActiveLanguageNameString = languageName;
                CurrentLanguage = LanguageFile.Sprachen[languageName];
                Logger.Instance.Log($"Aktive Sprache auf '{languageName}' gesetzt.", LogLevel.Info);
                SetSaveConfigFlag();
            }
            else
            {
                Logger.Instance.Log($"SetActiveLanguage: Sprache '{languageName}' nicht gefunden.", LogLevel.Warn);
            }
        }

        public List<LanguageData> GetAllLanguages()
        {
            if (LanguageFile.Sprachen != null)
            {
                return LanguageFile.Sprachen.Values.ToList();
            }
            Logger.Instance.Log("GetAllLanguages: Keine Sprachen gefunden.", LogLevel.Warn);
            return new List<LanguageData>();
        }
        public List<string> GetAllLanguageString()
        {
            if (LanguageFile.Sprachen != null)
            {
                var names = LanguageFile.Sprachen.Values.Select(l => l.Name).ToList();
                return names;
            }
            Logger.Instance.Log("GetAllLanguageNamesString: Keine Sprachen-Namen gefunden.", LogLevel.Warn);
            return new List<string>();
        }


        // --- Globale Einstellungen (Flags, Timer, etc.) ---
        public bool GetEnableShowProcessesWithWindowOnly()
        {
            return Config.EnableShowProcessesWithWindowOnly;
        }
        public void SetEnableShowProcessesWithWindowOnly(bool value)
        {
            Config.EnableShowProcessesWithWindowOnly = value;
            SetSaveConfigFlag();
        }

        public bool GetEnableIncludeFoundGames()
        {
            return Config.EnableIncludeFoundGames;
        }
        public void SetEnableIncludeFoundGames(bool value)
        {
            Config.EnableIncludeFoundGames = value;
            SetSaveConfigFlag();
        }


        public void SetEnableUpdateCheck(bool value)
        {
            Config.EnableUpdateCheck = value;
            SetSaveConfigFlag();
        }
        public bool GetEnableUpdateCheck()
        {
            return Config.EnableUpdateCheck;
        }

        public void SetEnableStartOnWindowsStartup(bool value)
        {
            Config.EnableStartOnWindowsStartup = value;
            SetSaveConfigFlag();
        }
        public bool GetEnableStartOnWindowsStartup()
        {
            return Config.EnableStartOnWindowsStartup;
        }
        public bool GetEnableVerboseMode()
        {
            return Config.EnableVerboseMode;
        }
        public void SetEnableVerboseMode(bool value)
        {
            Config.EnableVerboseMode = value;
            SetSaveConfigFlag();
        }

        public bool GetWebsiteBlockingEnabled()
        {
            return Config.EnableWebsiteBlocking;
        }
        public void SetWebsiteBlockingEnabled(bool enabled)
        {
            Config.EnableWebsiteBlocking = enabled;
            SetSaveConfigFlag();
        }

        public bool GetAppBlockingEnabled()
        {
            return Config.EnableAppBlocking;
        }
        public void SetEnableAppBlocking(bool enabled)
        {
            Config.EnableAppBlocking = enabled;
            SetSaveConfigFlag();
        }

        public bool GetStartTimerAtStartup()
        {
            return Config.StartTimerAtStartup;
        }
        public void SetStartTimerAtStartup(bool value)
        {
            Config.StartTimerAtStartup = value;
            SetSaveConfigFlag();
        }

        public int GetIntervalFreeMs()
        {
            return Config.IntervalFreeMs;
        }
        public void SetIntervalFreeMs(int value)
        {
            Config.IntervalFreeMs = value;
            SetSaveConfigFlag();
        }

        public int GetIntervalBreakMs()
        {
            return Config.IntervalBreakMs;
        }
        public void SetIntervalBreakMs(int value)
        {
            Config.IntervalBreakMs = value;
            SetSaveConfigFlag();
        }

        public int GetIntervalCheckMs()
        {
            return Config.IntervalCheckMs;
        }
        public void SetIntervalCheckMs(int value)
        {
            Config.IntervalCheckMs = value;
            SetSaveConfigFlag();
        }

        public bool GetEnableDebugMode()
        {
            return Config.EnableDebugMode;
        }
        public void SetEnableDebugMode(bool value)
        {
            Config.EnableDebugMode = value;
            SetSaveConfigFlag();
        }

        public bool GetShowTimeInOverlayEnable()
        {
            return Config.EnableShowTimeInOverlay;
        }
        public void EnableShowTimeInOverlay(bool value)
        {
            Config.EnableShowTimeInOverlay = value;
            SetSaveConfigFlag();
        }

        public bool GetShowAppTimeInOverlayEnable()
        {
            return Config.EnableShowAppTimeInOverlay;
        }
        public void SetShowAppTimeInOverlayEnable(bool value)
        {
            Config.EnableShowAppTimeInOverlay = value;
            SetSaveConfigFlag();
        }

        public bool GetEnableOverlay()
        {
            return Config.EnableOverlay;
        }
        public void SetEnableOverlay(bool value)
        {
            Config.EnableOverlay = value;
            SetSaveConfigFlag();
        }

        public bool GetEnableGroupBlocking()
        {
            return Config.EnableGroupBlocking;
        }
        public void SetEnableGroupBlocking(bool value)
        {
            Config.EnableGroupBlocking = value;
        }

        public string GetPauseTimerForegroundColorHex()
        {
            return Config.PauseTimerForegroundColorHex;
        }
        public void SetPauseTimerForegroundColorHex(string value)
        {
            Config.PauseTimerForegroundColorHex = value;
            SetSaveConfigFlag();
        }

        public string GetPauseTimerBackgroundColorHex()
        {
            return Config.PauseTimerBackgroundColorHex;
        }
        public void SetPauseTimerBackgroundColorHex(string value)
        {
            Config.PauseTimerBackgroundColorHex = value;
            SetSaveConfigFlag();
        }

        public double GetPauseTimerForegroundOpacity()
        {
            return Config.PauseTimerForegroundOpacity;
        }
        public void SetPauseTimerForegroundOpacity(double value)
        {
            Config.PauseTimerForegroundOpacity = value;
            SetSaveConfigFlag();
        }

        public double GetPauseTimerBackgroundOpacity()
        {
            return Config.PauseTimerBackgroundOpacity;
        }
        public void SetPauseTimerBackgroundOpacity(double value)
        {
            Config.PauseTimerBackgroundOpacity = value;
            SetSaveConfigFlag();
        }

        public string GetAppTimerForegroundColorHex()
        {
            return Config.AppTimerForegroundColorHex;
        }
        public void SetAppTimerForegroundColorHex(string value)
        {
            Config.AppTimerForegroundColorHex = value;
            SetSaveConfigFlag();
        }

        public string GetAppTimerBackgroundColorHex()
        {
            return Config.AppTimerBackgroundColorHex;
        }
        public void SetAppTimerBackgroundColorHex(string value)
        {
            Config.AppTimerBackgroundColorHex = value;
            SetSaveConfigFlag();
        }

        public double GetAppTimerForegroundOpacity()
        {
            return Config.AppTimerForegroundOpacity;
        }
        public void SetAppTimerForegroundOpacity(double value)
        {
            Config.AppTimerForegroundOpacity = value;
            SetSaveConfigFlag();
        }

        public double GetAppTimerBackgroundOpacity()
        {
            return Config.AppTimerBackgroundOpacity;
        }
        public void SetAppTimerBackgroundOpacity(double value)
        {
            Config.AppTimerBackgroundOpacity = value;
            SetSaveConfigFlag();
        }

        public string GetMessageForegroundColorHex()
        {
            return Config.MessageForegroundColorHex;
        }
        public void SetMessageForegroundColorHex(string value)
        {
            Config.MessageForegroundColorHex = value;
            SetSaveConfigFlag();
        }

        public string GetMessageBackgroundColorHex()
        {
            return Config.MessageBackgroundColorHex;
        }
        public void SetMessageBackgroundColorHex(string value)
        {
            Config.MessageBackgroundColorHex = value;
            SetSaveConfigFlag();
        }

        public double GetMessageForegroundOpacity()
        {
            return Config.MessageForegroundOpacity;
        }
        public void SetMessageForegroundOpacity(double value)
        {
            Config.MessageForegroundOpacity = value;
            SetSaveConfigFlag();
        }

        public double GetMessageBackgroundOpacity()
        {
            return Config.MessageBackgroundOpacity;
        }
        public void SetMessageBackgroundOpacity(double value)
        {
            Config.MessageBackgroundOpacity = value;
            SetSaveConfigFlag();
        }

        public bool GetEnableScanForNewApps()
        {
            return Config.EnabbleScanForNewApps;
        }
        public void SetEnableScanForNewApps(bool value)
        {
            Config.EnabbleScanForNewApps = value;
            SetSaveConfigFlag();

        }

        public bool GetEnableShowTimerWhenAppIsOpen()
        {
            return Config.EnableShowTimerWhenAppIsOpen;
        }
        public void SetEnableShowTimerWhenAppIsOpen(bool value)
        {
            Config.EnableShowTimerWhenAppIsOpen = value;
            SetSaveConfigFlag();
        }

        public int GetPauseTimerStrokeThickness()
        {
            return Config.PauseTimerStrokeThickness;
        }
        public void SetPauseTimerStrokeThickness(int value)
        {
            Config.PauseTimerStrokeThickness = value;
            SetSaveConfigFlag();
        }

        public int GetAppTimerStrokeThickness()
        {
            return Config.AppTimerStrokeThickness;
        }
        public void SetAppTimerStrokeThickness(int value)
        {
            Config.AppTimerStrokeThickness = value;
            SetSaveConfigFlag();
        }

        private async Task OnNewProcessFileCreated()
        {
            await Task.Delay(10000);
            NewProcessFileCreated?.Invoke();
        }
        
    }
}

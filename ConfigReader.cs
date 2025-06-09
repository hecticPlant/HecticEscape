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
        private readonly string _filePathConfig = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HecticEscape",
            "Config.json");
        private readonly string _langFilePath;

        private TimersTimer _saveConfigTimer = null!;
        private bool _saveConfigFlag = false;

        public ConfigReader()
        {
            Logger.Instance.Log("Starte Initialisierung des ConfigReaders.", LogLevel.Info);

            Directory.CreateDirectory(Path.GetDirectoryName(_filePathConfig)!);

            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HecticEscape");
            Directory.CreateDirectory(appDataPath);
            _langFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lang.json");

            SetupConfig();
            LoadLanguages();
            SetupConfigTimer();

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

            };

            var defaultGroup = new Gruppe
            {
                Name = "Gruppe 1",
                Aktiv = true,
                Apps = new List<AppHE>(),
                Websites = new List<Website>(),
            };
            Config.Gruppen.Add(defaultGroup.Name, defaultGroup);

            SaveConfig();

            Logger.Instance.Log("Eine neue Konfigurationsdatei mit Standard-Gruppe wurde erstellt.", LogLevel.Info);
            string json = File.ReadAllText(_filePathConfig);
            Logger.Instance.Log($"Neue Config-Inhalte: {json}", LogLevel.Info);
        }

        public void SetSaveConfigFlag()
        {
            Logger.Instance.Log("Setze SaveConfigFlag.", LogLevel.Verbose);
            if (!_saveConfigFlag)
            {
                _saveConfigFlag = true;
            }
            else
            {
                Logger.Instance.Log("SaveConfigFlag wurde bereits gesetzt, Timer läuft.", LogLevel.Verbose);
                return;
            }
        }

        public void SaveConfig()
        {
            try
            {
                string json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePathConfig, json);
                Logger.Instance.Log("Konfiguration gespeichert.", LogLevel.Verbose);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Speichern der Konfiguration: {ex.Message}", LogLevel.Error);
            }
        }

        private void EnsureValidConfigStructure()
        {
            if (Config.Gruppen == null)
                Config.Gruppen = new Dictionary<string, Gruppe>();

            foreach (var kvp in Config.Gruppen)
            {
                var grp = kvp.Value;
                if (grp.Apps == null) grp.Apps = new List<AppHE>();
                if (grp.Websites == null) grp.Websites = new List<Website>();

                foreach (var app in grp.Apps)
                {
                    if (app.Logs == null) app.Logs = new List<Log>();
                }
            }
        }

        // --- Sprachverwaltung und globale Settings bleiben erhalten ---
        public LanguageData GetCurrentLanguage()
        {
            if (CurrentLanguage != null)
            {
                Logger.Instance.Log($"Aktuelle Sprache: {CurrentLanguage.Name}", LogLevel.Verbose);
                return CurrentLanguage;
            }
            Logger.Instance.Log("Keine aktuelle Sprache gesetzt.", LogLevel.Warn);
            return new LanguageData { Name = "ERROR:LANGUAGE" };
        }
        public string GetCurrentLanguageString()
        {
            if (!string.IsNullOrEmpty(Config.ActiveLanguageNameString))
            {
                Logger.Instance.Log($"Aktive Sprache aus Config: {Config.ActiveLanguageNameString}", LogLevel.Verbose);
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
                Logger.Instance.Log($"GetAllLanguages: {LanguageFile.Sprachen.Count} Sprachen gefunden.", LogLevel.Verbose);
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
                Logger.Instance.Log($"GetAllLanguageNamesString: {names.Count} Sprachen-Namen gefunden.", LogLevel.Verbose);
                return names;
            }
            Logger.Instance.Log("GetAllLanguageNamesString: Keine Sprachen-Namen gefunden.", LogLevel.Warn);
            return new List<string>();
        }


        // --- Globale Einstellungen (Flags, Timer, etc.) ---
        public void SetEnableUpdateCheck(bool value)
        {
            Config.EnableUpdateCheck = value;
            Logger.Instance.Log($"Setze EnableUpdateCheck auf {value}.", LogLevel.Verbose);
            SetSaveConfigFlag();
        }
        public bool GetEnableUpdateCheck()
        {
            Logger.Instance.Log($"GetEnableUpdateCheck: {Config.EnableUpdateCheck}", LogLevel.Verbose);
            return Config.EnableUpdateCheck;
        }

        public void SetEnableStartOnWindowsStartup(bool value)
        {
            Config.EnableStartOnWindowsStartup = value;
            Logger.Instance.Log($"Setze EnableStartOnWindowsStartup auf {value}.", LogLevel.Verbose);
            SetSaveConfigFlag();
        }
        public bool GetEnableStartOnWindowsStartup() 
        {
            Logger.Instance.Log($"GetEnableStartOnWindowsStartup: {Config.EnableStartOnWindowsStartup}", LogLevel.Verbose);
            return Config.EnableStartOnWindowsStartup;
        }
        public bool GetEnableVerboseMode()
        {
            Logger.Instance.Log($"GetEnableVerboseMode: {Config.EnableVerboseMode}", LogLevel.Verbose);
            return Config.EnableVerboseMode;
        }
        public void SetEnableVerboseMode(bool value)
        {
            Config.EnableVerboseMode = value;
            Logger.Instance.Log($"Setze EnableVerboseMode auf {value}.", LogLevel.Verbose);
            SetSaveConfigFlag();
        }

        public bool GetWebsiteBlockingEnabled()
        {
            Logger.Instance.Log($"GetWebsiteBlockingEnabled: {Config.EnableWebsiteBlocking}", LogLevel.Verbose);
            return Config.EnableWebsiteBlocking;
        }
        public void SetWebsiteBlockingEnabled(bool enabled)
        {
            Config.EnableWebsiteBlocking = enabled;
            Logger.Instance.Log($"Setze EnableWebsiteBlocking auf {enabled}.", LogLevel.Verbose);
            SetSaveConfigFlag();
        }

        public bool GetAppBlockingEnabled()
        {
            Logger.Instance.Log($"GetAppBlockingEnabled: {Config.EnableAppBlocking}", LogLevel.Verbose);
            return Config.EnableAppBlocking;
        }
        public void SetEnableAppBlocking(bool enabled)
        {
            Config.EnableAppBlocking = enabled;
            Logger.Instance.Log($"Setze EnableAppBlocking auf {enabled}.", LogLevel.Verbose);
            SetSaveConfigFlag();
        }

        public bool GetStartTimerAtStartup()
        {
            Logger.Instance.Log($"GetStartTimerAtStartup: {Config.StartTimerAtStartup}", LogLevel.Verbose);
            return Config.StartTimerAtStartup;
        }
        public void SetStartTimerAtStartup(bool value)
        {
            Config.StartTimerAtStartup = value;
            Logger.Instance.Log($"Setze StartTimerAtStartup auf {value}.", LogLevel.Verbose);
            SetSaveConfigFlag();
        }

        public int GetIntervalFreeMs() 
        {
            Logger.Instance.Log($"GetIntervalFreeMs: {Config.IntervalFreeMs}", LogLevel.Verbose);
            return Config.IntervalFreeMs;
        }
        public void SetIntervalFreeMs(int value)
        {
            Config.IntervalFreeMs = value;
            Logger.Instance.Log($"Setze IntervalFreeMs auf {value} ms.", LogLevel.Verbose);
            SetSaveConfigFlag();
        }

        public int GetIntervalBreakMs()
        {
            Logger.Instance.Log($"GetIntervalBreakMs: {Config.IntervalBreakMs}", LogLevel.Verbose);
            return Config.IntervalBreakMs;
        }
        public void SetIntervalBreakMs(int value)
        {
            Config.IntervalBreakMs = value;
            Logger.Instance.Log($"Setze IntervalBreakMs auf {value} ms.", LogLevel.Verbose);
            SetSaveConfigFlag();
        }

        public int GetIntervalCheckMs()
        {
            Logger.Instance.Log($"GetIntervalCheckMs: {Config.IntervalCheckMs}", LogLevel.Verbose);
            return Config.IntervalCheckMs;
        }
        public void SetIntervalCheckMs(int value)
        {
            Config.IntervalCheckMs = value;
            Logger.Instance.Log($"Setze IntervalCheckMs auf {value} ms.", LogLevel.Verbose);
            SetSaveConfigFlag();
        }

        public bool GetEnableDebugMode()
        {             
            Logger.Instance.Log($"GetEnableDebugMode: {Config.EnableDebugMode}", LogLevel.Verbose);
            return Config.EnableDebugMode;
        }
        public void SetEnableDebugMode(bool value)
        {
            Config.EnableDebugMode = value;
            Logger.Instance.Log($"Setze EnableDebugMode auf {value}.", LogLevel.Verbose);
            SetSaveConfigFlag();
        }

        public bool GetShowTimeInOverlayEnable()
        {
            Logger.Instance.Log($"GetShowTimeInOverlayEnable: {Config.EnableShowTimeInOverlay}", LogLevel.Verbose);
            return Config.EnableShowTimeInOverlay;
        }
        public void EnableShowTimeInOverlay(bool value)
        {
            Config.EnableShowTimeInOverlay = value;
            Logger.Instance.Log($"Setze EnableShowTimeInOverlay auf {value}.", LogLevel.Verbose);
            SetSaveConfigFlag();
        }

        public bool GetShowAppTimeInOverlayEnable()
        {
            Logger.Instance.Log($"GetShowAppTimeInOverlayEnable: {Config.EnableShowAppTimeInOverlay}", LogLevel.Verbose);
            return Config.EnableShowAppTimeInOverlay;
        }
        public void SetShowAppTimeInOverlayEnable(bool value)
        {
            Config.EnableShowAppTimeInOverlay = value;
            Logger.Instance.Log($"Setze EnableShowAppTimeInOverlayEnable auf {value}.", LogLevel.Verbose);
            SetSaveConfigFlag();
        }

        public bool GetEnableOverlay()
        {
            Logger.Instance.Log($"GetEnableOverlay: {Config.EnableOverlay}", LogLevel.Verbose);
            return Config.EnableOverlay;
        }
        public void SetEnableOverlay(bool value)
        {
            Config.EnableOverlay = value;
            Logger.Instance.Log($"Setze EnableOverlay auf {value}.", LogLevel.Verbose);
            SetSaveConfigFlag();
        }
    }
}

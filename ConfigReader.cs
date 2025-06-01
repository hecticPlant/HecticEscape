using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HecticEscape
{
    /// <summary>
    /// Diese Klasse verwaltet die Config.json Datei und arbeitet
    /// nun mit Instanzen von Gruppe und AppHE, statt nur mit String-Namen.
    /// </summary>
    public class ConfigReader
    {
        private Config _config;
        public LanguageFile LanguageFile { get; private set; }
        public LanguageData CurrentLanguage { get; private set; }
        private readonly string _filePathConfig = "Config.json";
        private readonly string _langFilePath = "Lang.json";

        public ConfigReader()
        {
            Logger.Instance.Log("Starte Initialisierung des ConfigReaders.", LogLevel.Info);
            SetupConfig();
            LoadLanguages();



            Logger.Instance.Log("ConfigReader initialisiert.", LogLevel.Info);
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
                    // Falls Deserialisierung fehlschlägt, erzeuge Default
                    _config = JsonSerializer.Deserialize<Config>(json)
                              ?? new Config();

                    // Sicherstellen, dass Listen nicht null sind
                    EnsureValidConfigStructure();

                    // Standardwerte setzen, falls Intervalle ≤ 0
                    if (_config.IntervalFreeMs <= 0) _config.IntervalFreeMs = 2 * 3600 * 1000;
                    if (_config.IntervalBreakMs <= 0) _config.IntervalBreakMs = 15 * 60 * 1000;
                    if (_config.IntervalCheckMs <= 0) _config.IntervalCheckMs = 1000;

                    if (_config.Gruppen == null || _config.Gruppen.Count == 0)
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
            if (_config != null)
            {
                try
                {
                    Logger.Instance.IsDebugEnabled = _config.EnableDebugMode;
                    Logger.Instance.IsVerboseEnabled = _config.EnableVerboseMode;
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
                            ErrorMessages = new Dictionary<string, string>()
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
            if (LanguageFile.Sprachen != null && LanguageFile.Sprachen.TryGetValue(GetActiveLanguageFromConfig(), out var langData))
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

        /// <summary>
        /// Erzeugt eine Default-Config mit einer Standard-Gruppe und speichert sie.
        /// </summary>
        private void CreateDefaultConfig()
        {
            Logger.Instance.Log("Erstelle Default-Konfiguration.", LogLevel.Info);

            _config = new Config
            {
                Gruppen = new Dictionary<string, Gruppe>(),
                EnableWebsiteBlocking = false,
                EnableAppBlocking = false,
                EnableDebugMode = false,
                EnableVerboseMode = false,
                EnableShowTimeInOverlay = false,
                StartTimerAtStartup = false,
                IntervalFreeMs = 2 * 3600 * 1000,
                IntervalBreakMs = 15 * 60 * 1000,
                IntervalCheckMs = 1000,
                ActiveLanguageNameString = "Deutsch",
            };

            // Eine Startgruppe anlegen
            var defaultGroup = new Gruppe
            {
                Name = "Gruppe 1",
                Aktiv = true,
                Apps = new List<AppHZ>(),
                Websites = new List<Website>(),
            };
            _config.Gruppen.Add(defaultGroup.Name, defaultGroup);

            SaveConfig();

            Logger.Instance.Log("Eine neue Konfigurationsdatei mit Standard-Gruppe wurde erstellt.", LogLevel.Info);
            string json = File.ReadAllText(_filePathConfig);
            Logger.Instance.Log($"Neue Config-Inhalte: {json}", LogLevel.Info);
        }

        /// <summary>
        /// Speichert die aktuelle Config in die JSON-Datei.
        /// </summary>
        public void SaveConfig()
        {
            try
            {
                string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePathConfig, json);
                Logger.Instance.Log("Konfiguration gespeichert.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Speichern der Konfiguration: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Stellt sicher, dass in der geladenen Config alle Collections (Gruppen, Apps, Websites, Logs) nicht auf null stehen.
        /// </summary>
        private void EnsureValidConfigStructure()
        {
            if (_config.Gruppen == null)
                _config.Gruppen = new Dictionary<string, Gruppe>();

            foreach (var kvp in _config.Gruppen)
            {
                var grp = kvp.Value;
                if (grp.Apps == null) grp.Apps = new List<AppHZ>();
                if (grp.Websites == null) grp.Websites = new List<Website>();

                foreach (var app in grp.Apps)
                {
                    if (app.Logs == null) app.Logs = new List<Log>();
                }
            }
        }

        // -------------------- Zugriff auf Gruppen --------------------

        /// <summary>
        /// Liefert alle Gruppen-Instanzen (ohne ihre Keys). 
        /// </summary>
        public List<Gruppe> GetAllGroups()
        {
            Logger.Instance.Log($"GetAllGroups: {_config.Gruppen.Count} Gruppen gefunden.", LogLevel.Verbose);
            return _config.Gruppen.Values.ToList();
        }

        /// <summary>
        /// Liefert die Gruppe mit dem gegebenen Namen oder null, falls nicht gefunden.
        /// </summary>
        public Gruppe? GetGroupByName(string groupName)
        {
            if (_config.Gruppen.TryGetValue(groupName, out var grp))
            {
                Logger.Instance.Log($"GetGroupByName: Gruppe '{groupName}' gefunden.", LogLevel.Verbose);
                return grp;
            }
            Logger.Instance.Log($"GetGroupByName: Gruppe '{groupName}' nicht gefunden.", LogLevel.Warn);
            return null;
        }

        /// <summary>
        /// Legt eine neue Gruppe an, gibt die erzeugte Gruppe zurück.
        /// </summary>
        public Gruppe CreateGroup()
        {
            // Bestimme nächste Nummer anhand vorhandener Gruppen-Namen "Gruppe X"
            int maxNum = 0;
            foreach (var key in _config.Gruppen.Keys)
            {
                if (key.StartsWith("Gruppe ", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = key.Split(' ');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int n))
                        maxNum = Math.Max(maxNum, n);
                }
            }
            int nextNum = maxNum + 1;
            string newName = $"Gruppe {nextNum}";

            var newGroup = new Gruppe
            {
                Name = newName,
                Aktiv = true,
                Apps = new List<AppHZ>(),
                Websites = new List<Website>(),
            };

            _config.Gruppen.Add(newName, newGroup);
            Logger.Instance.Log($"Neue Gruppe erstellt: '{newName}'.", LogLevel.Info);
            SaveConfig();
            return newGroup;
        }

        /// <summary>
        /// Löscht eine bestehende Gruppe anhand ihrer Instanz.
        /// </summary>
        public bool DeleteGroup(Gruppe group)
        {
            // Finde dazu zuerst den Key in der Dictionary
            var pair = _config.Gruppen.FirstOrDefault(kvp => kvp.Value == group);
            if (pair.Value != null)
            {
                _config.Gruppen.Remove(pair.Key);
                Logger.Instance.Log($"Gruppe '{pair.Key}' gelöscht.", LogLevel.Info);
                SaveConfig();
                return true;
            }
            Logger.Instance.Log($"DeleteGroup: Gruppe nicht gefunden.", LogLevel.Warn);
            return false;
        }

        /// <summary>
        /// Setzt den Aktivitätsstatus einer Gruppe (true = aktiv, false = inaktiv).
        /// </summary>
        public bool SetGroupActiveStatus(Gruppe group, bool isActive)
        {
            var pair = _config.Gruppen.FirstOrDefault(kvp => kvp.Value == group);
            if (pair.Value != null)
            {
                group.Aktiv = isActive;
                Logger.Instance.Log($"Aktivitätsstatus der Gruppe '{pair.Key}' auf {isActive} gesetzt.", LogLevel.Info);
                SaveConfig();
                return true;
            }
            Logger.Instance.Log($"SetGroupActiveStatus: Gruppe nicht gefunden.", LogLevel.Warn);
            return false;
        }

        /// <summary>
        /// Liest den Aktivitätsstatus einer Gruppe.
        /// </summary>
        public bool GetGroupActiveStatus(Gruppe group)
        {
            var pair = _config.Gruppen.FirstOrDefault(kvp => kvp.Value == group);
            if (pair.Value != null)
            {
                Logger.Instance.Log($"GetGroupActiveStatus: Gruppe '{pair.Key}' ist {(group.Aktiv ? "aktiv" : "inaktiv")}.", LogLevel.Verbose);
                return group.Aktiv;
            }
            Logger.Instance.Log($"GetGroupActiveStatus: Gruppe nicht gefunden.", LogLevel.Warn);
            return false;
        }

        /// <summary>
        /// Liest alle Gruppennamen als Strings aus der Konfiguration.
        /// </summary>
        /// <returns></returns>
        public List<String> GetAllGroupNamesString()
        {
            List<string> groupNames = new List<string>();
            foreach (var grp in _config.Gruppen.Values)
            {
                if (grp.Name != null)
                {
                    groupNames.Add(grp.Name);
                }
                else
                {
                    Logger.Instance.Log($"GetAllGroupNamesString: Gruppe ohne Namen gefunden. {string.Join(", ", groupNames)}", LogLevel.Warn);
                }
            }
            Logger.Instance.Log($"GetAllGroupNamesString: {groupNames?.Count} Gruppen-Namen gefunden.", LogLevel.Verbose);
            if (groupNames == null || groupNames.Count == 0)
            {
                Logger.Instance.Log("GetAllGroupNamesString: Keine Gruppen-Namen gefunden.", LogLevel.Warn);
            }
            else
            {
                Logger.Instance.Log($"GetAllGroupNamesString: Gruppen-Namen: {string.Join(", ", groupNames)}", LogLevel.Verbose);
                return groupNames;
            }
            return new List<string>();
        }

        public List<Gruppe> GetAllActiveGroups()
        {
            var activeGroups = _config.Gruppen.Values.Where(g => g.Aktiv).ToList();
            Logger.Instance.Log($"GetAllActiveGroups: {activeGroups.Count} aktive Gruppen gefunden.", LogLevel.Verbose);
            return activeGroups;
        }
        // -------------------- App-Verwaltung --------------------

        /// <summary>
        /// Gibt alle AppHE-Instanzen zurück, die in aktiven Gruppen enthalten sind.
        /// </summary>
        public List<AppHZ> GetActiveGroupsApps()
        {
            var apps = new List<AppHZ>();
            foreach (var grp in _config.Gruppen.Values)
            {
                if (grp.Aktiv)
                {
                    apps.AddRange(grp.Apps);
                }
            }
            Logger.Instance.Log($"GetActiveGroupsApps: {apps.Count} Apps gefunden.", LogLevel.Verbose);
            return apps;
        }

        /// <summary>
        /// Fügt eine existierende AppHE-Instanz in die angegebene Gruppe ein.
        /// </summary>
        public bool AddAppToGroup(Gruppe group, AppHZ app)
        {
            if (!_config.Gruppen.Values.Contains(group))
            {
                Logger.Instance.Log($"AddAppToGroup: Gruppe nicht gefunden.", LogLevel.Warn);
                return false;
            }

            // Prüfen, ob in der Gruppe bereits eine App mit demselben Namen existiert
            if (group.Apps.Any(a => a.Name.Equals(app.Name, StringComparison.OrdinalIgnoreCase)))
            {
                Logger.Instance.Log($"App '{app.Name}' existiert bereits in Gruppe '{group.Name}'.", LogLevel.Warn);
                return false;
            }

            // Neue App anlegen (von außen bereitgestellte Instanz verwenden)
            group.Apps.Add(app);
            Logger.Instance.Log($"App '{app.Name}' zu Gruppe '{group.Name}' hinzugefügt.", LogLevel.Info);
            SaveConfig();
            return true;
        }

        /// <summary>
        /// Entfernt eine AppHE-Instanz aus der angegebenen Gruppe.
        /// </summary>
        public bool DeleteAppFromGroup(Gruppe group, AppHZ app)
        {
            if (!_config.Gruppen.Values.Contains(group))
            {
                Logger.Instance.Log($"DeleteAppFromGroup: Gruppe nicht gefunden.", LogLevel.Warn);
                return false;
            }

            var existing = group.Apps.FirstOrDefault(a => a == app);
            if (existing != null)
            {
                group.Apps.Remove(existing);
                Logger.Instance.Log($"App '{app.Name}' aus Gruppe '{group.Name}' gelöscht.", LogLevel.Info);
                SaveConfig();
                return true;
            }

            Logger.Instance.Log($"DeleteAppFromGroup: App '{app.Name}' nicht in Gruppe '{group.Name}' gefunden.", LogLevel.Warn);
            return false;
        }

        /// <summary>
        /// Setzt die tägliche Zeit (in Millisekunden) für eine App in einer Gruppe.
        /// </summary>
        public void SetDailyAppTime(Gruppe group, AppHZ app, long dailyTimeMs)
        {
            if (!_config.Gruppen.Values.Contains(group))
            {
                Logger.Instance.Log($"SetDailyAppTime: Gruppe nicht gefunden.", LogLevel.Warn);
                return;
            }

            var existing = group.Apps.FirstOrDefault(a => a == app);
            if (existing != null)
            {
                existing.DailyTimeMs = dailyTimeMs;
                Logger.Instance.Log($"Tägliche Zeit für App '{app.Name}' in Gruppe '{group.Name}' auf {dailyTimeMs} gesetzt.", LogLevel.Info);
                SaveConfig();
            }
            else
            {
                Logger.Instance.Log($"SetDailyAppTime: App '{app.Name}' nicht in Gruppe '{group.Name}' gefunden.", LogLevel.Warn);
            }
        }

        /// <summary>
        /// Liest die tägliche Zeit (in Millisekunden) für eine App in einer Gruppe.
        /// </summary>
        public long GetDailyAppTime(Gruppe group, AppHZ app)
        {
            if (!_config.Gruppen.Values.Contains(group))
            {
                Logger.Instance.Log($"GetDailyAppTime: Gruppe nicht gefunden.", LogLevel.Warn);
                return 0;
            }

            var existing = group.Apps.FirstOrDefault(a => a == app);
            if (existing != null)
            {
                Logger.Instance.Log($"GetDailyAppTime: Tägliche Zeit für App '{app.Name}' in Gruppe '{group.Name}' ist {existing.DailyTimeMs}.", LogLevel.Verbose);
                return existing.DailyTimeMs;
            }

            Logger.Instance.Log($"GetDailyAppTime: App '{app.Name}' nicht in Gruppe '{group.Name}' gefunden. Rückgabe 0.", LogLevel.Warn);
            return 0;
        }

        /// <summary>
        /// Gibt alle Log-Einträge für eine App in einer Gruppe zurück.
        /// </summary>
        public List<Log> GetAppLogs(Gruppe group, AppHZ app)
        {
            if (!_config.Gruppen.Values.Contains(group))
            {
                Logger.Instance.Log($"GetAppLogs: Gruppe nicht gefunden.", LogLevel.Warn);
                return new List<Log>();
            }

            var existing = group.Apps.FirstOrDefault(a => a == app);
            if (existing != null)
            {
                Logger.Instance.Log($"GetAppLogs: App '{app.Name}' in Gruppe '{group.Name}' gefunden.", LogLevel.Verbose);
                return existing.Logs;
            }

            Logger.Instance.Log($"GetAppLogs: App '{app.Name}' nicht in Gruppe '{group.Name}' gefunden.", LogLevel.Warn);
            return new List<Log>();
        }

        /// <summary>
        /// Liest die aufgelaufene Zeit (TimeMs) für eine App an einem bestimmten Datum.
        /// </summary>
        public long GetAppDateTimeMs(Gruppe group, AppHZ app, DateOnly date)
        {
            if(group == null || app == null)
            {
                Logger.Instance.Log("GetAppDateTimeMs: Gruppe oder App ist null.", LogLevel.Error);
                return 0;
            }
            var logs = GetAppLogs(group, app);
            var entry = logs.FirstOrDefault(l => l.Date == date);
            if (entry != null)
            {
                Logger.Instance.Log($"GetAppDateTimeMs: Log für App '{app.Name}' am {date} gefunden ({entry.TimeMs} ms).", LogLevel.Verbose);
                return entry.TimeMs;
            }

            Logger.Instance.Log($"GetAppDateTimeMs: Kein Log für App '{app.Name}' am {date} gefunden. Erstelle Log.", LogLevel.Warn);
            if(app.Logs == null)
            {
                CreateLog(group, app, date);
            }
            return 0;
        }

        public void CreateLog(Gruppe group, AppHZ app, DateOnly date, long timeMs = 7200000)
        {
            if (group == null || app == null)
            {
                Logger.Instance.Log("CreateLog: Gruppe oder App ist null.", LogLevel.Error);
                return;
            }
            var logs = GetAppLogs(group, app);
            var entry = logs.FirstOrDefault(l => l.Date == date);
            if (entry == null)
            {
                entry = new Log { Date = date, TimeMs = timeMs };
                logs.Add(entry);
                Logger.Instance.Log($"CreateLog: Neuer Log-Eintrag für App '{app.Name}' am {date} mit {timeMs} ms erstellt.", LogLevel.Verbose);
            }
            else
            {
                entry.TimeMs = timeMs;
                Logger.Instance.Log($"CreateLog: Log-Eintrag für App '{app.Name}' am {date} aktualisiert auf {timeMs} ms.", LogLevel.Verbose);
            }
            SaveConfig();
        }

        /// <summary>
        /// Fügt (oder aktualisiert) einen Log-Eintrag für eine App an einem bestimmten Datum hinzu.
        /// </summary>
        public void SetAppDateTimeMs(Gruppe group, AppHZ app, DateOnly date, long timeMs)
        {
            var logs = GetAppLogs(group, app);
            var entry = logs.FirstOrDefault(l => l.Date == date);
            if (entry != null)
            {
                entry.TimeMs = timeMs;
                Logger.Instance.Log($"SetAppDateTimeMs: Log für App '{app.Name}' am {date} auf {timeMs} ms aktualisiert.", LogLevel.Verbose);
            }
            else
            {
                logs.Add(new Log { Date = date, TimeMs = timeMs });
                Logger.Instance.Log($"SetAppDateTimeMs: Neuer Log für App '{app.Name}' am {date} mit {timeMs} ms hinzugefügt.", LogLevel.Verbose);
            }
            SaveConfig();
        }

        public AppHZ GetAppFromGroup(Gruppe group, string appName)
        {
            if (!_config.Gruppen.Values.Contains(group))
            {
                Logger.Instance.Log($"GetAppFromGroup: Gruppe nicht gefunden.", LogLevel.Warn);
                return new AppHZ();
            }
            var app = group.Apps.FirstOrDefault(a => a.Name.Equals(appName, StringComparison.OrdinalIgnoreCase));
            if (app != null)
            {
                Logger.Instance.Log($"GetAppFromGroup: App '{appName}' in Gruppe '{group.Name}' gefunden.", LogLevel.Verbose);
                return app;
            }
            Logger.Instance.Log($"GetAppFromGroup: App '{appName}' nicht in Gruppe '{group.Name}' gefunden.", LogLevel.Warn);
            return new AppHZ();
        }

        /// <summary>
        /// Listet alle App-Namen (Strings) aus einer bestimmten Gruppe.
        /// </summary>
        /// <param name="group"></param>
        /// <returns></returns>
        public List<string> GetAppNamesFromGroupString(Gruppe group)
        {
            if (!_config.Gruppen.Values.Contains(group))
            {
                Logger.Instance.Log($"GetAppNamesFromGroup: Gruppe nicht gefunden.", LogLevel.Warn);
                return new List<string>();
            }
            var names = group.Apps.Select(a => a.Name).ToList();
            Logger.Instance.Log($"GetAppNamesFromGroup: {names.Count} Apps in '{group.Name}'.", LogLevel.Verbose);
            return names;
        }

        // -------------------- Website-Verwaltung --------------------

        /// <summary>
        /// Gibt alle Website-Instanzen zurück, die in aktiven Gruppen enthalten sind.
        /// </summary>
        public List<Website> GetActiveGroupsWebsites()
        {
            var websites = new List<Website>();
            foreach (var grp in _config.Gruppen.Values)
            {
                if (grp.Aktiv)
                {
                    websites.AddRange(grp.Websites);
                }
            }
            Logger.Instance.Log($"GetActiveGroupsWebsites: {websites.Count} Websites gefunden.", LogLevel.Verbose);
            return websites;
        }

        /// <summary>
        /// Fügt eine Website in eine angegebene Gruppe ein.
        /// </summary>
        public bool AddWebsiteToGroup(Gruppe group, Website site)
        {
            if (!_config.Gruppen.Values.Contains(group))
            {
                Logger.Instance.Log($"AddWebsiteToGroup: Gruppe nicht gefunden.", LogLevel.Warn);
                return false;
            }

            if (group.Websites.Any(w => w.Name.Equals(site.Name, StringComparison.OrdinalIgnoreCase)))
            {
                Logger.Instance.Log($"Website '{site.Name}' existiert bereits in Gruppe '{group.Name}'.", LogLevel.Warn);
                return false;
            }

            group.Websites.Add(site);
            Logger.Instance.Log($"Website '{site.Name}' zu Gruppe '{group.Name}' hinzugefügt.", LogLevel.Info);
            SaveConfig();
            return true;
        }

        /// <summary>
        /// Entfernt eine Website aus einer angegebenen Gruppe.
        /// </summary>
        public bool DeleteWebsiteFromGroup(Gruppe group, Website site)
        {
            if (!_config.Gruppen.Values.Contains(group))
            {
                Logger.Instance.Log($"DeleteWebsiteFromGroup: Gruppe nicht gefunden.", LogLevel.Warn);
                return false;
            }

            var existing = group.Websites.FirstOrDefault(w => w == site);
            if (existing != null)
            {
                group.Websites.Remove(existing);
                Logger.Instance.Log($"Website '{site.Name}' aus Gruppe '{group.Name}' gelöscht.", LogLevel.Info);
                SaveConfig();
                return true;
            }

            Logger.Instance.Log($"DeleteWebsiteFromGroup: Website '{site.Name}' nicht in Gruppe '{group.Name}' gefunden.", LogLevel.Warn);
            return false;
        }

        /// <summary>
        /// Liest alle Websites (Namen) aus einer bestimmten Gruppe.
        /// </summary>
        public List<string> GetWebsiteNamesFromGroupString(Gruppe group)
        {
            if (!_config.Gruppen.Values.Contains(group))
            {
                Logger.Instance.Log($"GetWebsiteNamesFromGroup: Gruppe nicht gefunden.", LogLevel.Warn);
                return new List<string>();
            }
            var names = group.Websites.Select(w => w.Name).ToList();
            Logger.Instance.Log($"GetWebsiteNamesFromGroup: {names.Count} Websites in '{group.Name}'.", LogLevel.Verbose);
            return names;
        }

        /// <summary>
        /// Liest alle Apps (Namen) aus einer bestimmten Gruppe.
        /// </summary>
        public List<string> GetAppNamesFromGroup(Gruppe group)
        {
            if (!_config.Gruppen.Values.Contains(group))
            {
                Logger.Instance.Log($"GetAppNamesFromGroup: Gruppe nicht gefunden.", LogLevel.Warn);
                return new List<string>();
            }
            var names = group.Apps.Select(a => a.Name).ToList();
            Logger.Instance.Log($"GetAppNamesFromGroup: {names.Count} Apps in '{group.Name}'.", LogLevel.Verbose);
            return names;
        }

        public Website GetWebsiteFromGroup(Gruppe group, string websiteName)
        {
            if (!_config.Gruppen.Values.Contains(group))
            {
                Logger.Instance.Log($"GetWebsiteFromGroup: Gruppe nicht gefunden.", LogLevel.Warn);
                return new Website();
            }
            var site = group.Websites.FirstOrDefault(w => w.Name.Equals(websiteName, StringComparison.OrdinalIgnoreCase));
            if (site != null)
            {
                Logger.Instance.Log($"GetWebsiteFromGroup: Website '{websiteName}' in Gruppe '{group.Name}' gefunden.", LogLevel.Verbose);
                return site;
            }
            Logger.Instance.Log($"GetWebsiteFromGroup: Website '{websiteName}' nicht in Gruppe '{group.Name}' gefunden.", LogLevel.Warn);
            return new Website();
        }

        // -------------------- Globale Einstellungen --------------------

        public bool GetEnableVerboseMode()
        {
            Logger.Instance.Log($"Abfrage: EnableGetMode = {_config.EnableVerboseMode}", LogLevel.Verbose);
            return _config.EnableVerboseMode;
        }

        public void SetEnablVerbosetMode(bool value)
        {
            _config.EnableVerboseMode = value;
            Logger.Instance.Log($"Setze EnableGetMode auf {value}.", LogLevel.Verbose);
            SaveConfig();
        }

        public bool GetWebsiteBlockingEnabled()
        {
            Logger.Instance.Log($"Abfrage: EnableWebsiteBlocking = {_config.EnableWebsiteBlocking}", LogLevel.Verbose);
            return _config.EnableWebsiteBlocking;
        }

        public void SetWebsiteBlockingEnabled(bool enabled)
        {
            _config.EnableWebsiteBlocking = enabled;
            Logger.Instance.Log($"Setze EnableWebsiteBlocking auf {enabled}.", LogLevel.Verbose);
            SaveConfig();
        }

        public bool GetAppBlockingEnabled()
        {
            Logger.Instance.Log($"Abfrage: EnableAppBlocking = {_config.EnableAppBlocking}", LogLevel.Verbose);
            return _config.EnableAppBlocking;
        }

        public void SetAppBlockingEnabled(bool enabled)
        {
            _config.EnableAppBlocking = enabled;
            Logger.Instance.Log($"Setze EnableAppBlocking auf {enabled}.", LogLevel.Verbose);
            SaveConfig();
        }

        public bool GetStartTimerAtStartup()
        {
            Logger.Instance.Log($"Abfrage: StartTimerAtStartup = {_config.StartTimerAtStartup}", LogLevel.Verbose);
            return _config.StartTimerAtStartup;
        }

        public void SetStartTimerAtStartup(bool value)
        {
            _config.StartTimerAtStartup = value;
            Logger.Instance.Log($"Setze StartTimerAtStartup auf {value}.", LogLevel.Verbose);
            SaveConfig();
        }

        public int GetIntervalFreeMs()
        {
            Logger.Instance.Log($"Abfrage: IntervalFreeMs = {_config.IntervalFreeMs}", LogLevel.Verbose);
            return _config.IntervalFreeMs;
        }

        public void SetIntervalFreeMs(int value)
        {
            _config.IntervalFreeMs = value;
            Logger.Instance.Log($"Setze IntervalFreeMs auf {value} ms.", LogLevel.Verbose);
            SaveConfig();
        }

        public int GetIntervalBreakMs()
        {
            Logger.Instance.Log($"Abfrage: IntervalBreakMs = {_config.IntervalBreakMs}", LogLevel.Verbose);
            return _config.IntervalBreakMs;
        }

        public void SetIntervalBreakMs(int value)
        {
            _config.IntervalBreakMs = value;
            Logger.Instance.Log($"Setze IntervalBreakMs auf {value} ms.", LogLevel.Verbose);
            SaveConfig();
        }

        public int GetIntervalCheckMs()
        {
            Logger.Instance.Log($"Abfrage: IntervalCheckMs = {_config.IntervalCheckMs}", LogLevel.Verbose);
            return _config.IntervalCheckMs;
        }

        public void SetIntervalCheckMs(int value)
        {
            _config.IntervalCheckMs = value;
            Logger.Instance.Log($"Setze IntervalCheckMs auf {value} ms.", LogLevel.Verbose);
            SaveConfig();
        }

        public bool GetEnableDebugMode()
        {
            Logger.Instance.Log($"Abfrage: EnableDebugMode = {_config.EnableDebugMode}", LogLevel.Verbose);
            return _config.EnableDebugMode;
        }

        public void SetEnableDebugMode(bool value)
        {
            _config.EnableDebugMode = value;
            Logger.Instance.Log($"Setze EnableDebugMode auf {value}.", LogLevel.Verbose);
            SaveConfig();
        }

        public bool GetShowTimeInOverlayEnable()
        {
            Logger.Instance.Log($"Abfrage: EnableShowTimeInOverlay = {_config.EnableShowTimeInOverlay}", LogLevel.Verbose);
            return _config.EnableShowTimeInOverlay;
        }

        public void SetShowTimeInOverlayEnable(bool value)
        {
            _config.EnableShowTimeInOverlay = value;
            Logger.Instance.Log($"Setze EnableShowTimeInOverlay auf {value}.", LogLevel.Verbose);
            SaveConfig();
        }

        public bool GetEnableOverlay()
        {
            Logger.Instance.Log($"Abfrage: EnableOverlay = {_config.EnableOverlay}", LogLevel.Verbose);
            return _config.EnableOverlay;
        }

        public void SetEnableOverlay(bool value)
        {
            _config.EnableOverlay = value;
            Logger.Instance.Log($"Setze EnableOverlay auf {value}.", LogLevel.Verbose);
            SaveConfig();
        }

        public string GetActiveLanguage()
        {
            if (CurrentLanguage != null && CurrentLanguage.Name != null)
            {
                Logger.Instance.Log($"Aktive Sprache: {CurrentLanguage.Name}", LogLevel.Verbose);
                return CurrentLanguage.Name;
            }
            Logger.Instance.Log("Keine aktive Sprache gesetzt.", LogLevel.Warn);
            return "Deutsch"; // Fallback
        }
        public string GetActiveLanguageFromConfig()
        {
            if (!string.IsNullOrEmpty(_config.ActiveLanguageNameString))
            {
                Logger.Instance.Log($"Aktive Sprache aus Config: {_config.ActiveLanguageNameString}", LogLevel.Verbose);
                return _config.ActiveLanguageNameString;
            }
            return "Deutsch"; // Fallback
        }

        public void SetActiveLanguage(string languageName)
        {
            if(LanguageFile.Sprachen != null && LanguageFile.Sprachen.ContainsKey(languageName))
            {
                _config.ActiveLanguageNameString = languageName;
                CurrentLanguage = LanguageFile.Sprachen[languageName];
                Logger.Instance.Log($"Aktive Sprache auf '{languageName}' gesetzt.", LogLevel.Info);
                SaveConfig();
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
    }
}

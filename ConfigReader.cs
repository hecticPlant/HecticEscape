using System.IO;
using System.Text.Json;

namespace ScreenZen
{
    /// <summary>
    /// Diese Klasse verwaltet die Config.json Datei.
    /// </summary>
    public class ConfigReader
    {
        private Config _config;
        private string filePath = "Config.json";

        public ConfigReader()
        {
            Logger.Instance.Log("Starte Initialisierung des ConfigReaders.", LogLevel.Info);

            if (File.Exists(filePath))
            {
                Logger.Instance.Log($"{filePath} existiert.", LogLevel.Info);
                try
                {
                    string json = File.ReadAllText(filePath);
                    Logger.Instance.Log($"Config-Inhalt geladen: {json}", LogLevel.Debug);
                    _config = JsonSerializer.Deserialize<Config>(json) ?? new Config { Gruppen = new Dictionary<string, Gruppe>(), EnableWebsiteBlocking = true, EnableAppBlocking = true };

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
            Logger.Instance.Log("ConfigReader initialisiert.", LogLevel.Info);
        }

        private void CreateDefaultConfig()
        {
            Logger.Instance.Log("Erstelle Default-Konfiguration.", LogLevel.Info);
            _config = new Config
            {
                Gruppen = new Dictionary<string, Gruppe>(),
                EnableWebsiteBlocking = true,
                EnableAppBlocking = true
            };
            CreateGroup();
            SaveConfig();
            Logger.Instance.Log("Eine neue Konfigurationsdatei wurde erstellt.", LogLevel.Info);
        }

        /// <summary>
        /// Getter für Website-Blocking
        /// </summary>
        public bool GetWebsiteBlockingEnabled()
        {
            Logger.Instance.Log($"Abfrage: EnableWebsiteBlocking = {_config.EnableWebsiteBlocking}", LogLevel.Debug);
            return _config.EnableWebsiteBlocking;
        }

        /// <summary>
        /// Setter für Website-Blocking
        /// </summary>
        public void SetWebsiteBlockingEnabled(bool enabled)
        {
            Logger.Instance.Log($"Setze EnableWebsiteBlocking auf {enabled}.", LogLevel.Info);
            _config.EnableWebsiteBlocking = enabled;
            SaveConfig();
        }

        /// <summary>
        /// Getter für App-Blocking
        /// </summary>
        public bool GetAppBlockingEnabled()
        {
            Logger.Instance.Log($"Abfrage: EnableAppBlocking = {_config.EnableAppBlocking}", LogLevel.Debug);
            return _config.EnableAppBlocking;
        }

        /// <summary>
        /// Setter für App-Blocking
        /// </summary>
        public void SetAppBlockingEnabled(bool enabled)
        {
            Logger.Instance.Log($"Setze EnableAppBlocking auf {enabled}.", LogLevel.Info);
            _config.EnableAppBlocking = enabled;
            SaveConfig();
        }

        /// <summary>
        /// Alle Gruppen als string
        /// </summary>
        public string GetAllGroups()
        {
            if (_config.Gruppen == null)
            {
                Logger.Instance.Log("GetAllGroups: Keine Gruppen vorhanden.", LogLevel.Warn);
                return string.Empty;
            }
            Logger.Instance.Log($"GetAllGroups: {_config.Gruppen.Count} Gruppen gefunden.", LogLevel.Debug);
            return string.Join(", ", _config.Gruppen.Keys);
        }

        /// <summary>
        /// Alle Websites, aus aktiven gruppen, als Liste
        /// </summary>
        public List<string> GetActiveGroupsDomains()
        {
            var activeDomains = new List<string>();
            foreach (var gruppe in _config.Gruppen)
            {
                if (gruppe.Value.Aktiv)
                {
                    foreach (var website in gruppe.Value.Websites)
                    {
                        activeDomains.Add(website.Name);
                    }
                }
            }
            Logger.Instance.Log($"GetActiveGroupsDomains: {activeDomains.Count} Domains gefunden.", LogLevel.Debug);
            return activeDomains;
        }

        /// <summary>
        /// Alle Apps, aus aktiven gruppen, als Liste
        /// </summary>
        public List<string> GetActiveGroupsApps()
        {
            var activeApps = new List<string>();
            foreach (var gruppe in _config.Gruppen)
            {
                if (gruppe.Value.Aktiv)
                {
                    foreach (var app in gruppe.Value.Apps)
                    {
                        activeApps.Add(app.Name);
                    }
                }
            }
            Logger.Instance.Log($"GetActiveGroupsApps: {activeApps.Count} Apps gefunden.", LogLevel.Debug);
            return activeApps;
        }

        /// <summary>
        /// Alle Websites aus einer bestimmten Gruppe als string
        /// </summary>
        public string GetWebsitesFromGroup(string groupName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                var websites = _config.Gruppen[groupName].Websites;
                var websiteNames = websites.Select(w => w.Name).ToList();
                Logger.Instance.Log($"GetWebsitesFromGroup: {websiteNames.Count} Websites in '{groupName}'.", LogLevel.Debug);
                return string.Join(", ", websiteNames);
            }
            Logger.Instance.Log($"GetWebsitesFromGroup: Gruppe '{groupName}' nicht gefunden.", LogLevel.Warn);
            return string.Empty;
        }

        /// <summary>
        /// Alle Apps aus einer bestimmten Gruppe als string
        /// </summary>
        public string GetAppsFromGroup(string groupName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                var apps = _config.Gruppen[groupName].Apps;
                var appNames = apps.Select(a => a.Name).ToList();
                Logger.Instance.Log($"GetAppsFromGroup: {appNames.Count} Apps in '{groupName}'.", LogLevel.Debug);
                return string.Join(", ", appNames);
            }
            Logger.Instance.Log($"GetAppsFromGroup: Gruppe '{groupName}' nicht gefunden.", LogLevel.Warn);
            return string.Empty;
        }

        /// <summary>
        /// Erstellt eine neue Gruppe
        /// </summary>
        public void CreateGroup()
        {
            int groupNumber = _config.Gruppen.Keys
                .Select(key => key.StartsWith("Gruppe") ? int.Parse(key.Split(' ')[1]) : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;

            string groupName = $"Gruppe {groupNumber}";
            string date = DateTime.Now.ToString("yyyy-MM-dd");
            bool aktiv = true;

            _config.Gruppen[groupName] = new Gruppe
            {
                Date = date,
                Aktiv = aktiv,
                Apps = new List<AppSZ>(),
                Websites = new List<Website>()
            };

            Logger.Instance.Log($"Neue Gruppe erstellt: '{groupName}' am {date}.", LogLevel.Info);
            SaveConfig();
        }

        /// <summary>
        /// Eine App zu einer bestehenden Gruppe hinzufügen
        /// </summary>
        public bool AddAppToGroup(string groupName, string appName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                if (!_config.Gruppen[groupName].Apps.Any(a => a.Name.Equals(appName, StringComparison.OrdinalIgnoreCase)))
                {
                    _config.Gruppen[groupName].Apps.Add(new AppSZ { Name = appName });
                    Logger.Instance.Log($"App '{appName}' zu Gruppe '{groupName}' hinzugefügt.", LogLevel.Info);
                    SaveConfig();
                    return true;
                }
                Logger.Instance.Log($"App '{appName}' existiert bereits in Gruppe '{groupName}'.", LogLevel.Warn);
                return false;
            }
            Logger.Instance.Log($"AddAppToGroup: Gruppe '{groupName}' nicht gefunden.", LogLevel.Warn);
            return false;
        }

        /// <summary>
        /// Eine Website zu einer bestehenden Gruppe hinzufügen
        /// </summary>
        public bool AddWebsiteToGroup(string groupName, string websiteName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                if (!_config.Gruppen[groupName].Websites.Any(w => w.Name.Equals(websiteName, StringComparison.OrdinalIgnoreCase)))
                {
                    _config.Gruppen[groupName].Websites.Add(new Website { Name = websiteName });
                    SaveConfig();
                    Logger.Instance.Log($"Website '{websiteName}' zu Gruppe '{groupName}' hinzugefügt.", LogLevel.Info);
                    return true;
                }
                Logger.Instance.Log($"Website '{websiteName}' existiert bereits in Gruppe '{groupName}'.", LogLevel.Warn);
                return false;
            }
            Logger.Instance.Log($"AddWebsiteToGroup: Gruppe '{groupName}' nicht gefunden.", LogLevel.Warn);
            return false;
        }

        /// <summary>
        /// Änderungen in der Datei speichern
        /// </summary>
        public void SaveConfig()
        {
            try
            {
                string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
                Logger.Instance.Log("Konfiguration gespeichert.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Speichern der Konfiguration: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Löscht eine Gruppe
        /// </summary>
        public bool DeleteGroup(string groupName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                _config.Gruppen.Remove(groupName);
                Logger.Instance.Log($"Gruppe '{groupName}' gelöscht.", LogLevel.Info);
                SaveConfig();
                return true;
            }
            else
            {
                Logger.Instance.Log($"DeleteGroup: Gruppe '{groupName}' existiert nicht.", LogLevel.Warn);
                return false;
            }
        }

        /// <summary>
        /// App aus einer Gruppe löschen
        /// </summary>
        public bool DeleteAppFromGroup(string groupName, string appName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                var apps = _config.Gruppen[groupName].Apps;
                var app = apps.FirstOrDefault(a => a.Name.Equals(appName, StringComparison.OrdinalIgnoreCase));
                if (app != null)
                {
                    apps.Remove(app);
                    Logger.Instance.Log($"App '{appName}' aus Gruppe '{groupName}' gelöscht.", LogLevel.Info);
                    SaveConfig();
                    return true;
                }
                else
                {
                    Logger.Instance.Log($"DeleteAppFromGroup: App '{appName}' nicht in Gruppe '{groupName}' gefunden.", LogLevel.Warn);
                    return false;
                }
            }
            Logger.Instance.Log($"DeleteAppFromGroup: Gruppe '{groupName}' nicht gefunden.", LogLevel.Warn);
            return false;
        }

        /// <summary>
        /// Website aus einer Gruppe löschen
        /// </summary>
        public bool DeleteWebsiteFromGroup(string groupName, string websiteName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                var websites = _config.Gruppen[groupName].Websites;
                var website = websites.FirstOrDefault(w => w.Name.Equals(websiteName, StringComparison.OrdinalIgnoreCase));
                if (website != null)
                {
                    websites.Remove(website);
                    Logger.Instance.Log($"Website '{websiteName}' aus Gruppe '{groupName}' gelöscht.", LogLevel.Info);
                    SaveConfig();
                    return true;
                }
                else
                {
                    Logger.Instance.Log($"DeleteWebsiteFromGroup: Website '{websiteName}' nicht in Gruppe '{groupName}' gefunden.", LogLevel.Warn);
                    return false;
                }
            }
            Logger.Instance.Log($"DeleteWebsiteFromGroup: Gruppe '{groupName}' nicht gefunden.", LogLevel.Warn);
            return false;
        }

        /// <summary>
        /// Aktivität einer Gruppe ändern
        /// </summary>
        public bool SetActiveStatus(string groupName, bool isActive)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                _config.Gruppen[groupName].Aktiv = isActive;
                Logger.Instance.Log($"Aktivitätsstatus der Gruppe '{groupName}' auf {isActive} gesetzt.", LogLevel.Info);
                SaveConfig();
                return true;
            }
            else
            {
                Logger.Instance.Log($"SetActiveStatus: Gruppe '{groupName}' nicht gefunden.", LogLevel.Warn);
                return false;
            }
        }

        public bool ReadActiveStatus(string groupName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                Logger.Instance.Log($"ReadActiveStatus: Gruppe '{groupName}' ist {( _config.Gruppen[groupName].Aktiv ? "aktiv" : "inaktiv")}.", LogLevel.Debug);
                return _config.Gruppen[groupName].Aktiv;
            }
            Logger.Instance.Log($"ReadActiveStatus: Gruppe '{groupName}' nicht gefunden.", LogLevel.Warn);
            return false;
        }

        /// <summary>
        /// Lese den Aktivitätsstatus einer Gruppe aus der Config.json
        /// </summary>
        public bool GetGroupActiveStatus(string groupName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                Logger.Instance.Log($"GetGroupActiveStatus: Gruppe '{groupName}' ist {( _config.Gruppen[groupName].Aktiv ? "aktiv" : "inaktiv")}.", LogLevel.Debug);
                return _config.Gruppen[groupName].Aktiv;
            }
            Logger.Instance.Log($"GetGroupActiveStatus: Gruppe '{groupName}' nicht gefunden.", LogLevel.Warn);
            return false;
        }
    }
}

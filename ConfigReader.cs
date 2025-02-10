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
            // Sicherstellen, dass die Datei existiert oder gültiges JSON enthält
            if (File.Exists(filePath))
            {
                Logger.Instance.Log($" {filePath} exestiert.");
                try
                {
                    string json = File.ReadAllText(filePath);
                    Logger.Instance.Log(json);
                    _config = JsonSerializer.Deserialize<Config>(json);

                    // Wenn die Datei leer oder ungültig ist, wird eine neue Struktur erstellt
                    if (_config == null)
                    {
                        Logger.Instance.Log("Ungültige oder leere Konfigurationsdatei. Eine neue Datei wird erstellt.");
                        CreateDefaultConfig();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Fehler beim Laden oder Deserialisieren der Datei: {ex.Message}");
                    CreateDefaultConfig();
                }
            }
            else
            {
                // Wenn die Datei nicht existiert, eine neue leere Konfiguration erstellen
                Logger.Instance.Log("Datei nicht gefunden. Erstelle eine neue.");
                CreateDefaultConfig();
            }
        }

        private void CreateDefaultConfig()
        {
            _config = new Config { Gruppen = new Dictionary<string, Gruppe>() };
            CreateGroup();
            SaveConfig();
            Logger.Instance.Log("Eine neue Konfigurationsdatei wurde erstellt.");
        }

        /// <summary>
        /// Alle Gruppen als string
        /// </summary>
        /// <returns></returns>
        public string GetAllGroups()
        {
            // Prüfen, ob Gruppen null ist und falls ja, eine leere Zeichenkette zurückgeben
            if (_config.Gruppen == null)
            {
                return string.Empty;
            }

            return string.Join(", ", _config.Gruppen.Keys);
        }

        /// <summary>
        /// Alle Websites, aus aktiven gruppen, als Liste
        /// </summary>
        /// <returns></returns>
        public List<string> GetActiveGroupsDomains()
        {
            var activeDomains = new List<string>();

            // Durchlaufe alle Gruppen und extrahiere die Domains, wenn sie aktiv sind
            foreach (var gruppe in _config.Gruppen)
            {
                if (gruppe.Value.Aktiv)
                {
                    // Füge alle Websites dieser Gruppe zur Liste der aktiven Domains hinzu
                    foreach (var website in gruppe.Value.Websites)
                    {
                        activeDomains.Add(website.Name);
                    }
                }
            }

            return activeDomains;
        }

        /// <summary>
        /// Alle Apps, aus aktiven gruppen, als Liste
        /// </summary>
        /// <returns></returns>
        public List<string> GetActiveGroupsApps()
        {
            var activeApps = new List<string>();

            // Durchlaufe alle Gruppen und extrahiere die Apps, wenn sie aktiv sind
            foreach (var gruppe in _config.Gruppen)
            {
                if (gruppe.Value.Aktiv)
                {
                    // Füge alle Apps dieser Gruppe zur Liste der aktiven Apps hinzu
                    foreach (var app in gruppe.Value.Apps)
                    {
                        activeApps.Add(app.Name);
                    }
                }
            }

            return activeApps;
        }


        /// <summary>
        /// Alle Websites aus einer bestimmten Gruppe als string
        /// </summary>
        /// <param name="groupName"></param>
        /// <returns></returns>
        public string GetWebsitesFromGroup(string groupName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                var websites = _config.Gruppen[groupName].Websites;
                var websiteNames = websites.Select(w => w.Name).ToList();
                return string.Join(", ", websiteNames);
            }

            Logger.Instance.Log($"{groupName} nicht gefunden");
            return string.Empty;
        }

        /// <summary>
        /// Alle Apps aus einer bestimmten Gruppe als string
        /// </summary>
        /// <param name="groupName"></param>
        /// <returns></returns>
        public string GetAppsFromGroup(string groupName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                var apps = _config.Gruppen[groupName].Apps;
                var appNames = apps.Select(a => a.Name).ToList();
                return string.Join(", ", appNames);
            }

            Logger.Instance.Log($"{groupName} nicht gefunden");
            return string.Empty;
        }

        /// <summary>
        /// Erstellt eine neue Gruppe
        /// </summary>
        public void CreateGroup()
        {
            // Bestimme den Namen der neuen Gruppe, basierend auf der höchsten existierenden Gruppen-ID
            int groupNumber = _config.Gruppen.Keys
                .Select(key => key.StartsWith("Gruppe") ? int.Parse(key.Split(' ')[1]) : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;

            string groupName = $"Gruppe {groupNumber}";
            string date = DateTime.Now.ToString("yyyy-MM-dd");
            bool aktiv = true;

            // Erstelle die neue Gruppe
            _config.Gruppen[groupName] = new Gruppe
            {
                Date = date,
                Aktiv = aktiv,
                Apps = new List<AppSZ>(),
                Websites = new List<Website>()
            };

            Logger.Instance.Log($"Gruppe '{groupName}' wurde erstellt.");
            SaveConfig();
        }

        /// <summary>
        /// Eine App zu einer bestehenden Gruppe hinzufügen
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="appName"></param>
        /// <returns></returns>
        public bool AddAppToGroup(string groupName, string appName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                if (!_config.Gruppen[groupName].Apps.Any(a => a.Name.Equals(appName, StringComparison.OrdinalIgnoreCase)))
                {
                    _config.Gruppen[groupName].Apps.Add(new AppSZ { Name = appName });
                    Logger.Instance.Log($"App '{appName}' wurde zu der Gruppe '{groupName}' hinzugefügt.");
                    SaveConfig();
                    return true;
                }
                Logger.Instance.Log($"App '{appName}' existiert bereits in der Gruppe '{groupName}'.");
                return false;
            }

            Logger.Instance.Log($"Gruppe '{groupName}' wurde nicht gefunden.");
            return false;
        }

        /// <summary>
        /// Eine Website zu einer bestehenden Gruppe hinzufügen
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="websiteName"></param>
        /// <returns></returns>
        public bool AddWebsiteToGroup(string groupName, string websiteName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                if (!_config.Gruppen[groupName].Websites.Any(w => w.Name.Equals(websiteName, StringComparison.OrdinalIgnoreCase)))
                {
                    _config.Gruppen[groupName].Websites.Add(new Website { Name = websiteName });
                    SaveConfig();
                    Logger.Instance.Log($"Website '{websiteName}' wurde zu der Gruppe '{groupName}' hinzugefügt.");
                    return true;
                }
                Logger.Instance.Log($"Website '{websiteName}' existiert bereits in der Gruppe '{groupName}'.");
                return false;
            }

            Logger.Instance.Log($"Gruppe '{groupName}' wurde nicht gefunden.");
            return false;
        }

        /// <summary>
        /// Änderungen in der Datei speichern
        /// </summary>
        public void SaveConfig()
        {
            string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Löscht eine Gruppe
        /// </summary>
        /// <param name="groupName"></param>
        /// <returns></returns>
        public bool DeleteGroup(string groupName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                _config.Gruppen.Remove(groupName);
                Logger.Instance.Log($"Gruppe '{groupName}' wurde gelöscht.");
                SaveConfig();
                return true;
            }
            else
            {
                Logger.Instance.Log($"Gruppe '{groupName}' existiert nicht.");
                return false;
            }
        }

        /// <summary>
        /// App aus einer Gruppe löschen
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="appName"></param>
        /// <returns></returns>
        public bool DeleteAppFromGroup(string groupName, string appName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                var apps = _config.Gruppen[groupName].Apps;
                var app = apps.FirstOrDefault(a => a.Name.Equals(appName, StringComparison.OrdinalIgnoreCase));
                if (app != null)
                {
                    apps.Remove(app);
                    Logger.Instance.Log($"App '{appName}' wurde aus der Gruppe '{groupName}' gelöscht.");
                    SaveConfig();
                    return true;
                }
                else
                {
                    Logger.Instance.Log($"App '{appName}' nicht in der Gruppe '{groupName}' gefunden.");
                    return false;
                }
            }

            Logger.Instance.Log($"Gruppe '{groupName}' wurde nicht gefunden.");
            return false;
        }

        /// <summary>
        /// Website aus einer Gruppe löschen
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="websiteName"></param>
        /// <returns></returns>
        public bool DeleteWebsiteFromGroup(string groupName, string websiteName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                var websites = _config.Gruppen[groupName].Websites;
                var website = websites.FirstOrDefault(w => w.Name.Equals(websiteName, StringComparison.OrdinalIgnoreCase));
                if (website != null)
                {
                    websites.Remove(website);
                    Logger.Instance.Log($"Website '{websiteName}' wurde aus der Gruppe '{groupName}' gelöscht.");
                    SaveConfig();
                    return true;
                }
                else
                {
                    Logger.Instance.Log($"Website '{websiteName}' nicht in der Gruppe '{groupName}' gefunden.");
                    return false;
                }
            }

            Logger.Instance.Log($"Gruppe '{groupName}' wurde nicht gefunden.");
            return false;
        }

        /// <summary>
        /// Aktivität einer Gruppe ändern
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="isActive"></param>
        /// <returns></returns>
        public bool SetActiveStatus(string groupName, bool isActive)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                _config.Gruppen[groupName].Aktiv = isActive;
                Logger.Instance.Log($"Der Aktivitätsstatus der Gruppe '{groupName}' wurde auf {isActive} gesetzt.");
                SaveConfig();
                return true;
            }
            else
            {
                Logger.Instance.Log($"Gruppe '{groupName}' wurde nicht gefunden.");
                return false;
            }
        }
    }
}

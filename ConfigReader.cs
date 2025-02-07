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

        // Alle Gruppen als string
        public string GetAllGroups()
        {
            // Prüfen, ob Gruppen null ist und falls ja, eine leere Zeichenkette zurückgeben
            if (_config.Gruppen == null)
            {
                return string.Empty;
            }

            return string.Join(", ", _config.Gruppen.Keys);
        }

        // Alle Gruppen, bei denen "Aktiv" true ist, als string
        public string GetActiveGroups()
        {
            var activeGroups = new List<string>();
            foreach (var gruppe in _config.Gruppen)
            {
                if (gruppe.Value.Aktiv)
                {
                    activeGroups.Add(gruppe.Key);
                }
            }
            return string.Join(", ", activeGroups);
        }

        // Alle Websites aus einer bestimmten Gruppe als string
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

        // Alle Apps aus einer bestimmten Gruppe als string
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

        // Erstellt eine neue Gruppe
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

        // Eine App zu einer bestehenden Gruppe hinzufügen
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

        // Eine Website zu einer bestehenden Gruppe hinzufügen
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

        // Änderungen in der Datei speichern (optional)
        public void SaveConfig()
        {
            string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(filePath, json);
        }

        // Löscht eine Gruppe
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

        // App aus einer Gruppe löschen
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

        // Website aus einer Gruppe löschen
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

        // Aktivität einer Gruppe ändern
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

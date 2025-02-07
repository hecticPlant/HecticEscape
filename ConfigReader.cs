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
            // Sicherstellen, dass die Datei existiert
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                _config = JsonSerializer.Deserialize<Config>(json);
            }
            else
            {
                // Wenn die Datei nicht existiert, eine neue leere Konfiguration erstellen
                _config = new Config { Gruppen = new Dictionary<string, Gruppe>() };
            }
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
                var websiteNames = new List<string>();
                foreach (var website in websites)
                {
                    websiteNames.Add(website.Name);
                }
                return string.Join(", ", websiteNames);
            }
            Logger.Instance.Log($"{groupName} nicht gefunden");
            return null;
        }

        // Alle Apps aus einer bestimmten Gruppe als string
        public string GetAppsFromGroup(string groupName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                var apps = _config.Gruppen[groupName].Apps;
                var appNames = new List<string>();
                foreach (var app in apps)
                {
                    appNames.Add(app.Name);
                }
                return string.Join(", ", appNames);
            }
            Logger.Instance.Log($"{groupName} nicht gefunden");
            return null;
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
        }

        // Eine App zu einer bestehenden Gruppe hinzufügen
        public void AddAppToGroup(string groupName, string appName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                _config.Gruppen[groupName].Apps.Add(new AppSZ { Name = appName });
            }
            else
            {
                Logger.Instance.Log($"Gruppe {groupName} wurde nicht gefunden.");
            }
        }

        // Eine Website zu einer bestehenden Gruppe hinzufügen
        public void AddWebsiteToGroup(string groupName, string websiteName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                _config.Gruppen[groupName].Websites.Add(new Website { Name = websiteName });
            }
            else
            {
                Logger.Instance.Log($"Gruppe {groupName} wurde nicht gefunden.");
            }
        }

        // Änderungen in der Datei speichern (optional)
        public void SaveConfig(string filePath)
        {
            string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(filePath, json);
        }

        public void DeleteGroup(string groupName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                _config.Gruppen.Remove(groupName);
                Logger.Instance.Log($"Gruppe '{groupName}' wurde gelöscht.");
            }
            else
            {
                Logger.Instance.Log($"Gruppe '{groupName}' existiert nicht.");
            }
        }

        // App aus einer Gruppe löschen
        public void DeleteAppFromGroup(string groupName, string appName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                var apps = _config.Gruppen[groupName].Apps;
                var app = apps.FirstOrDefault(a => a.Name.Equals(appName, StringComparison.OrdinalIgnoreCase));
                if (app != null)
                {
                    apps.Remove(app);
                    Logger.Instance.Log($"App '{appName}' wurde aus der Gruppe '{groupName}' gelöscht.");
                }
                else
                {
                    Logger.Instance.Log($"App '{appName}' nicht in der Gruppe '{groupName}' gefunden.");
                }
            }
            else
            {
                Logger.Instance.Log($"Gruppe '{groupName}' wurde nicht gefunden.");
            }
        }

        // Website aus einer Gruppe löschen
        public void DeleteWebsiteFromGroup(string groupName, string websiteName)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                var websites = _config.Gruppen[groupName].Websites;
                var website = websites.FirstOrDefault(w => w.Name.Equals(websiteName, StringComparison.OrdinalIgnoreCase));
                if (website != null)
                {
                    websites.Remove(website);
                    Logger.Instance.Log($"Website '{websiteName}' wurde aus der Gruppe '{groupName}' gelöscht.");
                }
                else
                {
                    Logger.Instance.Log($"Website '{websiteName}' nicht in der Gruppe '{groupName}' gefunden.");
                }
            }
            else
            {
                Logger.Instance.Log($"Gruppe '{groupName}' wurde nicht gefunden.");
            }
        }

        // Aktivität einer Gruppe ändern
        public void SetActiveStatus(string groupName, bool isActive)
        {
            if (_config.Gruppen.ContainsKey(groupName))
            {
                _config.Gruppen[groupName].Aktiv = isActive;
                Logger.Instance.Log($"Der Aktivitätsstatus der Gruppe '{groupName}' wurde auf {isActive} gesetzt.");
            }
            else
            {
                Logger.Instance.Log($"Gruppe '{groupName}' wurde nicht gefunden.");
            }
        }
    }
}

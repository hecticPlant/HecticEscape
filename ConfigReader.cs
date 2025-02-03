using System.Text.Json.Nodes;
using System.IO;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using System.Windows;

namespace ScreenZen
{
    /// <summary>
    /// Diese Klasse verwaltet die Config.json Datei
    /// </summary>
    public class ConfigReader
    {
        private JsonObject config;
        private string filePath = "Config.JSON";

        public ConfigReader()
        {
            if (!File.Exists(filePath))
            {
                CreateDefaultConfig();
            }
            string jsonString = File.ReadAllText(filePath);
            config = JsonNode.Parse(jsonString).AsObject();
        }

        /// <summary>
        /// Erstellt eine Deafult Config
        /// </summary>
        private void CreateDefaultConfig()
        {
            // Standardkonfiguration für die Datei
            var defaultConfig = new JsonObject
            {
                ["Gruppe 1"] = new JsonObject
                {
                    ["Date"] = DateTime.Now.ToString("yyyy-MM-dd"),
                    ["Aktiv"] = true,
                    ["Apps"] = new JsonArray { new JsonObject { ["Name"] = "Spotify" } },
                    ["Websites"] = new JsonArray { new JsonObject { ["Name"] = "youtube" } }
                }
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(filePath, defaultConfig.ToJsonString(options));
            Console.WriteLine($"Datei '{filePath}' wurde erstellt.");
        }

        /// <summary>
        /// Erstelle eine Neue Gruppe
        /// </summary>
        private void CreateNewGroup()
        {
            try
            {
                // Falls die Datei nicht existiert, erstelle sie
                if (!File.Exists(filePath))
                {
                    File.WriteAllText(filePath, "{}"); // Leere JSON-Struktur erstellen
                }
                JsonObject jsonObject = config;

                // Finde einen eindeutigen Gruppennamen
                int groupNumber = 1;
                string newGroupName;
                do
                {
                    newGroupName = "Gruppe " + groupNumber;
                    groupNumber++;
                } while (jsonObject.ContainsKey(newGroupName));

                // Neue Gruppe erstellen mit aktueller Struktur
                JObject newGroup = new JObject
                {
                    ["Date"] = DateTime.Now.ToString("yyyy-MM-dd"), // Setzt das heutige Datum
                    ["Aktiv"] = false,
                    ["Apps"] = new JArray(), // Leere App-Liste
                    ["Websites"] = new JArray() // Leere Website-Liste
                };

                // Neue Gruppe zum JSON hinzufügen
                jsonObject[newGroupName] = newGroupName;

                // Aktualisierte JSON-Datei speichern
                SaveConfig();

                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Neue Gruppe '{newGroupName}' wurde erfolgreich erstellt.");
            }
            catch (Exception ex)
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler beim Erstellen der neuen Gruppe: {ex.Message}");
            }
        }

        /// <summary>
        /// Gibt alle aktiven Gruppen zurück
        /// </summary>
        /// <returns>JsonObject mit allen aktiven Gruppen</returns>
        public JsonObject GetAktiveGroups()
        {
            JsonObject aktiveGroups = new JsonObject();

            foreach (var group in config.AsObject())
            {
                var groupName = group.Key;
                var groupData = group.Value.AsObject();

                // Prüfen, ob die Gruppe das Attribut "Aktiv" hat und ob es true ist
                if (groupData.ContainsKey("Aktiv") && groupData["Aktiv"].GetValue<bool>())
                {
                    aktiveGroups[groupName] = groupData;
                }
            }

            return aktiveGroups;
        }

        ///<summary>Gibt die Komplette Gruppe oder Elemente aus der Gruppe zurück</summary>
        /// <param name= "groupId">Gruppen Name. Z.B. Gruppe 1</param>
        /// <param name="attribute">Attribu das geladen werden soll
        /// a gibt Apps zurück
        /// w: Websites
        /// d: Date
        /// s: Status
        ///</param>
        public JsonNode ReadConfig(string ?groupId = null, string ?attribute = null)
        {
            if(groupId == null)
            {
                return config;
            }
            string groupKey = $"Gruppe {groupId}";

            if (!config.ContainsKey(groupKey))
            {
                Console.WriteLine($"Gruppe {groupId} nicht gefunden.");
                return null;
            }

            var groupData = config[groupKey];

            if (attribute == null)
            {
                // Ganze Gruppe zurückgeben
                return groupData;
            }
            else
            {
                // Attribute abrufen
                switch (attribute.ToLower())
                {
                    case "a": // Apps
                        return groupData["Apps"];
                    case "w": // Websites
                        return groupData["Websites"];
                    case "d": // Datum
                        return groupData["Date"];
                    case "s": // Aktivstatus
                        return groupData["Aktiv"];
                    default:
                        Console.WriteLine("Ungültiges Attribut. Verwende 'a' für Apps, 'w' für Websites, 'Date' für Datum oder 'Aktiv' für Aktivstatus.");
                        return null;
                }
            }
        }

        /// <summary>
        /// Fügt einem Attribut einen Wert hinzu.
        /// </summary>
        /// <param name="groupId">Gruppenname</param>
        /// <param name="attribute">Attributname
        ///     a: Apps
        ///     w: Websites
        /// </param>
        /// <param name="newValue">Wert, der hinzugefügt werden soll</param>
        public void AppendToConfig(string groupId, string attribute, string newValue)
        {
            string groupKey = $"Gruppe {groupId}";

            if (!config.ContainsKey(groupKey))
            {
                Logger.Instance.Log($"Gruppe {groupId} nicht gefunden.");
                return;
            }

            var groupData = config[groupKey];

            switch (attribute.ToLower())
            {
                case "a":  // Apps aktualisieren
                    JsonArray apps = groupData["Apps"].AsArray();

                    // Überprüfe, ob die App bereits existiert
                    bool appExists = apps.Any(app => app["Name"].ToString().Trim().Equals(newValue, StringComparison.OrdinalIgnoreCase));

                    if (appExists)
                    {
                        Logger.Instance.Log($"Versuche '{newValue}' den Apps der Gruppe '{groupId}' hinzuzufügen, aber '{newValue}' existiert bereits in der Gruppe.");
                        return;
                    }

                    apps.Add(newValue);
                    SaveConfig();
                    break;

                case "w":  // Websites aktualisieren
                    JsonArray websites = groupData["Websites"].AsArray();

                    // Überprüfe, ob die Website bereits existiert
                    bool websiteExists = websites.Any(website => website["Name"].ToString().Trim().Equals(newValue, StringComparison.OrdinalIgnoreCase));

                    if (websiteExists)
                    {
                        Logger.Instance.Log($"Versuche '{newValue}' den Websites der Gruppe '{groupId}' hinzuzufügen, aber '{newValue}' existiert bereits in der Gruppe.");
                        return;
                    }

                    websites.Add(newValue);
                    SaveConfig();
                    break;

                default:
                    Logger.Instance.Log($"Ungültiges Attribut. Verwende 'a' für Apps oder 'w' für Websites. Wert: {attribute}");
                    return;
            }
        }

        /// <summary>
        /// Entfernt einen Wert aus einem Attribut
        /// </summary>
        /// <param name="groupId">Gruppen Name</param>
        /// <param name="attribute">Attribut Name
        ///     a: Apps
        ///     w: Websites
        /// </param>
        /// <param name="valueToRemove">Wert, der entfernt werden soll</param>
        public void RemoveFromConfig(string groupId, string attribute, string valueToRemove)
        {
            string groupKey = $"Gruppe {groupId}";

            if (!config.ContainsKey(groupKey))
            {
                Logger.Instance.Log($"Gruppe {groupId} nicht gefunden.");
                return;
            }

            var groupData = config[groupKey];

            switch (attribute.ToLower())
            {
                case "a":  // Apps aktualisieren
                    JsonArray apps = groupData["Apps"].AsArray();

                    // Überprüfe, ob die App existiert
                    var appToRemove = apps.FirstOrDefault(app => app["Name"].ToString().Trim().Equals(valueToRemove, StringComparison.OrdinalIgnoreCase));

                    if (appToRemove != null)
                    {
                        apps.Remove(appToRemove);
                        SaveConfig();
                    }
                    else
                    {
                        Logger.Instance.Log($"App '{valueToRemove}' existiert nicht in der Gruppe '{groupId}'.");
                    }
                    break;

                case "w":  // Websites aktualisieren
                    JsonArray websites = groupData["Websites"].AsArray();

                    // Überprüfe, ob die Website existiert
                    var websiteToRemove = websites.FirstOrDefault(website => website["Name"].ToString().Trim().Equals(valueToRemove, StringComparison.OrdinalIgnoreCase));

                    if (websiteToRemove != null)
                    {
                        websites.Remove(websiteToRemove);
                        SaveConfig();
                    }
                    else
                    {
                        Logger.Instance.Log($"Website '{valueToRemove}' existiert nicht in der Gruppe '{groupId}'.");
                    }
                    break;

                default:
                    Logger.Instance.Log($"Ungültiges Attribut. Verwende 'a' für Apps oder 'w' für Websites. Wert: {attribute}");
                    return;
            }
        }

        /// <summary>
        /// Schreibt in die Config.json Datei. 
        /// </summary>
        private void SaveConfig()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(filePath, config.ToJsonString(options));

        }
    }
}



using System.Text.Json.Nodes;
using System.IO;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using System.Windows;
using System.Text.RegularExpressions;

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
            UpdateConfig();
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
        public void CreateNewGroup()
        {
            try
            {
                JsonObject newConfig = config;

                // Finde einen eindeutigen Gruppennamen
                int groupNumber = 1;
                string newGroupName;
                do
                {
                    newGroupName = "Gruppe " + groupNumber;
                    groupNumber++;
                } while (newConfig.ContainsKey(newGroupName));

                var newGroup = new JsonObject
                {
                    ["Date"] = DateTime.Now.ToString("yyyy-MM-dd"), // Setzt das heutige Datum
                    ["Aktiv"] = false,
                    ["Apps"] = new JsonArray(), // Leere App-Liste
                    ["Websites"] = new JsonArray() // Leere Website-Liste
                };

                // Neue Gruppe zum JSON hinzufügen
                newConfig[newGroupName] = newGroup;

                // Aktualisierte JSON-Datei speichern
                SaveConfig(newConfig);

                Logger.Instance.Log($"Neue Gruppe '{newGroupName}' wurde erfolgreich erstellt.");
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Erstellen der neuen Gruppe: {ex.Message}");
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

        /// <summary>
        /// Gibt die komplette Gruppe oder ein bestimmtes Element als JsonObject zurück.
        /// </summary>
        /// <param name="groupId">Gruppenname, z. B. "Gruppe 1".</param>
        /// <param name="attribute">Attribut, das geladen werden soll:
        ///   "a" gibt Apps zurück,
        ///   "w" gibt Websites zurück,
        ///   "d" gibt das Datum zurück,
        ///   "s" gibt den Aktivstatus zurück.
        /// </param>
        /// <returns>Die gesamte Gruppe oder das gewünschte Attribut als JsonObject.</returns>
        public JsonObject ReadConfig(string? groupId = null, string? attribute = null)
        {
            if (groupId == null)
            {
                return config; // Gesamte Konfiguration zurückgeben
            }

            if (!config.ContainsKey(groupId))
            {
                Logger.Instance.Log($"Gruppe {groupId} nicht gefunden.");
                return new JsonObject(); // Leeres Objekt statt null zurückgeben
            }

            JsonObject groupData = config[groupId].AsObject();

            if (attribute == null)
            {
                return groupData; // Ganze Gruppe zurückgeben
            }

            // Ein bestimmtes Attribut abrufen und in ein JsonObject packen
            JsonNode? value = attribute.ToLower() switch
            {
                "a" => groupData["Apps"],
                "w" => groupData["Websites"],
                "d" => groupData["Date"],
                "s" => groupData["Aktiv"],
                _ => null
            };

            if (value == null)
            {
                Logger.Instance.Log($"Ungültiges Attribut '{attribute}'. Verwende 'a' für Apps, 'w' für Websites, 'd' für Datum oder 's' für Aktivstatus.");
                return new JsonObject();
            }

            return new JsonObject { [attribute] = value };
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
            if (!config.ContainsKey(groupId))
            {
                Logger.Instance.Log($"Gruppe {groupId} nicht gefunden.");
                return;
            }

            JsonObject newConfig = config;
            var groupData = newConfig[groupId];

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

                    JsonObject newApp = new JsonObject { ["Name"] = newValue };
                    apps.Add(newApp);
                    Logger.Instance.Log($"'{newValue}' wurde den Apps der Gruppe '{groupId}' hizugefügt");
                    SaveConfig(newConfig);
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

                    JsonObject newWebsite = new JsonObject { ["Name"] = newValue };
                    websites.Add(newWebsite);
                    SaveConfig(newConfig);
                    break;

                default:
                    Logger.Instance.Log($"Ungültiges Attribut. Verwende 'a' für Apps oder 'w' für Websites. Wert: {attribute}");
                    return;
            }
        }

        /// <summary>
        /// Entfernt einen Wert aus einem Attribut oder löscht eine ganze Gruppe
        /// </summary>
        /// <param name="groupId">Gruppen Name</param>
        /// <param name="attribute">
        /// Attribut Name:
        ///     a: Apps
        ///     w: Websites
        ///     g: Ganze Gruppe löschen
        /// </param>
        /// <param name="valueToRemove">Wert, der entfernt werden soll (wird nicht benötigt, wenn eine ganze Gruppe gelöscht wird)</param>
        public void RemoveFromConfig(string groupId, string attribute, string valueToRemove)
        {
            string groupKey = groupId;

            if (!config.ContainsKey(groupKey))
            {
                Logger.Instance.Log($"Gruppe {groupId} nicht gefunden.");
                return;
            }

            JsonObject newConfig = config;
            var groupData = newConfig[groupKey];

            switch (attribute.ToLower())
            {
                case "a":  // Apps aktualisieren
                    JsonArray apps = groupData["Apps"].AsArray();

                    // Überprüfe, ob die App existiert
                    var appToRemove = apps.FirstOrDefault(app => app["Name"].ToString().Trim().Equals(valueToRemove, StringComparison.OrdinalIgnoreCase));

                    if (appToRemove != null)
                    {
                        apps.Remove(appToRemove);
                        SaveConfig(newConfig);
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
                        SaveConfig(newConfig);
                    }
                    else
                    {
                        Logger.Instance.Log($"Website '{valueToRemove}' existiert nicht in der Gruppe '{groupId}'.");
                    }
                    break;

                case "g":  // Ganze Gruppe löschen
                    config.Remove(groupKey);
                    SaveConfig(newConfig);
                    Logger.Instance.Log($"Gruppe '{groupId}' wurde gelöscht.");
                    break;

                default:
                    Logger.Instance.Log($"Ungültiges Attribut. Verwende 'a' für Apps, 'w' für Websites oder 'g' für das Löschen der gesamten Gruppe. Wert: {attribute}");
                    return;
            }
        }

        /// <summary>
        /// Schreibt in die Config.json Datei. 
        /// </summary>
        private void SaveConfig(JsonObject newConfig)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(filePath, newConfig.ToJsonString(options));
            Logger.Instance.Log("Saving to file");

        }

        /// <summary>
        /// Togglet den Aktivitätsstatus einer Gruppe anhand des Schlüssels.
        /// </summary>
        /// <param name="groupKey">Der Schlüssel der Gruppe</param>
        public void ToggleGroup(string groupKey)
        {
            JsonObject newConfig = config;
            newConfig = ReadConfig();
            JsonNode? groups = config?["Gruppen"];
            if (groups == null)
            {
                return;
            }
            JsonNode? group = groups[groupKey];
            if (group == null)
            {
                return;
            }
            if (group["Aktiv"] is JsonValue aktivValue && aktivValue.TryGetValue<bool>(out bool aktiv))
            {
                group["Aktiv"] = !aktiv;

                SaveConfig(newConfig);
            }
            else
            {
                group["Aktiv"] = true;

                SaveConfig(newConfig);
            }
        }

        /// <summary>
        /// Updatet die Config
        /// </summary>
        private void UpdateConfig()
        {
            if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            {
                CreateDefaultConfig();
                return;
            }

            string jsonString = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(jsonString))
            {
                CreateDefaultConfig();
                return;
            }

            config = JsonNode.Parse(jsonString).AsObject();
        }
    }
}



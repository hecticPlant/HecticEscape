using System.IO;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace ScreenZen
{
    public class WebManagerSZ
    {
        public WebManagerSZ()
        {
            this.currentDirectory = Directory.GetCurrentDirectory();
        }

        string currentDirectory;
        public void SaveSelectedWebsiteToFile(string selectedGroup, string websiteName)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Config.json"); // JSON-Datei mit den Gruppen

            try
            {
                // Überprüfe, ob die Datei existiert
                if (!File.Exists(filePath))
                {
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Die Datei '{filePath}' wurde nicht gefunden.");
                    return;
                }

                // Lese den Inhalt der JSON-Datei
                string jsonContent = File.ReadAllText(filePath);
                JObject jsonObject = JObject.Parse(jsonContent);

                // Überprüfen, ob die angegebene Gruppe existiert
                if (!jsonObject.ContainsKey(selectedGroup))
                {
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Die Gruppe '{selectedGroup}' existiert nicht.");
                    return;
                }

                // Hole die "Websites"-Liste der ausgewählten Gruppe
                JObject selectedGroupObject = (JObject)jsonObject[selectedGroup];
                JArray websites = (JArray)selectedGroupObject["Websites"];

                // Überprüfe, ob die Website bereits existiert
                bool websiteExists = websites.Any(website => website["Name"].ToString().Trim() == websiteName);

                if (websiteExists)
                {
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole($"'{websiteName}' existiert bereits in der Gruppe '{selectedGroup}'.");
                    return;
                }

                // Erstelle ein neues Website-Objekt gemäß neuer JSON-Struktur
                JObject newWebsite = new JObject
                {
                    ["Name"] = websiteName,
                    ["Blockiert"] = false // Standardwert
                };

                // Füge das neue Website-Objekt zur "Websites"-Liste hinzu
                websites.Add(newWebsite);

                // Speichere die Änderungen in der JSON-Datei
                File.WriteAllText(filePath, jsonObject.ToString());

                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Website '{websiteName}' wurde erfolgreich in der Gruppe '{selectedGroup}' gespeichert.");
            }
            catch (Exception ex)
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler beim Speichern der Datei: {ex.Message}");
            }
        }

        public void RemoveSelectedWebsiteFromFile(string selectedGroup, string websiteName)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Config.json");  // Die Datei, in der die Gruppen gespeichert sind

            try
            {
                // Überprüfen, ob die Datei existiert
                if (File.Exists(filePath))
                {
                    // Lese den Inhalt der JSON-Datei
                    string jsonContent = File.ReadAllText(filePath);
                    JObject jsonObject = JObject.Parse(jsonContent);

                    // Überprüfen, ob die angegebene Gruppe existiert
                    if (jsonObject.ContainsKey(selectedGroup))
                    {
                        // Hole die "Websites"-Liste der ausgewählten Gruppe
                        JObject selectedGroupObject = (JObject)jsonObject[selectedGroup];
                        JArray websites = (JArray)selectedGroupObject["Websites"];

                        // Suche nach der Website, die entfernt werden soll, indem der "Name"-Wert verglichen wird
                        var websiteToRemove = websites.FirstOrDefault(website => website["Name"].ToString().Trim() == websiteName);

                        if (websiteToRemove != null)
                        {
                            // Entferne die Website aus der Liste
                            websites.Remove(websiteToRemove);
                            ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Website '{websiteName}' wurde erfolgreich aus der Gruppe '{selectedGroup}' entfernt.");
                        }
                        else
                        {
                            ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Website '{websiteName}' wurde in der Gruppe '{selectedGroup}' nicht gefunden.");
                        }

                        // Speichere die Änderungen in der JSON-Datei
                        File.WriteAllText(filePath, jsonObject.ToString());
                    }
                    else
                    {
                        ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Die Gruppe '{selectedGroup}' existiert nicht.");
                    }
                }
                else
                {
                    ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Die Datei '{filePath}' wurde nicht gefunden.");
                }
            }
            catch (Exception ex)
            {
                ((MainWindow)Application.Current.MainWindow).AppendToConsole($"Fehler beim Entfernen der Website: {ex.Message}");
            }
        }


    }
}
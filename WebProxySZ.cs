using System.Text;
using System.IO;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;
using Newtonsoft.Json.Linq;

namespace ScreenZen
{
    public class WebProxySZ
    {
        // Liste der geblockten Domains
        private static List<string> blockedDomains = new List<string>();

        private ProxyServer proxy;
        private bool isProxyRunning;

        public WebProxySZ()
        {
            proxy = new ProxyServer();
            isProxyRunning = false;
        }

        public async Task StartProxy()
        {
            if (isProxyRunning)
            {
                // Lese die geblockten Domains aus der Datei
                await Task.Run(() => ReadBlockedDomains()); // Hier wird das Lesen der Datei asynchron gemacht

                // Endpunkt für den Proxy (Port 8888)
                var proxyEndPoint = new ExplicitProxyEndPoint(System.Net.IPAddress.Any, 8888, true);
                proxy.AddEndPoint(proxyEndPoint);

                // Event-Handler für eingehende Anfragen
                proxy.BeforeRequest += OnRequestAsync;

                // Starte den Proxy
                await Task.Run(() => proxy.Start()); // Auch das Starten des Proxys asynchron ausführen
                proxy.SetAsSystemProxy(proxyEndPoint, ProxyProtocolType.AllHttp);

                Logger.Instance.Log("Proxy läuft auf Port 8888...");
                isProxyRunning = true;
            }
            else
            {
                Logger.Instance.Log("Proxy läuft bereits");
            }
        }

        public async Task StopProxy()
        {
            Task.Run(() => proxy.Stop());
            isProxyRunning= false;
        }


        // Event-Handler, um Anfragen zu blockieren
        private static async Task OnRequestAsync(object sender, SessionEventArgs e)
        {
            var requestUrl = e.HttpClient.Request.Url;
            Console.WriteLine($"Anfrage erhalten: {requestUrl}");

            // Überprüfe, ob die angeforderte URL mit einer der geblockten Domains übereinstimmt
            foreach (var domain in blockedDomains)
            {
                if (requestUrl.Contains(domain))
                {
                    Console.WriteLine($"Blockiere {requestUrl}");

                    // Sende eine 403-Fehlermeldung zurück
                    var blockMessage = "Zugriff auf diese Website ist blockiert.";
                    var responseBytes = Encoding.UTF8.GetBytes(blockMessage);

                    // Verwende e.Ok(), um eine benutzerdefinierte Antwort zu senden
                    e.Ok(responseBytes);
                    e.HttpClient.Response.StatusCode = 403;
                    e.HttpClient.Response.StatusDescription = "Forbidden";
                    e.HttpClient.Response.ContentType = "text/plain";
                    break; // Keine weitere Prüfung der Domains
                }
            }
        }

        //JSON 
        // Methode, um die geblockten Domains aus der Datei zu lesen
        private static void ReadBlockedDomains()
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Config.json");  // Der Pfad zur Konfigurationsdatei

            if (File.Exists(filePath))
            {
                try
                {
                    // Lese den Inhalt der JSON-Datei
                    string jsonContent = File.ReadAllText(filePath);
                    JObject jsonObject = JObject.Parse(jsonContent);

                    // Iteriere durch jede Gruppe im JSON
                    foreach (var group in jsonObject)
                    {
                        // Hole die Websites-Liste der jeweiligen Gruppe
                        JObject groupObject = (JObject)group.Value;
                        JArray websites = (JArray)groupObject["Websites"];

                        if (websites != null && websites.Count > 0)
                        {
                            foreach (JObject website in websites)
                            {
                                // Füge den Namen der Website zur blockierten Domain-Liste hinzu
                                string websiteName = website["Name"].ToString().Trim();
                                if (!string.IsNullOrWhiteSpace(websiteName) && !blockedDomains.Contains(websiteName))
                                {
                                    blockedDomains.Add(websiteName);
                                }
                            }
                        }
                    }

                    Console.WriteLine("Geblockte Websites wurden erfolgreich geladen.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fehler beim Lesen der Datei: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Die Datei 'Config.json' wurde nicht gefunden.");
            }
        }

        private static void RemoveBlockedDomains()
        {
            try
            {
                blockedDomains.Clear();
                Console.WriteLine("Geblockte Domains wurden entblockt.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim entblocken der geblockten Domains: {ex.Message}");
            }

        }
    }
}
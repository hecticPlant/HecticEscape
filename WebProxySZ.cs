using System.Text;
using System.IO;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;
using Newtonsoft.Json.Linq;
using System.Windows.Documents;

namespace ScreenZen
{
    /// <summary>
    /// Blockiert die Domains
    /// </summary>
    public class WebProxySZ
    {
        /// <summary>
        /// Liste der geblockten Domains
        /// </summary>
        private List<string> blockedDomains = new List<string>();
        private ExplicitProxyEndPoint proxyEndPoint;
        private ProxyServer proxy;
        private bool isProxyRunning;

        public WebProxySZ()
        {
            proxy = new ProxyServer();
            isProxyRunning = false;
            proxyEndPoint = new ExplicitProxyEndPoint(System.Net.IPAddress.Any, 8888, true);
            proxy.AddEndPoint(proxyEndPoint);
        }

        /// <summary>
        /// Setzt isProxyRunning
        /// </summary>
        /// <param name="isProxyRunning"></param>
        public void setIsProxyRunnin(bool isProxyRunning)
        {
            this.isProxyRunning = isProxyRunning;
        }
        
        /// <summary>
        /// Gibt den Wert von isProxyRunning zurück
        /// </summary>
        /// <returns></returns>
        public bool getIsProxyRunning()
        {
            return isProxyRunning; 
        }

        /// <summary>
        /// Startet den Proxy-Server, richtet einen Endpunkt auf Port 8888 ein und 
        /// registriert einen Event-Handler, um eingehende Anfragen zu überprüfen und gegebenenfalls zu blockieren.
        /// </summary>
        /// <returns>Ein asynchroner Task, der den Startvorgang des Proxy-Servers repräsentiert.</returns>
        public async Task StartProxy()
        {
            if (!isProxyRunning)
            {
                try
                {
                    await Task.Run(() => proxy.Start());
                    proxy.SetAsSystemProxy(proxyEndPoint, ProxyProtocolType.AllHttp);
                    Logger.Instance.Log("Proxy läuft auf Port 8888...");
                    isProxyRunning = true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Fehler beim Starten des Proxys: {ex.Message}");
                }
            }
            else
            {
                Logger.Instance.Log("Proxy läuft bereits");
            }
        }

        /// <summary>
        /// Stoppt den Proxy-Server und entfernt die System-Proxy-Einstellungen.
        /// </summary>
        /// <returns>Ein asynchroner Task, der den Stoppvorgang des Proxy-Servers repräsentiert.</returns>
        public async Task StopProxy()
        {
            if (isProxyRunning)
            {
                await Task.Run(() => proxy.Stop());
                isProxyRunning = false;
                Logger.Instance.Log("Proxy wurde gestoppt.");
            }
            else
            {
                Logger.Instance.Log("Proxy läuft nicht.");
            }
        }

        /// <summary>
        /// Überprüft eingehende HTTP-Anfragen und blockiert den Zugriff auf Websites, 
        /// die in der Liste der geblockten Domains enthalten sind. 
        /// Wenn eine blockierte Domain erkannt wird, wird eine 403-Fehlermeldung zurückgegeben.
        /// </summary>
        /// <param name="sender">Das Objekt, das das Event ausgelöst hat (in diesem Fall der Proxy-Server).</param>
        /// <param name="e">Die Event-Argumente, die Informationen über die HTTP-Anfrage enthalten.</param>
        /// <returns>Ein asynchroner Task, der die Verarbeitung der Anfrage steuert.</returns>
        private async Task OnRequestAsync(object sender, SessionEventArgs e)
        {
            try
            {
                var requestUrl = e.HttpClient.Request.Url;
                Console.WriteLine($"Anfrage erhalten: {requestUrl}");

                // Extrahiere den Hostnamen aus der URL
                if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out var uri))
                {
                    Console.WriteLine($"Ungültige URL: {requestUrl}");
                    return;
                }

                string requestHost = uri.Host.ToLowerInvariant();

                // Überprüfe, ob der Host mit einer der geblockten Domains übereinstimmt
                foreach (var domain in blockedDomains)
                {
                    if (requestHost.EndsWith(domain.ToLowerInvariant()))
                    {
                        Console.WriteLine($"Blockiere {requestUrl}");

                        // Sende eine 403-Fehlermeldung zurück
                        var blockMessage = "Zugriff auf diese Website ist blockiert.";
                        var responseBytes = Encoding.UTF8.GetBytes(blockMessage);

                        // e.Ok ist eine void-Methode, daher ohne await aufrufen
                        e.Ok(responseBytes);

                        e.HttpClient.Response.StatusCode = 403;
                        e.HttpClient.Response.StatusDescription = "Forbidden";
                        e.HttpClient.Response.ContentType = "text/plain";
                        break; // Keine weitere Prüfung der Domains
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler bei der Verarbeitung der Anfrage: {ex.Message}");
            }
        }

        /// <summary>
        /// Setzt die List der geblockten Domains 
        /// </summary>
        /// <param name="blockedDomains">Liste der geblockten Domains</param>
        public void setBlockedDomains(List<string> blockedDomains)
        {
            this.blockedDomains = blockedDomains;
        }

        /// <summary>
        /// Leert die Liste der geblockten Domains
        /// </summary>
        private void RemoveBlockedDomains()
        {
            try
            {
                blockedDomains.Clear();
                Logger.Instance.Log("Geblockte Domains wurden entblockt.");
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim entblocken der geblockten Domains: {ex.Message}");
            }

        }
    }
}
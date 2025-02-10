using System.Text;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;
using Titanium.Web.Proxy;
using System.Diagnostics;

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
        public event Action<bool> ProxyStatusChanged;

        /// <summary>
        /// Konstruktor für WebProxySZ. Initialisiert den Proxy und fügt den Endpunkt hinzu.
        /// </summary>
        public WebProxySZ()
        {
            proxy = new ProxyServer();
            proxyEndPoint = new ExplicitProxyEndPoint(System.Net.IPAddress.Any, 8888, true);
            proxy.AddEndPoint(proxyEndPoint);

            proxy.BeforeRequest += OnRequestAsync;
            isProxyRunning = false;
        }

        /// <summary>
        /// Gibt den aktuellen Status des Proxys zurück.
        /// </summary>
        public bool IsProxyRunning
        {
            get
            {
                return isProxyRunning;
            }
            private set
            {
                if (isProxyRunning != value)
                {
                    isProxyRunning = value;
                    ProxyStatusChanged?.Invoke(isProxyRunning);  // Event auslösen
                    Logger.Instance.Log($"IsProxyRunning geändert: {isProxyRunning}");
                }
            }
        }

        /// <summary>
        /// Startet den Proxy, falls er noch nicht läuft. Setzt den System-Proxy auf den lokalen Proxy.
        /// </summary>
        public async Task StartProxy()
        {
            if (!isProxyRunning)
            {
                try
                {
                    Logger.Instance.Log("Starte Proxy und setze System-Proxy.");
                    // Setze den System-Proxy auf den lokalen Proxy
                    SetSystemProxy();

                    await Task.Run(() => proxy.Start());
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
            Logger.Instance.Log("StartProxy abgeschlossen.");
        }

        /// <summary>
        /// Stoppt den Proxy und setzt den System-Proxy zurück.
        /// </summary>
        public async Task StopProxy()
        {
            if (isProxyRunning)
            {
                await Task.Run(() => proxy.Stop());

                // Setze den System-Proxy zurück
                ResetSystemProxy();

                isProxyRunning = false;
                Logger.Instance.Log("Proxy wurde gestoppt und System-Proxy zurückgesetzt.");
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
        private async Task OnRequestAsync(object sender, SessionEventArgs e)
        {
            try
            {
                var requestUrl = e.HttpClient.Request.Url;
                Logger.Instance.Log($"Anfrage erhalten: {requestUrl}");

                // Extrahiere den Hostnamen aus der URL
                if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out var uri))
                {
                    Logger.Instance.Log($"Ungültige URL: {requestUrl}");
                    return;
                }

                string requestHost = uri.Host.ToLowerInvariant();

                // Überprüfe, ob der Host mit einer der geblockten Domains übereinstimmt
                foreach (var domain in blockedDomains)
                {
                    if (requestHost.EndsWith(domain.ToLowerInvariant()) || requestHost.Contains(domain.ToLowerInvariant()))
                    {
                        Logger.Instance.Log($"Blockiere {requestUrl}");

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
                Logger.Instance.Log($"Fehler bei der Verarbeitung der Anfrage: {ex.Message}");
            }
        }

        /// <summary>
        /// Setzt den System-Proxy auf den lokalen Proxy, um den Netzwerkverkehr durch den Proxy zu leiten.
        /// </summary>
        private void SetSystemProxy()
        {
            Logger.Instance.Log("Setze System-Proxy auf 127.0.0.1:8888.");
            Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "winhttp set proxy 127.0.0.1:8888",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        /// <summary>
        /// Setzt den System-Proxy zurück auf die Standardwerte (automatische Erkennung).
        /// </summary>
        private void ResetSystemProxy()
        {
            Logger.Instance.Log("Setze System-Proxy zurück auf Standardwerte.");
            Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "winhttp reset proxy",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        /// <summary>
        /// Setzt die Liste der geblockten Domains.
        /// </summary>
        public void setBlockedDomains(List<string> blockedDomains)
        {
            Logger.Instance.Log($"setBlockedDomains gestartet mit {blockedDomains.Count} Domains.");
            this.blockedDomains = blockedDomains;
        }

        /// <summary>
        /// Leert die Liste der geblockten Domains.
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

using System;

namespace ScreenZen
{
    /// <summary>
    /// Verwaltet die Websites und den Proxy
    /// </summary>
    public class WebManager : IDisposable
    {
        private ConfigReader configReader;
        private WebProxySZ webProxy;
        private bool disposed = false;

        public bool IsProxyRunning => webProxy.IsProxyRunning;
        public event Action<bool> ProxyStatusChanged;

        public WebManager(ConfigReader configReader, WebProxySZ webProxy)
        {
            this.configReader = configReader;
            this.webProxy = webProxy;
            SetBlockedList();
            Logger.Instance.Log("Initialisiert", LogLevel.Info);

            webProxy.ProxyStatusChanged += (status) =>
            {
                ProxyStatusChanged?.Invoke(status);
            };
        }

        /// <summary>
        /// Speichert eine Website
        /// </summary>
        /// <param name="selectedGroup">Name der Gruppe</param>
        /// <param name="websiteName">Name der Website</param>
        public void SaveSelectedWebsiteToFile(string selectedGroup, string websiteName)
        {
            configReader.AddWebsiteToGroup(selectedGroup, websiteName);
            SetBlockedList();
        }

        /// <summary>
        /// Löscht eine Webite
        /// </summary>
        /// <param name="selectedGroup">Gruppen Name</param>
        /// <param name="websiteName">Website Name</param>
        public void RemoveSelectedWebsiteFromFile(string selectedGroup, string websiteName)
        {
            configReader.DeleteWebsiteFromGroup(selectedGroup, websiteName);
            SetBlockedList();
        }

        /// <summary>
        ///Startet den Proxy 
        /// </summary>
        public void StartProxy()
        {
            if (!configReader.GetWebsiteBlockingEnabled())
            {
                Logger.Instance.Log("Website-Blocking ist deaktiviert. Proxy wird nicht gestartet.", LogLevel.Info);
                return;
            }
            webProxy.StartProxy();
        }

        /// <summary>
        /// Startet den Proxy 
        /// </summary>
        public async Task StartProxyAsync()
        {
            await webProxy.StartProxy();
        }

        /// <summary>
        /// Stoppe den Proxy
        /// </summary>
        public void StopProxy()
        {
            webProxy.StopProxy();
        }

        /// <summary>
        /// Stoppe den Proxy
        /// </summary>
        public async Task StopProxyAsync()
        {
            await webProxy.StopProxy();
        }

        /// <summary>
        /// Setzt die Liste der geblockten Domains
        /// </summary>
        public void SetBlockedList()
        {
            if (!configReader.GetWebsiteBlockingEnabled())
            {
                Logger.Instance.Log("Website-Blocking ist deaktiviert. Blocklist wird nicht gesetzt.", LogLevel.Info);
                webProxy.SetBlockedDomains(new List<string>()); // Leere Liste setzen
                return;
            }
            try
            {
                List<string> blockedDomains = new List<string>();
                var domains = configReader.GetActiveGroupsDomains();

                // Domains zur Blocklist hinzufügen
                blockedDomains.AddRange(domains);

                // Blockierte Domains im Proxy setzen
                webProxy.SetBlockedDomains(blockedDomains);
                Logger.Instance.Log("Blocklist erfolgreich gesetzt.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Setzen der Blocklist: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Ressourcen aufräumen
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                // Hier verwaltete Ressourcen freigeben
                try
                {
                    StopProxy();
                }
                catch { /* Fehlerbehandlung optional */ }
                // Falls WebProxySZ IDisposable implementiert:
                (webProxy as IDisposable)?.Dispose();
            }
            // Hier ggf. unmanaged Ressourcen freigeben
            disposed = true;
        }
    }
}
namespace ScreenZen
{
    /// <summary>
    /// Verwaltet die Websites und den Proxy
    /// </summary>
    public class WebManager
    {
        private ConfigReader configReader;
        private WebProxySZ webProxy;
        public bool IsProxyRunning => webProxy.IsProxyRunning;
        public event Action<bool> ProxyStatusChanged;

        public WebManager(ConfigReader configReader, WebProxySZ webProxy)
        {
            this.configReader = configReader;
            this.webProxy = webProxy;
            SetBlockedList();
            Logger.Instance.Log("Initialisiert");

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
            //SetBlockedList();
            webProxy.StartProxy();
        }

        /// <summary>
        /// Stoppe den Proxy
        /// </summary>
        public void StopProxy()
        {
            webProxy.StopProxy();

        }

        /// <summary>
        /// Setzt die Liste der geblockten Domains
        /// </summary>
        public void SetBlockedList()
        {
            try
            {
                List<string> blockedDomains = new List<string>();
                var domains = configReader.GetActiveGroupsDomains();

                // Domains zur Blocklist hinzufügen
                blockedDomains.AddRange(domains);

                // Blockierte Domains im Proxy setzen
                webProxy.SetBlockedDomains(blockedDomains);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Setzen der Blocklist: {ex.Message}");
            }
        }

    }
}
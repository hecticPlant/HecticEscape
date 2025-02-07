using System.Text.Json.Nodes;

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
        }

        /// <summary>
        /// Löscht eine Webite
        /// </summary>
        /// <param name="selectedGroup">Gruppen Name</param>
        /// <param name="websiteName">Website Name</param>
        public void RemoveSelectedWebsiteFromFile(string selectedGroup, string websiteName)
        {
            configReader.DeleteWebsiteFromGroup(selectedGroup, websiteName);

        }

        /// <summary>
        ///Startet den Proxy 
        /// </summary>
        public void StartProxy()
        {
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
        ///         Setzt die List der geblockten Domains
        /// </summary
        public void setBlockedList()
        {
            List<string> blockedDomains = new List<string>();
            JsonNode jsonNode = configReader.GetActiveGroups();
            if (jsonNode is JsonArray jsonArray)
            {
                string[] domains = jsonArray.Select(node => node.ToString()).ToArray();
                blockedDomains.AddRange(domains);
                webProxy.setBlockedDomains(blockedDomains);
            }
        }

    }
}
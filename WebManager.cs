namespace ScreenZen
{
    /// <summary>
    /// Verwaltet die Websites
    /// </summary>
    public class WebManager
    {
        private ConfigReader configReader;

        public WebManager(ConfigReader configReader)
        {
            this.configReader = configReader;

        }
        /// <summary>
        /// Speichert eine Website
        /// </summary>
        /// <param name="selectedGroup">Name der Gruppe</param>
        /// <param name="websiteName">Name der Website</param>
        public void SaveSelectedWebsiteToFile(string selectedGroup, string websiteName)
        {
            configReader.AppendToConfig(selectedGroup, "w", websiteName);
        }

        /// <summary>
        /// Löscht eine Webite
        /// </summary>
        /// <param name="selectedGroup">Gruppen Name</param>
        /// <param name="websiteName">Website Name</param>
        public void RemoveSelectedWebsiteFromFile(string selectedGroup, string websiteName)
        {
            configReader.RemoveFromConfig(selectedGroup, "w", websiteName);

        }
    }
}
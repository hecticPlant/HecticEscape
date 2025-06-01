using System.Windows;

namespace HecticEscape
{
    class MainHE
    {
        public void Start()
        {
            var container = new ContainerHE();

            try
            {
                container.RegisterSingleton(new ConfigReader());
                var configReader = container.Resolve<ConfigReader>();
                var langData = configReader.CurrentLanguage;
                MainWindowSection mwSection;
                if (langData.Content != null
                    && langData.Content.TryGetValue("MainWindow", out var section))
                {
                    mwSection = section;
                }
                else
                {
                    Logger.Instance.Log(
                        "In CurrentLanguage.Content fehlt Key \"MainWindow\". Nutze leere Sektion.",
                        LogLevel.Warn);

                    mwSection = new MainWindowSection
                    {
                        TimerTab = new Dictionary<string, string>(),
                        WebsitesTab = new Dictionary<string, string>(),
                        ProzesseTab = new Dictionary<string, string>(),
                        GruppenTab = new Dictionary<string, string>(),
                        SteuerungTab = new Dictionary<string, string>(),
                        StatusBar = new Dictionary<string, string>()
                    };
                }

                var languageManager = new LanguageManager(mwSection);
                container.RegisterSingleton(languageManager);
                container.RegisterSingleton(new WebProxyHE());
                container.RegisterSingleton(Logger.Instance);
                container.RegisterSingleton(new Overlay());
                container.RegisterSingleton(new AppManager(configReader));
                container.RegisterSingleton(new WebManager(
                    configReader,
                    container.Resolve<WebProxyHE>()));
                container.RegisterSingleton(new TimeManagement(
                    container.Resolve<AppManager>(),
                    container.Resolve<WebManager>(),
                    container.Resolve<Overlay>(),
                    configReader
                ));
                container.Register(() => new MainWindow(
                    container.Resolve<TimeManagement>(),
                    container.Resolve<AppManager>(),
                    container.Resolve<WebManager>(),
                    container.Resolve<Overlay>(),
                    configReader,
                    container.Resolve<LanguageManager>()
                ));
                var mainWindow = container.Resolve<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler bei der Initialisierung in MainHE: {ex}", LogLevel.Error);
                MessageBox.Show(
                    "Fehler bei der Initialisierung. Siehe Log für Details.",
                    "Fehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Application.Current?.Shutdown();
            }
        }
    }
}

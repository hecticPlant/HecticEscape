using System.Windows;

namespace ScreenZen
{
    /// <summary>
    /// Initiert und Startet das Progam
    /// </summary>
    class MainSZ
    {
        public MainSZ()
        {
            Logger.Instance.Log("MainSZ Konstruktor aufgerufen", LogLevel.Info);
            Start();
        }

        /// <summary>
        /// Initiert und Startet das Progam
        /// </summary>
        [STAThread]
        public void Start()
        {
            Logger.Instance.Log("Start-Methode von MainSZ aufgerufen", LogLevel.Info);
            var container = new ContainerSZ();

            try
            {
                Logger.Instance.Log("Registriere Singletons im Container", LogLevel.Debug);
                container.RegisterSingleton(new ConfigReader());
                container.RegisterSingleton(new WebProxySZ());
                container.RegisterSingleton(Logger.Instance);
                container.RegisterSingleton(new Overlay());
                container.RegisterSingleton(new AppManager(container.Resolve<ConfigReader>()));
                container.RegisterSingleton(new WebManager(container.Resolve<ConfigReader>(), container.Resolve<WebProxySZ>()));
                container.RegisterSingleton(new TimeManagement(
                    container.Resolve<AppManager>(), 
                    container.Resolve<WebManager>(), 
                    container.Resolve<Overlay>(),
                    container.Resolve<ConfigReader>() // Hinzufügen des fehlenden Parameters
                ));

                Logger.Instance.Log("Registriere MainWindow im Container", LogLevel.Debug);
                container.Register(() => new MainWindow(
                    container.Resolve<TimeManagement>(),
                    container.Resolve<AppManager>(),
                    container.Resolve<WebManager>(),
                    container.Resolve<Overlay>(),
                    container.Resolve<ConfigReader>()
                ));

                Logger.Instance.Log("Löse MainWindow auf und zeige es an", LogLevel.Info);
                var mainWindow = container.Resolve<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler bei der Initialisierung in MainSZ: {ex}", LogLevel.Error);
                MessageBox.Show("Fehler bei der Initialisierung. Siehe Log für Details.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current?.Shutdown();
            }
        }
    }
}

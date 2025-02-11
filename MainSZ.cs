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
            Logger.Instance.Log("Initialisiert");
            Start();
        }
        /// <summary>
        /// Initiert und Startet das Progam
        /// </summary>
        [STAThread]
        public void Start()
        {
            Logger.Instance.Log("Starte...");
            var container = new ContainerSZ();

            // Singleton-Registrierungen
            container.RegisterSingleton(new ConfigReader());
            container.RegisterSingleton(new WebProxySZ());
            container.RegisterSingleton(new Logger());
            container.RegisterSingleton(new Overlay());
            container.RegisterSingleton(new AppManager(container.Resolve<ConfigReader>()));
            container.RegisterSingleton(new WebManager(container.Resolve<ConfigReader>(), container.Resolve<WebProxySZ>()));
            container.RegisterSingleton(new TimeManagement(container.Resolve<AppManager>(), container.Resolve<WebManager>()));

            // UI-Komponenten
            container.Register(() => new MainWindow(
                container.Resolve<TimeManagement>(),
                container.Resolve<AppManager>(),
                container.Resolve<WebManager>(),
                container.Resolve<Overlay>(),
                container.Resolve<ConfigReader>()
            ));

            // Anwendung starten
            var app = new Application();
            app.Run(container.Resolve<MainWindow>());


        }
    }
}

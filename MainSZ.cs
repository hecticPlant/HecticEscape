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
            Start();
        }
        /// <summary>
        /// Initiert und Startet das Progam
        /// </summary>
        [STAThread]
        public void Start()
        {
            var container = new ContainerSZ();

            // Registriere die Komponenten als Singleton
            var configReader = new ConfigReader();
            container.RegisterSingleton(configReader); // Als Singleton registrieren

            container.Register<Logger>();
            container.Register<Overlay>();

            container.Register(() => new AppManager(
                       container.Resolve<ConfigReader>()
                   ));
            container.Register(() => new TimeManagement(
                container.Resolve<AppManager>(),
                container.Resolve<WebManager>()
            ));
            container.Register(() => new WebManager(
                       container.Resolve<ConfigReader>(),
                       container.Resolve<WebProxySZ>()
                   ));
            container.Register<WebProxySZ>();
            container.Register(() => new MainWindow(
                container.Resolve<TimeManagement>(),
                container.Resolve<AppManager>(),
                container.Resolve<WebManager>(),
                container.Resolve<Overlay>(),
                container.Resolve<ConfigReader>()
            ));

            var mainWindow = container.Resolve<MainWindow>();

            var app = new Application();
            app.Run(mainWindow);
        }
    }
}

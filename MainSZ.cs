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

            // Registriere die Komponenten:
            
            container.Register<Logger>();
            container.Register<Overlay>();
            container.Register<ConfigReader>();
            container.Register(() => new AppManager(
                           container.Resolve<ConfigReader>()
                       ));
            container.Register(() => new TimeManagement(
                container.Resolve<ConfigReader>(),
                container.Resolve<AppManager>(),
                container.Resolve<WebProxySZ>()  
            ));
            container.Register(() => new WebManager(
                           container.Resolve<ConfigReader>()
                       ));
            container.Register<WebProxySZ>();
            container.Register(() => new MainWindow(
                container.Resolve<TimeManagement>(),
                container.Resolve<AppManager>(),
                container.Resolve<WebManager>(),
                container.Resolve<WebProxySZ>(),
                container.Resolve<Overlay>()
            ));

            var mainWindow = container.Resolve<MainWindow>();

            var app = new Application();
            app.Run(mainWindow);
        }
    }
}

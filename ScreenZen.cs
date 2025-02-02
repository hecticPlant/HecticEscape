using System.Windows;

namespace ScreenZen
{
    class Program
    {
        public Program() 
        { 
            start();
        }
        [STAThread]
        public void start()
        {
            var container = new ContainerSZ();

            // Registriere die Komponenten:
            container.Register<ProcessManager>();
            container.Register(() => new TimeManagement(container.Resolve<ProcessManager>()));
            container.Register<WebManagerSZ>();
            container.Register<WebProxySZ>();
            container.Register(() => new MainWindow(
                container.Resolve<TimeManagement>(),
                container.Resolve<ProcessManager>(),
                container.Resolve<WebManagerSZ>(),
                container.Resolve<WebProxySZ>()
            ));

            var mainWindow = container.Resolve<MainWindow>();

            var app = new Application();
            app.Run(mainWindow);
        }
    }
}

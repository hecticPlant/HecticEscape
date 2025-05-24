using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace ScreenZen
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IServiceProvider Services { get; private set; }

        public App()
        {
            Logger.Instance.Log("App Konstruktor gestartet", LogLevel.Info);
            var services = new ServiceCollection();
            services.AddSingleton<ConfigReader>();
            services.AddSingleton<WebProxySZ>();
            services.AddSingleton(Logger.Instance);
            services.AddSingleton<Overlay>();
            services.AddSingleton<AppManager>();
            services.AddSingleton<WebManager>();
            services.AddSingleton<TimeManagement>(provider =>
                new TimeManagement(
                    provider.GetRequiredService<AppManager>(),
                    provider.GetRequiredService<WebManager>(),
                    provider.GetRequiredService<Overlay>(), // Overlay wird übergeben
                    provider.GetRequiredService<ConfigReader>() // ConfigReader hinzugefügt
                ));
            services.AddSingleton<MainWindow>();
            Services = services.BuildServiceProvider();
            Logger.Instance.Log("ServiceProvider erstellt", LogLevel.Info);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Logger.Instance.Log("OnStartup gestartet", LogLevel.Info);
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                Logger.Instance.Log($"Unhandled Exception: {ex.ExceptionObject}", LogLevel.Error);
            };
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger.Instance.Log("Application Exit. Ressourcen werden freigegeben.", LogLevel.Info);
            // Hier ggf. globale Ressourcen aufräumen
            base.OnExit(e);
        }
    }
}

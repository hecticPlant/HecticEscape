using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace HecticEscape
{
    public partial class App : Application
    {
        public IServiceProvider Services { get; }

        public App()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ConfigReader>();

            services.AddSingleton<LanguageManager>(sp =>
            {
                var cfg = sp.GetRequiredService<ConfigReader>();
                if (cfg.CurrentLanguage.Content.TryGetValue("MainWindow", out var mwSection))
                {
                    return new LanguageManager(mwSection);
                }
                else
                {
                    Logger.Instance.Log("In CurrentLanguage.Content fehlt der 'MainWindow'-Key. Erzeuge leeren MainWindowSection.",
                                        LogLevel.Warn);
                    var emptySection = new MainWindowSection
                    {
                        TimerTab = new Dictionary<string, string>(),
                        WebsitesTab = new Dictionary<string, string>(),
                        ProzesseTab = new Dictionary<string, string>(),
                        GruppenTab = new Dictionary<string, string>(),
                        SteuerungTab = new Dictionary<string, string>(),
                        StatusBar = new Dictionary<string, string>()
                    };
                    return new LanguageManager(emptySection);
                }
            });

            services.AddSingleton<WebProxyHE>();
            services.AddSingleton<Logger>(sp => Logger.Instance);
            services.AddSingleton<Overlay>();

            services.AddSingleton<AppManager>(sp =>
                new AppManager(sp.GetRequiredService<ConfigReader>()));

            services.AddSingleton<WebManager>(sp =>
                new WebManager(
                    sp.GetRequiredService<ConfigReader>(),
                    sp.GetRequiredService<WebProxyHE>()));

            services.AddSingleton<TimeManagement>(sp =>
                new TimeManagement(
                    sp.GetRequiredService<AppManager>(),
                    sp.GetRequiredService<WebManager>(),
                    sp.GetRequiredService<Overlay>(),
                    sp.GetRequiredService<ConfigReader>()));
            services.AddTransient<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                var mainWindow = Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Starten von MainWindow: {ex}", LogLevel.Error);
                MessageBox.Show("Fehler beim Starten der Anwendung. Siehe Log für Details.",
                                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown();
            }
        }
    }
}

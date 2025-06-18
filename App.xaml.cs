using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Threading;
using System.Threading; // <--- Hinzugefügt für Mutex
using System.Linq;
using System.Threading.Tasks;

namespace HecticEscape
{
    public partial class App : Application
    {
        private Mutex? _mutex = null;
        public IServiceProvider Services { get; }

        public App()
        {
            // Eindeutigen Mutex-Namen erstellen (verwenden Sie Ihren eigenen Namespace)
            const string mutexName = "Global\\HecticEscape_SingleInstance";
            bool createdNew;

            try
            {
                _mutex = new Mutex(true, mutexName, out createdNew);

                if (!createdNew)
                {
                    MessageBox.Show("Eine Instanz von HecticEscape läuft bereits.", "HecticEscape", MessageBoxButton.OK, MessageBoxImage.Information);
                    Environment.Exit(1);
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Erstellen des Mutex: {ex.Message}", LogLevel.Error);
                Environment.Exit(1);
                return;
            }

            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ConfigReader>();
            services.AddSingleton<GroupManager>();
            services.AddSingleton<GameManager>();

            services.AddSingleton<LanguageManager>(sp =>
            {
                var cfg = sp.GetRequiredService<ConfigReader>();
                if (cfg.CurrentLanguage.Content.TryGetValue("MainWindow", out var mwSection))
                {
                    return new LanguageManager(mwSection, cfg);
                }
                else
                {
                    Logger.Instance.Log("In CurrentLanguage.Content fehlt der 'MainWindow'-Key", LogLevel.Error);
                    return new LanguageManager(new MainWindowSection { }, cfg); 
                }
            });

            services.AddSingleton<UpdateManager>();
            services.AddSingleton<WebProxyHE>();
            services.AddSingleton<Logger>(sp => Logger.Instance);
            services.AddSingleton<Overlay>(sp =>
            {
                var languageManager = sp.GetRequiredService<LanguageManager>();
                return new Overlay(languageManager);
            });
            services.AddSingleton<OverlayManager>(sp =>
            {
                var configReader = sp.GetRequiredService<ConfigReader>();
                var languageManager = sp.GetRequiredService<LanguageManager>();
                var overlay = sp.GetRequiredService<Overlay>();
                var manager = new OverlayManager(configReader, languageManager, overlay);
                overlay.OverlayManager = manager;
                return manager;
            });

            services.AddSingleton<AppManager>();

            services.AddSingleton<WebManager>(sp =>
                new WebManager(
                    sp.GetRequiredService<ConfigReader>(),
                    sp.GetRequiredService<WebProxyHE>()));

            services.AddSingleton<TimeManager>(sp =>
                new TimeManager(
                    sp.GetRequiredService<AppManager>(),
                    sp.GetRequiredService<WebManager>(),
                    sp.GetRequiredService<OverlayManager>(),
                    sp.GetRequiredService<ConfigReader>(),
                    sp.GetRequiredService<LanguageManager>(),
                    sp.GetRequiredService<GroupManager>()
                    ));

            services.AddSingleton<WindowManager>();
            services.AddSingleton<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Prüfe auf /minimized Parameter
            bool startMinimized = e.Args.Contains("/minimized");

            try
            {
                var windowManager = Services.GetRequiredService<WindowManager>();
                var mainWindow = Services.GetRequiredService<MainWindow>();
                windowManager.MainWindow = mainWindow;

                MainWindow = mainWindow; 

                if (startMinimized)
                {
                    // Warte auf Shell-Initialisierung und starte minimiert
                    Dispatcher.BeginInvoke(async () =>
                    {
                        await Task.Delay(2000); 

                        if (MainWindow is MainWindow window)
                        {
                            window.WindowState = WindowState.Minimized;
                            window.ShowInTaskbar = false;
                            window.Show();
                            window.Hide();
                        }
                    }, DispatcherPriority.Background);
                }
                else
                {
                    mainWindow.Show();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Starten von MainWindow: {ex}", LogLevel.Error);
                MessageBox.Show("Fehler beim Starten der Anwendung. Siehe Log für Details.",
                                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}

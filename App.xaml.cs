using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Threading;

namespace HecticEscape
{
    public partial class App : Application
    {
        private Mutex _mutex = null;
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

            services.AddSingleton<LanguageManager>(sp =>
            {
                var cfg = sp.GetRequiredService<ConfigReader>();
                if (cfg.CurrentLanguage.Content.TryGetValue("MainWindow", out var mwSection))
                {
                    return new LanguageManager(mwSection);
                }
                else
                {
                    Logger.Instance.Log("In CurrentLanguage.Content fehlt der 'MainWindow'-Key", LogLevel.Error);
                    return new LanguageManager(new MainWindowSection { }) ;
                }
            });

            services.AddSingleton<UpdateManager>();
            services.AddSingleton<WebProxyHE>();
            services.AddSingleton<Logger>(sp => Logger.Instance);
            services.AddSingleton<Overlay>(sp =>
                new Overlay(sp.GetRequiredService<LanguageManager>()));

            services.AddSingleton<AppManager>();
            
            services.AddSingleton<WebManager>(sp =>
                new WebManager(
                    sp.GetRequiredService<ConfigReader>(),
                    sp.GetRequiredService<WebProxyHE>()));

            services.AddSingleton<TimeManagement>(sp =>
                new TimeManagement(
                    sp.GetRequiredService<AppManager>(),
                    sp.GetRequiredService<WebManager>(),
                    sp.GetRequiredService<Overlay>(),
                    sp.GetRequiredService<ConfigReader>(),
                    sp.GetRequiredService<LanguageManager>()));
                        services.AddSingleton<MainWindow>();
            

        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Prüfe auf /minimized Parameter
            bool startMinimized = e.Args.Contains("/minimized");
            
            try
            {
                var mainWindow = Services.GetRequiredService<MainWindow>();
                MainWindow = mainWindow; // Wichtig: Setze das MainWindow
                
                if (startMinimized)
                {
                    // Warte auf Shell-Initialisierung und starte minimiert
                    Dispatcher.BeginInvoke(async () =>
                    {
                        // Warte kurz, bis Windows Shell initialisiert ist
                        await Task.Delay(2000); // 2 Sekunden Verzögerung
                        
                        if (MainWindow is MainWindow window)
                        {
                            // Verstecke das Fenster bevor es angezeigt wird
                            window.WindowState = WindowState.Minimized;
                            window.ShowInTaskbar = false;
                            window.Show();
                            window.Hide(); // Sofort verstecken
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

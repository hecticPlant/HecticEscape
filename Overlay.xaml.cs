using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ScreenZen
{
    /// <summary>
    /// Interaktionslogik für Overlay.xaml
    /// </summary>
    public partial class Overlay : Window, IDisposable
    {
        private bool disposed = false;

        public Overlay()
        {
            InitializeComponent();

            // Fenster ohne Rahmen, ohne Hintergrund und immer im Vordergrund
            WindowStyle = WindowStyle.None;
            Background = Brushes.Transparent;
            Topmost = true; // Stellt sicher, dass das Fenster immer oben bleibt
            AllowsTransparency = true;
            ShowInTaskbar = false;

            // Optional: Fenstergröße und Position setzen (z.B. auf gesamten Bildschirm)
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
            Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
        }

        public void ShowMessage(string message, int durationMs = 2000)
        {
            OverlayMessageTextBlock.Text = message;
            Show();
            Topmost = true;
            Activate();

            var timer = new System.Timers.Timer(durationMs);
            timer.Elapsed += (s, e) =>
            {
                Dispatcher.Invoke(() => Hide());
                timer.Dispose();
            };
            timer.AutoReset = false;
            timer.Start();
        }

        public async Task ShowCountdownAsync(int seconds)
        {
            Show();
            Topmost = true;
            Activate();
            for (int i = seconds; i > 0; i--)
            {
                OverlayMessageTextBlock.Text = $"Pause in {i} Sekunden";
                await Task.Delay(1000);
            }
            Hide();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                // Hier ggf. Event-Handler abmelden, Timer stoppen etc.
                this.Close();
                Logger.Instance.Log("Overlay wurde disposed.", LogLevel.Info);
            }
            disposed = true;
        }
    }
}

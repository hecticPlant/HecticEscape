using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ScreenZen
{
    /// <summary>
    /// Interaktionslogik für Overlay.xaml
    /// </summary>
    public partial class Overlay : Window, IDisposable
    {
        private bool disposed = false;
        private CancellationTokenSource? _countdownCts;
        // P/Invoke Konstanten und Methoden
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

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

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Jetzt ist das Hwnd bereit – hier die Click-Through-Styles setzen:
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            // Layered ist nötig für Transparenz, Transparent für Durchklickbarkeit
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
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
            _countdownCts?.Cancel(); // Vorherigen Countdown abbrechen
            var cts = new CancellationTokenSource();
            _countdownCts = cts;

            Show();
            Topmost = true;
            Activate();
            try
            {
                for (int i = seconds; i > 0; i--)
                {
                    OverlayMessageTextBlock.Text = $"Pause in {i} Sekunden";
                    await Task.Delay(1000, cts.Token);
                }
            }
            catch (TaskCanceledException e)
            {
                Logger.Instance.Log($"Countdown abgebrochen: {e.Message}", LogLevel.Debug);
            }
            Hide();
        }

        public void CancelCountdown()
        {
            _countdownCts?.Cancel();
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

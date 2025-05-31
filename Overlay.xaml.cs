using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ScreenZen
{
    public partial class Overlay : Window, IDisposable
    {
        private bool disposed = false;

        // Status-Flags
        private bool _timerActive = false;
        private bool _messageActive = false;
        private bool _showTimer = true;
        private bool _overlayEnabled = true;
        public bool GetShowTimer() => _showTimer;
        public bool SetShowTimer(bool value)
        {
            _showTimer = value;
            Dispatcher.Invoke(() =>
            {
                OverlayTimerBorder.Visibility = (_showTimer && _timerActive)
                    ? Visibility.Visible
                    : Visibility.Hidden;
                UpdateOverlayVisibility();
            });
            return _showTimer;
        }

        // CancellationTokens
        private CancellationTokenSource? _countdownCts;
        private CancellationTokenSource? _timerCts;

        // P/Invoke Konstanten
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public Overlay()
        {
            InitializeComponent();

            // Fenster ohne Rahmen & transparent & klick-durchlässig
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;

            // Vollbild
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
            Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;

            // Initial versteckt
            OverlayMessageBorder.Visibility = Visibility.Hidden;
            OverlayTimerBorder.Visibility = Visibility.Hidden;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_LAYERED | WS_EX_TRANSPARENT);
        }

        public void ShowMessage(string message, int durationMs = 2000)
        {
            // Flag setzen
            _messageActive = true;

            Dispatcher.Invoke(() =>
            {
                OverlayMessageTextBlock.Text = message;
                OverlayMessageBorder.Visibility = Visibility.Visible;
                Topmost = true;
                Activate();
                UpdateOverlayVisibility();
            });

            var timer = new System.Timers.Timer(durationMs);
            timer.Elapsed += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    OverlayMessageBorder.Visibility = Visibility.Hidden;
                    _messageActive = false;                 // Flag zurücksetzen
                    UpdateOverlayVisibility();
                });
                timer.Dispose();
            };
            timer.AutoReset = false;
            timer.Start();
        }

        public void StartOverlayTimer(int durationMs)
        {
            // alten Timer abbrechen
            _timerCts?.Cancel();

            // neuen TokenSource
            var cts = new CancellationTokenSource();
            _timerCts = cts;
            _timerActive = true;   // Flag setzen

            var start = DateTime.Now;
            var end = start.AddMilliseconds(durationMs);

            Dispatcher.Invoke(() =>
            {
                OverlayTimerBorder.Visibility = _showTimer
                    ? Visibility.Visible
                    : Visibility.Hidden;
                UpdateOverlayVisibility();
            });

            // jede Sekunde Text aktualisieren
            var updateTimer = new System.Timers.Timer(1000);
            updateTimer.Elapsed += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    var rem = end - DateTime.Now;
                    if (rem < TimeSpan.Zero) rem = TimeSpan.Zero;
                    OverlayTimerTextBlock.Text = $"{rem.Hours:D2}:{rem.Minutes:D2}:{rem.Seconds:D2}";
                });
            };
            updateTimer.AutoReset = true;
            updateTimer.Start();

            // nach Ablauf aufräumen
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(durationMs, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    // ignoriere Abbruch
                }

                updateTimer.Stop();
                updateTimer.Dispose();
            });
        }

        public void SetTimerVisibility(bool visible)
        {
            _showTimer = visible;
            Dispatcher.Invoke(() =>
            {
                OverlayTimerBorder.Visibility = (_showTimer && _timerActive)
                    ? Visibility.Visible
                    : Visibility.Hidden;
                UpdateOverlayVisibility();
            });
        }

        public async Task ShowCountdownAsync(int seconds)
        {
            // Falls Timer parallel läuft, bleibt er unsichtbar, wenn _showTimer == false
            _countdownCts?.Cancel();
            var cts = new CancellationTokenSource();
            _countdownCts = cts;
            _messageActive = true;

            Dispatcher.Invoke(() =>
            {
                OverlayMessageBorder.Visibility = Visibility.Visible;
                Topmost = true;
                Activate();
                UpdateOverlayVisibility();
            });

            try
            {
                for (int i = seconds; i > 0; i--)
                {
                    Dispatcher.Invoke(() =>
                        OverlayMessageTextBlock.Text = $"Pause in {i} Sekunden"
                    );
                    await Task.Delay(1000, cts.Token);
                }
            }
            catch (TaskCanceledException) { }

            Dispatcher.Invoke(() =>
            {
                OverlayMessageBorder.Visibility = Visibility.Hidden;
                _messageActive = false;
                UpdateOverlayVisibility();
            });
        }

        public void CancelCountdown()
        {
            _countdownCts?.Cancel();
            Dispatcher.Invoke(() =>
            {
                OverlayMessageBorder.Visibility = Visibility.Hidden;
                _messageActive = false;
                UpdateOverlayVisibility();
            });
        }

        public void CancelTimer()
        {
            _timerCts?.Cancel();
            Dispatcher.Invoke(() =>
            {
                OverlayTimerBorder.Visibility = Visibility.Hidden;
                _timerActive = false;
                UpdateOverlayVisibility();
            });
        }

        private void UpdateOverlayVisibility()
        {
            // Neu: Wenn Overlay global deaktiviert ist, verstecke es sofort.
            if (!_overlayEnabled)
            {
                if (IsVisible)
                    Hide();
                return;
            }

            // Alte Logik: Nur anzeigen, wenn Timer aktiv sein darf UND _timerActive, oder eine Message aktiv ist.
            if ((_showTimer && _timerActive) || _messageActive)
            {
                if (!IsVisible)
                    Show();
            }
            else
            {
                if (IsVisible)
                    Hide();
            }
        }

        /// <summary>
        /// Aktiviert das Overlay (also erlaubt, dass es wieder sichtbar wird, 
        /// sofern gerade ein Timer oder eine Message aktiv ist).
        /// </summary>
        public void EnableOverlay()
        {
            _overlayEnabled = true;
            Dispatcher.Invoke(UpdateOverlayVisibility);
        }

        /// <summary>
        /// Deaktiviert das Overlay komplett (versteckt es sofort, 
        /// selbst wenn gerade ein Timer läuft oder eine Nachricht angezeigt wird).
        /// </summary>
        public void DisableOverlay()
        {
            _overlayEnabled = false;
            Dispatcher.Invoke(UpdateOverlayVisibility);
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
                this.Close();
                Logger.Instance.Log("Overlay disposed.", LogLevel.Info);
            }
            disposed = true;
        }
    }
}

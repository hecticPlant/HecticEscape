using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;

namespace HecticEscape
{
    public partial class Overlay : Window, IDisposable
    {
        private bool disposed = false;
        private readonly LanguageManager _languageManager;
        private OverlayManager? _overlayManager; // nicht mehr readonly

        public OverlayManager? OverlayManager
        {
            get => _overlayManager;
            set => _overlayManager = value;
        }

        // Status-Flags
        private bool _messageActive = false;

        // CancellationTokens
        private CancellationTokenSource? _countdownCts;
        private CancellationTokenSource? _timerCts;

        // P/Invoke Konstanten
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public Overlay(LanguageManager languageManager)
        {
            InitializeComponent();
            Logger.Instance.Log("Overlay initialisiert.", LogLevel.Info);

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            Left = 0;
            Top = 0;

            OverlayMessageBorder.Visibility = Visibility.Hidden;
            OverlayTimerBorder.Visibility = Visibility.Hidden;
            _languageManager = languageManager;
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
                    _messageActive = false;
                    UpdateOverlayVisibility();
                });
                timer.Dispose();
            };
            timer.AutoReset = false;
            timer.Start();
        }

        public async Task ShowCountdownAsync(int seconds)
        {
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
                        OverlayMessageTextBlock.Text = $"{_languageManager.Get("Overlay.PauseIn")}" + $" {i} {_languageManager.Get("Misc.Seconds")}"
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

        public void ShowTimer(TimeSpan remaining)
        {
            Dispatcher.Invoke(() =>
            {
                OverlayTimerTextBlock.Text = $"{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
                OverlayTimerBorder.Visibility = Visibility.Visible;
            });
        }

        public void HideTimer()
        {
            Dispatcher.Invoke(() =>
            {
                OverlayTimerBorder.Visibility = Visibility.Hidden;
                OverlayTimerTextBlock.Text = "";
            });
        }

        private void UpdateOverlayVisibility()
        {
            if (!_overlayManager.GetEnableOverlay())
            {
                if (IsVisible)
                    Hide();
                return;
            }
            else
            {
                if (IsVisible)
                    Hide();
            }
        }

        public void EnableOverlay()
        {
            _overlayManager.SetEnableOverlay(true);
            Dispatcher.Invoke(UpdateOverlayVisibility);
        }

         public void DisableOverlay()
        {
            _overlayManager.SetEnableOverlay(false);
            Dispatcher.Invoke(UpdateOverlayVisibility);
        }

        public bool IsMessageActive() => _messageActive;

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

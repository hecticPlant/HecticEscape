using HecticEscape;
using System;
using System.Collections.Generic;
using System.Windows;
using Xceed.Wpf.AvalonDock.Controls;
namespace HecticEscape
{
    /// <summary>
    /// Verwaltet das Overlay-Fenster und kapselt dessen Steuerung.
    /// </summary>
    public class OverlayManager : AManager, IDisposable
    {
        private readonly Overlay _overlay;
        private readonly LanguageManager _languageManager;
        private bool _disposed = false;
        private TimeSpan? _lastTimerRemaining = null;


        public OverlayManager(ConfigReader configReader, LanguageManager languageManager, Overlay overlay)
            : base(configReader)
        {
            _languageManager = languageManager ?? throw new ArgumentNullException(nameof(languageManager));
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            _overlay.OverlayManager = this;
        }

        public override void Initialize()
        {
            Logger.Instance.Log("OverlayManager initialisiert", LogLevel.Info);
            if (_configReader.GetEnableOverlay())
                EnableOverlay();
            else
                DisableOverlay();
        }

        public void AttachToTimeManager(TimeManager timeManager)
        {
            Logger.Instance.Log("OverlayManager an TimeManager angehängt", LogLevel.Verbose);
            timeManager.TimerTicked += remaining =>
            {
                ShowTimer(remaining);
            };
            timeManager.AppTimerTicked += remaining =>
            {
                ShowAppTimer(remaining);
            };
        }

        public void ShowMessage(string message, int durationMs = 2000)
        {
            Logger.Instance.Log($"OverlayManager: Zeige Nachricht '{message}' für {durationMs}ms", LogLevel.Verbose);
            _overlay.ShowMessage(message, durationMs);
            UpdateOverlayVisibility();
        }

        public void ShowCountdown(int seconds)
        {
            Logger.Instance.Log($"OverlayManager: Zeige Countdown für {seconds} Sekunden", LogLevel.Verbose);
            _overlay.ShowCountdownAsync(seconds);
            UpdateOverlayVisibility();
        }

        public void CancelCountdown()
        {
            Logger.Instance.Log("OverlayManager: Countdown abgebrochen", LogLevel.Verbose);
            _overlay.CancelCountdown();
        }

        public void ShowTimer(TimeSpan remaining)
        {
            Logger.Instance.Log($"OverlayManager: Zeige Timer für {remaining.TotalSeconds} Sekunden", LogLevel.Verbose);
            _lastTimerRemaining = remaining;
            if (GetEnableOverlay() && GetShowTimer() && remaining.TotalSeconds > 0)
                _overlay.ShowTimer(remaining);
            else
                _overlay.HideTimer();
            UpdateOverlayVisibility();
        }

        public void ShowAppTimer(TimeSpan remainig)
        {
            Logger.Instance.Log($"OverlayManager: Zeige App-Timer für {remainig.TotalSeconds} Sekunden", LogLevel.Verbose);
            if (GetEnableOverlay() && GetShowAppTimer() && remainig.TotalSeconds > 0)
                _overlay.ShowAppTimer(remainig);
            else
                _overlay.HideTimer();
            UpdateOverlayVisibility();
        }

        public void HideTimer()
        {
            Logger.Instance.Log("OverlayManager: Timer ausgeblendet", LogLevel.Verbose);
            _lastTimerRemaining = null;
            _overlay.HideTimer();
            UpdateOverlayVisibility();
        }
       
        public void ToggleOverlayVisibility()
        {
            Logger.Instance.Log("OverlayManager: Overlay-Sichtbarkeit umgeschaltet", LogLevel.Verbose);
            if (GetEnableOverlay())
            {
                DisableOverlay();
            }
            else
            {
                EnableOverlay();
            }
        }
       
        public void EnableOverlay()
        {
            Logger.Instance.Log("OverlayManager: Overlay aktiviert", LogLevel.Info);
            _configReader.SetEnableOverlay(true);
            _overlay.Dispatcher.Invoke(() => _overlay.Show());
        }

        public void DisableOverlay()
        {
            Logger.Instance.Log("OverlayManager: Overlay deaktiviert", LogLevel.Info);
            _configReader.SetEnableOverlay(false);
            _overlay.Dispatcher.Invoke(() => _overlay.Hide());
        }

        public bool GetEnableOverlay()
        {
            Logger.Instance.Log("OverlayManager: Abfrage EnableOverlay", LogLevel.Verbose);
            return _configReader.GetEnableOverlay();
        }

        public async Task ShowPauseMessage()
        {
            Logger.Instance.Log("OverlayManager: Zeige Pause-Nachricht", LogLevel.Verbose);
            var overlayWindow = _overlay;
            if (overlayWindow != null)
            {
                await overlayWindow.Dispatcher.InvokeAsync(() =>
                {
                    ShowMessage(_languageManager.Get("Overlay.PauseBeginnt"), 2500);
                });
            }
            else
            {
                ShowMessage(_languageManager.Get("Overlay.PauseBeginnt"), 2500);
            }
        }

        public bool GetShowTimer()
        {
            return _configReader.GetShowTimeInOverlayEnable();
        }

        public bool GetShowAppTimer() 
        {
            return _configReader.GetShowAppTimeInOverlayEnable();
        }

        public void SetShowTimer(bool value)
        {
            _configReader.EnableShowTimeInOverlay(value);
        }

        public void HideAppTimer()
        {
            Logger.Instance.Log("OverlayManager: App-Timer ausgeblendet", LogLevel.Verbose);
            _overlay.HideAppTimer();
            UpdateOverlayVisibility();
        }

        public void UpdateOverlayVisibility()
        {
            Logger.Instance.Log("OverlayManager: Aktualisiere Overlay-Sichtbarkeit", LogLevel.Verbose);
            if (!GetEnableOverlay())
            {
                _overlay.Dispatcher.Invoke(() => _overlay.Hide());
                return;
            }

            // Timer oder Nachricht aktiv?
            bool timerVisible = GetShowTimer() && (_lastTimerRemaining?.TotalSeconds > 0);
            bool messageActive = _overlay.IsMessageActive();
            bool appTimerVisible = GetShowAppTimer();

            if (timerVisible || messageActive || appTimerVisible)
            {
                _overlay.Dispatcher.Invoke(() => _overlay.Show());
                Logger.Instance.Log("OverlayManager: Overlay sichtbar, da Timer oder Nachricht aktiv", LogLevel.Verbose);
            }
            else
            {
                _overlay.Dispatcher.Invoke(() => _overlay.Hide());
                Logger.Instance.Log("OverlayManager: Overlay ausgeblendet, da keine aktiven Timer oder Nachrichten", LogLevel.Verbose);
            }
        }

        protected override void Dispose(bool disposing)
        {
            Logger.Instance.Log("OverlayManager: Dispose aufgerufen", LogLevel.Info);
            if (!_disposed)
            {
                if (disposing)
                {
                    _overlay.Dispatcher.Invoke(() =>
                    {
                        _overlay.Close();
                    });
                    Logger.Instance.Log("Overlay disposed.", LogLevel.Info);
                }
                _disposed = true;
                base.Dispose(disposing);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
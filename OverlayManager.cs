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
            if (_configReader.GetEnableOverlay())
                EnableOverlay();
            else
                DisableOverlay();
        }
        
        public void ShowMessage(string message, int durationMs = 2000)
        {
            _overlay.ShowMessage(message, durationMs);
            UpdateOverlayVisibility();
        }

        public void ShowCountdownAsync(int seconds)
        {
            _ = _overlay.ShowCountdownAsync(seconds);
        }

        public void CancelCountdown()
        {
            _overlay.CancelCountdown();
        }

        public void ShowTimer(TimeSpan remaining)
        {
            _lastTimerRemaining = remaining;
            if (GetEnableOverlay() && GetShowTimer() && remaining.TotalSeconds > 0)
                _overlay.ShowTimer(remaining);
            else
                _overlay.HideTimer();
            UpdateOverlayVisibility();
        }

        public void HideTimer()
        {
            _lastTimerRemaining = null;
            _overlay.HideTimer();
            UpdateOverlayVisibility();
        }
       
        public void ToggleOverlayVisibility()
        {
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
            _configReader.SetEnableOverlay(true);
            _overlay.Dispatcher.Invoke(() => _overlay.Show());
        }

        public void DisableOverlay()
        {
            _configReader.SetEnableOverlay(false);
            _overlay.Dispatcher.Invoke(() => _overlay.Hide());
        }

        public bool GetEnableOverlay()
        {
            return _configReader.GetEnableOverlay();
        }

        public async Task ShowPauseMessage()
        {
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

        public void SetShowTimer(bool value)
        {
            _configReader.EnableShowTimeInOverlay(value);
        }

        public void UpdateOverlayVisibility()
        {
            if (!GetEnableOverlay())
            {
                _overlay.Dispatcher.Invoke(() => _overlay.Hide());
                return;
            }

            // Timer oder Nachricht aktiv?
            bool timerVisible = GetShowTimer() && (_lastTimerRemaining?.TotalSeconds > 0);
            bool messageActive = _overlay.IsMessageActive();

            if (timerVisible || messageActive)
                _overlay.Dispatcher.Invoke(() => _overlay.Show());
            else
                _overlay.Dispatcher.Invoke(() => _overlay.Hide());
        }

        protected override void Dispose(bool disposing)
        {
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
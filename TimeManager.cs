using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Header;
using TimersTimer = System.Timers.Timer;

namespace HecticEscape
{
    /// <summary>
    /// Verwaltet Arbeits-, Pausen- und Prüf-Timer für App- und Webseiten-Blockierung.
    /// </summary>
    public class TimeManager : AManager
    {
        public event Action<string>? StatusChanged;
        public event Action? OverlayToggleRequested;
        public event Action<TimeSpan>? TimerTicked;
        public event Action<TimeSpan>? AppTimerTicked;
        public event Action<string>? NewProcessDetected;

        private readonly AppManager _appManager;
        private readonly WebManager _webManager;
        private readonly LanguageManager _languageManager;
        private readonly OverlayManager _overlayManager;
        private readonly GroupManager _groupManager;

        private TimersTimer _workTimer = null!;
        private TimersTimer _breakTimer = null!;
        private TimersTimer _checkTimer = null!;
        private DateTime? _timerEnd;

        private int _intervalWorkMs;
        private int _intervalBreakMs;
        private int _intervalCheckMs;
        private bool _startTimerAtStartup;
        private bool _disposed = false;
        private bool _isBreakActive = false;
        private bool _countdownActive = false;
        private bool _showApptimer = false;

        private DateTime? _workTimerEnd;
        private DateTime? _breakTimerEnd;
        private DateTime? _checkTimerEnd;

        private const int PufferMs = 1000;
        private readonly TimeSpan _toleranz = TimeSpan.FromSeconds(0.95);

        private readonly List<TimeSpan> _announceTimes = new()
        {
            TimeSpan.FromHours(1),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(15),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(30)
        };

        public TimeManager(
            AppManager appManager,
            WebManager webManager,
            OverlayManager overlayManager,
            ConfigReader configReader,
            LanguageManager languageManager,
            GroupManager groupManager
        ) : base(configReader)
        {
            _appManager = appManager ?? throw new ArgumentNullException(nameof(appManager));
            _webManager = webManager ?? throw new ArgumentNullException(nameof(webManager));
            _overlayManager = overlayManager ?? throw new ArgumentNullException(nameof(overlayManager));
            _languageManager = languageManager ?? throw new ArgumentNullException(nameof(languageManager));
            _groupManager = groupManager ?? throw new ArgumentNullException(nameof(groupManager));

            Initialize();
        }

        public override void Initialize()
        {
            Logger.Instance.Log("TimeManager wird initialisiert", LogLevel.Info);
            _intervalWorkMs = _configReader.GetIntervalFreeMs();
            _intervalBreakMs = _configReader.GetIntervalBreakMs();
            _intervalCheckMs = _configReader.GetIntervalCheckMs();
            _startTimerAtStartup = _configReader.GetStartTimerAtStartup();

            if (_intervalWorkMs <= 0 || _intervalBreakMs <= 0 || _intervalCheckMs <= 0)
                throw new ArgumentException("Timer-Intervalle müssen größer als 0 sein.");

            InitializeTimers();

            _webManager.ProxyStatusChanged += OnProxyStatusChanged;
            Logger.Instance.Log("TimeManager initialisiert", LogLevel.Info);

            if (_startTimerAtStartup)
            {
                StartTimer(TimerType.Work);
                StartTimer(TimerType.Check);
            }
        }

        private void InitializeTimers()
        {
            Logger.Instance.Log("Initialisiere Timer", LogLevel.Info);
            _workTimer = new TimersTimer(_intervalWorkMs);
            _workTimer.Elapsed += async (s, e) => await SafeSwitchToBreakAsync();
            _workTimer.AutoReset = false;

            _breakTimer = new TimersTimer(_intervalBreakMs);
            _breakTimer.Elapsed += async (s, e) => await SafeSwitchToWorkAsync();
            _breakTimer.AutoReset = false;

            _checkTimer = new TimersTimer(_intervalCheckMs);
            _checkTimer.Elapsed += (s, e) => CheckHandler();
            _checkTimer.AutoReset = true;
        }

        public void StopAllTimers()
        {
            try
            {
                Logger.Instance.Log("TimeManager: StopAllTimers", LogLevel.Info);
                _workTimer.Stop();
                _breakTimer.Stop();
                _checkTimer.Stop();
                _overlayManager.CancelCountdown();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Stoppen der Timer: {ex.Message}", LogLevel.Error);
            }
        }

        private async Task SwitchToBreakAsync()
        {
            try
            {
                Logger.Instance.Log("Pause wird gestartet", LogLevel.Info);
                _appManager.ListAllActiveApps();
                _isBreakActive = true;
                StatusChanged?.Invoke("Momentan Pause");

                _overlayManager.ShowMessage(_languageManager.Get("Overlay.PauseBeginnt"), 2500);

                _breakTimer.Stop();
                _breakTimerEnd = DateTime.Now.AddMilliseconds(_breakTimer.Interval);
                _breakTimer.Start();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler in SwitchToBreakAsync: {ex.Message}", LogLevel.Error);
            }
        }

        private async Task SwitchToWorkAsync()
        {
            try
            {
                Logger.Instance.Log("Pause wird beendet", LogLevel.Info);
                _isBreakActive = false;
                StatusChanged?.Invoke("Momentan freie Zeit");

                await Task.Run(() =>
                {
                    _overlayManager.ShowMessage(_languageManager.Get("Overlay.PauseVorbei"), 2500);
                });
                _workTimer.Stop();
                _workTimerEnd = DateTime.Now.AddMilliseconds(_workTimer.Interval);
                _workTimer.Start();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler in SwitchToWorkAsync: {ex.Message}", LogLevel.Error);
            }
        }

        private async Task CheckHandler()
        {
            try
            {
                await OnTimerTicked();
                await _appManager.BlockHandler(_intervalCheckMs, _isBreakActive);
                if (_workTimer.Enabled)
                {
                    WarnAboutPause(GetRemainingWorkTime(), CloseType.Pause);
                }
                ShowAppTimerIfDailyTimeIsLow();
                ScanForNewProcesses();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler in CheckHandler: {ex.Message}", LogLevel.Error);
            }
        }
        private async Task ScanForNewProcesses()
        {
            string newProcess = _appManager.ScanForNewProcesses();
            if (string.IsNullOrEmpty(newProcess)) return;
            if (_appManager.EnableScanForNewApps)
            { 
                await OnNewProcessDetected(newProcess); 
            }

        }

        private async Task ShowAppTimerIfDailyTimeIsLow()
        {
            TimeSpan lowestTimeSpan = TimeSpan.MaxValue;

            DateOnly date = DateOnly.FromDateTime(DateTime.Now);
            lowestTimeSpan = _appManager.GetLowestTimeRemaining();

            if (lowestTimeSpan < TimeSpan.FromHours(1.1))
            {
                try
                {
                    await OnAppTimerTicked(lowestTimeSpan);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Fehler in OnAppTimerTicked nach Gruppen-Warnung: {ex}", LogLevel.Error);
                }
                _overlayManager.ShowAppTimer(lowestTimeSpan);
                WarnAboutPause(lowestTimeSpan, CloseType.OutOfTime);
                return;
            }
            else
            {
                try
                {
                    _overlayManager.HideAppTimer();
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Fehler beim Ausblenden des Timers: {ex}", LogLevel.Error);
                }
            }
        }

        private bool IsNearWarningTime(TimeSpan eingabe)
        {

            foreach (var target in _announceTimes)
            {
                TimeSpan diff = (eingabe - target).Duration();

                if (diff <= _toleranz)
                {
                    return true;
                }
            }
            return false;
        }

        private void WarnAboutPause(TimeSpan remaining, CloseType closeType)
        {
            if (IsNearWarningTime(remaining))
            {
                string message = "Error";
                if (_configReader.GetEnableDebugMode())
                {
                    message = string.Format(_languageManager.Get("Overlay.PauseBeginntInDebug"), FormatTimeSpan(remaining), closeType.ToString());
                }
                else if(!_configReader.GetEnableDebugMode())
                {
                    message = string.Format(_languageManager.Get("Overlay.PauseBeginntIn"), FormatTimeSpan(remaining));
                }
                _overlayManager.ShowMessage(message, 2000);
            }
            if (remaining <= TimeSpan.FromSeconds(10) && !_countdownActive)
            {
                _countdownActive = true;
                _overlayManager.ShowCountdown(remaining.Seconds);
            }
        }

        public void ForceBreak()
        {
            try
            {
                Logger.Instance.Log("Erzwinge eine Pause.", LogLevel.Warn);
                _workTimer.Stop();
                _ = SwitchToBreakAsync();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Erzwingen einer Pause: {ex.Message}", LogLevel.Error);
            }
        }

        public void EndBreak()
        {
            try
            {
                Logger.Instance.Log("Erzwinge das Ende einer Pause", LogLevel.Warn);
                _breakTimer.Stop();
                _ = SwitchToWorkAsync();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Erzwingen des Endes einer Pause: {ex.Message}", LogLevel.Error);
            }
        }

        public void SetTimerInterval(TimerType timerType, int seconds)
        {
            try
            {
                int ms = seconds * 1000;
                switch (timerType)
                {
                    case TimerType.Work:
                        Logger.Instance.Log($"Arbeitszeit-Intervall auf {seconds} Sekunden gesetzt", LogLevel.Info);
                        _intervalWorkMs = ms;
                        _workTimer.Interval = ms;
                        _configReader.SetIntervalFreeMs(ms);
                        break;
                    case TimerType.Break:
                        Logger.Instance.Log($"Pausenzeit-Intervall auf {seconds} Sekunden gesetzt", LogLevel.Info);
                        _intervalBreakMs = ms;
                        _breakTimer.Interval = ms;
                        _configReader.SetIntervalBreakMs(ms);
                        break;
                    case TimerType.Check:
                        Logger.Instance.Log($"Check-Intervall auf {seconds} Sekunden gesetzt", LogLevel.Info);
                        _intervalCheckMs = ms;
                        _checkTimer.Interval = ms;
                        _configReader.SetIntervalCheckMs(ms);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Setzen des Timer-Intervalls: {ex.Message}", LogLevel.Error);
            }
        }

        public void StartTimer(TimerType timerType)
        {
            try
            {
                switch (timerType)
                {
                    case TimerType.Work:
                        Logger.Instance.Log("Starte Arbeitszeit-Timer", LogLevel.Info);
                        _workTimer.Stop();
                        _workTimerEnd = DateTime.Now.AddMilliseconds(_workTimer.Interval);
                        _workTimer.Start();
                        break;
                    case TimerType.Break:
                        Logger.Instance.Log("Starte Pausenzeit-Timer", LogLevel.Info);
                        _breakTimer.Stop();
                        _breakTimerEnd = DateTime.Now.AddMilliseconds(_breakTimer.Interval);
                        _breakTimer.Start();
                        break;
                    case TimerType.Check:
                        Logger.Instance.Log("Starte Check-Timer", LogLevel.Info);
                        _checkTimer.Stop();
                        _checkTimerEnd = DateTime.Now.AddMilliseconds(_checkTimer.Interval);
                        _checkTimer.Start();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Starten des Timers: {ex.Message}", LogLevel.Error);
            }
        }

        public void StopTimer(TimerType timerType)
        {
            try
            {
                switch (timerType)
                {
                    case TimerType.Work:
                        Logger.Instance.Log("Stoppe Arbeitszeit-Timer", LogLevel.Info);
                        _workTimer.Stop();
                        break;
                    case TimerType.Break:
                        Logger.Instance.Log("Stoppe Pausenzeit-Timer", LogLevel.Info);
                        _breakTimer.Stop();
                        break;
                    case TimerType.Check:
                        Logger.Instance.Log("Stoppe Check-Timer", LogLevel.Info);
                        _checkTimer.Stop();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Stoppen des Timers: {ex.Message}", LogLevel.Error);
            }
        }

        public int GetWorkIntervalSeconds()
        {
            return _intervalWorkMs / 1000;
        }
        public int GetBreakIntervalSeconds()
        {
            return _intervalBreakMs / 1000;
        }
        public int GetCheckIntervalSeconds()
        {
            return _intervalCheckMs / 1000;
        }

        public bool IsWorkTimerRunning()
        {
            return _workTimer.Enabled;
        }
        public bool IsBreakTimerRunning()
        {
            return _breakTimer.Enabled;
        }
        public bool IsCheckTimerRunning()
        {
            return _checkTimer.Enabled;
        }
        public bool IsBreakActive()
        {
            return _isBreakActive;
        }

        public bool IsCountdownActive()
        {
            return _countdownActive;
        }
        public void SetCountdownActive(bool active)
        {
            _countdownActive = active;
            if (!active)
            {
                _overlayManager.CancelCountdown();
            }
        }

        public TimeSpan GetRemainingWorkTime()
        {
            if (!_workTimer.Enabled || !_workTimerEnd.HasValue)
                return TimeSpan.Zero;
            var remaining = _workTimerEnd.Value - DateTime.Now;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        public TimeSpan GetRemainingBreakTime()
        {
            if (!_breakTimer.Enabled || !_breakTimerEnd.HasValue)
                return TimeSpan.Zero;
            var remaining = _breakTimerEnd.Value - DateTime.Now;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        public TimeSpan GetRemainingCheckTime()
        {
            if (!_checkTimer.Enabled || !_checkTimerEnd.HasValue)
                return TimeSpan.Zero;
            var remaining = _checkTimerEnd.Value - DateTime.Now;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        private void OnProxyStatusChanged(bool isRunning)
        {
            Logger.Instance.Log(isRunning ? "Proxy gestartet." : "Proxy gestoppt.", LogLevel.Info);
        }

        private async Task SafeSwitchToBreakAsync()
        {
            try { await SwitchToBreakAsync(); }
            catch (Exception ex) { Logger.Instance.Log($"Fehler in SwitchToBreakAsync: {ex.Message}", LogLevel.Error); }
        }

        private async Task SafeSwitchToWorkAsync()
        {
            try { await SwitchToWorkAsync(); }
            catch (Exception ex) { Logger.Instance.Log($"Fehler in SwitchToWorkAsync: {ex.Message}", LogLevel.Error); }
        }

        private string FormatTimeSpan(TimeSpan t)
        {
            if (t.TotalHours >= 1)
            {
                int hours = (int)t.TotalHours;
                int minutes = t.Minutes;
                if (minutes > 0)
                    return $"{hours} Stunden {minutes} Minuten";
                else
                    return $"{hours} Stunden";
            }
            else if (t.TotalMinutes >= 1)
            {
                return $"{(int)t.TotalMinutes} Minuten";
            }
            else
            {
                return $"{(int)t.TotalSeconds} Sekunden";
            }
        }

        private async Task OnTimerTicked()
        {
            if (_workTimer.Enabled)
            {
                TimerTicked?.Invoke(GetRemainingWorkTime());
            }
            else if (_breakTimer.Enabled)
            {
                TimerTicked?.Invoke(GetRemainingBreakTime());
            }
            else if (_checkTimer.Enabled)
            {
                TimerTicked?.Invoke(GetRemainingCheckTime());
            }
        }

        private async Task OnAppTimerTicked(TimeSpan timeSpan)
        {
            AppTimerTicked?.Invoke(timeSpan);
        }

        private async Task OnNewProcessDetected(string processName)
        {
            Logger.Instance.Log($"Neuer Prozess erkannt: {processName}", LogLevel.Info);
            NewProcessDetected?.Invoke(processName);
        }

        protected override void Dispose(bool disposing)
        {
            Logger.Instance.Log("TimeManager wird disposed", LogLevel.Debug);
            if (!_disposed)
            {
                if (disposing)
                {
                    _workTimer?.Dispose();
                    _breakTimer?.Dispose();
                    _checkTimer?.Dispose();
                    Logger.Instance.Log("TimeManager wurde disposed.", LogLevel.Info);
                }
                base.Dispose(disposing);
            }
        }
    }

    public enum TimerType
    {
        Work,
        Break,
        Check
    }
    public enum CloseType
    {
        Pause,
        OutOfTime,
    } 
}

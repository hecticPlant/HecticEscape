using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
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

        private readonly AppManager _appManager;
        private readonly WebManager _webManager;
        private readonly LanguageManager _languageManager;
        private readonly OverlayManager _overlayManager;

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

        private DateTime? _workTimerEnd;
        private DateTime? _breakTimerEnd;
        private DateTime? _checkTimerEnd;

        private readonly List<TimeSpan> _announceTimes = new()
        {
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(15),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(30)
        };
        private readonly HashSet<int> _alreadyAnnouncedIntervals = new();

        public TimeManager(
            AppManager appManager,
            WebManager webManager,
            OverlayManager overlayManager,
            ConfigReader configReader,
            LanguageManager languageManager
        ) : base(configReader)
        {
            _appManager = appManager ?? throw new ArgumentNullException(nameof(appManager));
            _webManager = webManager ?? throw new ArgumentNullException(nameof(webManager));
            _overlayManager = overlayManager ?? throw new ArgumentNullException(nameof(overlayManager));
            _languageManager = languageManager ?? throw new ArgumentNullException(nameof(languageManager));

            Initialize();
        }

        public override void Initialize()
        {
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
                _alreadyAnnouncedIntervals.Clear();
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
                _alreadyAnnouncedIntervals.Clear();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler in SwitchToWorkAsync: {ex.Message}", LogLevel.Error);
            }
        }

        private void CheckHandler()
        {
            Logger.Instance.Log("CheckHandler wird aufgerufen", LogLevel.Verbose);
            try
            {
                _appManager.HandleAppBlocking(_intervalCheckMs, _isBreakActive);
                OnTimerTicked();
                CountDown();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler in CheckHandler: {ex.Message}", LogLevel.Error);
            }

        }

        private void CountDown()
        {
            if (_workTimer.Enabled)
            {
                int[] countdownIntervals = { 60 * 60, 30 * 60, 15 * 60, 10 * 60, 5 * 60, 1 * 60, 30}; // in Sekunden
                var remaining = GetRemainingWorkTime();
                int remainingSeconds = (int)Math.Ceiling(remaining.TotalSeconds);

                foreach (var interval in countdownIntervals)
                {
                    if (interval >= (_configReader.GetIntervalFreeMs() / 1000))
                        continue;
                    if (remainingSeconds == interval && !_alreadyAnnouncedIntervals.Contains(interval))
                    {
                        _alreadyAnnouncedIntervals.Add(interval);
                        string message = string.Format(_languageManager.Get("Overlay.PauseBeginntIn"), FormatTimeSpan(remaining));
                        _overlayManager.ShowMessage(message, 1000);
                        Logger.Instance.Log($"Countdown-Anzeige: {message}", LogLevel.Debug);
                    }
                }

                if (remainingSeconds <= 10 && !_countdownActive)
                {
                    _countdownActive = true;
                    _overlayManager.ShowCountdown(remaining.Seconds);
                    Logger.Instance.Log("Countdown gestartet", LogLevel.Verbose);
                }
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
                        _alreadyAnnouncedIntervals.Clear();
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

        public int GetWorkIntervalSeconds() => _intervalWorkMs / 1000;
        public int GetBreakIntervalSeconds() => _intervalBreakMs / 1000;
        public int GetCheckIntervalSeconds() => _intervalCheckMs / 1000;

        public bool IsWorkTimerRunning() => _workTimer.Enabled;
        public bool IsBreakTimerRunning() => _breakTimer.Enabled;
        public bool IsCheckTimerRunning() => _checkTimer.Enabled;
        public bool IsBreakActive() => _isBreakActive;

        public bool IsCountdownActive() => _countdownActive;
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

        private void OnTimerTicked()
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

        protected override void Dispose(bool disposing)
        {
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
}

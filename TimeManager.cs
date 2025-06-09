using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;
using static System.Runtime.InteropServices.JavaScript.JSType;
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
        private bool _showApptimer = false;

        private readonly int[] _countdownIntervalsMS = { 60 * 60 * 1000, 30 * 60 * 1000, 15 * 60 * 1000, 10 * 60 * 1000, 5 * 60 * 1000, 1 * 60 * 1000, 30 * 1000};
        private readonly int _toleranzMS = 1000;

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
            Logger.Instance.Log("CheckHandler wird aufgerufen", LogLevel.Verbose);
            try
            {
                await OnTimerTicked();
                await _appManager.HandleAppBlocking(_intervalCheckMs, _isBreakActive);
                await CountDown();
                await WarnIfDailyTimeIsLow();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler in CheckHandler: {ex.Message}", LogLevel.Error);
            }
        }

        private async Task WarnIfDailyTimeIsLow()
        {
            Logger.Instance.Log("WarnIfDailyTimeIsLow wird aufgerufen", LogLevel.Verbose);
            List<(Gruppe, AppHE)> lowTimeGroups = new List<(Gruppe, AppHE)>();
            DateOnly date = DateOnly.FromDateTime(DateTime.Now);
            lowTimeGroups = _appManager.WarnIfDailyTimeIsLow();

            if (lowTimeGroups.Count == 0)
            {
                Logger.Instance.Log("Keine Apps mit niedriger Tageszeit gefunden", LogLevel.Verbose);
                _overlayManager.HideAppTimer();
                return;
            }
            else
            {
                Logger.Instance.Log($"Suche nach intervallen für {lowTimeGroups.Count} laufenden Apps", LogLevel.Verbose);
                foreach (var item in lowTimeGroups)
                {
                    long timeLeft = _appManager.GetDailyTimeLeft(item.Item1, item.Item2, date);
                    Logger.Instance.Log($"App {item.Item2.Name} hat noch {timeLeft} ms übrig", LogLevel.Verbose);
                    if (timeLeft <= 15 * 60 * 1000)
                    {
                        TriggerAppTimerWaring(item.Item1, item.Item2, date);
                    }
                    var (gefunden, ziel) = await FindeNextIntervallAsync((int)timeLeft);
                    Logger.Instance.Log($"App {item.Item2.Name} hat noch {timeLeft} ms übrig => {gefunden}", LogLevel.Verbose);
                    if (gefunden)
                    {
                        Logger.Instance.Log($"Nächstes Intervall für {item.Item2.Name} gefunden: {ziel} ms", LogLevel.Verbose);
                        string message = string.Format(_languageManager.Get("Overlay.TageszeitWarnung"),
                        item.Item2.Name, FormatTimeSpan(TimeSpan.FromMilliseconds(timeLeft)));

                        _overlayManager.ShowMessage(message, 2000);
                    }
                }
            }
        }

        private void TriggerAppTimerWaring(Gruppe gruppe, AppHE app, DateOnly date)
        {
            Logger.Instance.Log($"Zeige Warn-Timer für App {app.Name} an", LogLevel.Debug);
            TimeSpan timeLeft = TimeSpan.FromMilliseconds(app.Logs.FirstOrDefault(l => l.Date == date).TimeMs);
            _overlayManager.ShowAppTimer(timeLeft);
            OnAppTimerTicked(gruppe, app, date);
        }

        private async Task<(bool Erfolg, int Ziel)> FindeNextIntervallAsync(int eingabe)
        {
            Logger.Instance.Log($"Finde nächstes Intervall für Eingabe: {eingabe} ms", LogLevel.Verbose);
            return await Task.Run(() =>
            {
                int? nächstes = null;
                int kleinsteDiff = int.MaxValue;

                foreach (int interval in _countdownIntervalsMS)
                {
                    if (eingabe > interval) continue;

                    int diff = Math.Abs(eingabe - interval);

                    if (diff <= _toleranzMS && diff < kleinsteDiff)
                    {
                        kleinsteDiff = diff;
                        nächstes = interval;
                    }
                }
                Logger.Instance.Log($"Nächstes Intervall: {nächstes} ms", LogLevel.Verbose);
                if (nächstes.HasValue)
                    return (true, nächstes.Value);
                else
                    return (false, 0);
            });
        }

        private async Task CountDown()
        {
            Logger.Instance.Log("CountDown wird aufgerufen", LogLevel.Verbose);
            if (_workTimer.Enabled)
            {
                var remaining = GetRemainingWorkTime();
                int remainingMilliseconds = (int)Math.Ceiling(remaining.TotalMilliseconds);
                var (gefunden, ziel) = await FindeNextIntervallAsync(remainingMilliseconds);
                if (gefunden)
                {
                    string message = string.Format(_languageManager.Get("Overlay.PauseBeginntIn"), FormatTimeSpan(remaining));
                    _overlayManager.ShowMessage(message, 2000);
                    Logger.Instance.Log($"Countdown-Anzeige: {message}", LogLevel.Debug);
                }
                if (remainingMilliseconds <= 10000 && !_countdownActive)
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
            Logger.Instance.Log($"Arbeitszeit-Intervall: {_intervalWorkMs / 1000} Sekunden", LogLevel.Verbose);
            return _intervalWorkMs / 1000;
        }
        public int GetBreakIntervalSeconds()
        {
            Logger.Instance.Log($"Pausenzeit-Intervall: {_intervalBreakMs / 1000} Sekunden", LogLevel.Verbose);
            return _intervalBreakMs / 1000;
        }
        public int GetCheckIntervalSeconds() 
        { 
            Logger.Instance.Log($"Check-Intervall: {_intervalCheckMs / 1000} Sekunden", LogLevel.Verbose);
            return _intervalCheckMs / 1000; 
        }

        public bool IsWorkTimerRunning()
        {
            Logger.Instance.Log($"Arbeitszeit-Timer läuft: {_workTimer.Enabled}", LogLevel.Verbose);
            return _workTimer.Enabled;
        }
        public bool IsBreakTimerRunning()
        {
            Logger.Instance.Log($"Pausenzeit-Timer läuft: {_breakTimer.Enabled}", LogLevel.Verbose);
            return _breakTimer.Enabled;
        }
        public bool IsCheckTimerRunning()
        {
            Logger.Instance.Log($"Check-Timer läuft: {_checkTimer.Enabled}", LogLevel.Verbose);
            return _checkTimer.Enabled;
        }
        public bool IsBreakActive()
        {
            Logger.Instance.Log($"Pause aktiv: {_isBreakActive}", LogLevel.Verbose);
            return _isBreakActive;
        }

        public bool IsCountdownActive()
        {
            Logger.Instance.Log($"Countdown aktiv: {_countdownActive}", LogLevel.Verbose);
            return _countdownActive;
        }
        public void SetCountdownActive(bool active)
        {
            Logger.Instance.Log($"Setze Countdown aktiv: {active}", LogLevel.Verbose);  
            _countdownActive = active;
            if (!active)
            {
                _overlayManager.CancelCountdown();
            }
        }

        public TimeSpan GetRemainingWorkTime()
        {
            Logger.Instance.Log("Berechne verbleibende Arbeitszeit", LogLevel.Verbose);
            if (!_workTimer.Enabled || !_workTimerEnd.HasValue)
                return TimeSpan.Zero;
            var remaining = _workTimerEnd.Value - DateTime.Now;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        public TimeSpan GetRemainingBreakTime()
        {
            Logger.Instance.Log("Berechne verbleibende Pausenzeit", LogLevel.Verbose);
            if (!_breakTimer.Enabled || !_breakTimerEnd.HasValue)
                return TimeSpan.Zero;
            var remaining = _breakTimerEnd.Value - DateTime.Now;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        public TimeSpan GetRemainingCheckTime()
        {
            Logger.Instance.Log("Berechne verbleibende Check-Zeit", LogLevel.Verbose);
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
            Logger.Instance.Log("Wechsel zu Pause wird versucht", LogLevel.Verbose);
            try { await SwitchToBreakAsync(); }
            catch (Exception ex) { Logger.Instance.Log($"Fehler in SwitchToBreakAsync: {ex.Message}", LogLevel.Error); }
        }

        private async Task SafeSwitchToWorkAsync()
        {
            Logger.Instance.Log("Wechsel zu Arbeitszeit wird versucht", LogLevel.Verbose);
            try { await SwitchToWorkAsync(); }
            catch (Exception ex) { Logger.Instance.Log($"Fehler in SwitchToWorkAsync: {ex.Message}", LogLevel.Error); }
        }

        private string FormatTimeSpan(TimeSpan t)
        {
            Logger.Instance.Log($"Formatiere TimeSpan: {t}", LogLevel.Verbose);
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
            Logger.Instance.Log("TimerTicked wird aufgerufen", LogLevel.Verbose);
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

        private async Task OnAppTimerTicked(Gruppe gruppe, AppHE app, DateOnly date)
        {
            Logger.Instance.Log($"AppTimerTicked für {app.Name} wird aufgerufen", LogLevel.Verbose);
            AppTimerTicked?.Invoke(TimeSpan.FromMilliseconds(_appManager.GetDailyTimeLeft(gruppe, app, date)));
        }

        protected override void Dispose(bool disposing)
        {
            Logger.Instance.Log("TimeManager wird disposed", LogLevel.Info);
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

namespace HecticEscape
{
    /// <summary>
    /// Verwaltet die Arbeits-, Pausen- und Prüf-Timer für die App- und Webseiten-Blockierung.
    /// </summary>
    public class TimeManagement : IDisposable
    {
        public event Action<string> StatusChanged;
        public event Action OverlayToggleRequested;

        private AppManager appManager;
        private WebManager webManager;
        private readonly LanguageManager _languageManager;
        private readonly Overlay _overlay;
        private readonly ConfigReader configReader;

        // Timer für Arbeitszeit, Pausenzeit und Prüfintervall
        private System.Timers.Timer workTimer;
        private System.Timers.Timer breakTimer;
        private System.Timers.Timer checkTimer;
        private System.Timers.Timer overlayAnnounceTimer;

        // Intervalle in Millisekunden
        private int intervalFreeMs;
        private int intervalBreakMs;
        private int intervalCheckMs;

        private bool startTimerAtStartup;

        private bool isBreakActive = false;

        private DateTime? workTimerEnd;
        private DateTime? breakTimerEnd;
        private DateTime? checkTimerEnd;

        private List<TimeSpan> announceTimes = new()
        {
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(15),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(30)
        };
        private HashSet<TimeSpan> alreadyAnnounced = new();

        public TimeManagement(AppManager appManager, WebManager webManager, Overlay overlay, ConfigReader configReader, LanguageManager languageManager)
        {
            this.webManager = webManager;
            this.appManager = appManager;
            this._overlay = overlay;
            this.configReader = configReader;
            this._languageManager = languageManager;

            // Werte aus der Config laden
            intervalFreeMs = configReader.GetIntervalFreeMs();
            if (intervalFreeMs <= 0)
                throw new ArgumentException("IntervalFreeMs muss größer als 0 sein.");

            intervalBreakMs = configReader.GetIntervalBreakMs();
            if (intervalBreakMs <= 0)
                throw new ArgumentException("IntervalBreakMs muss größer als 0 sein.");

            intervalCheckMs = configReader.GetIntervalCheckMs();
            if (intervalCheckMs <= 0)
                throw new ArgumentException("IntervalCheckMs muss größer als 0 sein.");

            startTimerAtStartup = configReader.GetStartTimerAtStartup();

            // Timer-Initialisierung
            workTimer = new System.Timers.Timer(intervalFreeMs);
            workTimer.Elapsed += (sender, e) => _ = SafeSwitchToBreakAsync();
            workTimer.AutoReset = false;

            checkTimer = new System.Timers.Timer(intervalCheckMs);
            checkTimer.Elapsed += (sender, e) => CheckAndCloseBlockedApps();
            checkTimer.AutoReset = true;

            breakTimer = new System.Timers.Timer(intervalBreakMs);
            breakTimer.Elapsed += (sender, e) => _ = SafeSwitchToFreeAsync();
            breakTimer.AutoReset = false;

            overlayAnnounceTimer = new System.Timers.Timer(1000);
            overlayAnnounceTimer.Elapsed += OverlayAnnounceTimer_Elapsed;
            overlayAnnounceTimer.Start();

            webManager.ProxyStatusChanged += OnProxyStatusChanged;
            Logger.Instance.Log("TimeManagement initialisiert", LogLevel.Info);

            if (startTimerAtStartup)
            {
                StartTimer("i");
                StartTimer("c");
            }
        }

        /// <summary>
        /// Stoppt alle Timer.
        /// </summary>
        public void Stop()
        {
            try
            {
                Logger.Instance.Log("TimeManagement: Stop", LogLevel.Info);
                workTimer.Stop();
                breakTimer.Stop();
                checkTimer.Stop();
                overlayAnnounceTimer.Stop();
                _overlay.CancelCountdown();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Stoppen der Timer: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Startet die Pausenphase.
        /// </summary>
        private async Task SwitchToBreakAsync()
        {
            try
            {
                Logger.Instance.Log("Pause wird gestartet", LogLevel.Info);
                isBreakActive = true;
                StatusChanged?.Invoke("Momentan Pause");

                // Overlay: Pause beginnt
                _overlay.Dispatcher.Invoke(() => _overlay.ShowMessage($"{_languageManager.Get("Overlay.PauseBeginnt")}", 2500));
                breakTimer.Stop();
                breakTimerEnd = DateTime.Now.AddMilliseconds(breakTimer.Interval);
                breakTimer.Start();
                alreadyAnnounced.Clear(); // HashSet für nächste Pause zurücksetzen

                _overlay.Dispatcher.Invoke(() => _overlay.StartOverlayTimer((int)breakTimer.Interval));

                if (!webManager.IsProxyRunning)
                {
                    await Task.Run(() => webManager.StartProxy());
                }
                else
                {
                    Logger.Instance.Log("Proxy läuft bereits.", LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler in SwitchToBreakAsync: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Beendet die Pausenphase und startet die Arbeitsphase.
        /// </summary>
        private async Task SwitchToFreeAsync()
        {
            try
            {
                Logger.Instance.Log("Pause wird beendet", LogLevel.Info);
                isBreakActive = false;
                StatusChanged?.Invoke("Momentan freie Zeit");

                // Overlay: Pause ist vorbei
                _overlay.Dispatcher.Invoke(() => _overlay.ShowMessage($"{_languageManager.Get("Overlay.PauseVorbei")}", 2500));
                workTimer.Stop();
                workTimerEnd = DateTime.Now.AddMilliseconds(workTimer.Interval);
                workTimer.Start();
                alreadyAnnounced.Clear(); // HashSet für neuen Arbeitszyklus zurücksetzen

                _overlay.Dispatcher.Invoke(() => _overlay.StartOverlayTimer((int)workTimer.Interval));

                if (webManager.IsProxyRunning)
                {
                    await Task.Run(() => webManager.StopProxy());
                    Logger.Instance.Log("Proxy wurde gestoppt.", LogLevel.Info);
                }
                else
                {
                    Logger.Instance.Log("Proxy war bereits gestoppt.", LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler in SwitchToFreeAsync: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Wird regelmäßig während der Pause aufgerufen und schließt blockierte Apps.
        /// </summary>
        private void CheckAndCloseBlockedApps()
        {
            Logger.Instance.Log("CheckAndCloseBlockedApps wird aufgerufen", LogLevel.Verbose);
            try
            {
                appManager.BlockHandler(intervalCheckMs, isBreakActive);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler in CheckAndCloseBlockedApps: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Erzwingt sofort eine Pause.
        /// </summary>
        public void ForceBreak()
        {
            try
            {
                Logger.Instance.Log("Erzwinge eine Pause.", LogLevel.Warn);
                workTimer.Stop();
                _ = SwitchToBreakAsync();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Erzwingen einer Pause: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Erzwingt das sofortige Ende einer Pause.
        /// </summary>
        public void EndBreak()
        {
            try
            {
                Logger.Instance.Log("Erzwinge das Ende einer Pause", LogLevel.Warn);
                breakTimer.Stop();
                _ = SwitchToFreeAsync();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Erzwingen des Endes einer Pause: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Setzt die Zeit für einen der Timer (in Sekunden).
        /// </summary>
        public void SetTimerTime(string timer, int time)
        {
            try
            {
                switch (timer)
                {
                    case "i":
                        Logger.Instance.Log($"intervalFreeMs wurde auf {time} Sekunden gesetzt", LogLevel.Info);
                        intervalFreeMs = time * 1000;
                        workTimer.Interval = intervalFreeMs;
                        configReader.SetIntervalFreeMs(intervalFreeMs);
                        configReader.SaveConfig();
                        break;
                    case "p":
                        Logger.Instance.Log($"intervalBreakMs wurde auf {time} Sekunden gesetzt", LogLevel.Info);
                        intervalBreakMs = time * 1000;
                        breakTimer.Interval = intervalBreakMs;
                        configReader.SetIntervalBreakMs(intervalBreakMs);
                        configReader.SaveConfig();
                        break;
                    case "c":
                        Logger.Instance.Log($"intervalCheckMs wurde auf {time} Sekunden gesetzt", LogLevel.Info);
                        intervalCheckMs = time * 1000;
                        checkTimer.Interval = intervalCheckMs;
                        configReader.SetIntervalCheckMs(intervalCheckMs);
                        configReader.SaveConfig();
                        break;
                    default:
                        Logger.Instance.Log($"'{timer}' ist keine gültige Eingabe.", LogLevel.Warn);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Setzen der Timer-Zeit: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Stoppt den gewählten Timer.
        /// </summary>
        public void StopTimer(string timer)
        {
            try
            {
                switch (timer)
                {
                    case "i":
                        Logger.Instance.Log("Stoppe intervalFreeMs", LogLevel.Info);
                        workTimer.Stop();
                        break;
                    case "p":
                        Logger.Instance.Log("Stoppe intervalBreakMs", LogLevel.Info);
                        breakTimer.Stop();
                        break;
                    case "c":
                        Logger.Instance.Log("Stoppe intervalCheckMs", LogLevel.Info);
                        checkTimer.Stop();
                        break;
                    default:
                        Logger.Instance.Log($"'{timer}' ist keine gültige Eingabe.", LogLevel.Warn);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Stoppen des Timers: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Startet den gewählten Timer.
        /// </summary>
        public void StartTimer(string timer)
        {
            try
            {
                switch (timer)
                {
                    case "i":
                        Logger.Instance.Log("Starte intervalFreeMs", LogLevel.Info);
                        workTimer.Stop();
                        workTimerEnd = DateTime.Now.AddMilliseconds(workTimer.Interval);
                        _overlay.StartOverlayTimer((int)workTimer.Interval);
                        workTimer.Start();
                        alreadyAnnounced.Clear();
                        if (!overlayAnnounceTimer.Enabled)
                        {
                            overlayAnnounceTimer.Start();
                        }
                        break;
                    case "p":
                        Logger.Instance.Log("Starte intervalBreakMs", LogLevel.Info);
                        breakTimer.Stop();
                        breakTimerEnd = DateTime.Now.AddMilliseconds(breakTimer.Interval);
                        _overlay.StartOverlayTimer((int)workTimer.Interval);
                        breakTimer.Start();
                        break;
                    case "c":
                        Logger.Instance.Log("Starte intervalCheckMs", LogLevel.Info);
                        checkTimer.Stop();
                        checkTimerEnd = DateTime.Now.AddMilliseconds(checkTimer.Interval);
                        checkTimer.Start();
                        break;
                    default:
                        Logger.Instance.Log($"'{timer}' ist keine gültige Eingabe.", LogLevel.Warn);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Starten des Timers: {ex.Message}", LogLevel.Error);
            }
        }

        public int GetIntervalFree() => intervalFreeMs / 1000;
        public int GetIntervalBreak() => intervalBreakMs / 1000;
        public int GetIntervalCheck() => intervalCheckMs / 1000;

        public bool IsWorkTimerRunning() => workTimer.Enabled;
        public bool IsBreakTimerRunning() => breakTimer.Enabled;
        public bool IsCheckTimerRunning() => checkTimer.Enabled;
        public bool IsBreakActive() => isBreakActive;

        public TimeSpan? GetRemainingWorkTime()
        {
            if (workTimer.Enabled && workTimerEnd.HasValue)
                return workTimerEnd.Value - DateTime.Now;
            return null;
        }

        public TimeSpan? GetRemainingBreakTime()
        {
            if (breakTimer.Enabled && breakTimerEnd.HasValue)
                return breakTimerEnd.Value - DateTime.Now;
            return null;
        }

        public TimeSpan? GetRemainingCheckTime()
        {
            if (checkTimer.Enabled && checkTimerEnd.HasValue)
                return checkTimerEnd.Value - DateTime.Now;
            return null;
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

        private async Task SafeSwitchToFreeAsync()
        {
            try { await SwitchToFreeAsync(); }
            catch (Exception ex) { Logger.Instance.Log($"Fehler in SwitchToFreeAsync: {ex.Message}", LogLevel.Error); }
        }

        private void OverlayAnnounceTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            // Nur während der Arbeitszeit Overlay-Ankündigungen machen!
            if (!workTimer.Enabled || !workTimerEnd.HasValue || isBreakActive)
                return;

            var remaining = workTimerEnd.Value - DateTime.Now;
            var totalWorkTime = TimeSpan.FromMilliseconds(workTimer.Interval);

            foreach (var t in announceTimes)
            {
                if (t > totalWorkTime)
                    continue;

                if (remaining <= t && !alreadyAnnounced.Contains(t))
                {
                    alreadyAnnounced.Add(t);
                    _overlay.Dispatcher.Invoke(() =>
                        _overlay.ShowMessage($"{_languageManager.Get("Overlay.PauseVorbei")}, {FormatTimeSpan(t)}"));
                }
            }

            if (totalWorkTime >= TimeSpan.FromSeconds(10) &&
                remaining.TotalSeconds <= 10 && remaining.TotalSeconds > 0 &&
                !alreadyAnnounced.Contains(TimeSpan.FromSeconds(10)))
            {
                alreadyAnnounced.Add(TimeSpan.FromSeconds(10));
                _overlay.Dispatcher.Invoke(async () => await _overlay.ShowCountdownAsync(10));
            }
        }

        // Hilfsmethode für Zeitformatierung
        private string FormatTimeSpan(TimeSpan t)
        {
            if (t.TotalMinutes >= 1)
                return $"{(int)t.TotalMinutes} Minuten";
            else
                return $"{(int)t.TotalSeconds} Sekunden";
        }

        /// <summary>
        /// Schaltet das Overlay um.
        /// </summary>
        public void ToggleOverlay()
        {
            try
            {
                Logger.Instance.Log("Overlay wird umgeschaltet", LogLevel.Info);
                OverlayToggleRequested?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Umschalten des Overlays: {ex.Message}", LogLevel.Error);
            }
        }

        public void Dispose()
        {
            workTimer?.Dispose();
            breakTimer?.Dispose();
            checkTimer?.Dispose();
            overlayAnnounceTimer?.Dispose();
        }
    }
}

namespace ScreenZen
{
    /// <summary>
    /// Verwaltet die Timer
    /// </summary>
    public class TimeManagement
    {
        public event Action<string> StatusChanged;
        public event Action OverlayToggleRequested;

        private AppManager appManager;
        private WebManager webManager;

        //Timer
        private System.Timers.Timer timerFree;
        private System.Timers.Timer timerBreak;
        private System.Timers.Timer timerCheck;

        private int intervall_Free = 2 * 3600 * 1000;
        private int intervall_Break = 15 * 60 * 1000;
        private int intervall_Check = 1000;

        private bool isBreakActive = false;

        public TimeManagement(AppManager appManager, WebManager webManager)
        {
            this.webManager = webManager;
            this.appManager = appManager;

            // Timer-Initialisierung:
            timerFree = new System.Timers.Timer(intervall_Free);
            timerFree.Elapsed += (sender, e) => SwitchToBreakAsync();
            timerFree.AutoReset = false;

            timerCheck = new System.Timers.Timer(intervall_Check);
            timerCheck.Elapsed += (sender, e) => CheckAndCloseBlockedApps();
            timerCheck.AutoReset = true;

            timerBreak = new System.Timers.Timer(intervall_Break);
            timerBreak.Elapsed += (sender, e) => SwitchToFree();
            timerBreak.AutoReset = false;

            webManager.ProxyStatusChanged += OnProxyStatusChanged;
            Logger.Instance.Log("Initialisiert");
        }

        /// <summary>
        /// DEBUG: Stoppt alle Timer
        /// </summary>
        public void Stop()
        {
            Logger.Instance.Log("TimeManagement: Stop");
            timerFree.Stop();
            timerBreak.Stop();
            timerCheck.Stop();
        }

        /// <summary>
        /// Startet eine Pause, wenn der Proxy nicht bereits aktiv ist.
        /// </summary>
        /// <returns>Eine asynchrone Task, die den Start der Pause verwaltet.</returns>
        private async Task SwitchToBreakAsync()
        {
            Logger.Instance.Log("Pause wird gestartet");
            isBreakActive = true;
            StatusChanged?.Invoke("Momentan Pause");
            timerBreak.Start();
            timerCheck.Start();

            // Proxy nur starten, wenn er nicht bereits läuft
            if (!webManager.IsProxyRunning)
            {
                await Task.Run(() => webManager.StartProxy());
                Logger.Instance.Log("Proxy wurde gestartet.");
            }
            else
            {
                Logger.Instance.Log("Proxy läuft bereits.");
            }
        }

        /// <summary>
        /// Beendet eine Pause, wenn der Proxy aktiv ist.
        /// </summary>
        /// <returns>Eine asynchrone Task, die das Ende der Pause verwaltet.</returns>
        private async Task SwitchToFree()
        {
            Logger.Instance.Log("Pause wird beendet");
            isBreakActive = false;
            StatusChanged?.Invoke("Momentan freie Zeit");
            timerFree.Start();

            // Proxy nur stoppen, wenn er aktuell läuft
            if (webManager.IsProxyRunning)
            {
                await Task.Run(() => webManager.StopProxy());
                Logger.Instance.Log("Proxy wurde gestoppt.");
            }
            else
            {
                Logger.Instance.Log("Proxy war bereits gestoppt.");
            }
        }

        /// <summary>
        /// Wird von timerCheck aufgerufen und schließt alle Apps
        /// </summary>
        private void CheckAndCloseBlockedApps()
        {
            //Logger.Instance.Log("TimeManagement: CheckAndCloseBlockedApps");
            if (isBreakActive)
            {
                appManager.BlockHandler();
            }
        }

        /// <summary>
        /// DEBUG: Erzwinge ein Pause
        /// </summary>
        public void ForceBreak()
        {
            Logger.Instance.Log("Erzwinge eine Pause.");
            timerFree.Stop();
            SwitchToBreakAsync();
        }

        /// <summary>
        /// DEBUG: Erzwinge das beenden eine Pause
        /// </summary>
        public void EndBreak()
        {
            Logger.Instance.Log("Erzwinge das Ende einer Pause");
            timerBreak.Stop();
            SwitchToFree();
        }
    
        /// <summary>
        /// Setzt die Timer Zeit
        /// </summary>
        /// <param name="timer">i: Intervall-Timer, p: Pause-Timer, c: Check-Timer</param>
        /// <param name="time">Zeit in Sekunden</param>
        public void SetTimerTime(string timer, int time) 
        {
            switch(timer)
            {
                case "i":
                    Logger.Instance.Log($"intervall_Free wurde auf {time} Sekunden gesetz");
                    intervall_Free = time*1000;
                    break;
                case "p":
                    Logger.Instance.Log($"intervall_Break wurde auf {time} Sekunden gesetz");
                    intervall_Break = time*1000;
                    break;
                case "c":
                    Logger.Instance.Log($"intervall_Check wurde auf {time} Sekunden gesetz");
                    intervall_Check = time*1000;
                    break;
                default:
                    Logger.Instance.Log($"'{timer}' ist keine gültige Eingabe.");
                    break;
            }
        }

        /// <summary>
        /// Stoppt den gewählten Timer
        /// </summary>
        /// <param name="timer">i: Intervall-Timer, p: Pause-Timer, c: Check-Timer</param>
        public void StopTimer(string timer)
        {
            switch (timer)
            {
                case "i":
                    Logger.Instance.Log("Stoppe intervall_Free");
                    timerFree.Stop();
                    break;
                case "p":
                    Logger.Instance.Log("Stoppe intervall_Free");
                    timerBreak.Stop();
                    break;
                case "c":
                    Logger.Instance.Log("Stoppe intervall_Free");
                    timerCheck.Stop(); ;
                    break;
                default:
                    Logger.Instance.Log($"'{timer}' ist keine gültige Eingabe.");
                    break;
            }
        }

        /// <summary>
        /// Startet den gewählten Timer
        /// </summary>
        /// <param name="timer">i: Intervall-Timer, p: Pause-Timer, c: Check-Timer</param>
        public void StartTimer(string timer)
        {
            switch (timer)
            {
                case "i":
                    Logger.Instance.Log("Starte intervall_Free");
                    timerFree.Stop();
                    timerFree.Start();
                    break;
                case "p":
                    Logger.Instance.Log("Starte intervall_Break");
                    timerBreak.Stop();
                    timerBreak.Start();
                    break;
                case "c":
                    Logger.Instance.Log("Starte intervall_Check");
                    timerCheck.Stop();
                    timerCheck.Start();
                    break;
                default:
                    Logger.Instance.Log($"'{timer}' ist keine gültige Eingabe.");
                    break;
            }
        }

        public int GetIntervall_Free()
        {
            return this.intervall_Free / 1000;
        }

        public int GetIntervall_Break()
        {
            return this.intervall_Break / 1000;
        }

        public int GetIntervall_Check()
        {
            return this.intervall_Check / 1000;
        }
        
        public bool TimerRunning_Free()
        {
            return timerFree.Enabled;
        }

        public bool TimerRunning_Break()
        {
            return timerBreak.Enabled;
        }

        public bool TimerRunning_Check()
        {
            return timerCheck.Enabled;
        }
    
        public bool IsBreakActive_Check()
        {
            return isBreakActive;
        }

        private void OnProxyStatusChanged(bool isRunning)
        {
            Logger.Instance.Log(isRunning ? "Proxy gestartet." : "Proxy gestoppt.");
        }
    }
}

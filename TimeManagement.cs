namespace ScreenZen
{
    /// <summary>
    /// Verwaltet die Timer
    /// </summary>
    public class TimeManagement
    {
        public event Action<string> StatusChanged;
        public event Action OverlayToggleRequested;
        public event Action<bool> UpdateProxyStatus;

        /// <summary>
        /// Timer der nach 2 Stunden eine Pause statet
        /// </summary>
        private System.Timers.Timer timerFree;
        private int intervall_Free = 2 * 3600 * 1000;
        /// <summary>
        /// Timer der nach 15 Minuten die Pause beendet
        /// </summary>
        private System.Timers.Timer timerBreak;
        private int intervall_Break = 15 * 60 * 1000;
        /// <summary>
        /// Timer der einmal die Sekunde alle Apps beendet
        /// </summary>
        private System.Timers.Timer timerCheck;

        private AppManager appManager;
        private WebProxySZ webProxy;
        private ConfigReader configReader;
        private bool isBreakActive = false;

        // Konstruktor-Injektion von ProcessManager
        public TimeManagement(ConfigReader configReader, AppManager appManager, WebProxySZ webProxy)
        {
            this.webProxy = webProxy;
            this.appManager = appManager;
            this.configReader = configReader;

            // Timer-Initialisierung:
            timerFree = new System.Timers.Timer(intervall_Free);
            timerFree.Elapsed += (sender, e) => SwitchToBreakAsync();
            timerFree.AutoReset = false;

            timerCheck = new System.Timers.Timer(1000);
            timerCheck.Elapsed += (sender, e) => CheckAndCloseBlockedApps();
            timerCheck.AutoReset = true;

            timerBreak = new System.Timers.Timer(intervall_Break);
            timerBreak.Elapsed += (sender, e) => SwitchToFree();
            timerBreak.AutoReset = false;
        }

        /// <summary>
        /// DEBUG: Startet die Timer timerCheck und timerFree
        /// </summary>
        public void Start()
        {
            Logger.Instance.Log("Start");
            isBreakActive = false;
            timerCheck.Start();
            timerFree.Start();
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
        /// Startet eine Pause
        /// </summary>
        /// <returns></returns>
        private async Task SwitchToBreakAsync()
        {
            Logger.Instance.Log("Pause wird gestartet");
            isBreakActive = true;
            StatusChanged?.Invoke("Momentan Pause");
            timerBreak.Start();
            await Task.Run(() => webProxy.StartProxy());
        }

        /// <summary>
        /// Beendet eine Pause
        /// </summary>
        /// <returns></returns>
        private async Task SwitchToFree()
        {
            Logger.Instance.Log("Pause wird beendet");
            isBreakActive = false;
            StatusChanged?.Invoke("Momentan freie Zeit");
            timerFree.Start();
            await webProxy.StopProxy();
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

        public void ForceBreak()
        {
            Logger.Instance.Log("TimeManagement: ForceBreak");
            timerFree.Stop();
            SwitchToBreakAsync();
        }

        public void EndBreak()
        {
            Logger.Instance.Log("TimeManagement: EndBreak");
            timerBreak.Stop();
            SwitchToFree();
        }
    }
}

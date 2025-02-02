namespace ScreenZen
{
    public class TimeManagement
    {
        public event Action<string> StatusChanged;
        private System.Timers.Timer timerFree;
        private System.Timers.Timer timerBreak;
        private System.Timers.Timer timerCheck;
        private ProcessManager processManager;
        private int intervall_Free = 2 * 3600 * 1000;
        private int intervall_Break = 15 * 60 * 1000;
        private bool isBreakActive = false;

        // Konstruktor-Injektion von ProcessManager
        public TimeManagement(ProcessManager processManager)
        {
            this.processManager = processManager;

            // Timer-Initialisierung:
            timerFree = new System.Timers.Timer(intervall_Free);
            timerFree.Elapsed += (sender, e) => SwitchToBreak();
            timerFree.AutoReset = false;

            timerCheck = new System.Timers.Timer(1000);
            timerCheck.Elapsed += (sender, e) => CheckAndCloseBlockedApps();
            timerCheck.AutoReset = true;

            timerBreak = new System.Timers.Timer(intervall_Break);
            timerBreak.Elapsed += (sender, e) => SwitchToFree();
            timerBreak.AutoReset = false;

            Logger.Instance.Log("TimeManagement: Initialized.");
        }

        public void Start()
        {
            Logger.Instance.Log("TimeManagement: Start");
            isBreakActive = false;
            timerCheck.Start();
            timerFree.Start();
        }

        public void Stop()
        {
            Logger.Instance.Log("TimeManagement: Stop");
            timerFree.Stop();
            timerBreak.Stop();
            timerCheck.Stop();
        }

        private void SwitchToBreak()
        {
            Logger.Instance.Log("TimeManagement: SwitchToBreak");
            isBreakActive = true;
            StatusChanged?.Invoke("Momentan Pause");
            processManager.BlockAppsFromGroup("Gruppe 1");
            timerBreak.Start();
        }

        private void SwitchToFree()
        {
            Logger.Instance.Log("TimeManagement: SwitchToFree");
            isBreakActive = false;
            StatusChanged?.Invoke("Momentan freie Zeit");
            timerFree.Start();
        }

        private void CheckAndCloseBlockedApps()
        {
            Logger.Instance.Log("TimeManagement: CheckAndCloseBlockedApps");
            if (isBreakActive)
            {
                processManager.BlockAppsFromGroup("Gruppe 1");
            }
        }

        public void ForceBreak()
        {
            Logger.Instance.Log("TimeManagement: ForceBreak");
            timerFree.Stop();
            SwitchToBreak();
        }

        public void EndBreak()
        {
            Logger.Instance.Log("TimeManagement: EndBreak");
            timerBreak.Stop();
            SwitchToFree();
        }
    }
}

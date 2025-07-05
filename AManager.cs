using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace HecticEscape
{
    /// <summary>
    /// Abstrakte Basisklasse für alle Manager mit gemeinsamen Systemvariablen
    /// (globale Einstellungen, Timer, Feature-Flags, Sprache).
    /// Keine Gruppen-Logik mehr!
    /// </summary>
    public abstract class AManager : IDisposable
    {
        protected readonly ConfigReader _configReader;
        private bool _disposed;

        protected AManager(ConfigReader configReader)
        {
            _configReader = configReader;
        }


        // Basis-Operationen
        public abstract void Initialize();


        // System-Konfiguration
        public bool EnableDebugMode => _configReader.GetEnableDebugMode();
        public void SetEnableDebugMode(bool enable) => _configReader.SetEnableDebugMode(enable);

        public bool EnableVerboseMode => _configReader.GetEnableVerboseMode();
        public void SetEnableVerboseMode(bool enable) => _configReader.SetEnableVerboseMode(enable);

        public bool EnableIncludeFoundGames => _configReader.GetEnableIncludeFoundGames();
        public void SetEnableIncludeFoundGames(bool enable) => _configReader.SetEnableIncludeFoundGames(enable);


        // Timer-Konfiguration
        public int IntervalFreeMs => _configReader.GetIntervalFreeMs();
        public void SetIntervalFreeMs(int interval) => _configReader.SetIntervalFreeMs(interval);

        public int IntervalBreakMs => _configReader.GetIntervalBreakMs();
        public void SetIntervalBreakMs(int interval) => _configReader.SetIntervalBreakMs(interval);

        public int IntervalCheckMs => _configReader.GetIntervalCheckMs();
        public void SetIntervalCheckMs(int interval) => _configReader.SetIntervalCheckMs(interval);

        public bool StartTimerAtStartup => _configReader.GetStartTimerAtStartup();
        public void SetStartTimerAtStartup(bool enable) => _configReader.SetStartTimerAtStartup(enable);


        // Feature-Flags
        public bool EnableWebsiteBlocking => _configReader.GetWebsiteBlockingEnabled();
        public void SetEnableWebsiteBlocking(bool enable) => _configReader.SetWebsiteBlockingEnabled(enable);

        public bool EnableAppBlocking => _configReader.GetAppBlockingEnabled();
        public void SetEnableAppBlocking(bool enable) => _configReader.SetEnableAppBlocking(enable);

        public bool EnableOverlay => _configReader.GetEnableOverlay();
        public void SetEnableOverlay(bool enable) => _configReader.SetEnableOverlay(enable);

        public bool EnableShowTimeInOverlay => _configReader.GetShowTimeInOverlayEnable();
        public void SetEnableShowTimeInOverlay(bool enable) => _configReader.EnableShowTimeInOverlay(enable);

        public bool EnableShowAppTimeInOverlay => _configReader.GetShowAppTimeInOverlayEnable();
        public void SetEnableShowAppTimeInOverlay(bool enable) => _configReader.SetShowAppTimeInOverlayEnable(enable);

        public bool EnableShowTimerWhenAppIsOpen => _configReader.GetEnableShowTimerWhenAppIsOpen();
        public void SetEnableShowTimerWhenAppIsOpen(bool enable) => _configReader.SetEnableShowTimerWhenAppIsOpen(enable);

        // System-Einstellungen
        public bool EnableUpdateCheck => _configReader.GetEnableUpdateCheck();
        public void SetEnableUpdateCheck(bool enable) => _configReader.SetEnableUpdateCheck(enable);

        public bool EnableStartOnWindowsStartup => _configReader.GetEnableStartOnWindowsStartup();
        public void SetEnableStartOnWindowsStartup(bool enable) => _configReader.SetEnableStartOnWindowsStartup(enable);

        public string GetCurrentLanguageString() => _configReader.GetCurrentLanguageString();

        public bool EnableShowProcessesWithWindowOnly => _configReader.GetEnableShowProcessesWithWindowOnly();
        public void SetEnableShowProcessesWithWindowOnly(bool enable) => _configReader.SetEnableShowProcessesWithWindowOnly(enable);

        public bool EnableGroupBlocking => _configReader.GetEnableGroupBlocking();
        public void SetEnableGroupBlocking(bool enable) => _configReader.SetEnableGroupBlocking(enable);

        public bool EnableScanForNewApps => _configReader.GetEnableScanForNewApps();
        public void SetEnableScanForNewApps(bool enable) => _configReader.SetEnableScanForNewApps(enable);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}

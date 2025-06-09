using System;
using System.Windows;

namespace HecticEscape
{
    /// <summary>
    /// Verwaltet MainWindow, Overlay und zentrale Manager.
    /// </summary>
    public class WindowManager : AManager
    {
        public readonly OverlayManager OverlayManager;
        public readonly TimeManager TimeManager;
        public readonly AppManager AppManager;
        public readonly WebManager WebManager;
        public readonly LanguageManager LanguageManager;
        public readonly GroupManager GroupManager;

        public WindowManager(
            ConfigReader configReader,
            OverlayManager overlayManager,
            TimeManager timeManager,
            AppManager appManager,
            WebManager webManager,
            LanguageManager languageManager,
            GroupManager groupManager
        )
            : base(configReader)
        {
            OverlayManager = overlayManager ?? throw new ArgumentNullException(nameof(overlayManager));
            TimeManager = timeManager ?? throw new ArgumentNullException(nameof(timeManager));
            AppManager = appManager ?? throw new ArgumentNullException(nameof(appManager));
            WebManager = webManager ?? throw new ArgumentNullException(nameof(webManager));
            LanguageManager = languageManager ?? throw new ArgumentNullException(nameof(languageManager));
            GroupManager = groupManager ?? throw new ArgumentNullException(nameof(groupManager));
        }

        public override void Initialize()
        {
        }

        public MainWindow? MainWindow { get; set; }

        public void ShowMainWindow()
        {
            Logger.Instance.Log("WindowManager: Zeige MainWindow", LogLevel.Verbose);
            MainWindow?.Show();
            MainWindow.WindowState = WindowState.Normal;
            MainWindow.Activate();
        }

        public void HideMainWindow()
        {
            MainWindow?.Hide();
        }
    }
}
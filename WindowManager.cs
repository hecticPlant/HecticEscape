using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using System.Windows.Media;

namespace HecticEscape
{
    public class WindowManager : AManager
    {
        private readonly IServiceProvider _services;
        public readonly OverlayManager OverlayManager;
        public readonly TimeManager TimeManager;
        public readonly AppManager AppManager;
        public readonly WebManager WebManager;
        public readonly LanguageManager LanguageManager;
        public readonly GroupManager GroupManager;
        public MainWindow? MainWindow { get; set; }
        public CustomizerWindow? CustomizerWindow { get; set; }
        public GroupSelectionWindow? GroupSelectionWindow { get; set; }

        public WindowManager(
            IServiceProvider services,
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
            _services = services ?? throw new ArgumentNullException(nameof(services));
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

        public void ShowCustomizerWindow(bool modal = false)
        {
            CustomizerWindow = _services.GetRequiredService<CustomizerWindow>(); // <<< hinzufügen

            if (MainWindow != null)
            {
                CustomizerWindow.Owner = MainWindow;
            }

            if (modal)
            {
                CustomizerWindow.ShowDialog();
            }
            else
            {
                CustomizerWindow.Show();
                CustomizerWindow.WindowState = WindowState.Normal;
                CustomizerWindow.Activate();
                InitializeValuesFromTarget();
            }
        }

        public void ShowGroupSelectionWindow(string processName, bool modal = false)
        {
            GroupSelectionWindow = _services.GetRequiredService<GroupSelectionWindow>();
            if (MainWindow != null)
            {
                GroupSelectionWindow.Owner = MainWindow;
            }
            if (modal)
            {
                GroupSelectionWindow.ShowDialog();
            }
            else
            {
                GroupSelectionWindow.Show();
                GroupSelectionWindow.WindowState = WindowState.Normal;
                GroupSelectionWindow.Activate();
                GroupSelectionWindow.SetProcessName(processName);
            }
        }
        public void InitializeValuesFromTarget()
        {
            try
            {
                OverlayManager.SetPauseTimerForegroundColorHex(_configReader.GetPauseTimerForegroundColorHex());
                OverlayManager.SetPauseTimerBackgroundColorHex(_configReader.GetPauseTimerBackgroundColorHex());
                OverlayManager.SetPauseTimerForegroundOpacity(_configReader.GetPauseTimerForegroundOpacity());
                OverlayManager.SetPauseTimerBackgroundOpacity(_configReader.GetPauseTimerBackgroundOpacity());

                OverlayManager.SetAppTimerForegroundColorHex(_configReader.GetAppTimerForegroundColorHex());
                OverlayManager.SetAppTimerBackgroundColorHex(_configReader.GetAppTimerBackgroundColorHex());
                OverlayManager.SetAppTimerForegroundOpacity(_configReader.GetAppTimerForegroundOpacity());
                OverlayManager.SetAppTimerBackgroundOpacity(_configReader.GetAppTimerBackgroundOpacity());

                OverlayManager.SetMessageForegroundColorHex(_configReader.GetMessageForegroundColorHex());
                OverlayManager.SetMessageBackgroundColorHex(_configReader.GetMessageBackgroundColorHex());
                OverlayManager.SetMessageForegroundOpacity(_configReader.GetMessageForegroundOpacity());
                OverlayManager.SetMessageBackgroundOpacity(_configReader.GetMessageBackgroundOpacity());

                Logger.Instance.Log("InitializeValuesFromTarget erfolgreich abgeschlossen.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"CustomizerWindow: Fehler in InitializeValuesFromTarget: {ex.Message}", LogLevel.Error);
            }

            try
            {
                if (CustomizerWindow == null) return;
                // Farben
                CustomizerWindow.PauseTimerForegroundHexTextBox.Text = _configReader.GetPauseTimerForegroundColorHex();
                CustomizerWindow.PauseTimerForegroundPreview.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_configReader.GetPauseTimerForegroundColorHex()));
                CustomizerWindow.PauseTimerBackgroundHexTextBox.Text = _configReader.GetPauseTimerBackgroundColorHex();
                CustomizerWindow.PauseTimerBackgroundPreview.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_configReader.GetPauseTimerBackgroundColorHex()));
                CustomizerWindow.AppTimerForegroundHexTextBox.Text = _configReader.GetAppTimerForegroundColorHex();
                CustomizerWindow.AppTimerForegroundPreview.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_configReader.GetAppTimerForegroundColorHex()));
                CustomizerWindow.AppTimerBackgroundHexTextBox.Text = _configReader.GetAppTimerBackgroundColorHex();
                CustomizerWindow.AppTimerBackgroundPreview.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_configReader.GetAppTimerBackgroundColorHex()));
                CustomizerWindow.MessageForegroundHexTextBox.Text = _configReader.GetMessageForegroundColorHex();
                CustomizerWindow.MessageForegroundPreview.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_configReader.GetMessageForegroundColorHex()));
                CustomizerWindow.MessageBackgroundHexTextBox.Text = _configReader.GetMessageBackgroundColorHex();
                CustomizerWindow.MessageBackgroundPreview.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_configReader.GetMessageBackgroundColorHex()));

                // Opacity-Slider
                CustomizerWindow.ForegroundTransparencySlider.Value = _configReader.GetPauseTimerForegroundOpacity();
                CustomizerWindow.BackgroundTransparencySlider.Value = _configReader.GetPauseTimerBackgroundOpacity();
                CustomizerWindow.AppTimerForegroundTransparencySlider.Value = _configReader.GetAppTimerForegroundOpacity();
                CustomizerWindow.AppTimerBackgroundTransparencySlider.Value = _configReader.GetAppTimerBackgroundOpacity();
                CustomizerWindow.MessageForegroundTransparencySlider.Value = _configReader.GetMessageForegroundOpacity();
                CustomizerWindow.MessageBackgroundTransparencySlider.Value = _configReader.GetMessageBackgroundOpacity();

                Logger.Instance.Log("CustomizerWindow: SetPreview erfolgreich ausgeführt.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"CustomizerWindow: Fehler in SetPreview: {ex.Message}", LogLevel.Error);
            }
        }

        public void ShowMainWindow()
        {
            if (MainWindow != null)
            {
                MainWindow.Show();
                MainWindow.WindowState = WindowState.Normal;
                MainWindow.Activate();
            }
        }

        public void HideMainWindow()
        {
            MainWindow?.Hide();
        }
    }
}

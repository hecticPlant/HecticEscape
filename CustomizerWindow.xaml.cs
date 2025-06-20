using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HecticEscape
{
    public partial class CustomizerWindow : Window, IDisposable
    {
        private readonly WindowManager _windowManager;
        private readonly LanguageManager _languageManager;
        private bool disposed = false;

        public CustomizerWindow(LanguageManager languageManager, WindowManager windowManager)
        {
            InitializeComponent();
            Logger.Instance.Log("CustomizerWindow initialisiert.", LogLevel.Info);

            _languageManager = languageManager ?? throw new ArgumentNullException(nameof(languageManager));
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        }

        private bool TryParseHexColor(string hex, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(hex))
                return false;
            if (!hex.StartsWith("#"))
                return false;
            string hexDigits = hex.Substring(1);
            if (hexDigits.Length == 6 || hexDigits.Length == 8)
            {
                try
                {
                    var conv = (Color)ColorConverter.ConvertFromString(hex);
                    color = conv;
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        private void SetTextBoxValid(TextBox tb)
        {
            tb.ClearValue(TextBox.BorderBrushProperty);
            tb.ClearValue(TextBox.ToolTipProperty);
        }

        private void SetTextBoxInvalid(TextBox tb, string toolTipMessage)
        {
            tb.BorderBrush = Brushes.Red;
            tb.ToolTip = toolTipMessage;
        }

        // PauseTimer-Handler
        private void PauseTimerForegroundHexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string hex = PauseTimerForegroundHexTextBox.Text.Trim();
            if (TryParseHexColor(hex, out var color))
            {
                PauseTimerForegroundPreview.Fill = new SolidColorBrush(color);
                SetTextBoxValid(PauseTimerForegroundHexTextBox);

                try
                {
                    _windowManager.OverlayManager.SetPauseTimerForegroundColorHex(hex);
                    Logger.Instance.Log($"Customizer: PauseTimer ForegroundColor gesetzt: {hex}", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Fehler beim Setzen von PauseTimer ForegroundColor: {ex.Message}", LogLevel.Error);
                }
            }
            else
            {
                if (hex.Length == 7 || hex.Length == 9)
                {
                    SetTextBoxInvalid(PauseTimerForegroundHexTextBox, "Ungültiges Format, nutze #RRGGBB oder #AARRGGBB.");
                    PauseTimerForegroundPreview.Fill = Brushes.Transparent;
                }
                else
                {
                    PauseTimerForegroundPreview.Fill = Brushes.Transparent;
                    SetTextBoxValid(PauseTimerForegroundHexTextBox);
                }
            }
        }
        private void PauseTimerBackgroundHexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string hex = PauseTimerBackgroundHexTextBox.Text.Trim();
            if (TryParseHexColor(hex, out var color))
            {
                PauseTimerBackgroundPreview.Fill = new SolidColorBrush(color);
                SetTextBoxValid(PauseTimerBackgroundHexTextBox);

                try
                {
                    _windowManager.OverlayManager.SetPauseTimerBackgroundColorHex(hex);
                    Logger.Instance.Log($"Customizer: PauseTimer BackgroundColor gesetzt: {hex}", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Fehler beim Setzen von PauseTimer BackgroundColor: {ex.Message}", LogLevel.Error);
                }
            }
            else
            {
                if (hex.Length == 7 || hex.Length == 9)
                {
                    SetTextBoxInvalid(PauseTimerBackgroundHexTextBox, "Ungültiges Format, nutze #RRGGBB oder #AARRGGBB.");
                    PauseTimerBackgroundPreview.Fill = Brushes.Transparent;
                }
                else
                {
                    PauseTimerBackgroundPreview.Fill = Brushes.Transparent;
                    SetTextBoxValid(PauseTimerBackgroundHexTextBox);
                }
            }
        }
        private void ForegroundTransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double op = e.NewValue;
            ForegroundTransparencyValueText.Text = $"Opacity: {(int)(op * 100)}%";
            try
            {
                _windowManager.OverlayManager.SetPauseTimerForegroundOpacity(op);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Setzen von PauseTimer ForegroundOpacity: {ex.Message}", LogLevel.Error);
            }
        }
        private void BackgroundTransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double op = e.NewValue;
            BackgroundTransparencyValueText.Text = $"Opacity: {(int)(op * 100)}%";
            try
            {
                _windowManager.OverlayManager.SetPauseTimerBackgroundOpacity(op);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Setzen von PauseTimer BackgroundOpacity: {ex.Message}", LogLevel.Error);
            }
        }

        // AppTimer-Handler
        private void AppTimerForegroundHexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string hex = AppTimerForegroundHexTextBox.Text.Trim();
            if (TryParseHexColor(hex, out var color))
            {
                AppTimerForegroundPreview.Fill = new SolidColorBrush(color);
                SetTextBoxValid(AppTimerForegroundHexTextBox);

                try
                {
                    _windowManager.OverlayManager.SetAppTimerForegroundColorHex(hex);
                    Logger.Instance.Log($"Customizer: AppTimer ForegroundColor gesetzt: {hex}", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Fehler beim Setzen von AppTimer ForegroundColor: {ex.Message}", LogLevel.Error);
                }
            }
            else
            {
                if (hex.Length == 7 || hex.Length == 9)
                {
                    SetTextBoxInvalid(AppTimerForegroundHexTextBox, "Ungültiges Format, nutze #RRGGBB oder #AARRGGBB.");
                    AppTimerForegroundPreview.Fill = Brushes.Transparent;
                }
                else
                {
                    AppTimerForegroundPreview.Fill = Brushes.Transparent;
                    SetTextBoxValid(AppTimerForegroundHexTextBox);
                }
            }
        }
        private void AppTimerBackgroundHexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string hex = AppTimerBackgroundHexTextBox.Text.Trim();
            if (TryParseHexColor(hex, out var color))
            {
                AppTimerBackgroundPreview.Fill = new SolidColorBrush(color);
                SetTextBoxValid(AppTimerBackgroundHexTextBox);

                try
                {
                    _windowManager.OverlayManager.SetAppTimerBackgroundColorHex(hex);
                    Logger.Instance.Log($"Customizer: AppTimer BackgroundColor gesetzt: {hex}", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Fehler beim Setzen von AppTimer BackgroundColor: {ex.Message}", LogLevel.Error);
                }
            }
            else
            {
                if (hex.Length == 7 || hex.Length == 9)
                {
                    SetTextBoxInvalid(AppTimerBackgroundHexTextBox, "Ungültiges Format, nutze #RRGGBB oder #AARRGGBB.");
                    AppTimerBackgroundPreview.Fill = Brushes.Transparent;
                }
                else
                {
                    AppTimerBackgroundPreview.Fill = Brushes.Transparent;
                    SetTextBoxValid(AppTimerBackgroundHexTextBox);
                }
            }
        }
        private void AppTimerForegroundTransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double op = e.NewValue;
            AppTimerForegroundTransparencyValueText.Text = $"Opacity: {(int)(op * 100)}%";
            try
            {
                _windowManager.OverlayManager.SetAppTimerForegroundOpacity(op);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Setzen von AppTimer ForegroundOpacity: {ex.Message}", LogLevel.Error);
            }
        }
        private void AppTimerBackgroundTransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double op = e.NewValue;
            AppTimerBackgroundTransparencyValueText.Text = $"Opacity: {(int)(op * 100)}%";
            try
            {
                _windowManager.OverlayManager.SetAppTimerBackgroundOpacity(op);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Setzen von AppTimer BackgroundOpacity: {ex.Message}", LogLevel.Error);
            }
        }

        // Message-Handler
        private void MessageForegroundHexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string hex = MessageForegroundHexTextBox.Text.Trim();
            if (TryParseHexColor(hex, out var color))
            {
                MessageForegroundPreview.Fill = new SolidColorBrush(color);
                SetTextBoxValid(MessageForegroundHexTextBox);

                try
                {
                    _windowManager.OverlayManager.SetMessageForegroundColorHex(hex);
                    Logger.Instance.Log($"Customizer: Message ForegroundColor gesetzt: {hex}", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Fehler beim Setzen von Message ForegroundColor: {ex.Message}", LogLevel.Error);
                }
            }
            else
            {
                if (hex.Length == 7 || hex.Length == 9)
                {
                    SetTextBoxInvalid(MessageForegroundHexTextBox, "Ungültiges Format, nutze #RRGGBB oder #AARRGGBB.");
                    MessageForegroundPreview.Fill = Brushes.Transparent;
                }
                else
                {
                    MessageForegroundPreview.Fill = Brushes.Transparent;
                    SetTextBoxValid(MessageForegroundHexTextBox);
                }
            }
        }
        private void MessageBackgroundHexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string hex = MessageBackgroundHexTextBox.Text.Trim();
            if (TryParseHexColor(hex, out var color))
            {
                MessageBackgroundPreview.Fill = new SolidColorBrush(color);
                SetTextBoxValid(MessageBackgroundHexTextBox);

                try
                {
                    _windowManager.OverlayManager.SetMessageBackgroundColorHex(hex);
                    Logger.Instance.Log($"Customizer: Message BackgroundColor gesetzt: {hex}", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Fehler beim Setzen von Message BackgroundColor: {ex.Message}", LogLevel.Error);
                }
            }
            else
            {
                if (hex.Length == 7 || hex.Length == 9)
                {
                    SetTextBoxInvalid(MessageBackgroundHexTextBox, "Ungültiges Format, nutze #RRGGBB oder #AARRGGBB.");
                    MessageBackgroundPreview.Fill = Brushes.Transparent;
                }
                else
                {
                    MessageBackgroundPreview.Fill = Brushes.Transparent;
                    SetTextBoxValid(MessageBackgroundHexTextBox);
                }
            }
        }
        private void MessageForegroundTransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double op = e.NewValue;
            MessageForegroundTransparencyValueText.Text = $"Opacity: {(int)(op * 100)}%";
            try
            {
                _windowManager.OverlayManager.SetMessageForegroundOpacity(op);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Setzen von Message ForegroundOpacity: {ex.Message}", LogLevel.Error);
            }
        }
        private void MessageBackgroundTransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double op = e.NewValue;
            MessageBackgroundTransparencyValueText.Text = $"Opacity: {(int)(op * 100)}%";
            try
            {
                _windowManager.OverlayManager.SetMessageBackgroundOpacity(op);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Setzen von Message BackgroundOpacity: {ex.Message}", LogLevel.Error);
            }
        }


        public void Dispose()
        {
            Logger.Instance.Log("Disposing CustomizerWindow.", LogLevel.Debug);
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                this.Close();
                Logger.Instance.Log("CustomizerWindow disposed.", LogLevel.Info);
            }
            disposed = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Dispose();
        }
    }
}

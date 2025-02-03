using System.Windows;
using System.Windows.Media;

namespace ScreenZen
{
    /// <summary>
    /// Interaktionslogik für Overlay.xaml
    /// </summary>
    public partial class Overlay : Window
    {
        public Overlay()
        {
            InitializeComponent();

            // Fenster ohne Rahmen, ohne Hintergrund und immer im Vordergrund
            WindowStyle = WindowStyle.None;
            Background = Brushes.Transparent;
            Topmost = true; // Stellt sicher, dass das Fenster immer oben bleibt
            AllowsTransparency = true;
            ShowInTaskbar = false;
      
            // Optional: Fenstergröße und Position setzen (z.B. auf gesamten Bildschirm)
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
            Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
        }
    }
}

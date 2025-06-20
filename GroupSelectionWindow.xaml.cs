using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MessageBox = System.Windows.MessageBox;

namespace HecticEscape
{
    /// <summary>
    /// Interaktionslogik für GroupSelectionWindow.xaml
    /// </summary>
    public partial class GroupSelectionWindow : Window, IDisposable
    {
        private readonly WindowManager _windowManager;
        private readonly LanguageManager _languageManager;
        private bool disposed = false;
        private string processName;
        public GroupSelectionWindow(LanguageManager languageManager, WindowManager windowManager)
        { 
            InitializeComponent();
            WindowStyle = WindowStyle.None;
            Topmost = true;
            ShowInTaskbar = false;

            Logger.Instance.Log("GroupSelectionWindow initialisiert.", LogLevel.Info);
            _languageManager = languageManager ?? throw new ArgumentNullException(nameof(languageManager));
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            string? selectedGroup = GroupComboBox?.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGroup) || string.IsNullOrEmpty(processName))
            {
                Logger.Instance.Log($"Keine Gruppe oder Prozessname ausgewählt. {selectedGroup} {processName}", LogLevel.Warn);
                Close();
                return;
            }
            var group = _windowManager.GroupManager.GetGroupByName(selectedGroup);
            if (group == null)
            {
                Logger.Instance.Log($"Gruppe '{selectedGroup}' nicht gefunden.", LogLevel.Warn);
                MessageBox.Show("Bitte wähele eine Gruppe aus.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            _windowManager.AppManager.AddAppToGroup(group, processName);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public void SetProcessName(string name)
        {
            processName = name;
            NewProcessFoundTextBlock.Text = $"Neuer Prozess gefunden: {name}";

            var groupNames = _windowManager.GroupManager.GetAllGroupNames().ToList();
            GroupComboBox.ItemsSource = groupNames;

            if (groupNames.Count > 0)
            {
                GroupComboBox.SelectedIndex = 0;
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

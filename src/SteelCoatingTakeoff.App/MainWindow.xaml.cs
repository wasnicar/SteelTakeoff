using System.Windows;
using System.Windows.Controls;
using SteelCoatingTakeoff.App.ViewModels;

namespace SteelCoatingTakeoff.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        /// <summary>
        /// Push the tick into the row on the click itself. A template checkbox in a
        /// DataGrid otherwise defers its binding until the row loses focus, so the
        /// coating type — and everything that keys off it (totals, the WFT column's
        /// enabled state, the calculation breakdown) — would lag a click behind.
        /// The row setter is idempotent, so this never double-fires.
        /// </summary>
        private void IntumescentCheck_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox box && box.DataContext is TakeoffRowViewModel row)
                row.IsIntumescent = box.IsChecked == true;
        }

        /// <summary>Open the connection settings on the shared view-model (modal).</summary>
        private void OpenConnectionSettings_Click(object sender, RoutedEventArgs e)
        {
            new ConnectionSettingsWindow(DataContext, this).ShowDialog();
        }
    }
}

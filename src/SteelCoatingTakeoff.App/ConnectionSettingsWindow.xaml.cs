using System.Windows;
using System.Windows.Input;
using SteelCoatingTakeoff.App.ViewModels;

namespace SteelCoatingTakeoff.App
{
    /// <summary>
    /// Sage connection + assembly configuration, split out of the takeoff screen so the
    /// main window stays focused on takeoff and labor. Shares the main window's
    /// view-model, so Test connection / Save / assembly loading reuse the same commands.
    /// </summary>
    public partial class ConnectionSettingsWindow : Window
    {
        private readonly MainViewModel _vm;

        public ConnectionSettingsWindow(object viewModel, Window owner)
        {
            InitializeComponent();
            DataContext = viewModel;
            _vm = viewModel as MainViewModel;
            Owner = owner;

            // Populate the dropdowns as soon as the window opens.
            Loaded += (_, __) =>
            {
                if (_vm != null && _vm.AssemblyChoices.Count == 0 && _vm.LoadAssembliesCommand.CanExecute(null))
                    _vm.LoadAssembliesCommand.Execute(null);
            };
        }

        private string Browse(string title)
        {
            if (_vm == null) return null;

            Cursor = Cursors.Wait;
            _vm.EnsureAssembliesLoaded();
            Cursor = Cursors.Arrow;

            var browser = new AssemblyBrowserWindow(title, _vm.AllAssemblies, this);
            return browser.ShowDialog() == true ? browser.SelectedAssemblyName : null;
        }

        private void BrowseIntumescent_Click(object sender, RoutedEventArgs e)
        {
            var picked = Browse("Choose the intumescent assembly");
            if (picked != null && _vm != null) _vm.IntumescentAssembly = picked;
        }

        private void BrowseStandard_Click(object sender, RoutedEventArgs e)
        {
            var picked = Browse("Choose the standard steel assembly");
            if (picked != null && _vm != null) _vm.StandardAssembly = picked;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}

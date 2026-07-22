using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SteelCoatingTakeoff.App.ViewModels;

namespace SteelCoatingTakeoff.App
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Guards the two-way sync between the DataGrid's own selection and each row's
        /// IsSelected flag, so one updating the other doesn't bounce back.
        /// </summary>
        private bool _syncingSelection;

        private MainViewModel Vm => DataContext as MainViewModel;

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

        /// <summary>
        /// Mirror the grid's native selection (click, ctrl-click, shift-click) onto the
        /// rows, which is what the labor panel and the checkbox column read.
        /// </summary>
        private void TakeoffGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingSelection) return;

            _syncingSelection = true;
            try
            {
                foreach (var row in e.RemovedItems.OfType<TakeoffRowViewModel>()) row.IsSelected = false;
                foreach (var row in e.AddedItems.OfType<TakeoffRowViewModel>()) row.IsSelected = true;
            }
            finally { _syncingSelection = false; }

            SyncFocusedRow();
            Vm?.RaiseSelectionState();
        }

        /// <summary>
        /// Mirror the focused row onto the view-model. Done here rather than by binding
        /// SelectedItem, which would round-trip and collapse a multi-row selection.
        /// </summary>
        private void SyncFocusedRow()
        {
            if (Vm != null) Vm.SelectedRow = TakeoffGrid.SelectedItem as TakeoffRowViewModel;
        }

        /// <summary>
        /// Toggle one member in or out of the selection.
        ///
        /// The mouse-down is swallowed deliberately: left to the DataGrid, a plain click
        /// on the checkbox cell would clear the rest of the selection and select just
        /// this row, which is the opposite of what a tick box is for.
        /// </summary>
        private void SelectCheck_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is CheckBox box) || !(box.DataContext is TakeoffRowViewModel row)) return;

            SetRowSelected(row, !row.IsSelected);
            e.Handled = true;
        }

        /// <summary>
        /// Header tick box: select every member, or clear the selection.
        ///
        /// Mouse-down rather than Click, and the state is derived from the rows rather
        /// than read off the box: a DataGridColumnHeader captures mouse input for
        /// resize/reorder, so a checkbox inside one never reliably raises Click.
        /// </summary>
        private void SelectAll_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Vm == null) return;

            // Anything unselected -> select all; otherwise clear.
            var select = Vm.Rows.Any(r => !r.IsSelected);
            if (sender is CheckBox box) box.IsChecked = select;

            _syncingSelection = true;
            try
            {
                if (select) TakeoffGrid.SelectAll();
                else TakeoffGrid.UnselectAll();
                foreach (var row in Vm.Rows) row.IsSelected = select;
            }
            finally { _syncingSelection = false; }

            SyncFocusedRow();
            Vm.RaiseSelectionState();
            e.Handled = true;
        }

        /// <summary>
        /// Keep the row flag and the grid's own selection in step, so a checkbox tick
        /// also paints the row blue and a ctrl-click also ticks the box.
        /// </summary>
        private void SetRowSelected(TakeoffRowViewModel row, bool selected)
        {
            _syncingSelection = true;
            try
            {
                if (selected)
                {
                    if (!TakeoffGrid.SelectedItems.Contains(row)) TakeoffGrid.SelectedItems.Add(row);
                }
                else
                {
                    TakeoffGrid.SelectedItems.Remove(row);
                }
                row.IsSelected = selected;
            }
            finally { _syncingSelection = false; }

            SyncFocusedRow();
            Vm?.RaiseSelectionState();
        }

        /// <summary>Open the connection settings on the shared view-model (modal).</summary>
        private void OpenConnectionSettings_Click(object sender, RoutedEventArgs e)
        {
            new ConnectionSettingsWindow(DataContext, this).ShowDialog();
        }
    }
}

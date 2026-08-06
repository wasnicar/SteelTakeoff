using System;
using System.Windows;
using Microsoft.Win32;
using SteelCoatingTakeoff.App.ViewModels;
using SteelCoatingTakeoff.Core.Projects;

namespace SteelCoatingTakeoff.App
{
    /// <summary>
    /// Project browser: save the current takeoff, reopen a previous one (e.g. to enter
    /// supplier-provided WFT before sending to Sage), rename, delete, and choose the
    /// projects folder. Talks straight to the shared <see cref="MainViewModel"/>.
    /// </summary>
    public partial class ProjectsWindow : Window
    {
        private readonly MainViewModel _vm;

        public ProjectsWindow(object viewModel, Window owner)
        {
            InitializeComponent();
            DataContext = viewModel;
            _vm = viewModel as MainViewModel;
            Owner = owner;

            Loaded += (_, __) =>
            {
                if (_vm == null) return;
                FolderBox.Text = _vm.Settings.ProjectsDirectory ?? "";
                if (!_vm.CurrentProjectName.Equals("Untitled"))
                    NameBox.Text = _vm.CurrentProjectName;
                Refresh();
            };
        }

        private void Refresh()
        {
            if (_vm == null) return;
            var projects = _vm.ListProjects();
            ProjectList.ItemsSource = projects;
            EmptyHint.Visibility = projects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EffectiveFolderText.Text = "Reading from: " + _vm.EffectiveProjectsDir;
        }

        private ProjectSummary Selected => ProjectList.SelectedItem as ProjectSummary;

        private void UseFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            _vm.Settings.ProjectsDirectory = (FolderBox.Text ?? "").Trim();
            SettingsStore.Save(_vm.Settings);
            Refresh();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null || Selected == null) return;
            if (_vm.IsDirty &&
                MessageBox.Show(this, "Discard unsaved changes to the current takeoff?",
                    "Open project", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
                return;

            if (_vm.OpenProject(Selected.Path, out var error)) Close();
            else MessageBox.Show(this, "Could not open the project:\n\n" + error, "Open project",
                                 MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            if (_vm.SaveProjectAs(NameBox.Text, out var error)) Refresh();
            else MessageBox.Show(this, error, "Save takeoff", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null || Selected == null) return;
            if (_vm.RenameProject(Selected.Path, NameBox.Text, out var error)) Refresh();
            else MessageBox.Show(this, error, "Rename", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null || Selected == null) return;
            if (MessageBox.Show(this, $"Delete '{Selected.Name}'? This cannot be undone.",
                    "Delete project", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
                return;
            _vm.DeleteProject(Selected.Path);
            Refresh();
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            if (_vm.IsDirty &&
                MessageBox.Show(this, "Discard unsaved changes to the current takeoff?",
                    "New takeoff", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
                return;
            _vm.NewProject();
            NameBox.Text = "";
            Close();
        }

        private void Backup_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            var dlg = new SaveFileDialog
            {
                FileName = "SteelCoatingTakeoff-Projects-" + DateTime.Now.ToString("yyyyMMdd") + ".zip",
                Filter = "Backup zip (*.zip)|*.zip",
                DefaultExt = ".zip"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var n = _vm.BackupProjects(dlg.FileName);
                MessageBox.Show(this, $"Backed up {n} project(s) to:\n\n{dlg.FileName}",
                    "Back up projects", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Backup failed:\n\n" + ex.Message, "Back up projects",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            var dlg = new OpenFileDialog { Filter = "Backup zip (*.zip)|*.zip", CheckFileExists = true };
            if (dlg.ShowDialog() != true) return;

            int inBackup;
            try { inBackup = ProjectBackup.Count(dlg.FileName); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not read the backup:\n\n" + ex.Message, "Restore projects",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show(this,
                    $"Restore {inBackup} project(s) into your projects folder?\n\n" +
                    "Any project with the same name will be replaced.",
                    "Restore projects", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
                return;

            try
            {
                var n = _vm.RestoreProjects(dlg.FileName);
                Refresh();
                MessageBox.Show(this, $"Restored {n} project(s).", "Restore projects",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Restore failed:\n\n" + ex.Message, "Restore projects",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}

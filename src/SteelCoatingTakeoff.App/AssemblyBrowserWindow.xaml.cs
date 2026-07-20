using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SteelCoatingTakeoff.Core.Sage;

namespace SteelCoatingTakeoff.App
{
    /// <summary>
    /// One node in the assembly picker tree — a group (section) or a selectable assembly.
    /// </summary>
    public sealed class AssemblyTreeNode
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsGroup { get; set; }
        public bool IsExpanded { get; set; }
        public ObservableCollection<AssemblyTreeNode> Children { get; } = new ObservableCollection<AssemblyTreeNode>();

        public string Display =>
            string.IsNullOrWhiteSpace(Description) ? Name : $"{Name}  —  {Description}";
    }

    /// <summary>
    /// Picks an assembly from the standard database, shown in its grouped hierarchy the
    /// way Sage displays it. <see cref="SelectedAssemblyName"/> holds the chosen name
    /// after the dialog returns true.
    /// </summary>
    public partial class AssemblyBrowserWindow : Window
    {
        private readonly List<SageAssemblyInfo> _all;

        public string SelectedAssemblyName { get; private set; }

        public AssemblyBrowserWindow(string title, IEnumerable<SageAssemblyInfo> assemblies, Window owner)
        {
            InitializeComponent();
            Owner = owner;
            Title = title;
            _all = (assemblies ?? Enumerable.Empty<SageAssemblyInfo>()).ToList();
            Tree.ItemsSource = BuildTree(_all, null);
        }

        /// <summary>
        /// Build the grouped tree from the flat, display-ordered list: each group header
        /// starts a section; the assemblies that follow it (until the next group) are its
        /// children. A <paramref name="filter"/> keeps only matching assemblies (and the
        /// groups that contain them, or a group whose own name/description matches).
        /// </summary>
        private static ObservableCollection<AssemblyTreeNode> BuildTree(List<SageAssemblyInfo> all, string filter)
        {
            var roots = new ObservableCollection<AssemblyTreeNode>();
            var hasFilter = !string.IsNullOrWhiteSpace(filter);
            var f = filter?.Trim();

            AssemblyTreeNode currentGroup = null;
            AssemblyTreeNode ungrouped = null;
            bool currentGroupMatches = false;

            bool Matches(SageAssemblyInfo a) =>
                (a.Name ?? "").IndexOf(f, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                (a.Description ?? "").IndexOf(f, System.StringComparison.OrdinalIgnoreCase) >= 0;

            foreach (var a in all)
            {
                if (a.IsGroup)
                {
                    currentGroup = new AssemblyTreeNode
                    {
                        Name = a.Name, Description = a.Description, IsGroup = true, IsExpanded = hasFilter
                    };
                    currentGroupMatches = hasFilter && Matches(a);
                    // Add the group lazily — only once it has a visible child (or itself matches).
                    if (!hasFilter || currentGroupMatches) roots.Add(currentGroup);
                    continue;
                }

                if (hasFilter && !currentGroupMatches && !Matches(a)) continue;

                var node = new AssemblyTreeNode { Name = a.Name, Description = a.Description, IsGroup = false };

                if (currentGroup != null)
                {
                    if (hasFilter && !currentGroupMatches && !roots.Contains(currentGroup))
                        roots.Add(currentGroup);
                    currentGroup.Children.Add(node);
                }
                else
                {
                    ungrouped = ungrouped ?? new AssemblyTreeNode { Name = "(ungrouped)", IsGroup = true, IsExpanded = true };
                    if (!roots.Contains(ungrouped)) roots.Insert(0, ungrouped);
                    ungrouped.Children.Add(node);
                }
            }

            return roots;
        }

        private void Filter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Tree != null) Tree.ItemsSource = BuildTree(_all, FilterBox.Text);
        }

        private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var node = e.NewValue as AssemblyTreeNode;
            var selectable = node != null && !node.IsGroup;
            ChooseButton.IsEnabled = selectable;
            SelectedText.Text = selectable ? node.Name : "";
        }

        private void Tree_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Tree.SelectedItem is AssemblyTreeNode node && !node.IsGroup) Choose(node);
        }

        private void Choose_Click(object sender, RoutedEventArgs e)
        {
            if (Tree.SelectedItem is AssemblyTreeNode node && !node.IsGroup) Choose(node);
        }

        private void Choose(AssemblyTreeNode node)
        {
            SelectedAssemblyName = node.Name;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}

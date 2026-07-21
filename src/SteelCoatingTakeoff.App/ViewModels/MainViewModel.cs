using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SteelCoatingTakeoff.App.Sage;
using SteelCoatingTakeoff.Core;
using SteelCoatingTakeoff.Core.Model;
using SteelCoatingTakeoff.Core.Sage;

namespace SteelCoatingTakeoff.App.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private readonly ShapeDatabase _db;

        public IReadOnlyList<ShapeFamily> Families => _db.Families;
        public ObservableCollection<TakeoffRowViewModel> Rows { get; } = new ObservableCollection<TakeoffRowViewModel>();
        public ObservableCollection<string> Activity { get; } = new ObservableCollection<string>();

        public SageSettings Settings { get; }

        private TakeoffRowViewModel _selectedRow;
        public TakeoffRowViewModel SelectedRow { get => _selectedRow; set => Set(ref _selectedRow, value); }

        // ---- assembly picker (Connection settings) ----
        /// <summary>Full assembly list in Sage display order (groups + members) for the tree.</summary>
        public List<SageAssemblyInfo> AllAssemblies { get; private set; } = new List<SageAssemblyInfo>();

        /// <summary>Selectable (non-group) assemblies for the dropdowns.</summary>
        public ObservableCollection<SageAssemblyInfo> AssemblyChoices { get; } = new ObservableCollection<SageAssemblyInfo>();

        private string _assemblyLoadStatus = "Assemblies not loaded.";
        public string AssemblyLoadStatus { get => _assemblyLoadStatus; private set => Set(ref _assemblyLoadStatus, value); }

        // ---- connection discovery (fills the Connection settings dropdowns) ----
        public ObservableCollection<string> SqlInstances { get; } = new ObservableCollection<string>();
        public ObservableCollection<SageDatabaseInfo> EstimateDatabases { get; } = new ObservableCollection<SageDatabaseInfo>();
        public ObservableCollection<SageDatabaseInfo> StandardDatabases { get; } = new ObservableCollection<SageDatabaseInfo>();
        public ObservableCollection<string> Estimates { get; } = new ObservableCollection<string>();

        private string _discoveryStatus = "Click Detect to find SQL servers.";
        public string DiscoveryStatus { get => _discoveryStatus; private set => Set(ref _discoveryStatus, value); }

        public ICommand DetectCommand { get; }

        /// <summary>SQL instance. Changing it re-reads the databases on that server.</summary>
        public string SqlServer
        {
            get => Settings.SqlServer;
            set
            {
                if (Settings.SqlServer == value) return;
                Settings.SqlServer = value;
                Raise(nameof(SqlServer));
                LoadDatabases();
            }
        }

        /// <summary>Estimating database. Changing it re-reads the estimates inside it.</summary>
        public string Database
        {
            get => Settings.Database;
            set
            {
                if (Settings.Database == value) return;
                Settings.Database = value;
                Raise(nameof(Database));
                LoadEstimates();
            }
        }

        public string StandardDatabase
        {
            get => Settings.StandardDatabase;
            set
            {
                if (Settings.StandardDatabase == value) return;
                Settings.StandardDatabase = value;
                Raise(nameof(StandardDatabase));
                // A different standard DB means a different assembly list.
                AllAssemblies = new List<SageAssemblyInfo>();
                AssemblyChoices.Clear();
                AssemblyLoadStatus = "Assemblies not loaded.";
            }
        }

        public string EstimateName
        {
            get => Settings.EstimateName;
            set { if (Settings.EstimateName != value) { Settings.EstimateName = value; Raise(nameof(EstimateName)); } }
        }

        /// <summary>Assembly names bound to the dropdowns; proxy so Browse updates reflect.</summary>
        public string IntumescentAssembly
        {
            get => Settings.IntumescentAssembly;
            set { if (Settings.IntumescentAssembly != value) { Settings.IntumescentAssembly = value; Raise(nameof(IntumescentAssembly)); RepriceLabor(); } }
        }
        public string StandardAssembly
        {
            get => Settings.StandardAssembly;
            set { if (Settings.StandardAssembly != value) { Settings.StandardAssembly = value; Raise(nameof(StandardAssembly)); RepriceLabor(); } }
        }

        private bool _busy;
        public bool IsBusy { get => _busy; private set { if (Set(ref _busy, value)) Raise(nameof(IsNotBusy)); } }
        public bool IsNotBusy => !_busy;

        private bool _showCalculation;
        /// <summary>Reveals the derivation panel under the selected row.</summary>
        public bool ShowCalculation { get => _showCalculation; set => Set(ref _showCalculation, value); }

        // ---- global labor inputs (right panel) ----
        // Proxied through the ViewModel so a change re-prices every line live.
        public double WageRate
        {
            get => Settings.WageRate;
            set { if (Settings.WageRate != value) { Settings.WageRate = value; Raise(nameof(WageRate)); RepriceLabor(); } }
        }

        public double Productivity
        {
            get => Settings.Productivity;
            set { if (Settings.Productivity != value) { Settings.Productivity = value; Raise(nameof(Productivity)); RepriceLabor(); } }
        }

        private void RepriceLabor()
        {
            foreach (var row in Rows) row.RefreshCalculation();
            RecomputeTotals();
        }

        // ---- totals ----
        public double TotalArea => Rows.Sum(r => r.AreaSquareFeet);
        public double IntumescentArea => Rows.Where(r => r.IsIntumescent).Sum(r => r.AreaSquareFeet);
        public double StandardArea => Rows.Where(r => !r.IsIntumescent).Sum(r => r.AreaSquareFeet);
        public double TotalLinearFeet => Rows.Sum(r => r.LinearFeet);
        public double TotalLabor => Rows.Sum(r => r.LaborAmount);
        public int LineCount => Rows.Count;

        // ---- commands ----
        public ICommand AddRowCommand { get; }
        public ICommand RemoveRowCommand { get; }
        public ICommand DuplicateRowCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand SendAllCommand { get; }
        public ICommand SendSelectedCommand { get; }
        public ICommand TestConnectionCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand LoadAssembliesCommand { get; }

        public MainViewModel()
        {
            _db = ShapeDatabase.Load();
            Settings = SettingsStore.Load();

            AddRowCommand = new RelayCommand(() => AddRow());
            RemoveRowCommand = new RelayCommand(RemoveRow, () => SelectedRow != null);
            DuplicateRowCommand = new RelayCommand(DuplicateRow, () => SelectedRow != null);
            ClearCommand = new RelayCommand(ClearRows, () => Rows.Count > 0);
            SendAllCommand = new RelayCommand(async _ => await SendAsync(Rows.ToList()), _ => IsNotBusy && Rows.Count > 0);
            SendSelectedCommand = new RelayCommand(async _ => await SendAsync(SelectedRow == null ? new List<TakeoffRowViewModel>() : new List<TakeoffRowViewModel> { SelectedRow }),
                                                   _ => IsNotBusy && SelectedRow != null);
            TestConnectionCommand = new RelayCommand(async _ => await TestConnectionAsync(), _ => IsNotBusy);
            ExportCsvCommand = new RelayCommand(ExportCsv, () => Rows.Count > 0);
            SaveSettingsCommand = new RelayCommand(() =>
            {
                SettingsStore.Save(Settings);
                // Routing and area-delivery are part of the shown derivation.
                foreach (var row in Rows) row.RefreshCalculation();
                Log("Settings saved.");
            });
            LoadAssembliesCommand = new RelayCommand(async _ => await LoadAssembliesAsync(), _ => IsNotBusy);
            DetectCommand = new RelayCommand(async _ => await DetectAsync(), _ => IsNotBusy);

            // Seed one row so the grid isn't empty on launch.
            AddRow(_db.GetFamily("W"));
        }

        /// <summary>
        /// Reads are safe, so assembly listing always uses the real connector regardless
        /// of Dry run; if the SDK isn't wired it falls back to the mock's sample list.
        /// </summary>
        private static SageAssemblyListResult FetchAssemblies(SageSettings settings)
        {
            using (var conn = new SageEstimatingConnector())
            {
                var r = conn.ListAssemblies(settings);
                if (r.Success) return r;
                using (var mock = new MockSageConnector())
                    return mock.ListAssemblies(settings);
            }
        }

        private void ApplyAssemblies(SageAssemblyListResult result)
        {
            AllAssemblies = result.Assemblies;
            AssemblyChoices.Clear();
            foreach (var a in result.Assemblies.Where(a => !a.IsGroup))
                AssemblyChoices.Add(a);
            AssemblyLoadStatus = result.Success
                ? $"{AssemblyChoices.Count} assemblies loaded."
                : result.Message;
            // Re-assert the current picks so the dropdowns show them now the list exists.
            Raise(nameof(IntumescentAssembly));
            Raise(nameof(StandardAssembly));
        }

        /// <summary>
        /// Discover SQL instances (local + network), then cascade into the databases on
        /// the selected server and the estimates inside the selected estimating DB.
        /// Reads only — safe regardless of Dry run.
        /// </summary>
        private async Task DetectAsync()
        {
            IsBusy = true;
            DiscoveryStatus = "Searching for SQL Server instances…";
            try
            {
                var result = await Task.Run(() =>
                {
                    using (var conn = new SageEstimatingConnector()) return conn.ListSqlInstances();
                });

                var previous = Settings.SqlServer;
                SqlInstances.Clear();
                foreach (var i in result.Instances) SqlInstances.Add(i);
                // Keep a configured server selectable even if discovery missed it.
                if (!string.IsNullOrWhiteSpace(previous) &&
                    !SqlInstances.Any(i => string.Equals(i, previous, StringComparison.OrdinalIgnoreCase)))
                {
                    SqlInstances.Insert(0, previous);
                }
                DiscoveryStatus = result.Message;
                Raise(nameof(SqlServer));

                if (!string.IsNullOrWhiteSpace(Settings.SqlServer)) LoadDatabases();
            }
            catch (Exception ex) { DiscoveryStatus = "Detect failed: " + ex.Message; }
            finally { IsBusy = false; }
        }

        /// <summary>Read the Sage databases on the selected server into the two dropdowns.</summary>
        private void LoadDatabases()
        {
            try
            {
                SageDatabaseListResult result;
                using (var conn = new SageEstimatingConnector()) result = conn.ListDatabases(Settings);

                EstimateDatabases.Clear();
                StandardDatabases.Clear();
                foreach (var db in result.Databases)
                {
                    if (db.Kind == SageDatabaseKind.Estimate) EstimateDatabases.Add(db);
                    else if (db.Kind == SageDatabaseKind.Standard) StandardDatabases.Add(db);
                }
                DiscoveryStatus = result.Message;
                Raise(nameof(Database));
                Raise(nameof(StandardDatabase));

                if (!string.IsNullOrWhiteSpace(Settings.Database)) LoadEstimates();
            }
            catch (Exception ex) { DiscoveryStatus = "Database lookup failed: " + ex.Message; }
        }

        /// <summary>Read the estimate names inside the selected estimating database.</summary>
        private void LoadEstimates()
        {
            try
            {
                SageEstimateListResult result;
                using (var conn = new SageEstimatingConnector()) result = conn.ListEstimates(Settings);

                Estimates.Clear();
                foreach (var e in result.Estimates) Estimates.Add(e);
                DiscoveryStatus = result.Message;
                Raise(nameof(EstimateName));
            }
            catch (Exception ex) { DiscoveryStatus = "Estimate lookup failed: " + ex.Message; }
        }

        /// <summary>Non-blocking load, for the "Load from Sage" button.</summary>
        private async Task LoadAssembliesAsync()
        {
            IsBusy = true;
            AssemblyLoadStatus = "Loading assemblies…";
            try { ApplyAssemblies(await Task.Run(() => FetchAssemblies(Settings))); }
            catch (Exception ex) { AssemblyLoadStatus = "Load failed: " + ex.Message; }
            finally { IsBusy = false; }
        }

        /// <summary>
        /// Synchronous load used by the Browse picker so the tree is never empty. The
        /// standard-DB query is quick; blocking briefly here is acceptable.
        /// </summary>
        public void EnsureAssembliesLoaded()
        {
            if (AllAssemblies.Count > 0) return;
            try { ApplyAssemblies(FetchAssemblies(Settings)); }
            catch (Exception ex) { AssemblyLoadStatus = "Load failed: " + ex.Message; }
        }

        // ---------- row management ----------
        public TakeoffRowViewModel AddRow(ShapeFamily family = null)
        {
            var row = new TakeoffRowViewModel(Families, family ?? Families.FirstOrDefault(), Settings);
            row.Changed += (_, __) => RecomputeTotals();
            Rows.Add(row);
            SelectedRow = row;
            RecomputeTotals();
            return row;
        }

        private void RemoveRow()
        {
            if (SelectedRow == null) return;
            Rows.Remove(SelectedRow);
            SelectedRow = Rows.LastOrDefault();
            RecomputeTotals();
        }

        private void DuplicateRow()
        {
            var src = SelectedRow;
            if (src == null) return;
            var row = AddRow(src.SelectedFamily);
            row.SelectedShape = src.SelectedShape;
            row.PlateWidthInches = src.PlateWidthInches;
            row.LinearFeet = src.LinearFeet;
            row.IsIntumescent = src.IsIntumescent;
            row.WftMils = src.WftMils;
            row.Coats = src.Coats;
        }

        private void ClearRows()
        {
            Rows.Clear();
            RecomputeTotals();
        }

        private void RecomputeTotals()
        {
            Raise(nameof(TotalArea));
            Raise(nameof(IntumescentArea));
            Raise(nameof(StandardArea));
            Raise(nameof(TotalLinearFeet));
            Raise(nameof(TotalLabor));
            Raise(nameof(LineCount));
        }

        // ---------- Sage ----------
        private ISageConnector CreateConnector()
        {
            return Settings.DryRun
                ? (ISageConnector)new MockSageConnector(Log)
                : new SageEstimatingConnector();
        }

        private async Task TestConnectionAsync()
        {
            IsBusy = true;
            try
            {
                await Task.Run(() =>
                {
                    using (var conn = CreateConnector())
                    {
                        var r = conn.Connect(Settings);
                        Log(r.Success
                            ? $"Connected to {r.EstimateName}. {r.Message}"
                            : $"Connect failed: {r.Message}");
                    }
                });
            }
            catch (Exception ex) { Log("Error: " + ex.Message); }
            finally { IsBusy = false; }
        }

        private async Task SendAsync(List<TakeoffRowViewModel> rows)
        {
            var lines = rows.Where(r => r.SelectedShape != null && r.LinearFeet > 0)
                            .Select(r => r.ToLine()).ToList();
            if (lines.Count == 0) { Log("Nothing to send (need a shape and linear feet > 0)."); return; }

            var requests = TakeoffRequestBuilder.BuildAll(lines, Settings);

            IsBusy = true;
            try
            {
                var result = await Task.Run(() =>
                {
                    using (var conn = CreateConnector())
                    {
                        var c = conn.Connect(Settings);
                        if (!c.Success) return SageTakeoffResult.Fail(c.Message);
                        return conn.TakeoffBatch(requests);
                    }
                });

                foreach (var l in result.Log) Log(l);
                Log(result.Success ? "✓ " + result.Message : "✗ " + result.Message);
            }
            catch (Exception ex) { Log("Error: " + ex.Message); }
            finally { IsBusy = false; }
        }

        // ---------- CSV ----------
        private void ExportCsv()
        {
            var dlg = new SaveFileDialog
            {
                FileName = "coating-takeoff.csv",
                Filter = "CSV file (*.csv)|*.csv"
            };
            if (dlg.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("Shape,Size,Coating,SF/LF,LinearFeet,Area_SF,Assembly");
            foreach (var r in Rows)
            {
                var asm = Settings.AssemblyFor(r.Coating);
                sb.AppendLine(string.Join(",",
                    Csv(r.SelectedFamily?.Label),
                    Csv(r.SelectedShape?.Display),
                    Csv(r.CoatingLabel),
                    r.SfPerFoot.ToString("0.####", CultureInfo.InvariantCulture),
                    r.LinearFeet.ToString("0.##", CultureInfo.InvariantCulture),
                    r.AreaSquareFeet.ToString("0.##", CultureInfo.InvariantCulture),
                    Csv(asm)));
            }
            sb.AppendLine();
            sb.AppendLine($"Total area SF,{TotalArea:0.##}");
            sb.AppendLine($"Intumescent area SF,{IntumescentArea:0.##}");
            sb.AppendLine($"Standard area SF,{StandardArea:0.##}");
            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            Log("Exported " + dlg.FileName);
        }

        private static string Csv(string s)
        {
            s = s ?? "";
            return s.Contains(",") || s.Contains("\"")
                ? "\"" + s.Replace("\"", "\"\"") + "\""
                : s;
        }

        private void Log(string message)
        {
            var line = DateTime.Now.ToString("HH:mm:ss") + "  " + message;
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                Application.Current.Dispatcher.Invoke(() => Activity.Insert(0, line));
            else
                Activity.Insert(0, line);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using SteelCoatingTakeoff.Core.Sage;

#if SAGE_SDK
using Sage.Estimating;
using Sage.Estimating.Data;
using Sage.Estimating.Takeoff;
#endif

namespace SteelCoatingTakeoff.App.Sage
{
    /// <summary>
    /// The ONE place that touches the Sage Estimating SDK.
    ///
    /// The body is compiled only when the <c>SAGE_SDK</c> constant is defined and the
    /// Sage Estimating SDK assembly is referenced (see the .csproj). Without that symbol
    /// this class is an explicit stub, so the solution always builds and the app runs
    /// against <see cref="MockSageConnector"/>.
    ///
    /// Written against Sage Estimating SDK 25.2 (Sage.Estimating.Sdk 25.2.0.0), whose
    /// shape was confirmed against the shipped samples and verified against a live
    /// estimate. Two facts drive the design and are worth keeping in mind:
    ///
    ///   * Assemblies live in the STANDARD database; items are written to the ESTIMATE
    ///     database. A takeoff therefore needs a connection to both.
    ///   * For an SF-unit assembly whose Calculation is empty, the coating area is the
    ///     assembly's takeoff QUANTITY (<see cref="TakeoffSession.QuantityMultiplier"/>),
    ///     not a variable. Verified: 3000.310.01 at QuantityMultiplier=1068 wrote all
    ///     five of its UseFactor items at 1068 sf. Assemblies that compute their own
    ///     quantity from a formula/table instead take the area through a named variable
    ///     (<see cref="SageSettings.AreaVariableName"/>).
    /// </summary>
    public sealed class SageEstimatingConnector : ISageConnector
    {
        public bool IsConnected { get; private set; }

#if SAGE_SDK
        private SageSettings _settings;
        private EstimateConnectionInfo _estimate;
        private StandardDBConnectionInfo _standardDB;

        /// <summary>Assemblies are re-used across a batch; reading one is expensive.</summary>
        private readonly Dictionary<string, StandardDBAssemblyEntity> _assemblies =
            new Dictionary<string, StandardDBAssemblyEntity>(StringComparer.OrdinalIgnoreCase);

        public SageConnectResult Connect(SageSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            try
            {
                var instance = ResolveInstance(settings);
                new InstanceService(instance).ReadInstanceInfo().VerifySupported();

                var dbService = new DBService(instance);

                var estimateDB = string.IsNullOrWhiteSpace(settings.Database)
                    ? EstimateDBService.ReadActiveEstimateDB()
                    : new EstimateDBConnectionInfo(instance, settings.Database);
                if (estimateDB == null)
                    return SageConnectResult.Fail(
                        "No estimates database configured. Set 'Estimating database' (e.g. Estimates).");
                Verify(dbService, estimateDB.Name, typeof(EstimateDBConnectionInfo));

                if (string.IsNullOrWhiteSpace(settings.StandardDatabase))
                    return SageConnectResult.Fail(
                        "No standard database configured. Assemblies are read from the standard " +
                        "database, so 'Standard database' is required.");
                _standardDB = new StandardDBConnectionInfo(instance, settings.StandardDatabase);
                Verify(dbService, _standardDB.Name, typeof(StandardDBConnectionInfo));

                _estimate = ResolveEstimate(estimateDB, settings.EstimateName);
                _assemblies.Clear();
                IsConnected = true;

                return SageConnectResult.Ok(_estimate.Name,
                    $"Estimate '{_estimate.Name}' on {instance.Name}/{estimateDB.Name}; " +
                    $"assemblies from {_standardDB.Name}.");
            }
            catch (Exception ex)
            {
                IsConnected = false;
                return SageConnectResult.Fail("Sage connect failed: " + Describe(ex));
            }
        }

        private static InstanceConnectionInfo ResolveInstance(SageSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.SqlServer))
                return new InstanceConnectionInfo(settings.SqlServer) { IntegratedSecurity = true };

            var active = InstanceService.ReadActiveInstance()
                ?? InstanceService.ReadLocallyInstalledInstancesFromRegistry().FirstOrDefault();
            if (active == null)
                throw new InvalidOperationException(
                    "No SQL Server instance configured. Set 'SQL Server' (e.g. MYPC\\SAGE_EST25).");
            return active;
        }

        private static void Verify(DBService dbService, string databaseName, Type expected)
        {
            var info = dbService.ReadDBInfo(databaseName);
            if (info == null)
                throw new InvalidOperationException($"Database '{databaseName}' was not found on the instance.");

            // Reports the version mismatch in the SDK's own words, which is far more
            // useful than "not supported" when an estimate DB has been upgraded past
            // the SDK (e.g. a 26.x database against the 25.2 SDK).
            info.VerifySupported(expected);
        }

        private static EstimateConnectionInfo ResolveEstimate(EstimateDBConnectionInfo estimateDB, string estimateName)
        {
            var service = new EstimateDBService(estimateDB);

            if (string.IsNullOrWhiteSpace(estimateName))
                throw new InvalidOperationException(
                    "No estimate name set. This SDK opens an estimate by name — type the estimate " +
                    "to take off into.");

            var matches = service.ReadEstimatesInfo(estimateName)
                .Where(e => !e.Invalid)
                .ToList();

            if (matches.Count == 0)
                throw new InvalidOperationException(
                    $"Estimate '{estimateName}' was not found in '{estimateDB.Name}'.");
            if (matches.Count > 1)
                throw new InvalidOperationException(
                    $"'{estimateName}' matches {matches.Count} estimates in '{estimateDB.Name}'. " +
                    "Use the exact estimate name.");

            var info = matches[0];
            if (!info.EditAllowed)
                throw new InvalidOperationException(
                    $"You do not have edit rights on estimate '{info.Name}'.");

            return info.ConnectionInfo;
        }

        public SageAssemblyListResult ListAssemblies(SageSettings settings)
        {
            if (settings == null) return SageAssemblyListResult.Fail("No settings.");
            if (string.IsNullOrWhiteSpace(settings.StandardDatabase))
                return SageAssemblyListResult.Fail("No standard database configured.");
            try
            {
                var instance = ResolveInstance(settings);
                var standardDB = new StandardDBConnectionInfo(instance, settings.StandardDatabase);
                var service = new StandardDBAssemblyService(standardDB);

                // ReadAssemblies returns display order: each group header precedes the
                // assemblies it contains, so the UI can rebuild the tree from this list.
                var result = SageAssemblyListResult.Ok();
                foreach (var a in service.ReadAssemblies())
                {
                    result.Assemblies.Add(new SageAssemblyInfo
                    {
                        Name = (a.Name ?? string.Empty).Trim(),
                        Description = a.Description,
                        IsGroup = a.IsGroup
                    });
                }
                result.Message = $"{result.Assemblies.Count} assemblies from {settings.StandardDatabase}.";
                return result;
            }
            catch (Exception ex)
            {
                return SageAssemblyListResult.Fail("Could not list assemblies: " + Describe(ex));
            }
        }

        // ---- Discovery: populate the connection dropdowns -------------------

        public SageInstanceListResult ListSqlInstances()
        {
            var result = SageInstanceListResult.Ok();
            var found = new List<string>();

            // Locally installed instances come from the registry — fast and reliable.
            try
            {
                foreach (var i in InstanceService.ReadLocallyInstalledInstancesFromRegistry())
                    if (!string.IsNullOrWhiteSpace(i?.Name)) found.Add(i.Name.Trim());
            }
            catch (Exception ex)
            {
                result.Message = "Local instance lookup failed: " + Describe(ex);
            }

            // Network browse (SQL Browser broadcast) can be slow or blocked; it is a
            // bonus on top of the local list, never a reason to fail.
            try
            {
                foreach (var i in InstanceService.ReadBrowsableInstances())
                    if (!string.IsNullOrWhiteSpace(i?.Name)) found.Add(i.Name.Trim());
            }
            catch
            {
                // Broadcast blocked/unavailable — keep whatever we already have.
            }

            // Whatever is configured should still be offered even if discovery missed it.
            foreach (var name in found
                         .Where(n => !string.IsNullOrWhiteSpace(n))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                result.Instances.Add(name);
            }

            if (result.Instances.Count == 0 && string.IsNullOrEmpty(result.Message))
                result.Message = "No SQL Server instances found on this machine or network.";
            else if (string.IsNullOrEmpty(result.Message))
                result.Message = $"{result.Instances.Count} SQL instance(s) found.";

            return result;
        }

        public SageDatabaseListResult ListDatabases(SageSettings settings)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.SqlServer))
                return SageDatabaseListResult.Fail("Pick a SQL Server first.");
            try
            {
                var instance = ResolveInstance(settings);
                var service = new DBService(instance);
                var result = SageDatabaseListResult.Ok();

                foreach (var name in service.ReadDBNames())
                {
                    DBInfo info;
                    try { info = service.ReadDBInfo(name); }
                    catch { continue; }                      // not readable — skip quietly
                    if (info?.ConnectionInfo == null) continue;   // not a Sage database

                    var kind = SageDatabaseKind.Unknown;
                    if (info.ConnectionInfo is EstimateDBConnectionInfo) kind = SageDatabaseKind.Estimate;
                    else if (info.ConnectionInfo is StandardDBConnectionInfo) kind = SageDatabaseKind.Standard;
                    else if (info.ConnectionInfo is AddressBookDBConnectionInfo) kind = SageDatabaseKind.AddressBook;
                    else if (info.ConnectionInfo is ExternalReportDBConnectionInfo ||
                             info.ConnectionInfo is ReportDesignDBConnectionInfo) kind = SageDatabaseKind.Report;

                    result.Databases.Add(new SageDatabaseInfo
                    {
                        Name = name,
                        Kind = kind,
                        IsSupported = info.IsSupported,
                        Version = info.EstimatingVersion?.ToString()
                    });
                }

                var est = result.Databases.Count(d => d.Kind == SageDatabaseKind.Estimate);
                var std = result.Databases.Count(d => d.Kind == SageDatabaseKind.Standard);
                result.Message = $"{est} estimate DB(s), {std} standard DB(s) on {settings.SqlServer}.";
                return result;
            }
            catch (Exception ex)
            {
                return SageDatabaseListResult.Fail("Could not list databases: " + Describe(ex));
            }
        }

        public SageEstimateListResult ListEstimates(SageSettings settings)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.Database))
                return SageEstimateListResult.Fail("Pick an estimating database first.");
            try
            {
                var instance = ResolveInstance(settings);
                var estimateDB = new EstimateDBConnectionInfo(instance, settings.Database);
                var service = new EstimateDBService(estimateDB);
                var result = SageEstimateListResult.Ok();

                foreach (var e in service.ReadEstimatesInfo()
                             .Where(e => !e.Invalid)
                             .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
                {
                    result.Estimates.Add(e.Name);
                }

                result.Message = result.Estimates.Count == 0
                    ? $"No estimates in '{settings.Database}'."
                    : $"{result.Estimates.Count} estimate(s) in '{settings.Database}'.";
                return result;
            }
            catch (Exception ex)
            {
                return SageEstimateListResult.Fail("Could not list estimates: " + Describe(ex));
            }
        }

        public SageTakeoffResult Takeoff(SageTakeoffRequest request)
            => TakeoffBatch(new[] { request });

        public SageTakeoffResult TakeoffBatch(IEnumerable<SageTakeoffRequest> requests)
        {
            if (!IsConnected || _estimate == null)
                return SageTakeoffResult.Fail("Not connected to an estimate.");

            var pending = requests?.ToList() ?? new List<SageTakeoffRequest>();
            if (pending.Count == 0) return SageTakeoffResult.Fail("Nothing to take off.");

            var result = SageTakeoffResult.Ok();
            var extras = _settings.ParseExtraVariables(result.Log);

            if (!string.IsNullOrWhiteSpace(_settings.TargetPhase))
                result.Log.Add(
                    $"NOTE: target phase '{_settings.TargetPhase}' was not applied — each item keeps " +
                    "the phase it carries in the standard database.");

            try
            {
                var estimateService = new EstimateService(_estimate);

                // One lock and one cache for the whole batch: every session commits into
                // the cache, then a single write persists them together.
                using (estimateService.LockEstimateTemporarily())
                using (var cache = new TakeoffSessionCache(_estimate, _standardDB))
                {
                    foreach (var req in pending)
                    {
                        if (req == null || string.IsNullOrWhiteSpace(req.AssemblyId))
                        {
                            result.Log.Add("SKIP (no assembly configured): " + req?.Description);
                            continue;
                        }

                        TakeoffOne(req, cache, extras, result);
                    }

                    if (result.AssembliesTakenOff == 0)
                    {
                        return SageTakeoffResult.Fail(
                            "Nothing was taken off — see the log above. The estimate was not changed.");
                    }

                    cache.WriteTakeoffSessionCommittedEntities();
                }

                result.Message =
                    $"Sent {result.AssembliesTakenOff} assembly takeoff(s), {result.ItemsCreated} item(s) " +
                    $"to estimate '{_estimate.Name}'.";
                return result;
            }
            catch (Exception ex)
            {
                // The lock/cache are disposed by the using blocks; nothing was written
                // because the single write is the last thing inside them.
                var failure = SageTakeoffResult.Fail("Sage takeoff failed: " + Describe(ex));
                foreach (var line in result.Log) failure.Log.Add(line);
                return failure;
            }
        }

        private void TakeoffOne(
            SageTakeoffRequest req,
            TakeoffSessionCache cache,
            IDictionary<string, double> extras,
            SageTakeoffResult result)
        {
            var assembly = ReadAssembly(req.AssemblyId);
            if (assembly == null)
            {
                result.Log.Add($"SKIP: assembly '{req.AssemblyId}' not found in {_standardDB.Name} — {req.Description}");
                return;
            }
            if (assembly.IsGroup)
            {
                result.Log.Add($"SKIP: '{req.AssemblyId}' is a group assembly, not a takeoff assembly.");
                return;
            }

            using (var session = new AssemblyTakeoffSession(assembly, cache))
            {
                // Populates TakeoffVariables from the assembly's formulas. Without this
                // a formula-driven assembly reports no variables at all.
                session.EvaluateVariables();

                // Start from each variable's own default so untouched formulas still
                // evaluate the way they would in Sage's takeoff dialog.
                foreach (var v in session.TakeoffVariables)
                {
                    if (v.Variable != null) v.Value = v.Variable.DefaultValue;
                }

                var wanted = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in extras) wanted[kv.Key] = kv.Value;
                foreach (var kv in req.Variables) wanted[kv.Key] = kv.Value;

                // The area is the assembly quantity unless it was routed to a variable.
                var areaVariable = _settings.AreaVariableName;
                var areaByVariable = !string.IsNullOrWhiteSpace(areaVariable);
                if (areaByVariable) wanted[areaVariable] = req.AreaSquareFeet;

                foreach (var kv in wanted)
                {
                    var target = session.TakeoffVariables.FirstOrDefault(
                        v => string.Equals(v.Name, kv.Key, StringComparison.OrdinalIgnoreCase));
                    if (target == null)
                    {
                        result.Log.Add(
                            $"WARNING: '{req.AssemblyId}' has no takeoff variable '{kv.Key}' — value {kv.Value} ignored.");
                        continue;
                    }
                    target.Value = kv.Value;
                }

                if (!areaByVariable) session.QuantityMultiplier = req.AreaSquareFeet;

                session.AddPass();

                var items = session.GetItems();
                var written = items.Count(i => i.TakeoffQuantity != 0);
                if (written == 0)
                {
                    // Committing here would add an assembly with nothing under it.
                    result.Log.Add(
                        $"SKIP: '{req.AssemblyId}' produced only zero quantities for {req.AreaSquareFeet:0.##} SF. " +
                        (areaByVariable
                            ? $"Check that '{areaVariable}' is the right area variable and that the assembly's other variables are set."
                            : "This assembly computes its own quantity, so it needs the area in a variable — set 'Area variable'."));
                    return;
                }

                if (req.AppliesLabor)
                    ApplyLabor(req, items, result);

                // Each member is its own assembly takeoff, so it can carry its own
                // description — stamp the steel type/size onto it.
                if (!string.IsNullOrWhiteSpace(req.MemberLabel))
                {
                    var estimateAssembly = session.Assembly;
                    var baseDescription = assembly.Description;
                    estimateAssembly.Description = string.IsNullOrWhiteSpace(baseDescription)
                        ? req.MemberLabel
                        : $"{req.MemberLabel} — {baseDescription}";
                }

                session.CommitEntitiesToCache();

                result.AssembliesTakenOff++;
                result.ItemsCreated += written;
                result.Log.Add(
                    $"Takeoff '{req.AssemblyId}' — {req.Description} → {written} item(s) @ {req.AreaSquareFeet:0.##} SF");
            }
        }

        /// <summary>
        /// Write the labor price onto the generated item(s) whose description matches
        /// (blank match = every item), via the labor UnitPrice. Verified against a live
        /// estimate: labor Amount = TakeoffQuantity × UnitPrice.
        /// </summary>
        private static void ApplyLabor(SageTakeoffRequest req, EstimateItemEntityCollection items, SageTakeoffResult result)
        {
            var match = req.LaborItemMatch;
            var matchAll = string.IsNullOrWhiteSpace(match);

            var targets = matchAll
                ? items.ToList()
                : items.Where(i => (i.Description ?? string.Empty).IndexOf(match, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            if (targets.Count == 0)
            {
                result.Log.Add(
                    $"WARNING: no item matching '{match}' in '{req.AssemblyId}' — labor not written. " +
                    "Items: " + string.Join(", ", items.Select(i => "'" + i.Description + "'")));
                return;
            }

            foreach (var item in targets)
            {
                var labor = item.LaborCategory;
                labor.UnitPrice = req.LaborUnitPrice;   // sets UnitCost and Amount = qty × UnitPrice

                result.Log.Add(
                    $"  labor → '{item.Description}': ${req.LaborUnitPrice:0.####}/SF " +
                    $"[{req.LaborBasis}] = ${labor.Amount:N2}");
            }
        }

        private StandardDBAssemblyEntity ReadAssembly(string assemblyId)
        {
            if (_assemblies.TryGetValue(assemblyId, out var cached)) return cached;

            var service = new StandardDBAssemblyService(_standardDB)
            {
                ReadOptions = new EntityReadOptions { ReadWithAll = true }
            };

            // Assembly names are stored space-padded to the database's format; FormatName
            // turns what the estimator typed into the stored form.
            var assembly = service.ReadAssembly(AssemblyEntity.FormatName(assemblyId))
                        ?? service.ReadAssembly(assemblyId);

            _assemblies[assemblyId] = assembly;
            return assembly;
        }

        /// <summary>
        /// The batch already writes once at the end of <see cref="TakeoffBatch"/>; this
        /// exists to satisfy the contract and is a no-op.
        /// </summary>
        public void Commit()
        {
        }

        public void Dispose()
        {
            _assemblies.Clear();
            _estimate = null;
            _standardDB = null;
            IsConnected = false;
        }

        /// <summary>Surface the SDK's inner message, which carries the useful detail.</summary>
        private static string Describe(Exception ex)
        {
            var text = ex.Message;
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
                text += " → " + inner.Message;
            return text;
        }
#else
        private const string NotWired =
            "The Sage Estimating SDK is not wired up in this build. " +
            "Add the SDK reference and define the SAGE_SDK constant (see README » Wiring the SDK), " +
            "or enable 'Dry run' to use the built-in simulator.";

        public SageConnectResult Connect(SageSettings settings) => SageConnectResult.Fail(NotWired);
        public SageTakeoffResult Takeoff(SageTakeoffRequest request) => SageTakeoffResult.Fail(NotWired);
        public SageTakeoffResult TakeoffBatch(IEnumerable<SageTakeoffRequest> requests) => SageTakeoffResult.Fail(NotWired);
        public SageAssemblyListResult ListAssemblies(SageSettings settings) => SageAssemblyListResult.Fail(NotWired);
        public SageInstanceListResult ListSqlInstances() => SageInstanceListResult.Fail(NotWired);
        public SageDatabaseListResult ListDatabases(SageSettings settings) => SageDatabaseListResult.Fail(NotWired);
        public SageEstimateListResult ListEstimates(SageSettings settings) => SageEstimateListResult.Fail(NotWired);
        public void Commit() { }
        public void Dispose() { IsConnected = false; }
#endif
    }
}

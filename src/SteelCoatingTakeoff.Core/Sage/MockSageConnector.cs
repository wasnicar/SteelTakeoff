using System;
using System.Collections.Generic;
using System.Linq;

namespace SteelCoatingTakeoff.Core.Sage
{
    /// <summary>
    /// A no-Sage-required stand-in for <see cref="ISageConnector"/>. It validates
    /// and logs requests exactly as the real connector would, but writes nothing.
    ///
    /// Used for: unit tests, the "Dry run" toggle, and running the UI on a machine
    /// without Sage Estimating installed.
    /// </summary>
    public sealed class MockSageConnector : ISageConnector
    {
        private readonly Action<string> _log;
        private SageSettings _settings;

        public bool IsConnected { get; private set; }

        public MockSageConnector(Action<string> log = null)
        {
            _log = log ?? (_ => { });
        }

        public SageConnectResult Connect(SageSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            IsConnected = true;
            var name = string.IsNullOrWhiteSpace(settings.EstimateName) ? "(open estimate)" : settings.EstimateName;
            _log($"[MOCK] Connected to '{name}' on {settings.SqlServer}/{settings.Database}.");
            return SageConnectResult.Ok(name, "Mock connection established (no data written).");
        }

        public SageTakeoffResult Takeoff(SageTakeoffRequest request)
        {
            return TakeoffBatch(new[] { request });
        }

        public SageTakeoffResult TakeoffBatch(IEnumerable<SageTakeoffRequest> requests)
        {
            if (!IsConnected) return SageTakeoffResult.Fail("Not connected.");

            var result = SageTakeoffResult.Ok();
            foreach (var r in requests)
            {
                if (r == null) continue;
                if (string.IsNullOrWhiteSpace(r.AssemblyId))
                {
                    result.Log.Add($"[MOCK] SKIP (no assembly): {r.Description}");
                    continue;
                }
                var vars = string.Join(", ", r.Variables.Select(kv => $"{kv.Key}={kv.Value:0.##}"));
                result.Log.Add($"[MOCK] Takeoff assembly '{r.AssemblyId}' — {r.Description}  {{{vars}}}");
                result.AssembliesTakenOff++;
            }
            _log($"[MOCK] Batch complete: {result.AssembliesTakenOff} assembly takeoff(s) simulated.");
            result.Message = $"Simulated {result.AssembliesTakenOff} assembly takeoff(s) — nothing written to Sage.";
            return result;
        }

        public SageAssemblyListResult ListAssemblies(SageSettings settings)
        {
            // A small sample hierarchy so the picker works without Sage installed.
            var result = SageAssemblyListResult.Ok();
            result.Message = "Mock assemblies (no Sage connection).";
            void Add(string name, string desc, bool group) =>
                result.Assemblies.Add(new SageAssemblyInfo { Name = name, Description = desc, IsGroup = group });

            Add("3000.310.000", "INDUSTRIAL HPC SYSTEMS", true);
            Add("3000.310.01", "Intumescent Coating", false);
            Add("3000.310.02", "Steel Coating - Standard", false);
            Add("2500.000.000", "STEEL SHAPES", true);
            Add("2500.000.110", "Steel W Flange", false);
            Add("2500.000.210", "Generic Steel", false);
            return result;
        }

        public SageInstanceListResult ListSqlInstances()
        {
            var r = SageInstanceListResult.Ok();
            r.Message = "Mock instances (no Sage connection).";
            r.Instances.Add("(local)\\SAGE_EST25");
            return r;
        }

        public SageDatabaseListResult ListDatabases(SageSettings settings)
        {
            var r = SageDatabaseListResult.Ok();
            r.Message = "Mock databases (no Sage connection).";
            r.Databases.Add(new SageDatabaseInfo { Name = "Estimates", Kind = SageDatabaseKind.Estimate, Version = "25.01.00.00030" });
            r.Databases.Add(new SageDatabaseInfo { Name = "Sample_Standard_DB", Kind = SageDatabaseKind.Standard, Version = "25.01.00.00010" });
            return r;
        }

        public SageEstimateListResult ListEstimates(SageSettings settings)
        {
            var r = SageEstimateListResult.Ok();
            r.Message = "Mock estimates (no Sage connection).";
            r.Estimates.Add("Sample Estimate");
            return r;
        }

        public void Commit()
        {
            _log("[MOCK] Commit (no-op).");
        }

        public void Dispose()
        {
            IsConnected = false;
        }
    }
}

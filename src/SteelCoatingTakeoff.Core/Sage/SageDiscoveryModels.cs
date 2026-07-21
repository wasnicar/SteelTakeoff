using System.Collections.Generic;

namespace SteelCoatingTakeoff.Core.Sage
{
    /// <summary>What a Sage SQL database is for — drives which dropdown it belongs in.</summary>
    public enum SageDatabaseKind
    {
        Unknown = 0,
        Estimate,
        Standard,
        AddressBook,
        Report
    }

    /// <summary>One Sage database found on a SQL instance.</summary>
    public sealed class SageDatabaseInfo
    {
        public string Name { get; set; }
        public SageDatabaseKind Kind { get; set; }

        /// <summary>False when the SDK cannot open it (e.g. a 26.x DB against the 25.2 SDK).</summary>
        public bool IsSupported { get; set; } = true;

        /// <summary>Sage database version, e.g. "25.01.00.00030".</summary>
        public string Version { get; set; }

        /// <summary>Name plus a warning marker when the SDK can't open it.</summary>
        public string Display => IsSupported ? Name : $"{Name}  (unsupported — {Version})";

        public override string ToString() => Display;
    }

    /// <summary>SQL Server instances discovered locally and on the network.</summary>
    public sealed class SageInstanceListResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<string> Instances { get; } = new List<string>();

        public static SageInstanceListResult Ok() => new SageInstanceListResult { Success = true };
        public static SageInstanceListResult Fail(string message) =>
            new SageInstanceListResult { Success = false, Message = message };
    }

    /// <summary>Databases on one SQL instance, classified by kind.</summary>
    public sealed class SageDatabaseListResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<SageDatabaseInfo> Databases { get; } = new List<SageDatabaseInfo>();

        public static SageDatabaseListResult Ok() => new SageDatabaseListResult { Success = true };
        public static SageDatabaseListResult Fail(string message) =>
            new SageDatabaseListResult { Success = false, Message = message };
    }

    /// <summary>Estimates inside the configured estimates database.</summary>
    public sealed class SageEstimateListResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<string> Estimates { get; } = new List<string>();

        public static SageEstimateListResult Ok() => new SageEstimateListResult { Success = true };
        public static SageEstimateListResult Fail(string message) =>
            new SageEstimateListResult { Success = false, Message = message };
    }
}

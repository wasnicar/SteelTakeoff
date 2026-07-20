using System.Collections.Generic;

namespace SteelCoatingTakeoff.Core.Sage
{
    /// <summary>
    /// One assembly (or group header) from the standard database, in the order Sage
    /// displays it. Groups precede the assemblies they contain, so a UI can rebuild the
    /// grouped hierarchy from an ordered list of these.
    /// </summary>
    public sealed class SageAssemblyInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>True for a group header (a section), false for a takeoff assembly.</summary>
        public bool IsGroup { get; set; }

        /// <summary>Name and description together, e.g. "3000.310.01 — Intumescent Coating".</summary>
        public string Display =>
            string.IsNullOrWhiteSpace(Description) ? Name : $"{Name} — {Description}";

        public override string ToString() => Display;
    }

    /// <summary>Outcome of listing the standard database's assemblies.</summary>
    public sealed class SageAssemblyListResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        /// <summary>Assemblies in Sage display order (group headers followed by their members).</summary>
        public List<SageAssemblyInfo> Assemblies { get; } = new List<SageAssemblyInfo>();

        public static SageAssemblyListResult Ok() => new SageAssemblyListResult { Success = true };
        public static SageAssemblyListResult Fail(string message) =>
            new SageAssemblyListResult { Success = false, Message = message };
    }
}

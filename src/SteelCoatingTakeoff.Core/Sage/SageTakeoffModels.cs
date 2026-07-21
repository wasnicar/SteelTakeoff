using System.Collections.Generic;
using SteelCoatingTakeoff.Core.Model;

namespace SteelCoatingTakeoff.Core.Sage
{
    /// <summary>
    /// A single assembly-takeoff instruction, fully resolved and ready for the
    /// connector. The Core layer builds these from takeoff lines so the
    /// SDK-specific connector never has to know about shapes or SF/LF factors.
    /// </summary>
    public sealed class SageTakeoffRequest
    {
        /// <summary>Description for logging / the Sage takeoff note, e.g. "W12×26 — 240 LF".</summary>
        public string Description { get; set; }

        /// <summary>Assembly database ID or name to take off (already routed by coating type).</summary>
        public string AssemblyId { get; set; }

        /// <summary>Coating classification that produced the routing.</summary>
        public CoatingType Coating { get; set; }

        /// <summary>Coating AREA in square feet — the primary quantity sent to the assembly.</summary>
        public double AreaSquareFeet { get; set; }

        /// <summary>Linear feet behind the area (optional secondary variable / audit).</summary>
        public double LinearFeet { get; set; }

        /// <summary>AISC key for traceability back to the source shape.</summary>
        public string AiscKey { get; set; }

        /// <summary>
        /// Steel type/size label written into the estimate assembly's Description, so each
        /// member's takeoff is identifiable in Sage (e.g. "W12 × 26"). Each member is a
        /// separate assembly takeoff precisely so it can carry its own description.
        /// </summary>
        public string MemberLabel { get; set; }

        // ---- Labor (set when a wage + productivity are configured) ----------
        // Labor price/SF = wage/productivity, times the WFT factor for intumescent.
        // The connector writes it onto the item(s) matched by LaborItemMatch.

        /// <summary>True when this request carries a labor price to write.</summary>
        public bool AppliesLabor { get; set; }

        /// <summary>
        /// Case-insensitive description substring identifying which generated item(s)
        /// receive the labor (e.g. "Insulation"). Blank = every generated item.
        /// </summary>
        public string LaborItemMatch { get; set; }

        /// <summary>Labor price per SF → item labor UnitPrice (Sage Amount = qty × this).</summary>
        public double LaborUnitPrice { get; set; }

        /// <summary>
        /// Productivity factor → item labor ProductivityFactor (Sage's L.Prod Factor).
        /// Informational for the estimator; it does not scale <see cref="LaborUnitPrice"/>.
        /// </summary>
        public double LaborProductivityFactor { get; set; } = 1.0;

        /// <summary>
        /// Achievable productivity (SF/hr) behind the price — the entered productivity for
        /// standard lines, divided by the WFT factor for intumescent. Reported in the log
        /// and the PDF; not written to Sage (see <see cref="SageSettings"/> for why).
        /// </summary>
        public double EffectiveProductivity { get; set; }

        /// <summary>How the price was derived, for the activity log (e.g. "WFT 20/5 × $0.50/SF").</summary>
        public string LaborBasis { get; set; }

        /// <summary>
        /// Additional assembly takeoff variables to set, keyed by variable name.
        /// The Core builder populates the area (and optionally LF) variable here
        /// using the names from <see cref="SageSettings"/>.
        /// </summary>
        public Dictionary<string, double> Variables { get; } = new Dictionary<string, double>();
    }

    /// <summary>Outcome of a connect attempt.</summary>
    public sealed class SageConnectResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string EstimateName { get; set; }

        public static SageConnectResult Ok(string estimate, string message = "Connected.")
            => new SageConnectResult { Success = true, EstimateName = estimate, Message = message };

        public static SageConnectResult Fail(string message)
            => new SageConnectResult { Success = false, Message = message };
    }

    /// <summary>Outcome of one or more takeoff requests.</summary>
    public sealed class SageTakeoffResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        /// <summary>How many assembly takeoffs were performed.</summary>
        public int AssembliesTakenOff { get; set; }

        /// <summary>How many estimate items the assemblies generated (if the SDK reports it).</summary>
        public int ItemsCreated { get; set; }

        /// <summary>Per-request log lines (useful for the UI activity pane and troubleshooting).</summary>
        public List<string> Log { get; } = new List<string>();

        public static SageTakeoffResult Ok(string message = "Takeoff complete.")
            => new SageTakeoffResult { Success = true, Message = message };

        public static SageTakeoffResult Fail(string message)
            => new SageTakeoffResult { Success = false, Message = message };
    }
}

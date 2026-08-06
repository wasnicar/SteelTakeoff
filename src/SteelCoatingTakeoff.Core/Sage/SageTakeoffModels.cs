using System.Collections.Generic;
using System.Globalization;
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

        /// <summary>
        /// Required fire rating for this member (e.g. "2 hr"). Folded into the assembly
        /// description via <see cref="AssemblyLabel"/>. Informational — no cost/quantity effect.
        /// </summary>
        public string FireRating { get; set; }

        /// <summary>
        /// Member classification (e.g. "Column/Floor", "Beam"). Folded into the assembly
        /// description. Informational — no cost/quantity effect.
        /// </summary>
        public string MemberClass { get; set; }

        /// <summary>
        /// The member label stamped onto the estimate assembly, with the classification and
        /// fire rating folded in when present — e.g. "W12 × 26 · Column/Floor · FR 2 hr".
        /// Kept here (not in the connector) so the composition is unit-testable without the SDK.
        /// </summary>
        public string AssemblyLabel()
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(MemberLabel)) parts.Add(MemberLabel.Trim());
            if (!string.IsNullOrWhiteSpace(MemberClass)) parts.Add(MemberClass.Trim());
            if (!string.IsNullOrWhiteSpace(FireRating)) parts.Add("FR " + FireRating.Trim());
            return string.Join(" · ", parts);
        }

        /// <summary>
        /// The full description to stamp on the estimate assembly:
        /// "001 · &lt;member label&gt; — &lt;base assembly description&gt;", omitting any empty part.
        /// The zero-padded takeoff <paramref name="sequence"/> (when <paramref name="number"/>
        /// is true) makes Sage's Assembly sort sequence follow takeoff order. Composed here,
        /// not in the connector, so it is unit-testable without the SDK.
        /// </summary>
        public string AssemblyDescription(int sequence, string baseDescription, bool number)
        {
            var label = AssemblyLabel();
            if (number)
            {
                var prefix = sequence.ToString("000", CultureInfo.InvariantCulture);
                label = string.IsNullOrWhiteSpace(label) ? prefix : prefix + " · " + label;
            }
            if (string.IsNullOrWhiteSpace(label)) return baseDescription ?? string.Empty;
            return string.IsNullOrWhiteSpace(baseDescription) ? label : label + " — " + baseDescription;
        }

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

        /// <summary>
        /// How labor reaches Sage, decided by coating type:
        ///
        ///   false (intumescent) → send a finished $/SF as the labor UnitPrice
        ///                         (<see cref="LaborUnitPrice"/>). Sage: Amount = qty × $/SF.
        ///   true  (standard)    → send the wage and productivity into Sage's own L.Price
        ///                         and L.Prod, with the labor line ordered in hours, so
        ///                         Sage computes the amount and an estimator can adjust
        ///                         either value there. Verified against a live estimate.
        /// </summary>
        public bool LaborAsProductivity { get; set; }

        /// <summary>Labor price per SF → item labor UnitPrice (intumescent). Sage Amount = qty × this.</summary>
        public double LaborUnitPrice { get; set; }

        /// <summary>Hourly wage → L.Price (standard lines, and the intumescent split's base coats).</summary>
        public double LaborWageRate { get; set; }

        /// <summary>Productivity (SF/hr) → L.Prod (standard lines, and the intumescent split's base coats — the raw value).</summary>
        public double LaborProductivity { get; set; }

        /// <summary>
        /// Effective productivity (SF/hr) → L.Prod on the intumescent PAINT line only:
        /// the raw productivity reduced by the thickness factor (WFT ÷ divisor). Sage then
        /// prices the paint line at wage ÷ this. Base coats use <see cref="LaborProductivity"/>.
        /// </summary>
        public double LaborPaintProductivity { get; set; }

        /// <summary>
        /// Intumescent only. When true the connector SPLITS labor across the assembly's
        /// items: the paint line (matched by <see cref="LaborItemMatch"/>) gets the WFT
        /// <see cref="LaborUnitPrice"/> as a fixed $/SF, while every OTHER item gets the
        /// wage (<see cref="LaborWageRate"/>) and productivity (<see cref="LaborProductivity"/>).
        /// </summary>
        public bool SplitIntumescentLabor { get; set; }

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

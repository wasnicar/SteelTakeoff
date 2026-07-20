using System.Collections.Generic;
using System.Globalization;
using SteelCoatingTakeoff.Core.Model;

namespace SteelCoatingTakeoff.Core.Sage
{
    /// <summary>
    /// All configuration the Sage connector needs. Persisted to appsettings.json
    /// next to the executable so an estimator can point the tool at their own
    /// database, estimate, and assembly names without a rebuild.
    /// </summary>
    public sealed class SageSettings
    {
        // ---- Connection ---------------------------------------------------
        // How to reach Sage Estimating (SQL). Depending on your SDK version you
        // either attach to the estimate the user has open, or open one by name.

        /// <summary>Sage Estimating SQL Server instance, e.g. "MYPC\SAGE_ESTIMATING".</summary>
        public string SqlServer { get; set; } = "WILLS-LAPTOP\\SAGE_EST25";

        /// <summary>Estimates database (catalog) name — where the estimate lives.</summary>
        public string Database { get; set; } = "Estimates";

        /// <summary>
        /// Standard database (catalog) name — where the ASSEMBLIES live. Takeoff reads
        /// the assembly from here and writes the resulting items into the estimate, so
        /// both databases are required.
        /// </summary>
        public string StandardDatabase { get; set; } = "DS0056_v23_Industrial_Coatings_04";

        /// <summary>
        /// Name of the estimate to take off into. Leave blank to use the estimate
        /// currently open in Sage Estimating, if your SDK build supports it.
        /// </summary>
        public string EstimateName { get; set; } = "";

        // ---- Assembly routing --------------------------------------------
        // The tool routes each line to one of two assemblies based on the
        // Intumescent Yes/No flag. Set these to the assembly's DATABASE ID or
        // NAME exactly as they appear in your Sage standard database.

        /// <summary>Assembly that consumes intumescent coating area (Intumescent = YES).</summary>
        public string IntumescentAssembly { get; set; } = "3000.310.01";

        /// <summary>Assembly that consumes standard coating area (Intumescent = NO).</summary>
        public string StandardAssembly { get; set; } = "3000.310.02";

        // ---- Labor ---------------------------------------------------------
        // Labor is priced from two typed inputs on the takeoff screen:
        //
        //     Labor Rate (LR, $/SF) = Wage Rate ($/hr) / Productivity (SF/hr)
        //
        //     standard line    : labor price/SF = LR
        //     intumescent line : labor price/SF = (WFT / divisor) x LR x coats
        //
        // The connector writes that price into the item's labor UnitPrice, so Sage
        // shows labor Amount = takeoff quantity x UnitPrice. Coating AREA is the same
        // for both types — thickness only changes labor.

        /// <summary>Wage rate ($/hr) applied to every line. Typed on the takeoff screen.</summary>
        public double WageRate { get; set; } = 0.0;

        /// <summary>Productivity (SF/hr) applied to every line. Typed on the takeoff screen.</summary>
        public double Productivity { get; set; } = 0.0;

        /// <summary>
        /// Divisor in the intumescent thickness factor (WFT / this). Default 5.
        /// Exposed so the rule can be tuned without a rebuild.
        /// </summary>
        public double WftLaborDivisor { get; set; } = 5.0;

        /// <summary>
        /// Default wet film thickness (mils) pre-filled into each new intumescent line.
        /// The estimator overrides it per member (thickness varies with W/D ratio and
        /// the required fire rating).
        /// </summary>
        public double DefaultWftMils { get; set; } = 0;

        /// <summary>
        /// Number of coats pre-filled into each new line (any coating type). Coats is a
        /// per-line input that multiplies the coating area (total SF), never the labor
        /// rate. Defaults to 1.
        /// </summary>
        public int DefaultCoats { get; set; } = 1;

        /// <summary>
        /// Which generated item carries the labor on the INTUMESCENT assembly, matched by
        /// a case-insensitive substring of the item description. Default "Insulation"
        /// targets the film line ("Generic Insulation Coat 1").
        /// </summary>
        public string IntumescentLaborItemMatch { get; set; } = "Insulation";

        /// <summary>
        /// Which generated item carries the labor on the STANDARD assembly, matched by a
        /// case-insensitive substring of the item description. Blank applies the labor
        /// price to every generated item (use when the assembly is a single coating line).
        /// </summary>
        public string StandardLaborItemMatch { get; set; } = "";

        /// <summary>
        /// How the coating AREA reaches the assembly.
        ///
        /// Blank (the default) sends the area as the assembly's TAKEOFF QUANTITY,
        /// which is what an SF-unit assembly whose Calculation is empty expects —
        /// e.g. 3000.310.01, whose items are all UseFactor x 1, so each lands at the
        /// area exactly.
        ///
        /// Set this only for an assembly that instead computes its quantity from a
        /// formula/table and takes the area through a named variable (e.g. "Area SF"
        /// on 1100.150.051, which also needs "Area SF Calculation Type" = 4 supplied
        /// via <see cref="ExtraVariables"/>).
        /// </summary>
        public string AreaVariableName { get; set; } = "";

        /// <summary>
        /// Extra assembly takeoff variables to set on every request, as
        /// "Variable Name=value" entries (e.g. "Area SF Calculation Type=4").
        /// Any name that the assembly does not expose is reported in the activity log
        /// rather than being silently dropped. Kept as strings so the whole config
        /// stays editable in appsettings.json without a rebuild.
        /// </summary>
        public List<string> ExtraVariables { get; set; } = new List<string>();

        /// <summary>
        /// Optional: also pass linear feet into this variable if the assembly
        /// uses it. Leave blank to send area only.
        /// </summary>
        public string LinearFeetVariableName { get; set; } = "";

        // ---- Placement ----------------------------------------------------

        /// <summary>
        /// Not applied during takeoff. Each item an assembly generates carries its own
        /// phase from the standard database (e.g. Surface Prep lands in 1500.101 while
        /// the finishes land in 1000.390), and reassigning that would decouple the item
        /// from the standard-database item it was taken off from. Left here only so an
        /// existing appsettings.json still loads; the connector logs a notice if it is
        /// set rather than pretending to honour it.
        /// </summary>
        public string TargetPhase { get; set; } = "";

        /// <summary>When true, don't write to Sage — just simulate and log. Uses MockSageConnector.</summary>
        public bool DryRun { get; set; } = false;

        /// <summary>Resolve the assembly identifier for a coating type.</summary>
        public string AssemblyFor(CoatingType coating)
        {
            return coating == CoatingType.Intumescent ? IntumescentAssembly : StandardAssembly;
        }

        /// <summary>Which generated item receives the labor price, by coating type.</summary>
        public string LaborItemMatchFor(CoatingType coating)
        {
            return coating == CoatingType.Intumescent ? IntumescentLaborItemMatch : StandardLaborItemMatch;
        }

        /// <summary>
        /// Parse <see cref="ExtraVariables"/> ("Name=value") into a name/value map.
        /// Malformed entries are skipped and reported through <paramref name="problems"/>
        /// so a typo surfaces in the activity log instead of silently doing nothing.
        /// </summary>
        public Dictionary<string, double> ParseExtraVariables(List<string> problems = null)
        {
            var map = new Dictionary<string, double>();
            if (ExtraVariables == null) return map;

            foreach (var entry in ExtraVariables)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;

                var split = entry.LastIndexOf('=');
                if (split <= 0)
                {
                    problems?.Add("Ignored extra variable (expected 'Name=value'): " + entry);
                    continue;
                }

                var name = entry.Substring(0, split).Trim();
                var text = entry.Substring(split + 1).Trim();
                if (name.Length == 0 ||
                    !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    problems?.Add("Ignored extra variable (value is not a number): " + entry);
                    continue;
                }

                map[name] = value;
            }
            return map;
        }
    }
}

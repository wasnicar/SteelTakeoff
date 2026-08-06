namespace SteelCoatingTakeoff.Core.Model
{
    /// <summary>
    /// One line of the steel takeoff: a shape at a size, a linear-foot quantity
    /// (as taken off in eTakeoff), and whether it receives intumescent coating.
    /// </summary>
    public sealed class TakeoffLine
    {
        public ShapeFamily Family { get; set; }
        public SteelShape Shape { get; set; }

        /// <summary>Plate strip width in inches. Only used when Family.IsPlate is true.</summary>
        public double PlateWidthInches { get; set; } = 12.0;

        /// <summary>Total linear feet for this member/size (the eTakeoff quantity).</summary>
        public double LinearFeet { get; set; }

        /// <summary>
        /// How many identical members this line stands for — the same length taken off more
        /// than once. Drives the Sage assembly QUANTITY (the area itself goes to the "Area SF"
        /// variable), and multiplies the displayed area and labor. Defaults to 1.
        /// </summary>
        public double Multiplier { get; set; } = 1.0;

        public CoatingType Coating { get; set; } = CoatingType.Standard;

        /// <summary>
        /// Specified DRY film thickness in mils — the value the coatings supplier gives.
        /// Only meaningful when <see cref="Coating"/> is Intumescent. Wet film thickness
        /// (WFT = DFT ÷ volume-solids) is derived from this and drives the application
        /// LABOR (productivity = WFT / divisor); it varies per member with W/D ratio and
        /// fire rating. 0 means "not specified".
        /// </summary>
        public double DftMils { get; set; }

        /// <summary>
        /// Number of coats for this member (any coating type). Multiplies the coating
        /// AREA, never the labor rate. Defaults to 1.
        /// </summary>
        public int Coats { get; set; } = 1;

        /// <summary>
        /// Required fire rating for this member, e.g. "2 hr". Free text — it is the
        /// information a coatings supplier needs to specify the WFT. It has NO effect on
        /// area or labor; it only travels onto the supplier report and into the generated
        /// assembly's Sage description.
        /// </summary>
        public string FireRating { get; set; } = "";

        /// <summary>
        /// Whether this member is a column or a beam. Informational only (supplier report,
        /// cost report, Sage description); no effect on area or labor.
        /// </summary>
        public MemberKind MemberType { get; set; } = MemberKind.Unspecified;

        /// <summary>
        /// For a COLUMN, whether it supports a floor or a roof. Meaningless for a beam and
        /// ignored there. Informational only.
        /// </summary>
        public SupportKind Support { get; set; } = SupportKind.Unspecified;

        // ---- Labor, per member -------------------------------------------
        // Wage and productivity are carried by the LINE, not by settings: two members
        // in the same takeoff can be priced by different crews, and intumescent work
        // is routinely quoted at a different rate from a standard coat. The values in
        // SageSettings seed a new line and are otherwise unused for pricing.

        /// <summary>Hourly wage applied to this member ($/hr).</summary>
        public double WageRate { get; set; }

        /// <summary>
        /// Productivity for this member (SF/hr) BEFORE the intumescent thickness
        /// penalty. <see cref="TakeoffCalculator.EffectiveProductivity(TakeoffLine, double)"/>
        /// divides it by the WFT factor on intumescent lines.
        /// </summary>
        public double Productivity { get; set; }

        /// <summary>
        /// Sage's L.Prod Factor for this member. Sent to the estimate; it does not
        /// scale the price computed here. Defaults to 1.
        /// </summary>
        public double LaborProductivityFactor { get; set; } = 1.0;
    }
}

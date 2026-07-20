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

        public CoatingType Coating { get; set; } = CoatingType.Standard;

        /// <summary>
        /// Specified wet film thickness in mils. Only meaningful when
        /// <see cref="Coating"/> is Intumescent — thickness drives the application
        /// LABOR (productivity = WFT / divisor), and varies per member with W/D ratio
        /// and fire rating. 0 means "not specified".
        /// </summary>
        public double WftMils { get; set; }

        /// <summary>
        /// Number of intumescent coats. Multiplies the application labor. Only used
        /// when <see cref="Coating"/> is Intumescent. Defaults to 1.
        /// </summary>
        public int Coats { get; set; } = 1;
    }
}

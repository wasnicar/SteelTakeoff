using System;
using SteelCoatingTakeoff.Core.Model;

namespace SteelCoatingTakeoff.Core
{
    /// <summary>
    /// Pure calculation of coating surface area from a steel takeoff.
    ///
    /// Convention: CONTOUR (full wrap) perimeter — the standard for paint and
    /// applied intumescent coatings. Boxed-perimeter (rectangular) is NOT used.
    ///
    ///   coating area (SF) = linear feet × SF/LF
    ///
    /// The SF/LF factor is the AISC-published value for the shape, except Plate,
    /// whose painted strip perimeter depends on the entered width:
    ///
    ///   plate SF/LF = 2 × (width_in + thickness_in) ÷ 12
    /// </summary>
    public static class TakeoffCalculator
    {
        /// <summary>Surface area per linear foot (ft²/ft) for a line, including plate handling.</summary>
        public static double SfPerFoot(TakeoffLine line)
        {
            if (line?.Shape == null) return 0.0;
            if (line.Family != null && line.Family.IsPlate)
                return PlateSfPerFoot(line.PlateWidthInches, line.Shape.PlateThicknessInches ?? 0.0);
            return line.Shape.SfPerFoot;
        }

        /// <summary>Contour SF/LF for a flat plate/bar strip of the given width and thickness (inches).</summary>
        public static double PlateSfPerFoot(double widthInches, double thicknessInches)
        {
            if (widthInches <= 0) return 0.0;
            return 2.0 * (widthInches + thicknessInches) / 12.0;
        }

        /// <summary>
        /// Wrapped area of the steel itself (ft²) = SF/LF × linear feet, before coats.
        /// Film thickness never changes this.
        /// </summary>
        public static double GeometricAreaSquareFeet(TakeoffLine line)
        {
            if (line == null) return 0.0;
            var lf = line.LinearFeet;
            if (lf <= 0) return 0.0;
            return SfPerFoot(line) * lf;
        }

        /// <summary>
        /// Total coating area (ft²) sent to Sage = geometric area × coats. Coats affects
        /// only this square footage — not the labor rate. Coats defaults to 1.
        /// </summary>
        public static double AreaSquareFeet(TakeoffLine line)
        {
            if (line == null) return 0.0;
            var coats = line.Coats > 0 ? line.Coats : 1;
            return GeometricAreaSquareFeet(line) * coats;
        }

        /// <summary>
        /// Base labor rate (LR, $/SF) = wage ($/hr) ÷ productivity (SF/hr). Zero when
        /// either input is non-positive (nothing to price).
        /// </summary>
        public static double LaborRate(double wageRate, double productivity)
        {
            if (wageRate <= 0 || productivity <= 0) return 0.0;
            return wageRate / productivity;
        }

        /// <summary>
        /// Intumescent thickness factor = WFT ÷ divisor. Zero for standard lines and when
        /// WFT is unset. Divisor defaults to 5 if non-positive. Coats does NOT enter here
        /// — it only multiplies the coating area.
        /// </summary>
        public static double IntumescentFactor(TakeoffLine line, double wftLaborDivisor)
        {
            if (line == null || line.Coating != CoatingType.Intumescent) return 0.0;
            if (line.WftMils <= 0) return 0.0;
            var divisor = wftLaborDivisor > 0 ? wftLaborDivisor : 5.0;
            return line.WftMils / divisor;
        }

        /// <summary>
        /// Productivity actually achievable on this line (SF/hr).
        ///
        ///   standard    → the entered productivity
        ///   intumescent → productivity ÷ (WFT ÷ divisor)
        ///
        /// Thickness slows the crew down, so it DIVIDES productivity: at 20 mils with the
        /// default divisor of 5 the factor is 4, and 100 SF/hr becomes 25 SF/hr. This is
        /// the same money as multiplying the price by that factor — wage ÷ (P ÷ f) is
        /// identically f × (wage ÷ P) — but expressed the way the trade thinks about it,
        /// and it is the number reported as "effective productivity".
        /// </summary>
        public static double EffectiveProductivity(TakeoffLine line, double productivity, double wftLaborDivisor)
        {
            if (productivity <= 0 || line == null) return 0.0;
            if (line.Coating != CoatingType.Intumescent) return productivity;

            var factor = IntumescentFactor(line, wftLaborDivisor);
            if (factor <= 0) return 0.0;   // intumescent with no WFT is not priceable
            return productivity / factor;
        }

        /// <summary>
        /// Labor price per SF written to the item's labor UnitPrice:
        ///   $/SF = wage ÷ effective productivity
        /// Zero when wage or productivity is unset, or (intumescent) WFT is unset.
        /// </summary>
        public static double LaborPricePerSquareFoot(TakeoffLine line, double wageRate, double productivity, double wftLaborDivisor)
        {
            if (wageRate <= 0) return 0.0;
            var effective = EffectiveProductivity(line, productivity, wftLaborDivisor);
            if (effective <= 0) return 0.0;
            return wageRate / effective;
        }

        /// <summary>Total labor dollars for the line = area × price/SF.</summary>
        public static double LaborAmount(TakeoffLine line, double wageRate, double productivity, double wftLaborDivisor)
            => AreaSquareFeet(line) * LaborPricePerSquareFoot(line, wageRate, productivity, wftLaborDivisor);

        /// <summary>
        /// Round for display / transmission. Sage takeoff quantities are typically
        /// carried to 2 decimals; keep it consistent everywhere.
        /// </summary>
        public static double RoundQty(double value, int decimals = 2)
        {
            return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        }
    }
}

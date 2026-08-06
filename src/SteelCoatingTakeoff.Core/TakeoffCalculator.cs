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
        /// Wet film thickness (mils) derived from the entered DRY film thickness:
        ///   WFT = DFT ÷ volume-solids fraction  (e.g. 65% solids → WFT = DFT ÷ 0.65).
        /// The solvent that evaporates is the difference. Used only for the labor factor;
        /// the supplier provides DFT and WFT is computed.
        /// </summary>
        public static double WftFromDft(double dftMils, double volumeSolidsPercent)
        {
            if (dftMils <= 0) return 0.0;
            var solids = volumeSolidsPercent > 0 ? volumeSolidsPercent : 65.0;
            return dftMils * 100.0 / solids;
        }

        /// <summary>WFT (mils) for a line — DFT-derived, and 0 for anything but intumescent.</summary>
        public static double WftMils(TakeoffLine line, double volumeSolidsPercent)
        {
            if (line == null || line.Coating != CoatingType.Intumescent) return 0.0;
            return WftFromDft(line.DftMils, volumeSolidsPercent);
        }

        /// <summary>
        /// Intumescent thickness factor = WFT ÷ divisor, where WFT is derived from the
        /// entered DFT and the volume-solids fraction. Zero for standard lines and when
        /// DFT is unset. Divisor defaults to 5, solids to 65% when non-positive. Coats does
        /// NOT enter here — it only multiplies the coating area.
        /// </summary>
        public static double IntumescentFactor(TakeoffLine line, double wftLaborDivisor, double volumeSolidsPercent = 65.0)
        {
            var wft = WftMils(line, volumeSolidsPercent);
            if (wft <= 0) return 0.0;
            var divisor = wftLaborDivisor > 0 ? wftLaborDivisor : 5.0;
            return wft / divisor;
        }

        /// <summary>
        /// Productivity actually achievable on this line (SF/hr).
        ///
        ///   standard    → the entered productivity
        ///   intumescent → productivity ÷ (WFT ÷ divisor)
        ///
        /// Thickness slows the crew down, so it DIVIDES productivity. This is the value
        /// written to the intumescent PAINT line's Labor Productivity in Sage; wage still
        /// goes to Labor Price, and Sage prices labor = wage ÷ effective-productivity.
        /// </summary>
        public static double EffectiveProductivity(TakeoffLine line, double productivity, double wftLaborDivisor, double volumeSolidsPercent = 65.0)
        {
            if (productivity <= 0 || line == null) return 0.0;
            if (line.Coating != CoatingType.Intumescent) return productivity;

            var factor = IntumescentFactor(line, wftLaborDivisor, volumeSolidsPercent);
            if (factor <= 0) return 0.0;   // intumescent with no DFT is not priceable
            return productivity / factor;
        }

        /// <summary>
        /// Labor price per SF for previewing the member's labor:
        ///   $/SF = wage ÷ effective productivity
        /// Zero when wage or productivity is unset, or (intumescent) DFT is unset.
        /// </summary>
        public static double LaborPricePerSquareFoot(TakeoffLine line, double wageRate, double productivity, double wftLaborDivisor, double volumeSolidsPercent = 65.0)
        {
            if (wageRate <= 0) return 0.0;
            var effective = EffectiveProductivity(line, productivity, wftLaborDivisor, volumeSolidsPercent);
            if (effective <= 0) return 0.0;
            return wageRate / effective;
        }

        /// <summary>Total labor dollars for the line = area × price/SF.</summary>
        public static double LaborAmount(TakeoffLine line, double wageRate, double productivity, double wftLaborDivisor, double volumeSolidsPercent = 65.0)
            => AreaSquareFeet(line) * LaborPricePerSquareFoot(line, wageRate, productivity, wftLaborDivisor, volumeSolidsPercent);

        // ---- Per-member overloads ----------------------------------------
        // Labor is priced from the wage and productivity carried by the LINE. These are
        // the forms the app and the report use; the explicit-value versions above remain
        // for callers that are pricing a hypothetical rather than a member.

        /// <summary>Effective productivity (SF/hr) using the line's own productivity.</summary>
        public static double EffectiveProductivity(TakeoffLine line, double wftLaborDivisor, double volumeSolidsPercent = 65.0)
            => EffectiveProductivity(line, line?.Productivity ?? 0.0, wftLaborDivisor, volumeSolidsPercent);

        /// <summary>Labor price per SF using the line's own wage and productivity.</summary>
        public static double LaborPricePerSquareFoot(TakeoffLine line, double wftLaborDivisor, double volumeSolidsPercent = 65.0)
            => LaborPricePerSquareFoot(line, line?.WageRate ?? 0.0, line?.Productivity ?? 0.0, wftLaborDivisor, volumeSolidsPercent);

        /// <summary>Labor dollars using the line's own wage and productivity.</summary>
        public static double LaborAmount(TakeoffLine line, double wftLaborDivisor, double volumeSolidsPercent = 65.0)
            => AreaSquareFeet(line) * LaborPricePerSquareFoot(line, wftLaborDivisor, volumeSolidsPercent);

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

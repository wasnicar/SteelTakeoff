using System.Collections.Generic;
using SteelCoatingTakeoff.Core.Model;
using SteelCoatingTakeoff.Core.Sage;

namespace SteelCoatingTakeoff.Core
{
    /// <summary>One line of the "show calculation" breakdown.</summary>
    public sealed class CalculationStep
    {
        public string Label { get; }
        public string Detail { get; }

        public CalculationStep(string label, string detail)
        {
            Label = label;
            Detail = detail;
        }

        public override string ToString() => Label + ": " + Detail;
    }

    /// <summary>
    /// Explains, step by step, how a takeoff line becomes the number sent to Sage —
    /// where the SF/LF factor came from, the multiply, the rounding, which assembly the
    /// line routes to, and how the area reaches that assembly.
    ///
    /// Lives beside <see cref="TakeoffCalculator"/> and calls it for every number it
    /// reports, so the explanation cannot drift from the arithmetic it describes.
    /// </summary>
    public static class TakeoffExplainer
    {
        public static IReadOnlyList<CalculationStep> Explain(TakeoffLine line, SageSettings settings)
        {
            var steps = new List<CalculationStep>();

            if (line?.Shape == null)
            {
                steps.Add(new CalculationStep("Nothing to show", "Pick a shape and size first."));
                return steps;
            }

            var isPlate = line.Family != null && line.Family.IsPlate;

            steps.Add(new CalculationStep(
                "Shape",
                $"{line.Shape.Display}  —  AISC key {line.Shape.AiscKey}" +
                (line.Family != null ? $", {line.Family.Label}" : "")));

            // ---- where SF/LF comes from -------------------------------------
            var sfPerFoot = TakeoffCalculator.SfPerFoot(line);
            if (isPlate)
            {
                var thickness = line.Shape.PlateThicknessInches ?? 0.0;
                steps.Add(new CalculationStep(
                    "SF/LF  (plate)",
                    $"2 × (width + thickness) ÷ 12  =  2 × ({Num(line.PlateWidthInches)} in + {Num(thickness)} in) ÷ 12" +
                    $"  =  {Num(sfPerFoot)} ft²/ft"));
                steps.Add(new CalculationStep(
                    "why",
                    "Plate is the only family whose factor is computed, not tabulated — the painted " +
                    "strip's contour perimeter depends on the width you entered."));
            }
            else
            {
                steps.Add(new CalculationStep(
                    "SF/LF",
                    $"{Num(sfPerFoot)} ft²/ft  —  published for this size"));
                steps.Add(new CalculationStep(
                    "why",
                    "AISC Design Guide 19 CONTOUR (full-wrap) perimeter ÷ 12. Boxed perimeter is not " +
                    "used: paint and applied intumescent follow the shape's surface."));
            }

            // ---- the multiply ------------------------------------------------
            if (line.LinearFeet <= 0)
            {
                steps.Add(new CalculationStep("Area", "0 ft² — enter linear feet greater than 0."));
                return steps;
            }

            var geometric = TakeoffCalculator.GeometricAreaSquareFeet(line);
            var area = TakeoffCalculator.AreaSquareFeet(line);
            var rounded = TakeoffCalculator.RoundQty(area);
            var coats = line.Coats > 0 ? line.Coats : 1;

            steps.Add(new CalculationStep(
                "Area",
                $"{Num(line.LinearFeet)} LF  ×  {Num(sfPerFoot)} ft²/ft  =  {Num(geometric, 6)} ft²"));

            if (coats > 1)
            {
                steps.Add(new CalculationStep(
                    "Coats",
                    $"{Num(geometric, 6)} ft²  ×  {coats} coats  =  {Num(area, 6)} ft²  (coats affects area, not the labor rate)"));
            }

            steps.Add(new CalculationStep(
                "Rounded",
                $"{rounded:0.00} ft²  —  2 dp, half away from zero. This is the quantity Sage receives."));

            if (settings == null) return steps;

            // ---- labor -------------------------------------------------------
            var divisor = settings.WftLaborDivisor > 0 ? settings.WftLaborDivisor : 5.0;
            var lr = TakeoffCalculator.LaborRate(settings.WageRate, settings.Productivity);
            if (lr <= 0)
            {
                steps.Add(new CalculationStep(
                    "Labor",
                    "⚠  Set Wage Rate and Productivity on the takeoff screen to price labor."));
            }
            else
            {
                var pricePerSf = TakeoffCalculator.LaborPricePerSquareFoot(
                    line, settings.WageRate, settings.Productivity, divisor);
                var amount = TakeoffCalculator.LaborAmount(
                    line, settings.WageRate, settings.Productivity, divisor);

                steps.Add(new CalculationStep(
                    "Labor rate (LR)",
                    $"${settings.WageRate:0.00}/hr ÷ {Num(settings.Productivity)} SF/hr  =  ${lr:0.####} /SF"));

                if (line.Coating == CoatingType.Intumescent)
                {
                    if (line.WftMils <= 0)
                    {
                        steps.Add(new CalculationStep("Labor", "⚠  WFT is not set — intumescent labor needs a thickness."));
                    }
                    else
                    {
                        var factor = TakeoffCalculator.IntumescentFactor(line, divisor);
                        steps.Add(new CalculationStep(
                            "Intumescent factor",
                            $"{Num(line.WftMils)} mils ÷ {Num(divisor)}  =  {Num(factor)}"));
                        steps.Add(new CalculationStep(
                            "Labor $/SF",
                            $"{Num(factor)}  ×  ${lr:0.####}  =  ${pricePerSf:0.####} /SF  → item labor UnitPrice"));
                        steps.Add(new CalculationStep(
                            "Labor total",
                            $"{rounded:0.00} SF  ×  ${pricePerSf:0.####}  =  ${amount:N2}"));
                    }
                }
                else
                {
                    steps.Add(new CalculationStep(
                        "Labor $/SF",
                        $"${pricePerSf:0.####} /SF (= LR)  → item labor UnitPrice"));
                    steps.Add(new CalculationStep(
                        "Labor total",
                        $"{rounded:0.00} SF  ×  ${pricePerSf:0.####}  =  ${amount:N2}"));
                }
            }

            // ---- routing -----------------------------------------------------
            var assembly = settings.AssemblyFor(line.Coating);
            var coating = line.Coating == CoatingType.Intumescent ? "Intumescent = YES" : "Intumescent = NO";
            steps.Add(new CalculationStep(
                "Routes to",
                string.IsNullOrWhiteSpace(assembly)
                    ? $"{coating}  →  no assembly configured — this line would be skipped."
                    : $"{coating}  →  assembly '{assembly}'"));

            // ---- how the number gets there -----------------------------------
            steps.Add(string.IsNullOrWhiteSpace(settings.AreaVariableName)
                ? new CalculationStep(
                    "Sent as",
                    "the assembly's takeoff QUANTITY (Area variable is blank). An SF-unit assembly " +
                    "with no calculation multiplies each of its items by this.")
                : new CalculationStep(
                    "Sent as",
                    $"assembly takeoff variable '{settings.AreaVariableName}' = {rounded:0.00}"));

            if (!string.IsNullOrWhiteSpace(settings.LinearFeetVariableName))
            {
                steps.Add(new CalculationStep(
                    "Also sent",
                    $"variable '{settings.LinearFeetVariableName}' = {TakeoffCalculator.RoundQty(line.LinearFeet):0.00} LF"));
            }

            var extras = settings.ParseExtraVariables();
            foreach (var kv in extras)
                steps.Add(new CalculationStep("Also sent", $"variable '{kv.Key}' = {Num(kv.Value)}"));

            return steps;
        }

        /// <summary>Trim trailing zeros so factors read like the tables they came from.</summary>
        private static string Num(double value, int decimals = 4)
            => value.ToString("0." + new string('#', decimals));
    }
}

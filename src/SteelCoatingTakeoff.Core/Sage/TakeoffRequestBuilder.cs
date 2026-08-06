using System;
using System.Collections.Generic;
using SteelCoatingTakeoff.Core.Model;

namespace SteelCoatingTakeoff.Core.Sage
{
    /// <summary>
    /// Converts UI takeoff lines into fully-resolved <see cref="SageTakeoffRequest"/>
    /// objects: computes the coating area, routes to the intumescent or standard
    /// assembly, and loads the assembly takeoff variables from settings.
    /// </summary>
    public static class TakeoffRequestBuilder
    {
        public static SageTakeoffRequest Build(TakeoffLine line, SageSettings settings)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            // Total coating area = geometric area × coats. Coats affects only the SF, not
            // the labor rate.
            var area = TakeoffCalculator.RoundQty(TakeoffCalculator.AreaSquareFeet(line));
            var lf = TakeoffCalculator.RoundQty(line.LinearFeet);

            var req = new SageTakeoffRequest
            {
                AiscKey = line.Shape?.AiscKey,
                MemberLabel = line.Shape?.Display,
                FireRating = line.FireRating,
                MemberClass = MemberClassification.ShortLabel(line.MemberType, line.Support),
                Coating = line.Coating,
                AssemblyId = settings.AssemblyFor(line.Coating),
                AreaSquareFeet = area,
                LinearFeet = lf,
                Description = Describe(line, area)
            };

            // Labor. Wage and productivity come from the LINE, so members can be priced
            // individually. How it reaches Sage depends on the coating type:
            //
            //   intumescent → a fixed $/SF (WFT/divisor × wage/productivity) as the labor
            //                 UnitPrice; thickness has divided the productivity already.
            //   standard    → the wage and productivity themselves, so Sage computes the
            //                 amount from them and they stay adjustable in the estimate.
            //
            // The dollar total is the same either way (area × wage/effective-productivity);
            // only the field it lands in differs. Coats is not part of the rate — it is
            // already in the area quantity.
            // Gate on wage + productivity (both mechanisms need them). For intumescent the
            // paint line's $/SF additionally needs a WFT; if that is missing pricePerSf is 0
            // and only the paint line is left unpriced — the base-coat items still get
            // wage+productivity.
            if (line.WageRate > 0 && line.Productivity > 0)
            {
                var solids = settings.VolumeSolidsPercent;
                var effective = TakeoffCalculator.EffectiveProductivity(line, settings.WftLaborDivisor, solids);
                var pricePerSf = TakeoffCalculator.LaborPricePerSquareFoot(line, settings.WftLaborDivisor, solids);

                req.AppliesLabor = true;
                req.LaborItemMatch = settings.LaborItemMatchFor(line.Coating);
                req.EffectiveProductivity = effective;
                req.LaborProductivityFactor = line.LaborProductivityFactor;
                req.LaborWageRate = TakeoffCalculator.RoundQty(line.WageRate, 4);
                req.LaborProductivity = TakeoffCalculator.RoundQty(line.Productivity, 4);
                req.LaborUnitPrice = TakeoffCalculator.RoundQty(pricePerSf, 4);   // fallback $/SF only

                if (line.Coating == CoatingType.Intumescent)
                {
                    // Everything gets wage → L.Price. The PAINT line gets the EFFECTIVE
                    // productivity as its L.Prod (so Sage prices it at wage ÷ effective);
                    // every OTHER item gets the raw productivity.
                    req.SplitIntumescentLabor = true;
                    req.LaborAsProductivity = false;
                    req.LaborPaintProductivity = TakeoffCalculator.RoundQty(effective, 4);
                    req.LaborBasis =
                        $"paint L.Price ${line.WageRate:0.00}/hr, L.Prod {effective:0.##} SF/hr (effective); "
                        + $"other items L.Prod {line.Productivity:0.##} SF/hr";
                }
                else
                {
                    req.LaborAsProductivity = true;
                    req.LaborBasis =
                        $"L.Price ${line.WageRate:0.00}/hr, L.Prod {line.Productivity:0.##} SF/hr → Sage computes ${req.LaborUnitPrice:0.####}/SF";
                }
            }

            // Primary quantity: coating area (SF) -> the configured area variable.
            if (!string.IsNullOrWhiteSpace(settings.AreaVariableName))
                req.Variables[settings.AreaVariableName] = area;

            // Optional secondary: linear feet, if the assembly uses it.
            if (!string.IsNullOrWhiteSpace(settings.LinearFeetVariableName))
                req.Variables[settings.LinearFeetVariableName] = lf;

            return req;
        }

        public static List<SageTakeoffRequest> BuildAll(IEnumerable<TakeoffLine> lines, SageSettings settings)
        {
            var list = new List<SageTakeoffRequest>();
            foreach (var line in lines)
            {
                if (line?.Shape == null || line.LinearFeet <= 0) continue;
                list.Add(Build(line, settings));
            }
            return list;
        }

        private static string Describe(TakeoffLine line, double area)
        {
            var name = line.Shape?.Display ?? "(shape)";
            string coat;
            if (line.Coating == CoatingType.Intumescent)
            {
                coat = "Intumescent";
                if (line.DftMils > 0) coat += $" @ {line.DftMils:0.##} mils DFT";
                if (line.Coats > 1) coat += $" ×{line.Coats} coats";
            }
            else
            {
                coat = "Standard";
            }
            return $"{name} — {line.LinearFeet:0.##} LF → {area:0.##} SF [{coat}]";
        }
    }
}

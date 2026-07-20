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
                Coating = line.Coating,
                AssemblyId = settings.AssemblyFor(line.Coating),
                AreaSquareFeet = area,
                LinearFeet = lf,
                Description = Describe(line, area)
            };

            // Labor price/SF = wage/productivity (× WFT factor for intumescent). Coats is
            // not part of this rate — it is already in the area quantity.
            var pricePerSf = TakeoffCalculator.LaborPricePerSquareFoot(
                line, settings.WageRate, settings.Productivity, settings.WftLaborDivisor);
            if (pricePerSf > 0)
            {
                var lr = TakeoffCalculator.LaborRate(settings.WageRate, settings.Productivity);
                req.AppliesLabor = true;
                req.LaborItemMatch = settings.LaborItemMatchFor(line.Coating);
                req.LaborUnitPrice = TakeoffCalculator.RoundQty(pricePerSf, 4);
                req.LaborBasis = line.Coating == CoatingType.Intumescent
                    ? $"WFT {line.WftMils:0.##}/{settings.WftLaborDivisor:0.##}"
                      + $" × ${lr:0.####}/SF (${settings.WageRate:0.00}/hr ÷ {settings.Productivity:0.##} SF/hr)"
                    : $"${lr:0.####}/SF (${settings.WageRate:0.00}/hr ÷ {settings.Productivity:0.##} SF/hr)";
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
                if (line.WftMils > 0) coat += $" @ {line.WftMils:0.##} mils WFT";
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

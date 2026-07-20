using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using SteelCoatingTakeoff.Core;
using SteelCoatingTakeoff.Core.Model;
using SteelCoatingTakeoff.Core.Sage;

namespace SteelCoatingTakeoff.CoreTests
{
    /// <summary>
    /// Lightweight assertion harness (no test framework needed) so the domain
    /// logic can be verified on any platform with `dotnet run`.
    /// </summary>
    internal static class Program
    {
        private static int _pass, _fail;

        private static void Check(string name, bool cond, string detail = "")
        {
            if (cond) { _pass++; Console.WriteLine($"  PASS  {name}"); }
            else { _fail++; Console.WriteLine($"  FAIL  {name}  {detail}"); }
        }

        private static void Near(string name, double actual, double expected, double tol = 0.005)
        {
            Check(name, Math.Abs(actual - expected) <= tol, $"(got {actual}, expected {expected})");
        }

        private static int Main()
        {
            Console.WriteLine("Steel Coating Takeoff — Core tests\n");

            var db = ShapeDatabase.Load();

            Console.WriteLine("Database:");
            var totalSizes = db.Families.Sum(f => f.Shapes.Count);
            Check("10 families loaded", db.Families.Count == 10, $"got {db.Families.Count}");
            Check("1416 sizes loaded", totalSizes == 1416, $"got {totalSizes}");
            foreach (var f in db.Families)
                Console.WriteLine($"    {f.Code,-10} {f.Shapes.Count,4}  e.g. {f.Shapes.First().Display}");

            Console.WriteLine("\nSF/LF spot checks (vs. AISC DG19 workbook):");
            Near("W4X13 SF/LF", db.GetShape("W", "W4X13").SfPerFoot, 1.9667);
            Near("W12X26 SF/LF", db.GetShape("W", "W12X26").SfPerFoot, 4.1167);
            Near("W14X90 SF/LF", db.GetShape("W", "W14X90").SfPerFoot, 7.0083);
            Near("HSS6X6X1_4 SF/LF", db.GetShape("HSS_RECT", "HSS6X6X1_4").SfPerFoot, 2.0);
            Near("HSS6.000x0.500 SF/LF", db.GetShape("HSS_ROUND", "HSS6_000X0_500").SfPerFoot, 1.5708);
            Near("C12X30 SF/LF", db.GetShape("CHANNEL", "C12X30").SfPerFoot, 2.9583);
            Near("WT2X6_5 SF/LF", db.GetShape("TEE", "WT2X6_5").SfPerFoot, 1.0);
            Near("L4X4X1_4 SF/LF", db.GetShape("ANGLE", "L4X4X1_4").SfPerFoot, 1.3333);

            Console.WriteLine("\nDisplay labels:");
            Check("W12X26 -> 'W12 × 26'", db.GetShape("W", "W12X26").Display == "W12 × 26",
                db.GetShape("W", "W12X26").Display);
            Check("HSS round has (round) suffix",
                db.GetShape("HSS_ROUND", "HSS6_000X0_500").Display.Contains("(round)"));

            Console.WriteLine("\nArea math:");
            var w12 = db.GetShape("W", "W12X26");
            var line = new TakeoffLine { Family = db.GetFamily("W"), Shape = w12, LinearFeet = 240 };
            Near("W12X26 × 240 LF area", TakeoffCalculator.AreaSquareFeet(line), 4.1167 * 240, 0.05);

            Console.WriteLine("\nPlate width formula:");
            var plateFam = db.GetFamily("PLATE");
            var plate = plateFam.Shapes.First(s => s.Size == "3/16");
            var pl = new TakeoffLine { Family = plateFam, Shape = plate, PlateWidthInches = 12, LinearFeet = 1 };
            Near("3/16 plate @12in SF/LF", TakeoffCalculator.SfPerFoot(pl), 2.03125);
            pl.PlateWidthInches = 24;
            Near("3/16 plate @24in SF/LF", TakeoffCalculator.SfPerFoot(pl), 2.0 * (24 + 0.1875) / 12.0);

            Console.WriteLine("\nAssembly routing:");
            var settings = new SageSettings
            {
                IntumescentAssembly = "INT-ASM",
                StandardAssembly = "STD-ASM",
                AreaVariableName = "SF"
            };
            var intLine = new TakeoffLine { Family = db.GetFamily("W"), Shape = w12, LinearFeet = 100, Coating = CoatingType.Intumescent };
            var stdLine = new TakeoffLine { Family = db.GetFamily("W"), Shape = w12, LinearFeet = 100, Coating = CoatingType.Standard };
            var intReq = TakeoffRequestBuilder.Build(intLine, settings);
            var stdReq = TakeoffRequestBuilder.Build(stdLine, settings);
            Check("Intumescent -> INT-ASM", intReq.AssemblyId == "INT-ASM", intReq.AssemblyId);
            Check("Standard -> STD-ASM", stdReq.AssemblyId == "STD-ASM", stdReq.AssemblyId);
            Check("area variable populated", intReq.Variables.ContainsKey("SF"));
            Near("routed area SF", intReq.Variables["SF"], TakeoffCalculator.RoundQty(4.1167 * 100), 0.05);

            Console.WriteLine("\nLabor model (wage/productivity → LR; intumescent ×WFT/5):");
            // Wage $50/hr, productivity 100 SF/hr → LR = 0.50 $/SF
            Near("LR = 50/100 = 0.50", TakeoffCalculator.LaborRate(50, 100), 0.50);
            Near("LR zero when productivity 0", TakeoffCalculator.LaborRate(50, 0), 0.0);
            Near("LR zero when wage 0", TakeoffCalculator.LaborRate(0, 100), 0.0);

            var wftLine = new TakeoffLine
            {
                Family = db.GetFamily("W"), Shape = w12, LinearFeet = 240,
                Coating = CoatingType.Intumescent, WftMils = 20, Coats = 1
            };
            // area is NOT changed by thickness
            Near("area ignores WFT (labor, not area)", TakeoffCalculator.AreaSquareFeet(wftLine), 4.1167 * 240, 0.05);
            // intumescent factor = WFT/5 x coats
            Near("intumescent factor = 20/5 = 4", TakeoffCalculator.IntumescentFactor(wftLine, 5), 4.0);
            // price/SF = factor x LR = 4 x 0.50 = 2.00
            Near("intumescent price/SF = 4 x 0.50 = 2.00",
                TakeoffCalculator.LaborPricePerSquareFoot(wftLine, 50, 100, 5), 2.00);
            Near("intumescent labor total = area x 2.00",
                TakeoffCalculator.LaborAmount(wftLine, 50, 100, 5), 4.1167 * 240 * 2.00, 0.5);

            Near("custom divisor 4 => 20/4 = 5", TakeoffCalculator.IntumescentFactor(wftLine, 4), 5.0);

            // Coats affects AREA (total SF), not the labor factor.
            var twoCoats = new TakeoffLine
            {
                Family = db.GetFamily("W"), Shape = w12, LinearFeet = 240,
                Coating = CoatingType.Intumescent, WftMils = 20, Coats = 2
            };
            Near("coats do NOT change the intumescent factor", TakeoffCalculator.IntumescentFactor(twoCoats, 5), 4.0);
            Near("geometric area ignores coats", TakeoffCalculator.GeometricAreaSquareFeet(twoCoats), 4.1167 * 240, 0.05);
            Near("2 coats doubles total area", TakeoffCalculator.AreaSquareFeet(twoCoats), 4.1167 * 240 * 2, 0.1);
            // labor price/SF is coats-free; total labor follows the (doubled) area
            Near("2-coat price/SF unchanged (= 1-coat)",
                TakeoffCalculator.LaborPricePerSquareFoot(twoCoats, 50, 100, 5), 2.00);
            Near("2-coat labor total = 2× area × price",
                TakeoffCalculator.LaborAmount(twoCoats, 50, 100, 5), 4.1167 * 240 * 2 * 2.00, 1.0);

            var stdLaborLine = new TakeoffLine
            {
                Family = db.GetFamily("W"), Shape = w12, LinearFeet = 240, Coating = CoatingType.Standard
            };
            // standard price/SF = LR (no thickness factor)
            Near("standard price/SF = LR = 0.50",
                TakeoffCalculator.LaborPricePerSquareFoot(stdLaborLine, 50, 100, 5), 0.50);
            Near("standard labor total = area x 0.50",
                TakeoffCalculator.LaborAmount(stdLaborLine, 50, 100, 5), 4.1167 * 240 * 0.50, 0.2);

            Near("no wage => no labor price",
                TakeoffCalculator.LaborPricePerSquareFoot(wftLine, 0, 100, 5), 0.0);
            Near("intumescent no WFT => no price",
                TakeoffCalculator.LaborPricePerSquareFoot(
                    new TakeoffLine { Coating = CoatingType.Intumescent }, 50, 100, 5), 0.0);

            var laborSettings = new SageSettings
            {
                IntumescentAssembly = "INT", StandardAssembly = "STD", AreaVariableName = "",
                WageRate = 50, Productivity = 100, WftLaborDivisor = 5,
                IntumescentLaborItemMatch = "Insulation", StandardLaborItemMatch = ""
            };
            var wftReq = TakeoffRequestBuilder.Build(wftLine, laborSettings);
            Near("1-coat request area = geometric", wftReq.AreaSquareFeet, TakeoffCalculator.RoundQty(4.1167 * 240), 0.1);
            Check("request carries the member label", wftReq.MemberLabel == w12.Display, wftReq.MemberLabel);
            var twoCoatReq = TakeoffRequestBuilder.Build(twoCoats, laborSettings);
            Near("2-coat request area is doubled", twoCoatReq.AreaSquareFeet, TakeoffCalculator.RoundQty(4.1167 * 240 * 2), 0.1);
            Check("intumescent request applies labor", wftReq.AppliesLabor);
            Check("intumescent targets insulation item", wftReq.LaborItemMatch == "Insulation");
            Near("intumescent request unit price = 2.00", wftReq.LaborUnitPrice, 2.00);

            var stdReq2 = TakeoffRequestBuilder.Build(stdLaborLine, laborSettings);
            Check("standard request applies labor too", stdReq2.AppliesLabor);
            Check("standard match is blank (all items)", stdReq2.LaborItemMatch == "");
            Near("standard request unit price = 0.50", stdReq2.LaborUnitPrice, 0.50);

            Console.WriteLine("\nShow-calculation breakdown:");
            var explainSettings = new SageSettings
            {
                IntumescentAssembly = "3000.310.01",
                StandardAssembly = "3000.310.02",
                AreaVariableName = "",           // area travels as the takeoff quantity
                ExtraVariables = new List<string> { "Area SF Calculation Type=4", "junk-no-equals" }
            };
            var explained = TakeoffExplainer.Explain(intLine, explainSettings);
            var explainedText = string.Join(" | ", explained.Select(s => s.Label + ": " + s.Detail));

            Check("explains the AISC source", explainedText.Contains("Design Guide 19"));
            Check("shows the LF x SF/LF multiply", explainedText.Contains("100 LF") && explainedText.Contains("4.1167"));
            Check("shows the rounded quantity", explainedText.Contains(TakeoffCalculator.RoundQty(4.1167 * 100).ToString("0.00")));
            Check("shows the routed assembly", explainedText.Contains("3000.310.01"));
            Check("blank area variable => sent as QUANTITY", explainedText.Contains("QUANTITY"));
            Check("lists a valid extra variable", explainedText.Contains("Area SF Calculation Type"));
            Check("malformed extra variable is not shown", !explainedText.Contains("junk-no-equals"));

            var plateExplained = string.Join(" | ",
                TakeoffExplainer.Explain(
                    new TakeoffLine { Family = plateFam, Shape = plate, PlateWidthInches = 12, LinearFeet = 10 },
                    explainSettings).Select(s => s.Detail));
            Check("plate shows the width formula", plateExplained.Contains("2 × (12 in + 0.1875 in) ÷ 12"));

            var areaVarSettings = new SageSettings { AreaVariableName = "Area SF", StandardAssembly = "X" };
            Check("area variable => sent as VARIABLE",
                string.Join(" | ", TakeoffExplainer.Explain(stdLine, areaVarSettings).Select(s => s.Detail))
                      .Contains("variable 'Area SF'"));

            Check("no shape => friendly message",
                TakeoffExplainer.Explain(new TakeoffLine(), explainSettings)[0].Label == "Nothing to show");

            explainSettings.WftLaborDivisor = 5;
            explainSettings.WageRate = 50;
            explainSettings.Productivity = 100;
            var wftExplained = string.Join(" | ",
                TakeoffExplainer.Explain(wftLine, explainSettings).Select(s => s.Label + ": " + s.Detail));
            Check("breakdown shows LR", wftExplained.Contains("$50.00/hr ÷ 100 SF/hr"));
            Check("breakdown shows the intumescent factor", wftExplained.Contains("20 mils ÷ 5"));
            Check("breakdown shows labor $/SF", wftExplained.Contains("Labor $/SF"));
            Check("breakdown shows labor total", wftExplained.Contains("Labor total"));

            var stdExplained = string.Join(" | ",
                TakeoffExplainer.Explain(stdLine, explainSettings).Select(s => s.Label + ": " + s.Detail));
            Check("standard breakdown shows LR", stdExplained.Contains("Labor rate (LR)"));
            Check("standard breakdown has no intumescent factor", !stdExplained.Contains("Intumescent factor"));

            explainSettings.WageRate = 0;
            Check("breakdown prompts for wage/productivity when unset",
                string.Join(" | ", TakeoffExplainer.Explain(wftLine, explainSettings).Select(s => s.Detail))
                      .Contains("Set Wage Rate and Productivity"));

            Console.WriteLine("\nSettings persistence (DataContractJsonSerializer round-trip):");
            var original = new SageSettings
            {
                SqlServer = "SVR\\INST", Database = "Est", StandardDatabase = "Std",
                WageRate = 55, Productivity = 120,
                WftLaborDivisor = 6, DefaultWftMils = 25, DefaultCoats = 2,
                StandardLaborItemMatch = "Coat"
            };
            SageSettings roundTripped;
            var ser = new DataContractJsonSerializer(typeof(SageSettings));
            using (var ms = new MemoryStream())
            {
                ser.WriteObject(ms, original);
                ms.Position = 0;
                roundTripped = (SageSettings)ser.ReadObject(ms);
            }
            Near("settings round-trip: wage", roundTripped.WageRate, 55.0);
            Near("settings round-trip: productivity", roundTripped.Productivity, 120.0);
            Near("settings round-trip: divisor", roundTripped.WftLaborDivisor, 6.0);
            Check("settings round-trip: default coats", roundTripped.DefaultCoats == 2);
            Check("settings round-trip: standard match", roundTripped.StandardLaborItemMatch == "Coat");

            Console.WriteLine("\nMock connector end-to-end:");
            var log = new List<string>();
            using (var conn = new MockSageConnector(m => log.Add(m)))
            {
                var c = conn.Connect(settings);
                Check("mock connects", c.Success);
                var reqs = TakeoffRequestBuilder.BuildAll(new[] { intLine, stdLine }, settings);
                var r = conn.TakeoffBatch(reqs);
                Check("batch success", r.Success);
                Check("2 assemblies taken off", r.AssembliesTakenOff == 2, $"got {r.AssembliesTakenOff}");
                conn.Commit();
            }

            Console.WriteLine($"\n================  {_pass} passed, {_fail} failed  ================");
            return _fail == 0 ? 0 : 1;
        }
    }
}

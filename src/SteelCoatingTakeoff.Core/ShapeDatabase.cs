using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using SteelCoatingTakeoff.Core.Model;

namespace SteelCoatingTakeoff.Core
{
    /// <summary>
    /// Loads the embedded AISC shape data (Resources/shapes.dat) and exposes it
    /// as ordered families of selectable sizes with display labels.
    ///
    /// SF/LF values are the AISC Design Guide 19 published "shape perimeter" ÷ 12
    /// (full CONTOUR surface). HSS use 2(H+B)/12; round HSS and pipe use π·OD/12;
    /// plate is width-dependent and computed at runtime.
    ///
    /// Data format (pipe-delimited, '@' introduces a family):
    ///   @W
    ///   W4X13|4 x 13||1.9667          (key | size | type | sflf)
    ///   @PLATE
    ///   3/16|0.1875                   (thickness | thickness_inches)
    /// </summary>
    public sealed class ShapeDatabase
    {
        private static readonly Dictionary<string, string> FamilyLabels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "W",         "Wide flange (W)" },
            { "M",         "M-shape (M)" },
            { "S",         "S-shape (S)" },
            { "HSS_RECT",  "HSS rect / square" },
            { "HSS_ROUND", "HSS round" },
            { "PIPE",      "Pipe (Std/XS/XXS)" },
            { "CHANNEL",   "Channel (C / MC)" },
            { "TEE",       "Tee (WT / MT / ST)" },
            { "ANGLE",     "Angle (L)" },
            { "PLATE",     "Plate" },
        };

        private readonly Dictionary<string, ShapeFamily> _byCode;

        public IReadOnlyList<ShapeFamily> Families { get; }

        private ShapeDatabase(List<ShapeFamily> families)
        {
            Families = families;
            _byCode = families.ToDictionary(f => f.Code, StringComparer.OrdinalIgnoreCase);
        }

        public ShapeFamily GetFamily(string code)
        {
            _byCode.TryGetValue(code, out var f);
            return f;
        }

        /// <summary>Look up a single shape by family code + AISC key. Null if not found.</summary>
        public SteelShape GetShape(string familyCode, string aiscKey)
        {
            var fam = GetFamily(familyCode);
            return fam?.Shapes.FirstOrDefault(s =>
                string.Equals(s.AiscKey, aiscKey, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Load from the embedded resource. Call once and reuse.</summary>
        public static ShapeDatabase Load()
        {
            var asm = typeof(ShapeDatabase).GetTypeInfo().Assembly;
            var resourceName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("shapes.dat", StringComparison.OrdinalIgnoreCase));
            if (resourceName == null)
                throw new InvalidOperationException("Embedded resource shapes.dat was not found.");

            using (var stream = asm.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream))
            {
                return Parse(reader.ReadToEnd());
            }
        }

        /// <summary>Parse the pipe-delimited data text. Exposed for testing.</summary>
        public static ShapeDatabase Parse(string data)
        {
            var families = new List<ShapeFamily>();
            ShapeFamily current = null;
            List<SteelShape> currentShapes = null;

            using (var reader = new StringReader(data))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0) continue;

                    if (line[0] == '@')
                    {
                        var code = line.Substring(1).Trim();
                        currentShapes = new List<SteelShape>();
                        current = new ShapeFamily
                        {
                            Code = code,
                            Label = FamilyLabels.TryGetValue(code, out var lbl) ? lbl : code,
                            IsPlate = code.Equals("PLATE", StringComparison.OrdinalIgnoreCase),
                            Shapes = currentShapes
                        };
                        families.Add(current);
                        continue;
                    }

                    if (current == null) continue;
                    var f = line.Split('|');

                    if (current.IsPlate)
                    {
                        // thk | thk_in
                        var thk = f[0];
                        var thkIn = ParseD(f[1]);
                        currentShapes.Add(new SteelShape
                        {
                            AiscKey = "PL" + thk,
                            Size = thk,
                            Type = "PL",
                            Display = thk + "\" plate",
                            SfPerFoot = 0.0,
                            PlateThicknessInches = thkIn
                        });
                    }
                    else
                    {
                        // key | size | type | sflf
                        var key = f[0];
                        var size = f[1];
                        var type = f.Length > 2 && f[2].Length > 0 ? f[2] : null;
                        var sflf = ParseD(f[f.Length - 1]);
                        currentShapes.Add(new SteelShape
                        {
                            AiscKey = key,
                            Size = size,
                            Type = type,
                            Display = BuildDisplay(current.Code, size, type),
                            SfPerFoot = sflf
                        });
                    }
                }
            }

            return new ShapeDatabase(families);
        }

        private static double ParseD(string s)
        {
            return double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static string BuildDisplay(string code, string size, string type)
        {
            var s = (size ?? string.Empty).Replace(" x ", " × ");
            switch (code)
            {
                case "W": return "W" + s;
                case "M": return "M" + s;
                case "S": return "S" + s;
                case "HSS_RECT": return "HSS " + s;
                case "HSS_ROUND": return "HSS " + s + " (round)";
                case "PIPE": return "Pipe " + size + "\"  " + type;
                case "CHANNEL": return type + s;
                case "TEE": return type + s;
                case "ANGLE": return "L " + s;
                default: return s;
            }
        }
    }
}

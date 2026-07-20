using System;
using System.Collections.Generic;

namespace SteelCoatingTakeoff.Core.Model
{
    /// <summary>
    /// A steel shape family (dropdown group) e.g. Wide Flange, HSS Round, Angle, Plate.
    /// </summary>
    public sealed class ShapeFamily
    {
        /// <summary>Stable code used internally, e.g. "W", "HSS_RECT", "PLATE".</summary>
        public string Code { get; set; }

        /// <summary>Human label shown in the UI, e.g. "Wide flange (W)".</summary>
        public string Label { get; set; }

        /// <summary>
        /// True for the Plate family, whose SF/LF depends on a user-entered
        /// strip width and therefore cannot be pre-tabulated.
        /// </summary>
        public bool IsPlate { get; set; }

        public IReadOnlyList<SteelShape> Shapes { get; set; }

        public override string ToString() => Label;
    }

    /// <summary>
    /// A single selectable steel size with its published contour surface area.
    /// </summary>
    public sealed class SteelShape
    {
        /// <summary>AISC database key, e.g. "W12X26", "HSS6X6X1_4", "Pipe4STD".</summary>
        public string AiscKey { get; set; }

        /// <summary>Raw size text from the AISC tables, e.g. "12 x 26".</summary>
        public string Size { get; set; }

        /// <summary>Sub-type where a family mixes types (e.g. Tee: WT/MT/ST; Channel: C/MC).</summary>
        public string Type { get; set; }

        /// <summary>Friendly display label, e.g. "W12 × 26".</summary>
        public string Display { get; set; }

        /// <summary>
        /// Surface area per linear foot (square feet per foot), CONTOUR perimeter,
        /// per AISC Design Guide 19. 0 for Plate (computed from width at runtime).
        /// </summary>
        public double SfPerFoot { get; set; }

        /// <summary>Plate thickness in inches; null for all non-plate shapes.</summary>
        public double? PlateThicknessInches { get; set; }

        public override string ToString() => Display;
    }

    /// <summary>Whether a member gets intumescent coating (routes to a different Sage assembly).</summary>
    public enum CoatingType
    {
        /// <summary>Generic / standard coating — routes to the standard steel assembly.</summary>
        Standard = 0,

        /// <summary>Intumescent fireproofing — routes to the intumescent assembly.</summary>
        Intumescent = 1
    }
}

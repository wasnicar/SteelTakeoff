using System;
using System.Collections.Generic;
using SteelCoatingTakeoff.Core.Model;

namespace SteelCoatingTakeoff.Core.Projects
{
    /// <summary>
    /// One takeoff line as persisted to disk. Plain, serializer-friendly primitives:
    /// the shape is stored by family code + AISC key and re-resolved from the shape
    /// database on load (the live <see cref="ShapeFamily"/>/<see cref="SteelShape"/>
    /// objects are not themselves serialized). Enums are stored as their names so the
    /// JSON is readable and stable if the enum values ever shift.
    /// </summary>
    public sealed class SavedTakeoffLine
    {
        public string FamilyCode { get; set; } = "";
        public string ShapeKey { get; set; } = "";
        public double PlateWidthInches { get; set; } = 12.0;
        public double LinearFeet { get; set; }
        public bool Intumescent { get; set; }
        public double WftMils { get; set; }
        public int Coats { get; set; } = 1;
        public string FireRating { get; set; } = "";
        public string MemberType { get; set; } = "";
        public string Support { get; set; } = "";
        public double WageRate { get; set; }
        public double Productivity { get; set; }
        public double LaborProductivityFactor { get; set; } = 1.0;
    }

    /// <summary>
    /// A saved takeoff: its members plus enough context to reopen and finish it (the
    /// target estimate name especially). Costs are derived, never stored. Bump
    /// <see cref="SchemaVersion"/> if the shape ever changes incompatibly.
    /// </summary>
    public sealed class TakeoffProject
    {
        public int SchemaVersion { get; set; } = 1;
        public string Name { get; set; } = "";
        public string EstimateName { get; set; } = "";

        /// <summary>ISO-8601 UTC timestamps (stored as strings so the JSON stays clean).</summary>
        public string CreatedUtc { get; set; } = "";
        public string ModifiedUtc { get; set; } = "";

        public List<SavedTakeoffLine> Lines { get; set; } = new List<SavedTakeoffLine>();
    }

    /// <summary>Lightweight row for the projects list — no need to fully load to show it.</summary>
    public sealed class ProjectSummary
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string EstimateName { get; set; }
        public int MemberCount { get; set; }
        public DateTime Modified { get; set; }
    }

    /// <summary>Maps between a live <see cref="TakeoffLine"/> and its saved form.</summary>
    public static class ProjectMapper
    {
        public static SavedTakeoffLine FromLine(TakeoffLine line)
        {
            return new SavedTakeoffLine
            {
                FamilyCode = line.Family?.Code ?? "",
                ShapeKey = line.Shape?.AiscKey ?? "",
                PlateWidthInches = line.PlateWidthInches,
                LinearFeet = line.LinearFeet,
                Intumescent = line.Coating == CoatingType.Intumescent,
                WftMils = line.WftMils,
                Coats = line.Coats,
                FireRating = line.FireRating ?? "",
                MemberType = line.MemberType.ToString(),
                Support = line.Support.ToString(),
                WageRate = line.WageRate,
                Productivity = line.Productivity,
                LaborProductivityFactor = line.LaborProductivityFactor
            };
        }

        /// <summary>
        /// Re-hydrate a saved line into a <see cref="TakeoffLine"/>, resolving the shape
        /// from <paramref name="db"/>. Family/shape may come back null if a saved shape is
        /// no longer in the database; the caller decides how to surface that.
        /// </summary>
        public static TakeoffLine ToLine(SavedTakeoffLine dto, ShapeDatabase db)
        {
            var family = string.IsNullOrEmpty(dto.FamilyCode) ? null : db.GetFamily(dto.FamilyCode);
            var shape = family == null ? null : db.GetShape(dto.FamilyCode, dto.ShapeKey);
            return new TakeoffLine
            {
                Family = family,
                Shape = shape,
                PlateWidthInches = dto.PlateWidthInches,
                LinearFeet = dto.LinearFeet,
                Coating = dto.Intumescent ? CoatingType.Intumescent : CoatingType.Standard,
                WftMils = dto.WftMils,
                Coats = dto.Coats <= 0 ? 1 : dto.Coats,
                FireRating = dto.FireRating ?? "",
                MemberType = ParseEnum(dto.MemberType, MemberKind.Unspecified),
                Support = ParseEnum(dto.Support, SupportKind.Unspecified),
                WageRate = dto.WageRate,
                Productivity = dto.Productivity,
                LaborProductivityFactor = dto.LaborProductivityFactor <= 0 ? 1.0 : dto.LaborProductivityFactor
            };
        }

        private static TEnum ParseEnum<TEnum>(string text, TEnum fallback) where TEnum : struct
        {
            return Enum.TryParse<TEnum>(text, true, out var value) ? value : fallback;
        }
    }
}

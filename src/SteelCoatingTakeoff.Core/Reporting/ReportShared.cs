using SteelCoatingTakeoff.Core.Model;

namespace SteelCoatingTakeoff.Core.Reporting
{
    /// <summary>Layout helpers shared by the takeoff (cost) and supplier reports.</summary>
    internal static class ReportShared
    {
        /// <summary>Member label for a row: shape/size, with plate width when it applies.</summary>
        public static string MemberLabel(TakeoffLine line)
        {
            var shape = line.Shape?.Display ?? "(shape)";
            if (line.Family != null && line.Family.IsPlate && line.PlateWidthInches > 0)
                return $"{shape} @ {line.PlateWidthInches:0.##}\" wide";
            return shape;
        }

        /// <summary>Truncate to fit a column, with an ellipsis so a clipped name is obvious.</summary>
        public static string Fit(string text, double maxWidth, double size, PdfFont font)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (PdfWriter.Width(text, size, font) <= maxWidth) return text;
            var keep = text;
            while (keep.Length > 1 && PdfWriter.Width(keep + "...", size, font) > maxWidth)
                keep = keep.Substring(0, keep.Length - 1);
            return keep + "...";
        }
    }
}

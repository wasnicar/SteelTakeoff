using System;
using System.Collections.Generic;
using System.Linq;
using SteelCoatingTakeoff.Core.Model;
using SteelCoatingTakeoff.Core.Sage;

namespace SteelCoatingTakeoff.Core.Reporting
{
    /// <summary>
    /// The supplier report: one row per member with its takeoff quantities and variables
    /// — shape, coating, fire rating, coats, linear feet, SF/LF, area — and a blank WFT
    /// column for the supplier to fill in. It carries NO wage, productivity or dollar
    /// figures; the whole point is to hand a coatings supplier the member information
    /// (fire rating especially) they need to specify the wet film thickness, without
    /// exposing internal pricing.
    /// </summary>
    public static class SupplierReport
    {
        private const double Margin = 36.0;
        private const double Right = PdfWriter.PageWidth - Margin;   // 756
        private const double RowHeight = 16.0;
        private const double BodySize = 9.0;

        // Text columns give a left edge; numeric columns a right edge.
        private const double XNum = 48.0;
        private const double XMember = 56.0;
        private const double XMemberWidth = 100.0;
        private const double XType = 162.0;
        private const double XTypeWidth = 72.0;
        private const double XCoating = 240.0;
        private const double XFire = 320.0;
        private const double XFireWidth = 62.0;
        private const double XCoats = 392.0;
        private const double XQty = 436.0;      // member count (same length taken off n times)
        private const double XLf = 492.0;
        private const double XSfLf = 556.0;
        private const double XArea = 610.0;
        private const double XDft = 680.0;      // supplier-provided DFT (fill-in when blank)
        private const double XDftLeft = 636.0;  // where the DFT write-in underline starts
        private const double XWft = Right;      // computed WFT, for reference

        public static void Write(
            string path,
            IEnumerable<TakeoffLine> lines,
            SageSettings settings,
            string estimateName,
            DateTime generatedAt)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            Build(lines, settings, estimateName, generatedAt).Save(path);
        }

        public static PdfWriter Build(
            IEnumerable<TakeoffLine> lines,
            SageSettings settings,
            string estimateName,
            DateTime generatedAt)
        {
            settings = settings ?? new SageSettings();
            var rows = (lines ?? Enumerable.Empty<TakeoffLine>())
                       .Where(l => l?.Shape != null)
                       .ToList();

            var pdf = new PdfWriter();
            var y = Heading(pdf, estimateName, generatedAt);

            double totalLf = 0, totalArea = 0;
            var page = 1;

            foreach (var line in rows)
            {
                if (y < Margin + 72.0)
                {
                    Footer(pdf, page);
                    pdf.NewPage();
                    page++;
                    y = Heading(pdf, estimateName, generatedAt);
                }

                var multiplier = TakeoffCalculator.Multiplier(line);
                var area = TakeoffCalculator.TotalAreaSquareFeet(line);    // × member count
                var extLf = line.LinearFeet * multiplier;
                var intumescent = line.Coating == CoatingType.Intumescent;
                totalLf += extLf;
                totalArea += area;

                var index = rows.IndexOf(line) + 1;
                var type = MemberClassification.ShortLabel(line.MemberType, line.Support);
                pdf.TextRight(XNum, y, index.ToString(), BodySize, PdfFont.Mono, 0.45);
                pdf.Text(XMember, y, ReportShared.Fit(ReportShared.MemberLabel(line), XMemberWidth, BodySize, PdfFont.Regular), BodySize);
                pdf.Text(XType, y, ReportShared.Fit(type.Length == 0 ? "-" : type, XTypeWidth, BodySize, PdfFont.Regular), BodySize);
                pdf.Text(XCoating, y, intumescent ? "Intumescent" : "Standard", BodySize, PdfFont.Regular,
                         intumescent ? 0.35 : 0.0);
                pdf.Text(XFire, y, ReportShared.Fit(string.IsNullOrWhiteSpace(line.FireRating) ? "-" : line.FireRating.Trim(),
                         XFireWidth, BodySize, PdfFont.Regular), BodySize);
                pdf.TextRight(XCoats, y, line.Coats > 0 ? line.Coats.ToString() : "1", BodySize);
                pdf.TextRight(XQty, y, multiplier.ToString("0.##"), BodySize);
                pdf.TextRight(XLf, y, extLf.ToString("N2"), BodySize);
                pdf.TextRight(XSfLf, y, TakeoffCalculator.SfPerFoot(line).ToString("0.####"), BodySize);
                pdf.TextRight(XArea, y, area.ToString("N2"), BodySize);

                // DFT is what the supplier provides: show any value already entered, else a
                // write-in underline. WFT is computed from it for reference (blank until DFT
                // is known). Standard members need neither.
                if (!intumescent)
                {
                    pdf.TextRight(XDft, y, "n/a", BodySize, PdfFont.Regular, 0.6);
                    pdf.TextRight(XWft, y, "n/a", BodySize, PdfFont.Regular, 0.6);
                }
                else if (line.DftMils > 0)
                {
                    var wft = TakeoffCalculator.WftFromDft(line.DftMils, settings.VolumeSolidsPercent);
                    pdf.TextRight(XDft, y, line.DftMils.ToString("0.##"), BodySize, PdfFont.Mono);
                    pdf.TextRight(XWft, y, wft.ToString("0.##"), BodySize, PdfFont.Mono, 0.35);
                }
                else
                {
                    pdf.Line(XDftLeft, y - 1.0, XDft, y - 1.0, 0.5, 0.6);   // supplier writes DFT here
                }

                y -= RowHeight;
            }

            if (rows.Count == 0)
            {
                pdf.Text(XMember, y, "No takeoff lines.", BodySize, PdfFont.Regular, 0.45);
                y -= RowHeight;
            }

            Totals(pdf, y, rows.Count, totalLf, totalArea);
            Footer(pdf, page);
            return pdf;
        }

        private static double Heading(PdfWriter pdf, string estimateName, DateTime at)
        {
            var y = PdfWriter.PageHeight - Margin - 6.0;

            pdf.Text(Margin, y, "Coating Supplier Request", 16.0, PdfFont.Bold);
            pdf.TextRight(Right, y, at.ToString("yyyy-MM-dd HH:mm"), 9.0, PdfFont.Mono, 0.40);
            y -= 17.0;

            var estimate = string.IsNullOrWhiteSpace(estimateName) ? "(project not named)" : estimateName;
            pdf.Text(Margin, y, "Project: " + estimate, 9.5, PdfFont.Regular, 0.25);
            y -= 13.0;

            pdf.Text(Margin, y,
                "Please specify the required DRY film thickness (DFT, mils) for each member below, using its fire rating. " +
                "WFT is computed for reference.",
                9.0, PdfFont.Regular, 0.35);
            y -= 16.0;

            pdf.Rect(Margin, y - 4.0, Right - Margin, 14.0, 0.92);
            pdf.Text(XMember, y, "Member", 8.5, PdfFont.Bold);
            pdf.Text(XType, y, "Type", 8.5, PdfFont.Bold);
            pdf.Text(XCoating, y, "Coating", 8.5, PdfFont.Bold);
            pdf.Text(XFire, y, "Fire Rating", 8.5, PdfFont.Bold);
            pdf.TextRight(XCoats, y, "Coats", 8.5, PdfFont.MonoBold);
            pdf.TextRight(XQty, y, "Qty", 8.5, PdfFont.MonoBold);
            pdf.TextRight(XLf, y, "LF", 8.5, PdfFont.MonoBold);
            pdf.TextRight(XSfLf, y, "SF/LF", 8.5, PdfFont.MonoBold);
            pdf.TextRight(XArea, y, "Area SF", 8.5, PdfFont.MonoBold);
            pdf.TextRight(XDft, y, "DFT", 8.5, PdfFont.MonoBold);
            pdf.TextRight(XWft, y, "WFT", 8.5, PdfFont.MonoBold);
            y -= 6.0;
            pdf.Line(Margin, y, Right, y, 0.7, 0.55);

            return y - 13.0;
        }

        private static void Totals(PdfWriter pdf, double y, int count, double lf, double area)
        {
            y -= 4.0;
            pdf.Line(Margin, y, Right, y, 0.7, 0.55);
            y -= 15.0;

            pdf.Text(XMember, y, $"TOTAL  ({count} member{(count == 1 ? "" : "s")})", 9.5, PdfFont.Bold);
            pdf.TextRight(XLf, y, lf.ToString("N2"), 9.0, PdfFont.MonoBold);
            pdf.TextRight(XArea, y, area.ToString("N2"), 9.0, PdfFont.MonoBold);
        }

        private static void Footer(PdfWriter pdf, int page)
        {
            pdf.Text(Margin, Margin - 8.0,
                     "Quantities only — no pricing. Qty is the member count; LF and Area are extended by it. " +
                     "Area = SF/LF x LF x coats x qty.",
                     7.5, PdfFont.Regular, 0.55);
            pdf.TextRight(Right, Margin - 8.0, "Page " + page, 7.5, PdfFont.Mono, 0.55);
        }
    }
}

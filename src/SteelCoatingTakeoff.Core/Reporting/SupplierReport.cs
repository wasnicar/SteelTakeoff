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
        private const double XNum = 52.0;
        private const double XMember = 62.0;
        private const double XMemberWidth = 150.0;
        private const double XCoating = 224.0;
        private const double XFire = 322.0;
        private const double XFireWidth = 96.0;
        private const double XCoats = 470.0;
        private const double XLf = 548.0;
        private const double XSfLf = 624.0;
        private const double XArea = 690.0;
        private const double XWft = Right;      // blank fill-in column
        private const double XWftLeft = 704.0;  // where the write-in underline starts

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

                var area = TakeoffCalculator.AreaSquareFeet(line);
                var intumescent = line.Coating == CoatingType.Intumescent;
                totalLf += line.LinearFeet;
                totalArea += area;

                var index = rows.IndexOf(line) + 1;
                pdf.TextRight(XNum, y, index.ToString(), BodySize, PdfFont.Mono, 0.45);
                pdf.Text(XMember, y, ReportShared.Fit(ReportShared.MemberLabel(line), XMemberWidth, BodySize, PdfFont.Regular), BodySize);
                pdf.Text(XCoating, y, intumescent ? "Intumescent" : "Standard", BodySize, PdfFont.Regular,
                         intumescent ? 0.35 : 0.0);
                pdf.Text(XFire, y, ReportShared.Fit(string.IsNullOrWhiteSpace(line.FireRating) ? "-" : line.FireRating.Trim(),
                         XFireWidth, BodySize, PdfFont.Regular), BodySize);
                pdf.TextRight(XCoats, y, line.Coats > 0 ? line.Coats.ToString() : "1", BodySize);
                pdf.TextRight(XLf, y, line.LinearFeet.ToString("N2"), BodySize);
                pdf.TextRight(XSfLf, y, TakeoffCalculator.SfPerFoot(line).ToString("0.####"), BodySize);
                pdf.TextRight(XArea, y, area.ToString("N2"), BodySize);

                // WFT is what the supplier provides: show any value already entered,
                // otherwise a write-in underline.
                if (intumescent && line.WftMils > 0)
                    pdf.TextRight(XWft, y, line.WftMils.ToString("0.##"), BodySize, PdfFont.Mono);
                else if (intumescent)
                    pdf.Line(XWftLeft, y - 1.0, XWft, y - 1.0, 0.5, 0.6);
                else
                    pdf.TextRight(XWft, y, "n/a", BodySize, PdfFont.Regular, 0.6);

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
                "Please specify the required wet film thickness (WFT, mils) for each member below, using its fire rating.",
                9.0, PdfFont.Regular, 0.35);
            y -= 16.0;

            pdf.Rect(Margin, y - 4.0, Right - Margin, 14.0, 0.92);
            pdf.Text(XMember, y, "Member", 8.5, PdfFont.Bold);
            pdf.Text(XCoating, y, "Coating", 8.5, PdfFont.Bold);
            pdf.Text(XFire, y, "Fire Rating", 8.5, PdfFont.Bold);
            pdf.TextRight(XCoats, y, "Coats", 8.5, PdfFont.MonoBold);
            pdf.TextRight(XLf, y, "LF", 8.5, PdfFont.MonoBold);
            pdf.TextRight(XSfLf, y, "SF/LF", 8.5, PdfFont.MonoBold);
            pdf.TextRight(XArea, y, "Area SF", 8.5, PdfFont.MonoBold);
            pdf.TextRight(XWft, y, "WFT (mils)", 8.5, PdfFont.MonoBold);
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
                     "Quantities only — no pricing. Area = SF/LF x linear feet x coats.",
                     7.5, PdfFont.Regular, 0.55);
            pdf.TextRight(Right, Margin - 8.0, "Page " + page, 7.5, PdfFont.Mono, 0.55);
        }
    }
}

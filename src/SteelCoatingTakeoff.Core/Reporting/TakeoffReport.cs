using System;
using System.Collections.Generic;
using System.Linq;
using SteelCoatingTakeoff.Core.Model;
using SteelCoatingTakeoff.Core.Sage;

namespace SteelCoatingTakeoff.Core.Reporting
{
    /// <summary>
    /// Renders the takeoff to a paginated PDF: one row per steel member with its
    /// description, quantities and labor, then totals.
    ///
    /// Every number comes from <see cref="TakeoffCalculator"/>, the same code that
    /// prices the grid and builds the Sage requests, so the report cannot disagree
    /// with what was sent.
    /// </summary>
    public static class TakeoffReport
    {
        private const double Margin = 36.0;
        private const double Right = PdfWriter.PageWidth - Margin;   // 756
        private const double RowHeight = 15.0;
        private const double BodySize = 8.5;

        // Right edge of each numeric column; text columns note their left edge.
        private const double XNum = 50.0;
        private const double XMember = 58.0;
        private const double XMemberWidth = 100.0;
        private const double XType = 166.0;
        private const double XTypeWidth = 62.0;
        private const double XCoating = 236.0;
        private const double XCoats = 300.0;
        private const double XWft = 340.0;
        private const double XLf = 392.0;
        private const double XSfLf = 446.0;
        private const double XArea = 508.0;
        private const double XWage = 566.0;
        private const double XProd = 622.0;
        private const double XRate = 678.0;
        private const double XLabor = Right;

        public static void Write(
            string path,
            IEnumerable<TakeoffLine> lines,
            SageSettings settings,
            string estimateName,
            DateTime generatedAt)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            var pdf = Build(lines, settings, estimateName, generatedAt);
            pdf.Save(path);
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

            var divisor = settings.WftLaborDivisor > 0 ? settings.WftLaborDivisor : 5.0;
            var pdf = new PdfWriter();

            var y = Heading(pdf, settings, estimateName, generatedAt, page: 1);

            double totalLf = 0, totalArea = 0, totalLabor = 0, intumescentArea = 0, standardArea = 0;
            var page = 1;

            foreach (var line in rows)
            {
                if (y < Margin + 96.0)      // leave room for the totals block
                {
                    Footer(pdf, page);
                    pdf.NewPage();
                    page++;
                    y = Heading(pdf, settings, estimateName, generatedAt, page);
                }

                var area = TakeoffCalculator.AreaSquareFeet(line);
                var labor = TakeoffCalculator.LaborAmount(line, divisor);
                var rate = TakeoffCalculator.LaborPricePerSquareFoot(line, divisor);
                var prod = TakeoffCalculator.EffectiveProductivity(line, divisor);
                var intumescent = line.Coating == CoatingType.Intumescent;

                totalLf += line.LinearFeet;
                totalArea += area;
                totalLabor += labor;
                if (intumescent) intumescentArea += area; else standardArea += area;

                var index = rows.IndexOf(line) + 1;
                var type = MemberClassification.ShortLabel(line.MemberType, line.Support);
                pdf.TextRight(XNum, y, index.ToString(), BodySize, PdfFont.Mono, 0.45);
                pdf.Text(XMember, y, ReportShared.Fit(ReportShared.MemberLabel(line), XMemberWidth, BodySize, PdfFont.Regular), BodySize);
                pdf.Text(XType, y, ReportShared.Fit(type, XTypeWidth, BodySize, PdfFont.Regular), BodySize, PdfFont.Regular, 0.2);
                pdf.Text(XCoating, y, intumescent ? "Intum." : "Std", BodySize, PdfFont.Regular,
                         intumescent ? 0.35 : 0.0);
                pdf.TextRight(XCoats, y, line.Coats > 0 ? line.Coats.ToString() : "1", BodySize);
                pdf.TextRight(XWft, y, intumescent && line.WftMils > 0 ? line.WftMils.ToString("0.##") : "-", BodySize);
                pdf.TextRight(XLf, y, line.LinearFeet.ToString("N2"), BodySize);
                pdf.TextRight(XSfLf, y, TakeoffCalculator.SfPerFoot(line).ToString("0.####"), BodySize);
                pdf.TextRight(XArea, y, area.ToString("N2"), BodySize);
                pdf.TextRight(XWage, y, line.WageRate > 0 ? line.WageRate.ToString("N2") : "-", BodySize);
                pdf.TextRight(XProd, y, prod > 0 ? prod.ToString("N2") : "-", BodySize);
                pdf.TextRight(XRate, y, rate > 0 ? rate.ToString("N4") : "-", BodySize);
                pdf.TextRight(XLabor, y, labor > 0 ? labor.ToString("N2") : "-", BodySize);

                y -= RowHeight;
            }

            if (rows.Count == 0)
            {
                pdf.Text(XMember, y, "No takeoff lines.", BodySize, PdfFont.Regular, 0.45);
                y -= RowHeight;
            }

            Totals(pdf, y, rows.Count, totalLf, totalArea, totalLabor, intumescentArea, standardArea);
            Footer(pdf, page);
            return pdf;
        }

        /// <summary>Page title, run parameters and the column header. Returns the first body row's baseline.</summary>
        private static double Heading(PdfWriter pdf, SageSettings settings, string estimateName, DateTime at, int page)
        {
            var y = PdfWriter.PageHeight - Margin - 6.0;

            pdf.Text(Margin, y, "Steel Coating Takeoff", 16.0, PdfFont.Bold);
            pdf.TextRight(Right, y, at.ToString("yyyy-MM-dd HH:mm"), 9.0, PdfFont.Mono, 0.40);
            y -= 17.0;

            var estimate = string.IsNullOrWhiteSpace(estimateName) ? "(no estimate selected)" : estimateName;
            pdf.Text(Margin, y, "Estimate: " + estimate, 9.5, PdfFont.Regular, 0.25);
            y -= 13.0;

            // Wage and productivity are per member, so they are columns rather than a
            // single figure here; this line states the rule that turns them into $/SF.
            var basis =
                "Labor is priced per member:  $/SF = wage / effective SF/hr.    " +
                $"Intumescent: productivity / (WFT / {settings.WftLaborDivisor:0.##})";
            pdf.Text(Margin, y, basis, 8.5, PdfFont.Regular, 0.40);
            y -= 16.0;

            // Column header band.
            pdf.Rect(Margin, y - 4.0, Right - Margin, 14.0, 0.92);
            pdf.Text(XMember, y, "Member", 8.5, PdfFont.Bold);
            pdf.Text(XType, y, "Type", 8.5, PdfFont.Bold);
            pdf.Text(XCoating, y, "Coat", 8.5, PdfFont.Bold);
            pdf.TextRight(XCoats, y, "Coats", 8.5, PdfFont.MonoBold);
            pdf.TextRight(XWft, y, "WFT", 8.5, PdfFont.MonoBold);
            pdf.TextRight(XLf, y, "LF", 8.5, PdfFont.MonoBold);
            pdf.TextRight(XSfLf, y, "SF/LF", 8.5, PdfFont.MonoBold);
            pdf.TextRight(XArea, y, "Area SF", 8.5, PdfFont.MonoBold);
            pdf.TextRight(XWage, y, "Wage/hr", 8.5, PdfFont.MonoBold);
            pdf.TextRight(XProd, y, "SF/hr", 8.5, PdfFont.MonoBold);
            pdf.TextRight(XRate, y, "$/SF", 8.5, PdfFont.MonoBold);
            pdf.TextRight(XLabor, y, "Labor $", 8.5, PdfFont.MonoBold);
            y -= 6.0;
            pdf.Line(Margin, y, Right, y, 0.7, 0.55);

            return y - 12.0;
        }

        private static void Totals(
            PdfWriter pdf, double y, int count,
            double lf, double area, double labor, double intumescentArea, double standardArea)
        {
            y -= 4.0;
            pdf.Line(Margin, y, Right, y, 0.7, 0.55);
            y -= 14.0;

            pdf.Text(XMember, y, $"TOTAL  ({count} line{(count == 1 ? "" : "s")})", 9.5, PdfFont.Bold);
            pdf.TextRight(XLf, y, lf.ToString("N2"), 9.0, PdfFont.MonoBold);
            pdf.TextRight(XArea, y, area.ToString("N2"), 9.0, PdfFont.MonoBold);
            pdf.TextRight(XLabor, y, labor.ToString("N2"), 9.0, PdfFont.MonoBold);
            y -= 18.0;

            pdf.Text(XMember, y, $"Intumescent area   {intumescentArea:N2} SF", 8.5, PdfFont.Regular, 0.30);
            y -= 12.0;
            pdf.Text(XMember, y, $"Standard area      {standardArea:N2} SF", 8.5, PdfFont.Regular, 0.30);
        }

        private static void Footer(PdfWriter pdf, int page)
        {
            pdf.Text(Margin, Margin - 8.0,
                     "Area = SF/LF x linear feet x coats. Labor = area x (wage / effective productivity).",
                     7.5, PdfFont.Regular, 0.55);
            pdf.TextRight(Right, Margin - 8.0, "Page " + page, 7.5, PdfFont.Mono, 0.55);
        }

    }
}

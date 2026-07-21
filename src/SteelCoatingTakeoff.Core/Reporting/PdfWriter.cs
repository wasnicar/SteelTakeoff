using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SteelCoatingTakeoff.Core.Reporting
{
    /// <summary>Base-14 PDF fonts — always present, so nothing has to be embedded.</summary>
    public enum PdfFont
    {
        /// <summary>Helvetica. Proportional; use for labels and prose.</summary>
        Regular = 0,
        /// <summary>Helvetica-Bold.</summary>
        Bold = 1,
        /// <summary>Courier. Monospaced, so numeric columns align exactly.</summary>
        Mono = 2,
        /// <summary>Courier-Bold.</summary>
        MonoBold = 3
    }

    /// <summary>
    /// A deliberately small PDF 1.4 writer — enough for a paginated table report and
    /// nothing more: text, lines and filled rectangles in the four base-14 fonts.
    ///
    /// Hand-rolled rather than taken from a package because the app ships with no NuGet
    /// dependencies (the installer stages an explicit file list), and because the
    /// alternative — printing to "Microsoft Print to PDF" — needs a print queue and a
    /// driver dialog, which rules out producing a report unattended or from a test.
    ///
    /// Everything is written in Latin-1 so one char is one byte and the xref offsets
    /// stay honest.
    /// </summary>
    public sealed class PdfWriter
    {
        /// <summary>US Letter, landscape — the table is wider than it is tall.</summary>
        public const double PageWidth = 792.0;
        public const double PageHeight = 612.0;

        private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

        private readonly List<StringBuilder> _pages = new List<StringBuilder>();
        private StringBuilder _current;

        public PdfWriter() => NewPage();

        public int PageCount => _pages.Count;

        public void NewPage()
        {
            _current = new StringBuilder();
            _pages.Add(_current);
        }

        /// <summary>Width of a string in points. Exact for the monospaced fonts.</summary>
        public static double Width(string text, double size, PdfFont font)
        {
            if (string.IsNullOrEmpty(text)) return 0.0;
            // Courier is 600/1000 em for every glyph. Helvetica varies; 0.52 is a close
            // enough average for truncation and for centring a heading.
            var perChar = (font == PdfFont.Mono || font == PdfFont.MonoBold) ? 0.600 : 0.520;
            return text.Length * perChar * size;
        }

        public void Text(double x, double y, string text, double size = 9.0, PdfFont font = PdfFont.Regular, double gray = 0.0)
        {
            if (string.IsNullOrEmpty(text)) return;
            _current.Append("BT ")
                    .Append(N(gray)).Append(" g ")
                    .Append('/').Append(FontResource(font)).Append(' ').Append(N(size)).Append(" Tf ")
                    .Append("1 0 0 1 ").Append(N(x)).Append(' ').Append(N(y)).Append(" Tm ")
                    .Append('(').Append(Escape(text)).Append(") Tj ET\n");
        }

        /// <summary>Draw <paramref name="text"/> ending at <paramref name="xRight"/>.</summary>
        public void TextRight(double xRight, double y, string text, double size = 9.0, PdfFont font = PdfFont.Mono, double gray = 0.0)
            => Text(xRight - Width(text, size, font), y, text, size, font, gray);

        public void Line(double x1, double y1, double x2, double y2, double width = 0.5, double gray = 0.0)
        {
            _current.Append("q ").Append(N(gray)).Append(" G ").Append(N(width)).Append(" w ")
                    .Append(N(x1)).Append(' ').Append(N(y1)).Append(" m ")
                    .Append(N(x2)).Append(' ').Append(N(y2)).Append(" l S Q\n");
        }

        public void Rect(double x, double y, double w, double h, double gray)
        {
            _current.Append("q ").Append(N(gray)).Append(" g ")
                    .Append(N(x)).Append(' ').Append(N(y)).Append(' ')
                    .Append(N(w)).Append(' ').Append(N(h)).Append(" re f Q\n");
        }

        public void Save(string path) => File.WriteAllBytes(path, Build());

        /// <summary>Serialize to PDF bytes. Object layout: catalog, pages, 4 fonts, then a page + content stream per page.</summary>
        public byte[] Build()
        {
            const int firstPageObj = 7;                 // 1 catalog, 2 pages, 3-6 fonts
            var pageObjIds = new List<int>();
            for (var i = 0; i < _pages.Count; i++) pageObjIds.Add(firstPageObj + i * 2);

            var body = new StringBuilder();
            var offsets = new Dictionary<int, int> { };
            var header = "%PDF-1.4\n";

            void Obj(int id, string content)
            {
                offsets[id] = header.Length + body.Length;
                body.Append(id).Append(" 0 obj\n").Append(content).Append("\nendobj\n");
            }

            Obj(1, "<< /Type /Catalog /Pages 2 0 R >>");

            var kids = new StringBuilder();
            foreach (var id in pageObjIds) kids.Append(id).Append(" 0 R ");
            Obj(2, $"<< /Type /Pages /Kids [ {kids.ToString().Trim()} ] /Count {_pages.Count} >>");

            Obj(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
            Obj(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
            Obj(5, "<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>");
            Obj(6, "<< /Type /Font /Subtype /Type1 /BaseFont /Courier-Bold /Encoding /WinAnsiEncoding >>");

            const string resources =
                "<< /Font << /F1 3 0 R /F2 4 0 R /F3 5 0 R /F4 6 0 R >> >>";

            for (var i = 0; i < _pages.Count; i++)
            {
                var pageId = pageObjIds[i];
                var streamId = pageId + 1;
                Obj(pageId,
                    $"<< /Type /Page /Parent 2 0 R /MediaBox [ 0 0 {N(PageWidth)} {N(PageHeight)} ] " +
                    $"/Resources {resources} /Contents {streamId} 0 R >>");

                var stream = _pages[i].ToString();
                Obj(streamId, $"<< /Length {Latin1.GetByteCount(stream)} >>\nstream\n{stream}endstream");
            }

            var maxId = _pages.Count > 0 ? pageObjIds[_pages.Count - 1] + 1 : 6;
            var xrefOffset = header.Length + body.Length;

            var xref = new StringBuilder();
            xref.Append("xref\n0 ").Append(maxId + 1).Append('\n');
            xref.Append("0000000000 65535 f \n");
            for (var id = 1; id <= maxId; id++)
            {
                var off = offsets.TryGetValue(id, out var o) ? o : 0;
                xref.Append(off.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
            }
            xref.Append("trailer\n<< /Size ").Append(maxId + 1).Append(" /Root 1 0 R >>\nstartxref\n")
                .Append(xrefOffset).Append("\n%%EOF\n");

            return Latin1.GetBytes(header + body + xref);
        }

        private static string FontResource(PdfFont font)
        {
            switch (font)
            {
                case PdfFont.Bold: return "F2";
                case PdfFont.Mono: return "F3";
                case PdfFont.MonoBold: return "F4";
                default: return "F1";
            }
        }

        private static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>
        /// PDF string escaping, plus folding the typographic characters the calculation
        /// text uses into WinAnsi-safe equivalents so nothing renders as a blank box.
        /// </summary>
        private static string Escape(string text)
        {
            var sb = new StringBuilder(text.Length + 8);
            foreach (var ch in text)
            {
                switch (ch)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '(': sb.Append("\\("); break;
                    case ')': sb.Append("\\)"); break;
                    case '×': sb.Append('x'); break;
                    case '÷': sb.Append('/'); break;
                    case '—': case '–': sb.Append('-'); break;
                    case '²': sb.Append('2'); break;
                    case '’': sb.Append('\''); break;
                    case '“': case '”': sb.Append('"'); break;
                    case '≥': sb.Append(">="); break;
                    case '⚠': sb.Append('!'); break;
                    default:
                        sb.Append(ch <= 0xFF ? ch : '?');
                        break;
                }
            }
            return sb.ToString();
        }
    }
}

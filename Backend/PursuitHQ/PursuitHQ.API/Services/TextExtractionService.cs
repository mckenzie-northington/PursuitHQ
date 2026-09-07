using System.Text;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// Text extraction for PDF, DOCX, PPTX, XLSX, and plain text.
    ///
    /// Deliberately extracts on demand rather than at upload time - most
    /// uploads are never previewed or turned into study tools, so doing this
    /// work for every file would be wasted.
    /// </summary>
    public class TextExtractionService : ITextExtractionService
    {
        private readonly ILogger<TextExtractionService> _logger;

        public TextExtractionService(ILogger<TextExtractionService> logger) => _logger = logger;

        public bool CanExtract(string fileName, string contentType)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext is ".pdf" or ".docx" or ".pptx" or ".xlsx" or ".txt" or ".md" or ".csv";
        }

        public async Task<TextExtractionResult> ExtractAsync(
            Stream file, string fileName, string contentType, int maxCharacters = 100_000,
            CancellationToken ct = default)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            // The libraries below need seekable streams, and a network or
            // storage stream may not be, so copy into memory first.
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            buffer.Position = 0;

            try
            {
                return ext switch
                {
                    ".pdf" => ExtractPdf(buffer, maxCharacters),
                    ".docx" => ExtractWord(buffer, maxCharacters),
                    ".pptx" => ExtractPowerPoint(buffer, maxCharacters),
                    ".xlsx" => ExtractExcel(buffer, maxCharacters),
                    _ => ExtractPlainText(buffer, maxCharacters)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Text extraction failed for {FileName}", fileName);
                return new TextExtractionResult(string.Empty, false, 0);
            }
        }

        private static TextExtractionResult ExtractPdf(Stream stream, int max)
        {
            var sb = new StringBuilder();
            var pages = 0;

            using var doc = PdfDocument.Open(stream);

            foreach (var page in doc.GetPages())
            {
                pages++;
                sb.AppendLine($"--- Page {page.Number} ---");
                sb.AppendLine(page.Text);
                sb.AppendLine();

                if (sb.Length >= max) return Truncate(sb, max, pages);
            }

            return new TextExtractionResult(sb.ToString().Trim(), false, pages);
        }

        private static TextExtractionResult ExtractWord(Stream stream, int max)
        {
            using var doc = WordprocessingDocument.Open(stream, false);
            var body = doc.MainDocumentPart?.Document?.Body;

            if (body is null) return new TextExtractionResult(string.Empty, false, 0);

            var sb = new StringBuilder();
            var paragraphs = 0;

            foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
            {
                var text = p.InnerText;
                if (string.IsNullOrWhiteSpace(text)) continue;

                paragraphs++;
                sb.AppendLine(text);

                if (sb.Length >= max) return Truncate(sb, max, paragraphs);
            }

            return new TextExtractionResult(sb.ToString().Trim(), false, paragraphs);
        }

        private static TextExtractionResult ExtractPowerPoint(Stream stream, int max)
        {
            using var doc = PresentationDocument.Open(stream, false);
            var presentation = doc.PresentationPart;

            if (presentation?.Presentation?.SlideIdList is null)
            {
                return new TextExtractionResult(string.Empty, false, 0);
            }

            var sb = new StringBuilder();
            var slideNumber = 0;

            foreach (var slideId in presentation.Presentation.SlideIdList
                         .Elements<DocumentFormat.OpenXml.Presentation.SlideId>())
            {
                if (slideId.RelationshipId?.Value is null) continue;
                if (presentation.GetPartById(slideId.RelationshipId!.Value!) is not SlidePart slidePart) continue;

                slideNumber++;
                sb.AppendLine($"--- Slide {slideNumber} ---");

                // Each text shape on the slide becomes its own line, which keeps
                // titles and bullets readable rather than one run-on string.
                foreach (var shape in slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Paragraph>())
                {
                    var text = shape.InnerText;
                    if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
                }

                // Speaker notes are often where the real explanation lives.
                var notes = slidePart.NotesSlidePart?.NotesSlide?.InnerText;
                if (!string.IsNullOrWhiteSpace(notes))
                {
                    sb.AppendLine($"[Notes] {notes}");
                }

                sb.AppendLine();

                if (sb.Length >= max) return Truncate(sb, max, slideNumber);
            }

            return new TextExtractionResult(sb.ToString().Trim(), false, slideNumber);
        }

        private static TextExtractionResult ExtractExcel(Stream stream, int max)
        {
            using var doc = SpreadsheetDocument.Open(stream, false);
            var workbook = doc.WorkbookPart;

            if (workbook is null) return new TextExtractionResult(string.Empty, false, 0);

            var sharedStrings = workbook.SharedStringTablePart?.SharedStringTable;
            var sb = new StringBuilder();
            var sheets = 0;

            foreach (var sheet in workbook.Workbook.Sheets?
                         .Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>()
                     ?? Enumerable.Empty<DocumentFormat.OpenXml.Spreadsheet.Sheet>())
            {
                if (sheet.Id?.Value is null) continue;
                if (workbook.GetPartById(sheet.Id!.Value!) is not WorksheetPart wsPart) continue;

                sheets++;
                sb.AppendLine($"--- Sheet: {sheet.Name} ---");

                foreach (var row in wsPart.Worksheet.Descendants<DocumentFormat.OpenXml.Spreadsheet.Row>())
                {
                    var cells = row.Elements<DocumentFormat.OpenXml.Spreadsheet.Cell>()
                        .Select(c => CellText(c, sharedStrings))
                        .Where(v => !string.IsNullOrWhiteSpace(v));

                    var line = string.Join("\t", cells);
                    if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine(line);

                    if (sb.Length >= max) return Truncate(sb, max, sheets);
                }

                sb.AppendLine();
            }

            return new TextExtractionResult(sb.ToString().Trim(), false, sheets);
        }

        private static string CellText(
            DocumentFormat.OpenXml.Spreadsheet.Cell cell,
            DocumentFormat.OpenXml.Spreadsheet.SharedStringTable? sharedStrings)
        {
            var value = cell.CellValue?.InnerText ?? string.Empty;

            // Text cells store an index into a shared string table rather than
            // the text itself, so it has to be looked up.
            if (cell.DataType?.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.SharedString
                && sharedStrings is not null
                && int.TryParse(value, out var index)
                && index >= 0 && index < sharedStrings.ChildElements.Count)
            {
                return sharedStrings.ChildElements[index].InnerText;
            }

            return value;
        }

        private static TextExtractionResult ExtractPlainText(Stream stream, int max)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = reader.ReadToEnd();

            if (text.Length > max)
            {
                return new TextExtractionResult(text[..max], true, 1);
            }

            return new TextExtractionResult(text, false, 1);
        }

        private static TextExtractionResult Truncate(StringBuilder sb, int max, int sections)
        {
            var text = sb.ToString();
            return new TextExtractionResult(
                text.Length > max ? text[..max] : text, true, sections);
        }
    }
}

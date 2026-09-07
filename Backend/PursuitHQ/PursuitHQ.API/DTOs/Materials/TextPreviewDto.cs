namespace PursuitHQ.API.DTOs.Materials
{
    public class TextPreviewDto
    {
        public string FileName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;

        /// <summary>True when the document was longer than the extraction limit.</summary>
        public bool Truncated { get; set; }

        /// <summary>Pages, slides, sheets, or paragraphs, depending on the format.</summary>
        public int SectionCount { get; set; }

        /// <summary>What SectionCount counts, e.g. "slides".</summary>
        public string SectionLabel { get; set; } = "sections";
    }
}

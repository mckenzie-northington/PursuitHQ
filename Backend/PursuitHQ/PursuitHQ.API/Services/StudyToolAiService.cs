using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// Prompts Gemini for study tools and decides what of the answer is usable.
    /// </summary>
    public class StudyToolAiService : IStudyToolAiService
    {
        /// <summary>
        /// Long enough for a lecture's worth of slides or a chapter, short
        /// enough to stay well inside the model's context and finish quickly.
        /// </summary>
        public const int MaxSourceCharacters = 40_000;

        private const int MaxFrontLength = 300;
        private const int MaxBackLength = 800;

        private readonly IAiService _ai;
        private readonly AiOptions _options;
        private readonly ILogger<StudyToolAiService> _logger;

        public StudyToolAiService(
            IAiService ai, IOptions<AiOptions> options, ILogger<StudyToolAiService> logger)
        {
            _ai = ai;
            _options = options.Value;
            _logger = logger;
        }

        public bool IsConfigured => _ai.IsConfigured;

        public async Task<List<GeneratedFlashcard>> GenerateFlashcardsAsync(
            string sourceText, string topic, int count, CancellationToken ct = default)
        {
            var trimmed = sourceText.Length > MaxSourceCharacters
                ? sourceText[..MaxSourceCharacters]
                : sourceText;

            var prompt = BuildFlashcardPrompt(trimmed, topic, count);

            string raw;
            try
            {
                raw = await _ai.CompleteAsync(prompt, _options.StudyToolModel, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // A timeout, not the student navigating away.
                throw new StudyToolException(
                    "The AI took too long to answer. Try again, or pick a shorter document.");
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new StudyToolException("The AI returned an empty answer. Try again.");
            }

            var cards = ParseFlashcards(raw);

            if (cards.Count == 0)
            {
                _logger.LogWarning(
                    "Flashcard generation produced nothing usable. First 500 chars: {Raw}",
                    raw.Length > 500 ? raw[..500] : raw);

                throw new StudyToolException(
                    "The AI's answer could not be read as flashcards. This usually means the "
                    + "document had little teachable content in it. Try a different file, or "
                    + "one with more written explanation in it.");
            }

            return cards;
        }

        /// <summary>
        /// The prompt.
        ///
        /// Built by concatenation rather than an interpolated raw string
        /// because the JSON example below is full of braces, which an
        /// interpolated string would try to read as holes.
        /// </summary>
        private static string BuildFlashcardPrompt(string text, string topic, int count)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are helping a college student study for a course.");
            sb.AppendLine();
            sb.Append("Write ").Append(count).AppendLine(" flashcards from the course material below.");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- Each card tests exactly one idea, and stands on its own without the others.");
            sb.AppendLine("- The front is a question or a term to define. Keep it under 200 characters.");
            sb.AppendLine("- The back is the answer, complete enough to actually learn from, under 500 characters.");
            sb.AppendLine("- Use only what is in the material. Do not add outside facts and do not invent examples.");
            sb.AppendLine("- Prefer ideas, definitions, causes, and comparisons over trivia.");
            sb.AppendLine("- Skip anything administrative: syllabus dates, office hours, page or slide numbers,");
            sb.AppendLine("  headings with no content under them, and the instructor's name.");
            sb.AppendLine("- Do not number the cards or repeat the same idea twice.");
            sb.AppendLine("- If the material does not contain enough to teach, return fewer cards rather than padding.");
            sb.AppendLine();
            sb.AppendLine("Return ONLY a JSON array. No explanation before or after it, no code fences.");
            sb.AppendLine("Each element must be an object with exactly the keys \"front\" and \"back\":");
            sb.AppendLine("[{\"front\": \"What is a binary search tree?\", \"back\": \"A binary tree where ...\"}]");
            sb.AppendLine();
            sb.Append("COURSE MATERIAL (topic: ").Append(topic).AppendLine("):");
            sb.AppendLine("---");
            sb.AppendLine(text);
            sb.AppendLine("---");

            return sb.ToString();
        }

        /// <summary>
        /// Reads the model's answer into cards, keeping only what is usable.
        ///
        /// Models are told to return bare JSON and quite often return it
        /// wrapped in prose or code fences anyway, so the array is located
        /// rather than assumed. Anything malformed is dropped rather than
        /// failing the whole batch - one bad card should not cost the student
        /// the other nineteen.
        /// </summary>
        private List<GeneratedFlashcard> ParseFlashcards(string raw)
        {
            var json = ExtractJsonArray(raw);
            if (json is null) return new List<GeneratedFlashcard>();

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Flashcard JSON did not parse");
                return new List<GeneratedFlashcard>();
            }

            using (doc)
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return new List<GeneratedFlashcard>();
                }

                var cards = new List<GeneratedFlashcard>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.ValueKind != JsonValueKind.Object) continue;

                    var front = ReadString(element, "front");
                    var back = ReadString(element, "back");

                    if (string.IsNullOrWhiteSpace(front) || string.IsNullOrWhiteSpace(back)) continue;

                    front = Clip(front, MaxFrontLength);
                    back = Clip(back, MaxBackLength);

                    // Same question twice is worse than one fewer card.
                    if (!seen.Add(front)) continue;

                    cards.Add(new GeneratedFlashcard(front, back));
                }

                return cards;
            }
        }

        private static string? ReadString(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()?.Trim()
                : null;

        private static string Clip(string value, int max) =>
            value.Length <= max ? value : value[..max].TrimEnd() + "...";

        /// <summary>
        /// Finds the JSON array inside whatever the model actually sent.
        ///
        /// Scans for the first '[' and then tracks bracket depth to its match,
        /// ignoring brackets that appear inside strings. Taking the last ']' in
        /// the response instead would swallow any prose the model added after
        /// the array.
        /// </summary>
        private static string? ExtractJsonArray(string raw)
        {
            var start = raw.IndexOf('[');
            if (start < 0) return null;

            var depth = 0;
            var inString = false;
            var escaped = false;

            for (var i = start; i < raw.Length; i++)
            {
                var c = raw[i];

                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inString = true;
                        break;
                    case '[':
                        depth++;
                        break;
                    case ']':
                        depth--;
                        if (depth == 0) return raw[start..(i + 1)];
                        break;
                }
            }

            return null;
        }
    }
}

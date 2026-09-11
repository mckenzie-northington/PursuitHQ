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

        // ---------- practice tests ----------

        public async Task<GeneratedTest> GenerateTestAsync(
            string sourceText, string topic, int count, string? styleRequest,
            CancellationToken ct = default)
        {
            var trimmed = sourceText.Length > MaxSourceCharacters
                ? sourceText[..MaxSourceCharacters]
                : sourceText;

            var raw = await CallAsync(BuildTestPrompt(trimmed, topic, count, styleRequest), ct);
            var questions = ParseQuestions(raw);

            if (questions.Count == 0)
            {
                _logger.LogWarning(
                    "Test generation produced nothing usable. First 500 chars: {Raw}",
                    raw.Length > 500 ? raw[..500] : raw);

                throw new StudyToolException(
                    "The AI's answer could not be read as a test. Try a different file, or one "
                    + "with more written explanation in it.");
            }

            return new GeneratedTest($"Practice test: {topic}", questions);
        }

        public async Task<IReadOnlyList<GradedAnswer>> GradeWrittenAnswersAsync(
            IReadOnlyList<AnswerToGrade> answers, CancellationToken ct = default)
        {
            if (answers.Count == 0) return Array.Empty<GradedAnswer>();

            var raw = await CallAsync(BuildGradingPrompt(answers), ct);
            var graded = ParseGrades(raw);

            // Anything the model failed to return a verdict for is marked
            // wrong with an explanation, never silently correct. Guessing in
            // the student's favour would quietly inflate every score.
            var byIndex = graded.ToDictionary(g => g.Index);

            return answers
                .Select(a => byIndex.TryGetValue(a.Index, out var g)
                    ? g
                    : new GradedAnswer(a.Index, false,
                        "This answer could not be graded automatically. Compare it with the key yourself."))
                .ToList();
        }

        private static string BuildTestPrompt(string text, string topic, int count, string? style)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are writing a practice test for a college student.");
            sb.AppendLine();
            sb.Append("Write ").Append(count).AppendLine(" questions from the course material below.");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(style))
            {
                sb.AppendLine("The student asked for this kind of test:");
                sb.Append("  ").AppendLine(style.Trim());
                sb.AppendLine("Follow that. If they named question types, use only those types.");
            }
            else
            {
                sb.AppendLine("Use a mix of multiple choice, true/false, and short answer.");
            }

            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- Test understanding, not recall of trivia. Ask why and how, not just what.");
            sb.AppendLine("- Use only what is in the material. Do not add outside facts.");
            sb.AppendLine("- Multiple choice: exactly 4 options, one clearly correct. The wrong ones must");
            sb.AppendLine("  be plausible to someone who half-understands - never filler.");
            sb.AppendLine("- The answer must be the full text of the correct option, not a letter.");
            sb.AppendLine("- True/false: the answer is exactly \"true\" or \"false\".");
            sb.AppendLine("- Short answer: answerable in one to three sentences. The answer is a model");
            sb.AppendLine("  answer that a correct response should match in meaning, not in wording.");
            sb.AppendLine("- Give every question a one-sentence explanation of why the answer is right.");
            sb.AppendLine();
            sb.AppendLine("Return ONLY a JSON array. No prose around it, no code fences.");
            sb.AppendLine("Each element is an object with these keys:");
            sb.AppendLine("  type        one of \"multiple_choice\", \"true_false\", \"short_answer\"");
            sb.AppendLine("  question    the question text");
            sb.AppendLine("  options     array of 4 strings, for multiple_choice only");
            sb.AppendLine("  answer      the correct answer");
            sb.AppendLine("  explanation one sentence");
            sb.AppendLine();
            sb.Append("COURSE MATERIAL (topic: ").Append(topic).AppendLine("):");
            sb.AppendLine("---");
            sb.AppendLine(text);
            sb.AppendLine("---");

            return sb.ToString();
        }

        private static string BuildGradingPrompt(IReadOnlyList<AnswerToGrade> answers)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are grading a student's written test answers.");
            sb.AppendLine();
            sb.AppendLine("Mark an answer correct when it shows the student understood the idea, even if");
            sb.AppendLine("the wording is nothing like the model answer. Do not require their phrasing to");
            sb.AppendLine("match. Mark it wrong when a key part is missing, backwards, or wrong.");
            sb.AppendLine("A blank or one-word answer that does not address the question is wrong.");
            sb.AppendLine();
            sb.AppendLine("For each answer, give one short sentence of feedback. When it is wrong, say what");
            sb.AppendLine("was missing rather than just restating the model answer.");
            sb.AppendLine();
            sb.AppendLine("Return ONLY a JSON array, one object per answer, no prose and no code fences:");
            sb.AppendLine("  index     the number given below");
            sb.AppendLine("  correct   true or false");
            sb.AppendLine("  feedback  one sentence");
            sb.AppendLine();

            foreach (var answer in answers)
            {
                sb.Append("--- ").Append(answer.Index).AppendLine(" ---");
                sb.Append("QUESTION: ").AppendLine(Clip(answer.Question, 1_000));
                sb.Append("MODEL ANSWER: ").AppendLine(Clip(answer.CorrectAnswer, 1_000));
                sb.Append("STUDENT ANSWER: ").AppendLine(Clip(answer.GivenAnswer, 2_000));
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Reads generated questions out of raw model output.
        ///
        /// Public and static because the study chat produces the same JSON in
        /// its own reply, and saving that must go through exactly this code -
        /// two parsers would drift and a test would behave differently
        /// depending on where it came from.
        /// </summary>
        public static List<GeneratedQuestion> ParseGeneratedQuestions(string raw) =>
            ParseQuestionsCore(raw, null);

        private List<GeneratedQuestion> ParseQuestions(string raw) =>
            ParseQuestionsCore(raw, _logger);

        private static List<GeneratedQuestion> ParseQuestionsCore(string raw, ILogger? logger)
        {
            var json = ExtractJsonArray(raw);
            if (json is null) return new List<GeneratedQuestion>();

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                logger?.LogWarning(ex, "Question JSON did not parse");
                return new List<GeneratedQuestion>();
            }

            using var _ = doc;

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new List<GeneratedQuestion>();
            }

            var questions = new List<GeneratedQuestion>();

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object) continue;

                var type = NormaliseType(ReadString(element, "type"));
                var text = ReadString(element, "question");
                var answer = ReadString(element, "answer");

                if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(answer)) continue;

                List<string>? options = null;

                if (type == "multiple_choice")
                {
                    options = ReadStringArray(element, "options");

                    // A multiple choice question whose answer is not among its
                    // own options is ungradeable, so it is dropped rather than
                    // shown as a question nobody can get right.
                    if (options is null || options.Count < 2) continue;
                    if (!options.Any(o => string.Equals(o, answer, StringComparison.OrdinalIgnoreCase))) continue;
                }
                else if (type == "true_false")
                {
                    var normalised = answer.Trim().ToLowerInvariant();
                    if (normalised is not ("true" or "false")) continue;
                    answer = normalised;
                }

                questions.Add(new GeneratedQuestion(
                    type,
                    Clip(text, 1_000),
                    options,
                    Clip(answer, 2_000),
                    ReadString(element, "explanation") is string e ? Clip(e, 1_000) : null));
            }

            return questions;
        }

        private List<GradedAnswer> ParseGrades(string raw)
        {
            var json = ExtractJsonArray(raw);
            if (json is null) return new List<GradedAnswer>();

            using var doc = TryParse(json);
            if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new List<GradedAnswer>();
            }

            var grades = new List<GradedAnswer>();

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object) continue;
                if (!element.TryGetProperty("index", out var index)) continue;
                if (!index.TryGetInt32(out var i)) continue;

                var correct = element.TryGetProperty("correct", out var c)
                              && c.ValueKind == JsonValueKind.True;

                grades.Add(new GradedAnswer(i, correct, ReadString(element, "feedback")));
            }

            return grades;
        }

        /// <summary>The three types we understand, however the model spelled them.</summary>
        private static string NormaliseType(string? type)
        {
            var value = (type ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", "_").Replace("-", "_");

            return value switch
            {
                "true_false" or "truefalse" or "tf" or "boolean" => "true_false",
                "short_answer" or "shortanswer" or "free_response" or "freeresponse"
                    or "written" or "essay" or "open" or "open_ended" => "short_answer",
                _ => "multiple_choice"
            };
        }

        private static List<string>? ReadStringArray(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value)) return null;
            if (value.ValueKind != JsonValueKind.Array) return null;

            var items = value.EnumerateArray()
                .Where(v => v.ValueKind == JsonValueKind.String)
                .Select(v => v.GetString()!.Trim())
                .Where(v => v.Length > 0)
                .ToList();

            return items.Count == 0 ? null : items;
        }

        private JsonDocument? TryParse(string json)
        {
            try
            {
                return JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Model returned JSON that did not parse");
                return null;
            }
        }

        /// <summary>One call to the model, with the timeout turned into plain English.</summary>
        private async Task<string> CallAsync(string prompt, CancellationToken ct)
        {
            string raw;
            try
            {
                raw = await _ai.CompleteAsync(prompt, _options.StudyToolModel, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new StudyToolException(
                    "The AI took too long to answer. Try again, or pick a shorter document.");
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new StudyToolException("The AI returned an empty answer. Try again.");
            }

            return raw;
        }

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

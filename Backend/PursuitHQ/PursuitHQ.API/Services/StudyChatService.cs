using System.Text;
using Microsoft.Extensions.Options;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// Turns a question plus the student's own material into an answer.
    /// </summary>
    public class StudyChatService : IStudyChatService
    {
        /// <summary>How much source material to put in front of the model.</summary>
        public const int MaxSourceCharacters = 30_000;

        /// <summary>How many earlier turns to carry. Enough to follow a thread,
        /// short enough that the prompt does not grow without limit.</summary>
        public const int MaxHistoryTurns = 10;

        // The markers the model wraps a study guide in.
        //
        // Delimiters rather than JSON: a study guide is long markdown full of
        // quotes, newlines, and backslashes, and asking a model to escape all
        // of that correctly inside a JSON string is where these things break.
        // Finding two markers in a text stream cannot fail the same way.
        private const string GuideStart = "===GUIDE===";
        private const string GuideEnd = "===END GUIDE===";
        private const string TitlePrefix = "TITLE:";

        private readonly IAiService _ai;
        private readonly AiOptions _options;

        public StudyChatService(IAiService ai, IOptions<AiOptions> options)
        {
            _ai = ai;
            _options = options.Value;
        }

        public bool IsConfigured => _ai.IsConfigured;

        public async Task<StudyChatReply> AskAsync(
            StudyChatRequest request, CancellationToken ct = default)
        {
            var prompt = BuildPrompt(request);

            string raw;
            try
            {
                raw = await _ai.CompleteAsync(prompt, _options.StudyToolModel, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new StudyToolException(
                    "The AI took too long to answer. Try asking for something smaller.");
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new StudyToolException("The AI returned an empty answer. Try again.");
            }

            return Parse(raw);
        }

        private static string BuildPrompt(StudyChatRequest request)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are a study tutor helping one college student with a specific course.");
            sb.Append("The course is: ").AppendLine(request.CourseName);
            sb.AppendLine();

            sb.AppendLine("How to answer:");
            sb.AppendLine("- Be direct and concrete. Explain, do not just summarise.");
            sb.AppendLine("- Work from the course material below whenever it covers the question.");
            sb.AppendLine("- If the material does not cover something, say so plainly, then answer");
            sb.AppendLine("  from general knowledge and make clear that is what you are doing.");
            sb.AppendLine("- Never invent details about this specific course: its dates, its grading,");
            sb.AppendLine("  what the professor said, or what is on the exam.");
            sb.AppendLine("- Keep normal answers short. Length is not helpfulness.");
            sb.AppendLine();

            sb.AppendLine("If, and only if, the student asks for a study guide, a summary sheet, review");
            sb.AppendLine("notes, or something else they would want to keep and read later, write it");
            sb.AppendLine("between these exact markers, after a one-line introduction:");
            sb.AppendLine();
            sb.Append(GuideStart).AppendLine();
            sb.Append(TitlePrefix).AppendLine(" A short title for the guide");
            sb.AppendLine("(the guide itself, in markdown, with ## headings)");
            sb.Append(GuideEnd).AppendLine();
            sb.AppendLine();
            sb.AppendLine("Use those markers only for a document worth keeping. A normal answer, an");
            sb.AppendLine("explanation, or a few practice questions asked in passing are just text.");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(request.SourceText))
            {
                sb.AppendLine("COURSE MATERIAL:");
                sb.AppendLine("---");
                sb.AppendLine(Clip(request.SourceText, MaxSourceCharacters));
                sb.AppendLine("---");
            }
            else
            {
                sb.AppendLine("No course material has been attached to this conversation. Answer from");
                sb.AppendLine("general knowledge, and mention that attaching a file would let you work");
                sb.AppendLine("from what is actually being taught.");
            }

            sb.AppendLine();

            if (request.History.Count > 0)
            {
                sb.AppendLine("CONVERSATION SO FAR:");
                foreach (var turn in request.History)
                {
                    sb.Append(turn.Role == StudyMessageRole.User ? "Student: " : "Tutor: ");
                    sb.AppendLine(Clip(turn.Content, 2_000));
                }
                sb.AppendLine();
            }

            sb.AppendLine("STUDENT'S QUESTION:");
            sb.AppendLine(request.Question);

            return sb.ToString();
        }

        /// <summary>
        /// Splits the reply into the conversational part and the guide, if the
        /// model produced one.
        ///
        /// A missing or unterminated end marker is treated as "no guide" rather
        /// than an error - the student still gets the answer, they just do not
        /// get a Save button.
        /// </summary>
        private static StudyChatReply Parse(string raw)
        {
            var start = raw.IndexOf(GuideStart, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return new StudyChatReply(raw.Trim(), null);

            var bodyStart = start + GuideStart.Length;
            var end = raw.IndexOf(GuideEnd, bodyStart, StringComparison.OrdinalIgnoreCase);

            var body = end < 0 ? raw[bodyStart..] : raw[bodyStart..end];

            var (title, content) = SplitTitle(body);
            if (string.IsNullOrWhiteSpace(content)) return new StudyChatReply(raw.Trim(), null);

            // Whatever the model said before the guide is the reply; anything
            // after the end marker is dropped as trailing chatter.
            var intro = raw[..start].Trim();
            if (intro.Length == 0) intro = "Here is the study guide.";

            return new StudyChatReply(
                intro,
                new StudyArtifact(StudyArtifactKind.StudyGuide, title, content.Trim()));
        }

        private static (string Title, string Content) SplitTitle(string body)
        {
            var trimmed = body.TrimStart('\r', '\n', ' ');

            if (!trimmed.StartsWith(TitlePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return ("Study guide", trimmed);
            }

            var lineEnd = trimmed.IndexOf('\n');
            if (lineEnd < 0) return ("Study guide", string.Empty);

            var title = trimmed[TitlePrefix.Length..lineEnd].Trim();

            return (string.IsNullOrWhiteSpace(title) ? "Study guide" : Clip(title, 200),
                    trimmed[(lineEnd + 1)..]);
        }

        private static string Clip(string value, int max) =>
            value.Length <= max ? value : value[..max];
    }
}

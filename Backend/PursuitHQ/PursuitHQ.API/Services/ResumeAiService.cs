using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PursuitHQ.API.DTOs.Resumes;

namespace PursuitHQ.API.Services
{
    public class ResumeAiService : IResumeAiService
    {
        /// <summary>A resume that runs past this is not a resume.</summary>
        public const int MaxResumeCharacters = 20_000;

        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IAiService _ai;
        private readonly AiOptions _options;
        private readonly ILogger<ResumeAiService> _logger;

        public ResumeAiService(
            IAiService ai, IOptions<AiOptions> options, ILogger<ResumeAiService> logger)
        {
            _ai = ai;
            _options = options.Value;
            _logger = logger;
        }

        public bool IsConfigured => _ai.IsConfigured;

        // ---------- reading an uploaded resume ----------

        public async Task<ResumeContentDto> ParseAsync(
            string resumeText, CancellationToken ct = default)
        {
            var text = resumeText.Length > MaxResumeCharacters
                ? resumeText[..MaxResumeCharacters]
                : resumeText;

            var raw = await CallAsync(BuildParsePrompt(text), _options.ResumeModel, ct);
            var json = ExtractJsonObject(raw);

            if (json is null)
            {
                throw new StudyToolException(
                    "The file was read, but its contents could not be sorted into sections. "
                    + "You can still start a blank resume and paste the parts in.");
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<ResumeContentDto>(json, Json);

                if (parsed is null) throw new JsonException("null");

                // Never trust lengths from a model - they land straight in the
                // database and then on a page.
                return Sanitise(parsed);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Resume JSON did not deserialise");

                throw new StudyToolException(
                    "The file was read, but its contents could not be sorted into sections. "
                    + "You can still start a blank resume and paste the parts in.");
            }
        }

        private static string BuildParsePrompt(string text)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Read this resume and sort it into structured data.");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- Copy what is written. Do not improve, shorten, or reword anything.");
            sb.AppendLine("- Do not invent anything that is not in the text. Leave fields out instead.");
            sb.AppendLine("- Dates stay as written: \"Aug 2024\", \"Expected May 2027\", \"Summer 2025\".");
            sb.AppendLine("- A bullet is one achievement. Keep them separate, drop the bullet character.");
            sb.AppendLine("- The text came out of a PDF, so columns may be interleaved and headings may");
            sb.AppendLine("  sit oddly. Use your judgement about what belongs together.");
            sb.AppendLine();
            sb.AppendLine("Return ONLY this JSON object, no prose and no code fences:");
            sb.AppendLine("{");
            sb.AppendLine("  \"contact\": {\"name\":\"\",\"email\":\"\",\"phone\":\"\",\"location\":\"\",");
            sb.AppendLine("               \"website\":\"\",\"linkedIn\":\"\",\"gitHub\":\"\"},");
            sb.AppendLine("  \"summary\": \"\",");
            sb.AppendLine("  \"education\": [{\"school\":\"\",\"degree\":\"\",\"location\":\"\",");
            sb.AppendLine("                  \"startDate\":\"\",\"endDate\":\"\",\"gpa\":\"\",\"details\":[]}],");
            sb.AppendLine("  \"experience\": [{\"title\":\"\",\"organization\":\"\",\"location\":\"\",");
            sb.AppendLine("                   \"startDate\":\"\",\"endDate\":\"\",\"bullets\":[]}],");
            sb.AppendLine("  \"projects\": [{\"name\":\"\",\"link\":\"\",\"technologies\":\"\",\"bullets\":[]}],");
            sb.AppendLine("  \"skillsText\": \"\",");
            sb.AppendLine("  \"custom\": [{\"title\":\"\",\"body\":\"\"}],");
            sb.AppendLine("  \"layout\": []");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("\"skillsText\" is the skills section copied as written - keep the student's");
            sb.AppendLine("own commas, line breaks and groupings. Start a line with \"-\" where it was a");
            sb.AppendLine("bullet, and wrap a word in **stars** where it was bold.");
            sb.AppendLine();
            sb.AppendLine("\"custom\" is for every section that is not one of the six above: things");
            sb.AppendLine("like Certifications, Awards, Leadership, Activities, Coursework,");
            sb.AppendLine("Publications, Volunteering. Never drop a section because it does not fit -");
            sb.AppendLine("put it here with its heading as \"title\" and its lines as \"body\", one per");
            sb.AppendLine("line, with the same \"-\" and **stars**. Everything on the page has to end");
            sb.AppendLine("up somewhere.");
            sb.AppendLine();
            sb.AppendLine("\"layout\" lists the sections in the order they appear on the page, each as");
            sb.AppendLine("either summary, education, experience, projects, skills, or a custom");
            sb.AppendLine("section's exact title. Contact details are the header and are not listed.");
            sb.AppendLine();
            sb.AppendLine("RESUME:");
            sb.AppendLine("---");
            sb.AppendLine(text);
            sb.AppendLine("---");

            return sb.ToString();
        }

        // ---------- reviewing ----------

        public async Task<ResumeReviewDto> ReviewAsync(
            ResumeContentDto resume, CancellationToken ct = default)
        {
            var raw = await CallAsync(BuildReviewPrompt(resume), _options.ResumeModel, ct);
            var json = ExtractJsonObject(raw);

            if (json is null)
            {
                throw new StudyToolException(
                    "The review came back in a shape that could not be read. Try again.");
            }

            try
            {
                var review = JsonSerializer.Deserialize<ResumeReviewDto>(json, Json)
                             ?? throw new JsonException("null");

                foreach (var finding in review.Findings)
                {
                    finding.FromRule = false;
                    finding.Severity = NormaliseSeverity(finding.Severity);
                    finding.Section = (finding.Section ?? "").Trim().ToLowerInvariant();
                }

                review.Findings = review.Findings
                    .Where(f => !string.IsNullOrWhiteSpace(f.Issue))
                    .ToList();

                return review;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Resume review JSON did not deserialise");

                throw new StudyToolException(
                    "The review came back in a shape that could not be read. Try again.");
            }
        }

        private static string BuildReviewPrompt(ResumeContentDto resume)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are a careful careers adviser reading a college student's resume.");
            sb.AppendLine();
            sb.AppendLine("Judge the writing, not the formatting - spacing, fonts and layout are");
            sb.AppendLine("checked separately and are not your job here.");
            sb.AppendLine();
            sb.AppendLine("What to look for:");
            sb.AppendLine("- Bullets that describe duties instead of what the student actually did");
            sb.AppendLine("- Claims with nothing behind them: \"improved efficiency\" with no measure");
            sb.AppendLine("- Vague language where something specific would fit");
            sb.AppendLine("- Repetition, and openings that repeat across bullets");
            sb.AppendLine("- Things a reader would obviously want to know and cannot find");
            sb.AppendLine();
            sb.AppendLine("How to write findings:");
            sb.AppendLine("- Be specific. Quote the words you mean.");
            sb.AppendLine("- Where a rewrite helps more than advice, give one in \"example\" - but only");
            sb.AppendLine("  using facts already in the resume. Do not invent numbers or achievements.");
            sb.AppendLine("- Judge it as a student's resume. Do not ask for ten years of experience.");
            sb.AppendLine("- At most 8 findings. The most important ones, not everything possible.");
            sb.AppendLine("- Name at least one real strength. If it is genuinely thin, say so kindly.");
            sb.AppendLine();
            sb.AppendLine("Return ONLY this JSON object, no prose and no code fences:");
            sb.AppendLine("{");
            sb.AppendLine("  \"summary\": \"two or three sentences\",");
            sb.AppendLine("  \"strengths\": [\"\"],");
            sb.AppendLine("  \"findings\": [{\"section\":\"experience\",\"severity\":\"high\",");
            sb.AppendLine("                 \"issue\":\"\",\"suggestion\":\"\",\"example\":\"\"}]");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("\"section\" is one of: contact, summary, education, experience, projects,");
            sb.AppendLine("skills, or format for anything in a section the student added themselves.");
            sb.AppendLine("\"severity\" is one of: high, medium, low.");
            sb.AppendLine();
            sb.AppendLine("Sections under \"custom\" are the student's own headings - read them like");
            sb.AppendLine("any other section. \"layout\" is the order the sections print in; anything");
            sb.AppendLine("missing from it has been taken off the resume, so do not comment on it.");
            sb.AppendLine();
            sb.AppendLine("RESUME:");
            sb.AppendLine("---");
            sb.AppendLine(JsonSerializer.Serialize(resume, new JsonSerializerOptions { WriteIndented = true }));
            sb.AppendLine("---");

            return sb.ToString();
        }

        // ---------- plumbing ----------

        private async Task<string> CallAsync(string prompt, string model, CancellationToken ct)
        {
            string raw;
            try
            {
                raw = await _ai.CompleteAsync(prompt, model, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new StudyToolException("The AI took too long to answer. Try again.");
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new StudyToolException("The AI returned an empty answer. Try again.");
            }

            return raw;
        }

        private static string NormaliseSeverity(string? severity) =>
            (severity ?? "").Trim().ToLowerInvariant() switch
            {
                "high" or "critical" or "major" => "high",
                "low" or "minor" or "nit" => "low",
                _ => "medium"
            };

        /// <summary>Caps every string so nothing oversized reaches the database.</summary>
        private static ResumeContentDto Sanitise(ResumeContentDto resume)
        {
            static string? Clip(string? value, int max) =>
                string.IsNullOrWhiteSpace(value) ? null
                : value.Length <= max ? value.Trim() : value[..max].Trim();

            static List<string> ClipAll(List<string>? items, int max, int maxCount) =>
                (items ?? new List<string>())
                    .Where(i => !string.IsNullOrWhiteSpace(i))
                    .Take(maxCount)
                    .Select(i => Clip(i, max)!)
                    .ToList();

            resume.Contact.Name = Clip(resume.Contact.Name, 200);
            resume.Contact.Email = Clip(resume.Contact.Email, 200);
            resume.Contact.Phone = Clip(resume.Contact.Phone, 50);
            resume.Contact.Location = Clip(resume.Contact.Location, 200);
            resume.Contact.Website = Clip(resume.Contact.Website, 300);
            resume.Contact.LinkedIn = Clip(resume.Contact.LinkedIn, 300);
            resume.Contact.GitHub = Clip(resume.Contact.GitHub, 300);

            resume.Summary = Clip(resume.Summary, 2000);

            resume.Education = (resume.Education ?? new()).Take(10).ToList();
            foreach (var e in resume.Education)
            {
                e.School = Clip(e.School, 300);
                e.Degree = Clip(e.Degree, 300);
                e.Location = Clip(e.Location, 200);
                e.StartDate = Clip(e.StartDate, 100);
                e.EndDate = Clip(e.EndDate, 100);
                e.Gpa = Clip(e.Gpa, 50);
                e.Details = ClipAll(e.Details, 500, 10);
            }

            resume.Experience = (resume.Experience ?? new()).Take(20).ToList();
            foreach (var x in resume.Experience)
            {
                x.Title = Clip(x.Title, 300);
                x.Organization = Clip(x.Organization, 300);
                x.Location = Clip(x.Location, 200);
                x.StartDate = Clip(x.StartDate, 100);
                x.EndDate = Clip(x.EndDate, 100);
                x.Bullets = ClipAll(x.Bullets, 600, 12);
            }

            resume.Projects = (resume.Projects ?? new()).Take(20).ToList();
            foreach (var p in resume.Projects)
            {
                p.Name = Clip(p.Name, 300);
                p.Link = Clip(p.Link, 300);
                p.Technologies = Clip(p.Technologies, 300);
                p.Bullets = ClipAll(p.Bullets, 600, 12);
            }

            resume.Skills = ClipAll(resume.Skills, 100, 60);
            resume.SkillsText = Clip(resume.SkillsText, 4000);

            // Whichever of the two the model filled in, the resume opens with
            // skills in it rather than with an empty section.
            if (string.IsNullOrWhiteSpace(resume.SkillsText) && resume.Skills.Count > 0)
            {
                resume.SkillsText = string.Join(", ", resume.Skills);
            }

            resume.Custom = (resume.Custom ?? new List<CustomSectionDto>()).Take(12).ToList();

            foreach (var section in resume.Custom)
            {
                // The model has no idea what id we will use, so it never sends
                // one worth keeping.
                section.Id = NewSectionId();
                section.Title = Clip(section.Title, 120);
                section.Body = Clip(section.Body, 8000);
            }

            resume.Custom = resume.Custom
                .Where(s => !string.IsNullOrWhiteSpace(s.Title) || !string.IsNullOrWhiteSpace(s.Body))
                .ToList();

            resume.Layout = BuildLayout(resume);

            return resume;
        }

        private static string NewSectionId() => "s" + Guid.NewGuid().ToString("N")[..8];

        /// <summary>
        /// Turns the order the model reported into section keys.
        ///
        /// It names custom sections by their heading, because it has not seen
        /// the ids. Anything it names that is not a real section is dropped, and
        /// anything real that it left out is appended - so the result is always
        /// a complete, valid layout however loosely the model answered, and a
        /// section can never go missing because of a typo in a heading.
        /// </summary>
        private static List<string> BuildLayout(ResumeContentDto resume)
        {
            var known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["summary"] = "summary",
                ["education"] = "education",
                ["experience"] = "experience",
                ["work experience"] = "experience",
                ["projects"] = "projects",
                ["skills"] = "skills"
            };

            foreach (var section in resume.Custom)
            {
                if (string.IsNullOrWhiteSpace(section.Title)) continue;

                // TryAdd, so a custom section called "Skills" cannot take the
                // built-in one's place in the order.
                known.TryAdd(section.Title!, $"custom:{section.Id}");
            }

            var layout = new List<string>();

            foreach (var name in resume.Layout ?? new List<string>())
            {
                if (known.TryGetValue((name ?? "").Trim(), out var key) && !layout.Contains(key))
                {
                    layout.Add(key);
                }
            }

            foreach (var key in new[] { "summary", "education", "experience", "projects", "skills" })
            {
                if (!layout.Contains(key)) layout.Add(key);
            }

            foreach (var section in resume.Custom)
            {
                var key = $"custom:{section.Id}";
                if (!layout.Contains(key)) layout.Add(key);
            }

            return layout;
        }

        /// <summary>
        /// Finds the JSON object inside whatever the model sent, by tracking
        /// brace depth and ignoring braces inside strings. Same approach as the
        /// study tools, for the same reason: models wrap JSON in prose however
        /// firmly you ask them not to.
        /// </summary>
        private static string? ExtractJsonObject(string raw)
        {
            var start = raw.IndexOf('{');
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
                    case '"': inString = true; break;
                    case '{': depth++; break;
                    case '}':
                        depth--;
                        if (depth == 0) return raw[start..(i + 1)];
                        break;
                }
            }

            return null;
        }
    }
}

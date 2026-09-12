using System.Text.RegularExpressions;
using PursuitHQ.API.DTOs.Resumes;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// The checks that do not need an AI.
    ///
    /// A missing phone number, a 40-word bullet, a job with no dates: these are
    /// facts about the document, and a rule gets them right every time for
    /// free. Asking a model would be slower, cost a request, and occasionally
    /// miss one. The AI is saved for the judgement calls it is actually better
    /// at - whether a bullet says anything worth reading.
    /// </summary>
    public static partial class ResumeFormatChecker
    {
        /// <summary>Past this, a bullet has stopped being a bullet.</summary>
        private const int LongBulletWords = 32;

        /// <summary>Openings that describe duties rather than results.</summary>
        private static readonly string[] WeakOpeners =
        {
            "responsible for", "worked on", "helped with", "assisted with",
            "duties included", "tasked with", "in charge of", "participated in"
        };

        /// <summary>The sections a resume has when nobody has rearranged it.</summary>
        private static readonly string[] DefaultLayout =
            { "summary", "education", "experience", "projects", "skills" };

        public static List<ReviewFindingDto> Check(ResumeContentDto resume)
        {
            var findings = new List<ReviewFindingDto>();
            var shown = VisibleSections(resume);

            // Contact is the header rather than a section, so it is always on the
            // page and always worth checking.
            CheckContact(resume.Contact, findings);

            if (shown.Contains("experience")) CheckExperience(resume.Experience, findings);
            if (shown.Contains("education")) CheckEducation(resume.Education, findings);
            if (shown.Contains("skills")) CheckSkills(resume, findings);

            CheckCustom(resume, shown, findings);

            return findings;
        }

        /// <summary>
        /// Which sections are actually on the resume.
        ///
        /// A removed section must not be complained about. Telling someone their
        /// education section is empty after they deliberately took it off the
        /// page is noise, and noise is what stops people reading findings at all.
        /// </summary>
        private static HashSet<string> VisibleSections(ResumeContentDto resume)
        {
            if (resume.Layout.Count > 0)
            {
                return new HashSet<string>(resume.Layout, StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>(
                DefaultLayout.Concat(resume.Custom.Select(c => $"custom:{c.Id}")),
                StringComparer.OrdinalIgnoreCase);
        }

        private static void CheckContact(ContactDto contact, List<ReviewFindingDto> findings)
        {
            if (string.IsNullOrWhiteSpace(contact.Name))
            {
                Add(findings, "contact", "high", "Your name is missing.",
                    "It is the first thing anyone looks for.");
            }

            if (string.IsNullOrWhiteSpace(contact.Email))
            {
                Add(findings, "contact", "high", "There is no email address.",
                    "Without one there is no way to reply to you.");
            }
            else if (!EmailShape().IsMatch(contact.Email))
            {
                Add(findings, "contact", "high", $"\"{contact.Email}\" does not look like an email address.",
                    "A typo here quietly costs you every reply.");
            }

            if (string.IsNullOrWhiteSpace(contact.Phone))
            {
                Add(findings, "contact", "low", "No phone number.",
                    "Optional, but some recruiters still call.");
            }

            if (string.IsNullOrWhiteSpace(contact.LinkedIn) && string.IsNullOrWhiteSpace(contact.GitHub))
            {
                Add(findings, "contact", "medium", "No LinkedIn or GitHub link.",
                    "For a student, a link to work you have done does a lot of the arguing for you.");
            }
        }

        private static void CheckExperience(List<ExperienceDto> experience, List<ReviewFindingDto> findings)
        {
            if (experience.Count == 0)
            {
                Add(findings, "experience", "medium", "No experience listed.",
                    "Part-time work, campus jobs, research and volunteering all count.");
                return;
            }

            foreach (var entry in experience)
            {
                var where = string.IsNullOrWhiteSpace(entry.Organization) ? "an entry" : entry.Organization;

                if (string.IsNullOrWhiteSpace(entry.StartDate) && string.IsNullOrWhiteSpace(entry.EndDate))
                {
                    Add(findings, "experience", "medium", $"{where} has no dates.",
                        "Gaps that are not explained get assumed.");
                }

                if (entry.Bullets.Count == 0)
                {
                    Add(findings, "experience", "high", $"{where} has no bullet points.",
                        "A job title alone says what you were called, not what you did.");
                    continue;
                }

                foreach (var bullet in entry.Bullets)
                {
                    var words = bullet.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

                    if (words > LongBulletWords)
                    {
                        Add(findings, "experience", "medium",
                            $"A bullet under {where} runs to {words} words.",
                            "Bullets are skimmed, not read. Two short ones beat one long one.");
                    }

                    var opener = WeakOpeners.FirstOrDefault(
                        w => bullet.TrimStart().StartsWith(w, StringComparison.OrdinalIgnoreCase));

                    if (opener is not null)
                    {
                        Add(findings, "experience", "medium",
                            $"A bullet under {where} starts with \"{opener}\".",
                            "That describes a duty. Start with what you did and what changed.");
                    }
                }

                // A section with no numbers anywhere is the most common thing
                // wrong with a student resume, and the easiest to fix.
                if (!entry.Bullets.Any(b => Digit().IsMatch(b)))
                {
                    Add(findings, "experience", "low", $"Nothing under {where} is quantified.",
                        "How many, how often, how much faster - a number makes a claim checkable.");
                }
            }
        }

        private static void CheckEducation(List<EducationDto> education, List<ReviewFindingDto> findings)
        {
            if (education.Count == 0)
            {
                Add(findings, "education", "high", "No education listed.",
                    "For a student this is usually the strongest section.");
                return;
            }

            foreach (var entry in education)
            {
                if (!string.IsNullOrWhiteSpace(entry.EndDate)) continue;

                var where = string.IsNullOrWhiteSpace(entry.School) ? "An entry" : entry.School;

                Add(findings, "education", "medium", $"{where} has no graduation date.",
                    "\"Expected May 2027\" is fine, and answers the question they are asking.");
            }
        }

        private static void CheckSkills(ResumeContentDto resume, List<ReviewFindingDto> findings)
        {
            var items = SkillItems(resume);

            if (items.Count == 0)
            {
                Add(findings, "skills", "medium", "No skills listed.",
                    "This is the section keyword filters read first.");
            }
            else if (items.Count > 30)
            {
                Add(findings, "skills", "low", $"{items.Count} skills listed.",
                    "A very long list reads as padding. Keep the ones you would be happy to be asked about.");
            }
        }

        /// <summary>
        /// Splits free-typed skills back into countable items.
        ///
        /// The section is typed however the student likes - commas, one per
        /// line, bullets, groups with their own headings - so any count is a
        /// guess. It only has to be good enough to tell "empty" from "sixty",
        /// which is all the two rules above ask of it.
        /// </summary>
        private static List<string> SkillItems(ResumeContentDto resume)
        {
            var text = resume.SkillsText;

            // Falls back to the old list so a resume saved before free typing
            // is still checked rather than reported as having no skills.
            if (string.IsNullOrWhiteSpace(text)) text = string.Join(", ", resume.Skills);

            return ItemSplit().Split(text ?? "")
                .Select(Plain)
                .Where(s => s.Length > 0)
                .ToList();
        }

        /// <summary>
        /// Checks the sections the student added themselves.
        ///
        /// They are free text, so there is no structure to check - but the
        /// bullets inside one are still bullets, and the same two mistakes show
        /// up in a Leadership section as in an Experience one.
        /// </summary>
        private static void CheckCustom(
            ResumeContentDto resume, HashSet<string> shown, List<ReviewFindingDto> findings)
        {
            foreach (var section in resume.Custom)
            {
                if (!shown.Contains($"custom:{section.Id}")) continue;

                var title = string.IsNullOrWhiteSpace(section.Title)
                    ? "a section you added"
                    : $"\"{section.Title}\"";

                if (string.IsNullOrWhiteSpace(section.Body))
                {
                    Add(findings, "format", "low", $"{title} is empty.",
                        "A heading with nothing under it looks like something went wrong "
                        + "when it prints. Fill it in or take the section off.");
                    continue;
                }

                foreach (var line in Lines(section.Body))
                {
                    var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

                    if (words > LongBulletWords)
                    {
                        Add(findings, "format", "medium",
                            $"A line under {title} runs to {words} words.",
                            "Bullets are skimmed, not read. Two short ones beat one long one.");
                    }

                    var opener = WeakOpeners.FirstOrDefault(
                        w => line.StartsWith(w, StringComparison.OrdinalIgnoreCase));

                    if (opener is not null)
                    {
                        Add(findings, "format", "medium",
                            $"A line under {title} starts with \"{opener}\".",
                            "That describes a duty. Start with what you did and what changed.");
                    }
                }
            }
        }

        private static IEnumerable<string> Lines(string body) =>
            body.Split('\n').Select(Plain).Where(l => l.Length > 0);

        /// <summary>Strips the bullet markers and bold markers off a line.</summary>
        private static string Plain(string line) =>
            line.Replace("**", "").Trim().TrimStart('-', '*', '•').Trim(' ', '\t', ':', '.');

        private static void Add(
            List<ReviewFindingDto> findings, string section, string severity,
            string issue, string suggestion) =>
            findings.Add(new ReviewFindingDto
            {
                Section = section,
                Severity = severity,
                Issue = issue,
                Suggestion = suggestion,
                FromRule = true
            });

        [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
        private static partial Regex EmailShape();

        [GeneratedRegex(@"\d")]
        private static partial Regex Digit();

        /// <summary>Commas, semicolons, pipes and line breaks all separate skills.</summary>
        [GeneratedRegex(@"[,;|\r\n]+")]
        private static partial Regex ItemSplit();
    }
}

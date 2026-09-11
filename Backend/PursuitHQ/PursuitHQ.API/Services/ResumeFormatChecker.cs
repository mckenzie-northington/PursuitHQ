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

        public static List<ReviewFindingDto> Check(ResumeContentDto resume)
        {
            var findings = new List<ReviewFindingDto>();

            CheckContact(resume.Contact, findings);
            CheckExperience(resume.Experience, findings);
            CheckEducation(resume.Education, findings);
            CheckSkills(resume.Skills, findings);

            return findings;
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

        private static void CheckSkills(List<string> skills, List<ReviewFindingDto> findings)
        {
            if (skills.Count == 0)
            {
                Add(findings, "skills", "medium", "No skills listed.",
                    "This is the section keyword filters read first.");
            }
            else if (skills.Count > 30)
            {
                Add(findings, "skills", "low", $"{skills.Count} skills listed.",
                    "A very long list reads as padding. Keep the ones you would be happy to be asked about.");
            }
        }

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
    }
}

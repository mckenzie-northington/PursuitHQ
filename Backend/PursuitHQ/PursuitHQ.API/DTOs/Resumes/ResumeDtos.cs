using System.ComponentModel.DataAnnotations;

namespace PursuitHQ.API.DTOs.Resumes
{
    public class ResumeSummaryDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }

        /// <summary>Enough to tell two resumes apart in a list.</summary>
        public string? OwnerName { get; set; }
        public int ExperienceCount { get; set; }
        public int ProjectCount { get; set; }
    }

    public class ResumeDto : ResumeSummaryDto
    {
        public ResumeContentDto Content { get; set; } = new();
    }

    /// <summary>
    /// A resume as structured data rather than a blob of text.
    ///
    /// Stored as JSON in Resume.Content. Structured because both halves of this
    /// feature need the parts separately: the editor needs fields to edit, and
    /// the checker needs to say "your third bullet under Experience" rather
    /// than gesturing at the whole document.
    /// </summary>
    public class ResumeContentDto
    {
        public ContactDto Contact { get; set; } = new();

        [MaxLength(2000)]
        public string? Summary { get; set; }

        public List<EducationDto> Education { get; set; } = new();
        public List<ExperienceDto> Experience { get; set; } = new();
        public List<ProjectDto> Projects { get; set; } = new();

        /// <summary>
        /// Skills as free text, typed however the student likes.
        ///
        /// Stored as plain text, not HTML. The only markup is ** for bold and a
        /// leading "-" for a bullet, and the page turns those into elements
        /// itself - so nothing typed or pasted in here can become markup when
        /// the resume is rendered.
        /// </summary>
        [MaxLength(4000)]
        public string? SkillsText { get; set; }

        /// <summary>
        /// The old comma-separated list. Kept so resumes saved before free
        /// typing still open; <see cref="SkillsText"/> wins when both are set.
        /// </summary>
        public List<string> Skills { get; set; } = new();

        /// <summary>Sections the student added themselves.</summary>
        public List<CustomSectionDto> Custom { get; set; } = new();

        /// <summary>
        /// Section keys, top to bottom: summary, education, experience,
        /// projects, skills, or custom:{id}.
        ///
        /// A key that is missing is a section the student removed. Its content
        /// stays in the record on purpose, so putting the section back is one
        /// click rather than retyping it.
        ///
        /// Empty means "never arranged", which is every resume saved before
        /// this existed - the editor falls back to the default order in that
        /// case rather than showing an empty page.
        /// </summary>
        public List<string> Layout { get; set; } = new();
    }

    /// <summary>
    /// A section the student made up: Certifications, Leadership, Awards,
    /// Coursework - anything the built-in sections do not cover. One heading
    /// and one block of free text, because the whole point of it is that
    /// nobody knew in advance what shape the content would be.
    /// </summary>
    public class CustomSectionDto
    {
        /// <summary>Stable within a resume, so Layout can point at it.</summary>
        [MaxLength(40)] public string Id { get; set; } = string.Empty;

        [MaxLength(120)] public string? Title { get; set; }

        /// <summary>Same plain-text rules as <see cref="ResumeContentDto.SkillsText"/>.</summary>
        [MaxLength(8000)] public string? Body { get; set; }
    }

    public class ContactDto
    {
        [MaxLength(200)] public string? Name { get; set; }
        [MaxLength(200)] public string? Email { get; set; }
        [MaxLength(50)] public string? Phone { get; set; }
        [MaxLength(200)] public string? Location { get; set; }
        [MaxLength(300)] public string? Website { get; set; }
        [MaxLength(300)] public string? LinkedIn { get; set; }
        [MaxLength(300)] public string? GitHub { get; set; }
    }

    public class EducationDto
    {
        [MaxLength(300)] public string? School { get; set; }
        [MaxLength(300)] public string? Degree { get; set; }
        [MaxLength(200)] public string? Location { get; set; }

        /// <summary>Free text like "Aug 2024" or "Expected May 2027".</summary>
        [MaxLength(100)] public string? StartDate { get; set; }
        [MaxLength(100)] public string? EndDate { get; set; }

        [MaxLength(50)] public string? Gpa { get; set; }
        public List<string> Details { get; set; } = new();
    }

    public class ExperienceDto
    {
        [MaxLength(300)] public string? Title { get; set; }
        [MaxLength(300)] public string? Organization { get; set; }
        [MaxLength(200)] public string? Location { get; set; }
        [MaxLength(100)] public string? StartDate { get; set; }
        [MaxLength(100)] public string? EndDate { get; set; }
        public List<string> Bullets { get; set; } = new();
    }

    public class ProjectDto
    {
        [MaxLength(300)] public string? Name { get; set; }
        [MaxLength(300)] public string? Link { get; set; }
        [MaxLength(300)] public string? Technologies { get; set; }
        public List<string> Bullets { get; set; } = new();
    }

    public class SaveResumeDto
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public ResumeContentDto Content { get; set; } = new();
    }

    // ---------- review ----------

    /// <summary>
    /// What the checker found.
    ///
    /// Deliberately no overall score out of 100. A single number invites you to
    /// chase the number instead of reading the findings, and any number here
    /// would be invented rather than measured.
    /// </summary>
    public class ResumeReviewDto
    {
        /// <summary>Two or three sentences on the resume as a whole.</summary>
        public string Summary { get; set; } = string.Empty;

        public List<ReviewFindingDto> Findings { get; set; } = new();

        /// <summary>What is already working. Worth saying, and worth not undoing.</summary>
        public List<string> Strengths { get; set; } = new();

        public DateTime ReviewedAt { get; set; }
    }

    // ---------- matching against a job description ----------

    /// <summary>
    /// How well a resume answers one specific job posting.
    ///
    /// This one does carry a percentage, unlike the general review, and the
    /// difference is that here there is something to actually count. "How good
    /// is this resume" has no denominator; "how many of these fourteen stated
    /// requirements does it evidence" has one.
    ///
    /// The number is worked out in C# from <see cref="Requirements"/>, not asked
    /// of the model. A model asked for a percentage produces a plausible-looking
    /// number with nothing behind it, and it will give a different one each
    /// time. Counting met requirements gives the same answer twice and can be
    /// explained line by line - which is what makes it worth showing at all.
    /// </summary>
    public class JobMatchDto
    {
        /// <summary>0-100, from the requirement counts below.</summary>
        public int Score { get; set; }

        /// <summary>The role this was matched against, as the posting described it.</summary>
        public string? RoleTitle { get; set; }
        public string? Company { get; set; }

        /// <summary>Two or three sentences on how the resume reads against this posting.</summary>
        public string Summary { get; set; } = string.Empty;

        public List<RequirementMatchDto> Requirements { get; set; } = new();

        /// <summary>
        /// Concrete changes for this posting: wording already in the resume that
        /// could be aimed better, not invented experience.
        /// </summary>
        public List<string> Suggestions { get; set; } = new();

        public int MetCount { get; set; }
        public int PartialCount { get; set; }
        public int MissingCount { get; set; }

        public DateTime MatchedAt { get; set; }
    }

    public class RequirementMatchDto
    {
        /// <summary>The requirement, in the posting's own words where possible.</summary>
        public string Requirement { get; set; } = string.Empty;

        /// <summary>met, partial, or missing.</summary>
        public string Status { get; set; } = "missing";

        /// <summary>
        /// True when the posting lists this as required rather than preferred.
        /// Required items count double, because missing one is a different kind
        /// of problem from missing a nice-to-have.
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// What in the resume supports this - quoted, so a claimed match can be
        /// checked rather than taken on faith.
        /// </summary>
        public string? Evidence { get; set; }
    }

    public class ReviewFindingDto
    {
        /// <summary>contact, summary, education, experience, projects, skills, or format.</summary>
        public string Section { get; set; } = string.Empty;

        /// <summary>high, medium, or low.</summary>
        public string Severity { get; set; } = "medium";

        public string Issue { get; set; } = string.Empty;
        public string? Suggestion { get; set; }

        /// <summary>A concrete rewrite, when one helps more than advice does.</summary>
        public string? Example { get; set; }

        /// <summary>
        /// True when this came from a rule rather than the AI. Rule findings
        /// are checkable facts - a missing phone number, a 40-word bullet - and
        /// are worth trusting differently from a judgement call.
        /// </summary>
        public bool FromRule { get; set; }
    }
}

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

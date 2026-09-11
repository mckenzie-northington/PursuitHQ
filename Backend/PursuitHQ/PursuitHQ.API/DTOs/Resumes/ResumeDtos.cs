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
        public List<string> Skills { get; set; } = new();
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

using System.ComponentModel.DataAnnotations;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.DTOs.StudyChat
{
    /// <summary>A conversation in the sidebar list.</summary>
    public class ConversationSummaryDto
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string? CourseName { get; set; }
        public string Title { get; set; } = string.Empty;
        public int MessageCount { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>A conversation with its messages and its chosen sources.</summary>
    public class ConversationDto : ConversationSummaryDto
    {
        public List<int> SourceMaterialIds { get; set; } = new();
        public List<int> SourceNoteIds { get; set; } = new();
        public List<StudyMessageDto> Messages { get; set; } = new();
    }

    public class StudyMessageDto
    {
        public int Id { get; set; }
        public StudyMessageRole Role { get; set; }
        public string Content { get; set; } = string.Empty;

        public StudyArtifactKind ArtifactKind { get; set; }
        public string? ArtifactTitle { get; set; }
        public string? ArtifactContent { get; set; }

        /// <summary>Set once kept, so the UI shows "Saved" instead of the button.</summary>
        public int? SavedStudyGuideId { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class StartConversationDto
    {
        [Required]
        public int CourseId { get; set; }

        [MaxLength(200)]
        public string? Title { get; set; }
    }

    public class RenameConversationDto
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;
    }

    public class AskDto
    {
        [Required, MaxLength(4000)]
        public string Question { get; set; } = string.Empty;
    }

    /// <summary>Which files and notes the conversation should work from.</summary>
    public class SetSourcesDto
    {
        public List<int> SourceMaterialIds { get; set; } = new();
        public List<int> SourceNoteIds { get; set; } = new();
    }

    public class StudyGuideSummaryDto
    {
        public int Id { get; set; }
        public int? CourseId { get; set; }
        public string? CourseName { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class StudyGuideDto : StudyGuideSummaryDto
    {
        public string Content { get; set; } = string.Empty;
    }
}

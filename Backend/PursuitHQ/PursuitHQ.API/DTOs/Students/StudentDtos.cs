using System.ComponentModel.DataAnnotations;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.DTOs.Students
{
    /// <summary>
    /// What one student may see of another.
    ///
    /// Deliberately separate from UserProfileDto. That one is for the account's
    /// own owner and carries the email; this is the only shape that crosses
    /// between students. Keeping them apart means a field added to the profile
    /// later cannot leak here by accident - somebody has to put it here on
    /// purpose.
    ///
    /// Email, Major and GraduationYear are null unless the two are connected.
    /// They are on this class rather than a subclass so that one endpoint can
    /// serve both levels without the client guessing which shape it got.
    /// </summary>
    public class StudentCardDto
    {
        public string Id { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? School { get; set; }
        public EducationLevel EducationLevel { get; set; }

        /// <summary>True if there is a photo to fetch from /api/students/{id}/photo.</summary>
        public bool HasPhoto { get; set; }

        // --- connected only ---
        public string? Email { get; set; }
        public string? Major { get; set; }
        public int? GraduationYear { get; set; }

        /// <summary>
        /// Where the viewer stands with this person: "none", "pending_out",
        /// "pending_in", "connected", "declined", "blocked".
        ///
        /// A string rather than the enum because the client needs to know the
        /// direction of a pending request, which the enum alone does not say.
        /// </summary>
        public string Relationship { get; set; } = "none";

        /// <summary>The connection row, when there is one, so it can be acted on.</summary>
        public int? ConnectionId { get; set; }
    }

    public class ConnectionRequestDto
    {
        [Required]
        public string AddresseeId { get; set; } = string.Empty;

        [MaxLength(300, ErrorMessage = "Keep the note under 300 characters.")]
        public string? Note { get; set; }
    }

    public class PendingConnectionDto
    {
        public int Id { get; set; }
        public StudentCardDto Student { get; set; } = new();
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class BlockDto
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
    }
}

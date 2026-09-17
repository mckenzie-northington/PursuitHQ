using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace PursuitHQ.API.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Major { get; set; }
        public int? GraduationYear { get; set; }
        public string TimeZone { get; set; } = "America/New_York";

        [MaxLength(200)]
        public string? School { get; set; }

        public EducationLevel EducationLevel { get; set; } = EducationLevel.NotSet;

        /// <summary>
        /// Whether this student shows up when another one searches.
        ///
        /// False by default, and that default is the point. Being findable by
        /// strangers is not something to be enrolled in by signing up for a
        /// planner - it has to be switched on, knowing what it does.
        /// </summary>
        public bool IsDiscoverable { get; set; }

        /// <summary>
        /// Storage key for the profile photo, never shown to a client.
        ///
        /// The photo is served through an endpoint that checks who is asking,
        /// not from a public folder - otherwise anyone who learned the file
        /// name could fetch a student's picture without an account.
        /// </summary>
        [MaxLength(300)]
        public string? PhotoPath { get; set; }

        [MaxLength(100)]
        public string? PhotoContentType { get; set; }
        /// <summary>
        /// Colors this student has saved to reuse, as a comma-separated list
        /// of hex values.
        ///
        /// A joined string rather than a table of its own: this is a short,
        /// ordered list that is always read and written whole, and it never
        /// needs to be queried or joined against. A ColorPreference table
        /// would be four files and a foreign key to store what fits in a
        /// column.
        /// </summary>
        [MaxLength(500)]
        public string? SavedColors { get; set; }

        /// <summary>
        /// Checked at sign-up against a minimum age.
        ///
        /// Stored rather than discarded after the check, because the rules that
        /// apply to somebody depend on their age and change as they get older -
        /// keeping only "passed the check on the day they joined" answers the
        /// wrong question a year later. It is the only reason this is here; it
        /// is never shown to another student.
        /// </summary>
        public DateOnly? DateOfBirth { get; set; }

        /// <summary>
        /// When this student accepted the terms and privacy policy.
        ///
        /// The evidence that they were actually shown, which is the part that
        /// matters - a policy nobody was presented with is a policy that did
        /// not do its job.
        /// </summary>
        public DateTime? TermsAcceptedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
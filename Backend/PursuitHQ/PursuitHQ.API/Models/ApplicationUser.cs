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

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
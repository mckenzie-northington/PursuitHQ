using System.ComponentModel.DataAnnotations;

namespace PursuitHQ.API.DTOs.Auth
{
    /// <summary>
    /// Changing only the time zone.
    ///
    /// Its own DTO rather than reusing UpdateProfileDto because that one carries
    /// the whole profile and is written wholesale - handing it a payload that
    /// knows only the zone would clear the student's name and major as a side
    /// effect.
    /// </summary>
    public class UpdateTimeZoneDto
    {
        /// <summary>IANA id, e.g. "America/Chicago".</summary>
        [Required(ErrorMessage = "Pick a time zone.")]
        [MaxLength(100)]
        public string TimeZone { get; set; } = string.Empty;
    }
}

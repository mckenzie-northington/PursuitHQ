using System.ComponentModel.DataAnnotations;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.DTOs.JobSearch
{
    public class SaveJobFromSearchDto
    {
        [Required, MaxLength(200)]
        public string ExternalId { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Company { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Role { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? ApplyUrl { get; set; }

        public JobType Type { get; set; } = JobType.Internship;
    }
}

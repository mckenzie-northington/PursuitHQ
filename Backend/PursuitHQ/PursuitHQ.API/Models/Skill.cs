namespace PursuitHQ.API.Models
{
    /// <summary>A skill the student has, with a self-assessed level.</summary>
    public class Skill
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public string Name { get; set; } = string.Empty;
        public SkillLevel Level { get; set; } = SkillLevel.Beginner;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

namespace PursuitHQ.API.Models
{
    /// <summary>One time a student took a quiz.</summary>
    public class QuizAttempt
    {
        public int Id { get; set; }

        public int QuizId { get; set; }
        public Quiz? Quiz { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        /// <summary>Percentage correct, set when the attempt is submitted.</summary>
        public int? Score { get; set; }

        public ICollection<QuizAnswer> Answers { get; set; } = new List<QuizAnswer>();
    }
}

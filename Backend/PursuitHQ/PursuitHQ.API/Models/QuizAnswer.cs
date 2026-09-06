namespace PursuitHQ.API.Models
{
    /// <summary>One answer given during a quiz attempt.</summary>
    public class QuizAnswer
    {
        public int Id { get; set; }

        public int AttemptId { get; set; }
        public QuizAttempt? Attempt { get; set; }

        public int QuestionId { get; set; }
        public QuizQuestion? Question { get; set; }

        public string GivenAnswer { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}

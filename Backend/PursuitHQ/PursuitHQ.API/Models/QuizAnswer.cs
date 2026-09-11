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

        /// <summary>
        /// Why it was marked that way. Only set for short answers, which are
        /// judged by AI rather than compared - "wrong" with no reason is not
        /// much use when your words differed from the key but your meaning did
        /// not.
        /// </summary>
        public string? Feedback { get; set; }
    }
}

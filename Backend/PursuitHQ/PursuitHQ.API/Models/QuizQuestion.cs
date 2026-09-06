namespace PursuitHQ.API.Models
{
    /// <summary>One question in a quiz.</summary>
    public class QuizQuestion
    {
        public int Id { get; set; }

        public int QuizId { get; set; }
        public Quiz? Quiz { get; set; }

        public string QuestionText { get; set; } = string.Empty;
        public QuestionType QuestionType { get; set; } = QuestionType.MultipleChoice;

        /// <summary>JSON array of choices, for multiple choice questions.</summary>
        public string? Options { get; set; }

        public string CorrectAnswer { get; set; } = string.Empty;

        /// <summary>Shown after the student answers.</summary>
        public string? Explanation { get; set; }

        public int Order { get; set; }
    }
}

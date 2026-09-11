using System.ComponentModel.DataAnnotations;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.DTOs.Quizzes
{
    public class QuizSummaryDto
    {
        public int Id { get; set; }
        public int? CourseId { get; set; }
        public string? CourseName { get; set; }
        public string Title { get; set; } = string.Empty;
        public int QuestionCount { get; set; }
        public bool IsAiGenerated { get; set; }
        public string? SourceName { get; set; }

        /// <summary>Best score so far, or null if never taken.</summary>
        public int? BestScore { get; set; }
        public int AttemptCount { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// A quiz as shown while taking it.
    ///
    /// Deliberately has no CorrectAnswer or Explanation: those are only sent
    /// back with the results. Anything the browser receives, the student can
    /// read.
    /// </summary>
    public class QuizDto : QuizSummaryDto
    {
        public List<QuizQuestionDto> Questions { get; set; } = new();
    }

    public class QuizQuestionDto
    {
        public int Id { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public QuestionType QuestionType { get; set; }
        public List<string> Options { get; set; } = new();
        public int Order { get; set; }
    }

    public class GenerateQuizDto
    {
        [Required]
        public int CourseId { get; set; }

        public int? SourceMaterialId { get; set; }
        public int? SourceNoteId { get; set; }

        /// <summary>Clamped server-side to 5-30.</summary>
        public int Count { get; set; } = 10;

        /// <summary>
        /// What kind of test, in the student's own words: "free response",
        /// "multiple choice only", "mostly true/false".
        /// </summary>
        [MaxLength(300)]
        public string? Style { get; set; }

        [MaxLength(200)]
        public string? Title { get; set; }
    }

    public class SubmitAttemptDto
    {
        public List<SubmittedAnswerDto> Answers { get; set; } = new();
    }

    public class SubmittedAnswerDto
    {
        public int QuestionId { get; set; }
        public string Answer { get; set; } = string.Empty;
    }

    /// <summary>The marked paper: score, and every question with its verdict.</summary>
    public class AttemptResultDto
    {
        public int AttemptId { get; set; }
        public int QuizId { get; set; }
        public string QuizTitle { get; set; } = string.Empty;
        public int Score { get; set; }
        public int CorrectCount { get; set; }
        public int TotalCount { get; set; }
        public DateTime CompletedAt { get; set; }

        public List<GradedQuestionDto> Questions { get; set; } = new();
    }

    public class GradedQuestionDto
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public QuestionType QuestionType { get; set; }
        public List<string> Options { get; set; } = new();

        public string GivenAnswer { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public string? Explanation { get; set; }

        /// <summary>Only for written answers, which are judged rather than matched.</summary>
        public string? Feedback { get; set; }
    }

    public class AttemptSummaryDto
    {
        public int Id { get; set; }
        public int Score { get; set; }
        public DateTime CompletedAt { get; set; }
    }
}

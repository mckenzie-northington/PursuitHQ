using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Quizzes;
using PursuitHQ.API.Models;
using PursuitHQ.API.Services;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// Practice tests: generated from course material, taken in the app, and
    /// graded - written answers by AI, the rest by comparison.
    /// </summary>
    [Route("api/quizzes")]
    public class QuizzesController : ApiControllerBase
    {
        private const int MinQuestions = 5;
        private const int MaxQuestions = 30;

        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _storage;
        private readonly ITextExtractionService _textExtraction;
        private readonly IStudyToolAiService _studyTools;
        private readonly IAiUsageLimiter _limiter;
        private readonly ILogger<QuizzesController> _logger;

        public QuizzesController(
            ApplicationDbContext db,
            IFileStorageService storage,
            ITextExtractionService textExtraction,
            IStudyToolAiService studyTools,
            IAiUsageLimiter limiter,
            ILogger<QuizzesController> logger)
        {
            _db = db;
            _storage = storage;
            _textExtraction = textExtraction;
            _studyTools = studyTools;
            _limiter = limiter;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<QuizSummaryDto>>> GetQuizzes([FromQuery] int? courseId)
        {
            var query = _db.Quizzes.Where(q => q.UserId == CurrentUserId);
            if (courseId.HasValue) query = query.Where(q => q.CourseId == courseId.Value);

            var quizzes = await query
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => new QuizSummaryDto
                {
                    Id = q.Id,
                    CourseId = q.CourseId,
                    CourseName = q.Course != null ? q.Course.Name : null,
                    Title = q.Title,
                    QuestionCount = q.Questions.Count,
                    IsAiGenerated = q.IsAiGenerated,
                    SourceName = q.SourceMaterial != null
                        ? q.SourceMaterial.FileName
                        : q.SourceNote != null ? q.SourceNote.Title : null,
                    AttemptCount = q.Attempts.Count(a => a.CompletedAt != null),
                    BestScore = q.Attempts
                        .Where(a => a.Score != null)
                        .Max(a => a.Score),
                    CreatedAt = q.CreatedAt
                })
                .ToListAsync();

            return Ok(quizzes);
        }

        /// <summary>The quiz to sit. Answers and explanations are not included.</summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<QuizDto>> GetQuiz(int id)
        {
            var quiz = await BuildQuizDtoAsync(id);

            return quiz is null ? NotFound(QuizNotFound()) : Ok(quiz);
        }

        private async Task<QuizDto?> BuildQuizDtoAsync(int id)
        {
            var quiz = await _db.Quizzes
                .Include(q => q.Course)
                .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.Id == id && q.UserId == CurrentUserId);

            if (quiz is null) return null;

            return new QuizDto
            {
                Id = quiz.Id,
                CourseId = quiz.CourseId,
                CourseName = quiz.Course?.Name,
                Title = quiz.Title,
                QuestionCount = quiz.Questions.Count,
                IsAiGenerated = quiz.IsAiGenerated,
                CreatedAt = quiz.CreatedAt,
                Questions = quiz.Questions
                    .OrderBy(q => q.Order)
                    .Select(q => new QuizQuestionDto
                    {
                        Id = q.Id,
                        QuestionText = q.QuestionText,
                        QuestionType = q.QuestionType,
                        Options = ReadOptions(q.Options),
                        Order = q.Order
                    })
                    .ToList()
            };
        }

        [HttpPost("generate")]
        public async Task<ActionResult<QuizDto>> Generate(GenerateQuizDto dto, CancellationToken ct)
        {
            if (!_studyTools.IsConfigured)
            {
                return StatusCode(503, new ApiErrorDto(
                    "AiNotConfigured",
                    "AI is not set up on this server. Set the Ai:ApiKey user secret and restart the API."));
            }

            if ((dto.SourceMaterialId is null) == (dto.SourceNoteId is null))
            {
                return BadRequest(new ApiErrorDto(
                    "InvalidSource", "Pick exactly one source: either an uploaded file or a note."));
            }

            var usage = _limiter.Peek(CurrentUserId);
            if (usage.Exceeded)
            {
                return StatusCode(429, new ApiErrorDto(
                    "DailyLimitReached",
                    $"You have used all {usage.Limit} AI requests for today. The count resets at midnight."));
            }

            if (!await _db.Courses.AnyAsync(c => c.Id == dto.CourseId && c.UserId == CurrentUserId, ct))
            {
                return NotFound(new ApiErrorDto(
                    "CourseNotFound", "That course does not exist, or it does not belong to you."));
            }

            var source = await ResolveSourceAsync(dto, ct);
            if (source.Error is not null) return source.Error;

            _limiter.Consume(CurrentUserId);

            GeneratedTest generated;
            try
            {
                generated = await _studyTools.GenerateTestAsync(
                    source.Text!, source.Name!,
                    Math.Clamp(dto.Count, MinQuestions, MaxQuestions), dto.Style, ct);
            }
            catch (StudyToolException ex)
            {
                return StatusCode(502, new ApiErrorDto("GenerationFailed", ex.Message));
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "AI provider call failed");
                return StatusCode(502, new ApiErrorDto("AiUnavailable", ex.Message));
            }

            var quiz = new Quiz
            {
                UserId = CurrentUserId,
                CourseId = dto.CourseId,
                SourceMaterialId = dto.SourceMaterialId,
                SourceNoteId = dto.SourceNoteId,
                Title = string.IsNullOrWhiteSpace(dto.Title) ? generated.Title : dto.Title.Trim(),
                IsAiGenerated = true,
                CreatedAt = DateTime.UtcNow,
                Questions = BuildQuestions(generated.Questions)
            };

            _db.Quizzes.Add(quiz);
            await _db.SaveChangesAsync(ct);

            return CreatedAtAction(
                nameof(GetQuiz), new { id = quiz.Id }, await BuildQuizDtoAsync(quiz.Id));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteQuiz(int id)
        {
            var quiz = await _db.Quizzes
                .Include(q => q.Questions)
                .Include(q => q.Attempts)
                .FirstOrDefaultAsync(q => q.Id == id && q.UserId == CurrentUserId);

            if (quiz is null) return NotFound(QuizNotFound());

            _db.Quizzes.Remove(quiz);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // ---------- taking it ----------

        /// <summary>
        /// Marks a completed attempt.
        ///
        /// Multiple choice and true/false are compared directly. Written
        /// answers go to the AI in a single batch, because ten one-at-a-time
        /// calls would spend ten requests against the rate limit to learn the
        /// same thing.
        /// </summary>
        [HttpPost("{id:int}/attempts")]
        public async Task<ActionResult<AttemptResultDto>> Submit(
            int id, SubmitAttemptDto dto, CancellationToken ct)
        {
            var quiz = await _db.Quizzes
                .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.Id == id && q.UserId == CurrentUserId, ct);

            if (quiz is null) return NotFound(QuizNotFound());

            var given = dto.Answers.ToDictionary(a => a.QuestionId, a => a.Answer ?? string.Empty);
            var questions = quiz.Questions.OrderBy(q => q.Order).ToList();

            var results = new List<GradedQuestionDto>();
            var toGrade = new List<AnswerToGrade>();

            foreach (var question in questions)
            {
                var answer = given.TryGetValue(question.Id, out var value) ? value.Trim() : string.Empty;

                var result = new GradedQuestionDto
                {
                    QuestionId = question.Id,
                    QuestionText = question.QuestionText,
                    QuestionType = question.QuestionType,
                    Options = ReadOptions(question.Options),
                    GivenAnswer = answer,
                    CorrectAnswer = question.CorrectAnswer,
                    Explanation = question.Explanation
                };

                if (question.QuestionType == QuestionType.ShortAnswer)
                {
                    // A blank is wrong without spending an AI request on it.
                    if (answer.Length == 0)
                    {
                        result.IsCorrect = false;
                        result.Feedback = "No answer given.";
                    }
                    else
                    {
                        toGrade.Add(new AnswerToGrade(
                            question.Id, question.QuestionText, question.CorrectAnswer, answer));
                    }
                }
                else
                {
                    result.IsCorrect = string.Equals(
                        answer, question.CorrectAnswer, StringComparison.OrdinalIgnoreCase);
                }

                results.Add(result);
            }

            if (toGrade.Count > 0)
            {
                var usage = _limiter.Peek(CurrentUserId);

                if (usage.Exceeded)
                {
                    // Out of allowance: the test is still marked and returned,
                    // with the written answers flagged rather than the whole
                    // submission thrown away.
                    foreach (var item in toGrade)
                    {
                        var result = results.First(r => r.QuestionId == item.Index);
                        result.IsCorrect = false;
                        result.Feedback =
                            "Not graded - you have used all your AI requests for today. Compare "
                            + "your answer with the model answer yourself.";
                    }
                }
                else
                {
                    _limiter.Consume(CurrentUserId);

                    try
                    {
                        foreach (var grade in await _studyTools.GradeWrittenAnswersAsync(toGrade, ct))
                        {
                            var result = results.First(r => r.QuestionId == grade.Index);
                            result.IsCorrect = grade.Correct;
                            result.Feedback = grade.Feedback;
                        }
                    }
                    catch (Exception ex) when (ex is StudyToolException or HttpRequestException)
                    {
                        _logger.LogWarning(ex, "Grading written answers failed");

                        foreach (var item in toGrade)
                        {
                            var result = results.First(r => r.QuestionId == item.Index);
                            result.IsCorrect = false;
                            result.Feedback =
                                "Could not be graded automatically just now. Compare your answer "
                                + "with the model answer yourself.";
                        }
                    }
                }
            }

            var correct = results.Count(r => r.IsCorrect);
            var score = results.Count == 0 ? 0 : (int)Math.Round(correct * 100.0 / results.Count);
            var now = DateTime.UtcNow;

            var attempt = new QuizAttempt
            {
                QuizId = quiz.Id,
                UserId = CurrentUserId,
                StartedAt = now,
                CompletedAt = now,
                Score = score,
                Answers = results.Select(r => new QuizAnswer
                {
                    QuestionId = r.QuestionId,
                    GivenAnswer = r.GivenAnswer,
                    IsCorrect = r.IsCorrect,
                    Feedback = r.Feedback
                }).ToList()
            };

            _db.QuizAttempts.Add(attempt);
            await _db.SaveChangesAsync(ct);

            return Ok(new AttemptResultDto
            {
                AttemptId = attempt.Id,
                QuizId = quiz.Id,
                QuizTitle = quiz.Title,
                Score = score,
                CorrectCount = correct,
                TotalCount = results.Count,
                CompletedAt = now,
                Questions = results
            });
        }

        [HttpGet("{id:int}/attempts")]
        public async Task<ActionResult<List<AttemptSummaryDto>>> GetAttempts(int id)
        {
            if (!await _db.Quizzes.AnyAsync(q => q.Id == id && q.UserId == CurrentUserId))
            {
                return NotFound(QuizNotFound());
            }

            var attempts = await _db.QuizAttempts
                .Where(a => a.QuizId == id && a.UserId == CurrentUserId && a.CompletedAt != null)
                .OrderByDescending(a => a.CompletedAt)
                .Select(a => new AttemptSummaryDto
                {
                    Id = a.Id,
                    Score = a.Score ?? 0,
                    CompletedAt = a.CompletedAt!.Value
                })
                .ToListAsync();

            return Ok(attempts);
        }

        // ---------- helpers ----------

        /// <summary>
        /// Turns generated questions into rows.
        ///
        /// Shared with StudyController, which saves a test the chat produced -
        /// both paths must store questions identically or the taking screen
        /// would behave differently depending on where the test came from.
        /// </summary>
        public static List<QuizQuestion> BuildQuestions(IReadOnlyList<GeneratedQuestion> questions) =>
            questions.Select((q, index) => new QuizQuestion
            {
                QuestionText = q.Question,
                QuestionType = q.Type switch
                {
                    "true_false" => QuestionType.TrueFalse,
                    "short_answer" => QuestionType.ShortAnswer,
                    _ => QuestionType.MultipleChoice
                },
                Options = q.Options is { Count: > 0 } ? JsonSerializer.Serialize(q.Options) : null,
                CorrectAnswer = q.Answer,
                Explanation = q.Explanation,
                Order = index
            }).ToList();

        public static List<string> ReadOptions(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch (JsonException)
            {
                return new List<string>();
            }
        }

        private record SourceText(string? Text, string? Name, ActionResult? Error);

        private async Task<SourceText> ResolveSourceAsync(GenerateQuizDto dto, CancellationToken ct)
        {
            if (dto.SourceNoteId is int noteId)
            {
                var note = await _db.Notes.FirstOrDefaultAsync(
                    n => n.Id == noteId && n.UserId == CurrentUserId && n.CourseId == dto.CourseId, ct);

                if (note is null)
                {
                    return new SourceText(null, null, NotFound(new ApiErrorDto(
                        "NoteNotFound", "That note does not exist in this course.")));
                }

                if (string.IsNullOrWhiteSpace(note.Content))
                {
                    return new SourceText(null, null, BadRequest(new ApiErrorDto(
                        "SourceEmpty", "That note is empty, so there is nothing to write questions from.")));
                }

                return new SourceText(note.Content, note.Title, null);
            }

            var material = await _db.StudyMaterials.FirstOrDefaultAsync(
                m => m.Id == dto.SourceMaterialId
                     && m.UserId == CurrentUserId
                     && m.CourseId == dto.CourseId, ct);

            if (material is null)
            {
                return new SourceText(null, null, NotFound(new ApiErrorDto(
                    "MaterialNotFound", "That file does not exist in this course.")));
            }

            if (!_textExtraction.CanExtract(material.FileName, material.ContentType))
            {
                return new SourceText(null, null, BadRequest(new ApiErrorDto(
                    "UnsupportedFile",
                    "Tests can only be made from PDF, Word, PowerPoint, and text files.")));
            }

            try
            {
                await using var stream = await _storage.OpenAsync(material.StoredPath, ct);

                var extracted = await _textExtraction.ExtractAsync(
                    stream, material.FileName, material.ContentType,
                    maxCharacters: StudyToolAiService.MaxSourceCharacters, ct: ct);

                if (extracted.IsEmpty)
                {
                    return new SourceText(null, null, StatusCode(422, new ApiErrorDto(
                        "NoTextFound",
                        "No readable text was found in that file. Scanned documents and slides "
                        + "made entirely of images have no text to work from.")));
                }

                return new SourceText(extracted.Text, material.FileName, null);
            }
            catch (FileNotFoundException)
            {
                return new SourceText(null, null, NotFound(new ApiErrorDto(
                    "FileMissing", "The stored file could not be found.")));
            }
        }

        private static ApiErrorDto QuizNotFound() =>
            new("QuizNotFound", "That test does not exist, or it does not belong to you.");
    }
}

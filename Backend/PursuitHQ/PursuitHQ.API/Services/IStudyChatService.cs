using PursuitHQ.API.Models;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// The study tutor: answers questions about a student's own course
    /// material, and writes study guides when asked for one.
    /// </summary>
    public interface IStudyChatService
    {
        bool IsConfigured { get; }

        Task<StudyChatReply> AskAsync(StudyChatRequest request, CancellationToken ct = default);
    }

    /// <param name="Question">What the student just asked.</param>
    /// <param name="CourseName">Named in the prompt so answers sound like they belong to the course.</param>
    /// <param name="SourceText">Text from the materials the student put in context. May be empty.</param>
    /// <param name="History">Earlier turns, oldest first, already trimmed to a sane length.</param>
    public record StudyChatRequest(
        string Question,
        string CourseName,
        string SourceText,
        IReadOnlyList<StudyChatTurn> History);

    public record StudyChatTurn(StudyMessageRole Role, string Content);

    /// <param name="Text">The conversational reply.</param>
    /// <param name="Artifact">Something worth keeping, when the reply produced one.</param>
    public record StudyChatReply(string Text, StudyArtifact? Artifact);

    public record StudyArtifact(StudyArtifactKind Kind, string Title, string Content);
}

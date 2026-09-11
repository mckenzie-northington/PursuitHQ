using PursuitHQ.API.DTOs.Resumes;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// The two AI jobs a resume needs: reading one in, and judging one.
    /// </summary>
    public interface IResumeAiService
    {
        bool IsConfigured { get; }

        /// <summary>
        /// Reads the text of an uploaded resume into structured sections.
        ///
        /// Whatever comes back is a first draft for the student to correct, not
        /// a source of truth - resumes are laid out in columns and tables that
        /// come out of a PDF in surprising orders.
        /// </summary>
        Task<ResumeContentDto> ParseAsync(string resumeText, CancellationToken ct = default);

        /// <summary>
        /// Reviews the content: what is vague, what is unsupported, what is
        /// missing. Format problems are caught by rules instead.
        /// </summary>
        Task<ResumeReviewDto> ReviewAsync(ResumeContentDto resume, CancellationToken ct = default);
    }
}

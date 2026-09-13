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
        /// Scores a resume against one job description.
        ///
        /// The model's job is to pull the posting's requirements apart and say,
        /// with evidence, which ones the resume answers. The percentage is
        /// arithmetic done afterwards - see JobMatchDto for why that split
        /// matters.
        /// </summary>
        Task<JobMatchDto> MatchAsync(
            ResumeContentDto resume, string jobDescription, CancellationToken ct = default);

        /// <summary>
        /// Reviews the content: what is vague, what is unsupported, what is
        /// missing. Format problems are caught by rules instead.
        /// </summary>
        Task<ResumeReviewDto> ReviewAsync(ResumeContentDto resume, CancellationToken ct = default);
    }
}

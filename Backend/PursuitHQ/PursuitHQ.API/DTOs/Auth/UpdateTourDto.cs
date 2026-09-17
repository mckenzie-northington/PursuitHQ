namespace PursuitHQ.API.DTOs.Auth
{
    /// <summary>
    /// True when the walkthrough has been finished or skipped, false to ask for
    /// it again. A field rather than two endpoints, because "show it again" and
    /// "do not show it again" are the same setting seen from either side.
    /// </summary>
    public class UpdateTourDto
    {
        public bool HasSeenTour { get; set; }
    }
}

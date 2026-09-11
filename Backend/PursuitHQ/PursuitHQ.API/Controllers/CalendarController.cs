using Microsoft.AspNetCore.Mvc;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Calendar;
using PursuitHQ.API.Services;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// The unified calendar feed.
    ///
    /// The merging itself lives in ICalendarFeedService, because the dashboard
    /// asks the same question for today and two answers would drift apart.
    /// </summary>
    [Route("api/calendar")]
    public class CalendarController : ApiControllerBase
    {
        private readonly ICalendarFeedService _feed;

        public CalendarController(ICalendarFeedService feed) => _feed = feed;

        /// <summary>Everything on the calendar between two dates, inclusive.</summary>
        [HttpGet]
        public async Task<ActionResult<List<CalendarItemDto>>> GetCalendar(
            [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
        {
            if (to < from)
            {
                return BadRequest(new ApiErrorDto(
                    "InvalidRange", "The end date must be on or after the start date."));
            }

            // A wide range would expand into a huge number of class occurrences,
            // and no view needs more than a few months at once.
            if (to.DayNumber - from.DayNumber > 400)
            {
                return BadRequest(new ApiErrorDto(
                    "RangeTooLarge", "Please request a range of about a year or less."));
            }

            var result = await _feed.GetAsync(CurrentUserId, from, to, ct);

            // Partial data is worth showing; nothing at all is not.
            if (result.Failures.Count > 0 && result.Items.Count == 0)
            {
                return StatusCode(500, new ApiErrorDto(
                    "CalendarFailed",
                    "Could not load the calendar.",
                    new Dictionary<string, string[]> { ["sources"] = result.Failures.ToArray() }));
            }

            return Ok(result.Items);
        }
    }
}

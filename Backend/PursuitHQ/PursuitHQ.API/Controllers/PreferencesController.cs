using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Preferences;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// Small per-student settings that are not big enough to deserve tables of
    /// their own. Right now that is the saved color palette.
    /// </summary>
    [Route("api/preferences")]
    public partial class PreferencesController : ApiControllerBase
    {
        /// <summary>More than this and the palette stops being a shortcut.</summary>
        private const int MaxColors = 24;

        private readonly ApplicationDbContext _db;

        public PreferencesController(ApplicationDbContext db) => _db = db;

        [HttpGet("colors")]
        public async Task<ActionResult<List<string>>> GetColors()
        {
            var saved = await _db.Users
                .Where(u => u.Id == CurrentUserId)
                .Select(u => u.SavedColors)
                .FirstOrDefaultAsync();

            return Ok(Split(saved));
        }

        [HttpPut("colors")]
        public async Task<ActionResult<List<string>>> SaveColors(SaveColorsDto dto)
        {
            var cleaned = new List<string>();

            foreach (var raw in dto.Colors ?? new List<string>())
            {
                var color = (raw ?? string.Empty).Trim().ToLowerInvariant();

                // Anything that is not a hex color is rejected rather than
                // stored: these values are written straight into a style
                // attribute in the browser, so this is the boundary where that
                // has to be true.
                if (!HexColor().IsMatch(color))
                {
                    return BadRequest(new ApiErrorDto(
                        "InvalidColor", $"\"{raw}\" is not a hex color like #3b82f6."));
                }

                if (!cleaned.Contains(color)) cleaned.Add(color);
            }

            if (cleaned.Count > MaxColors)
            {
                return BadRequest(new ApiErrorDto(
                    "TooManyColors", $"You can save up to {MaxColors} colors."));
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
            if (user is null) return NotFound(new ApiErrorDto("UserNotFound", "Could not find your account."));

            user.SavedColors = cleaned.Count == 0 ? null : string.Join(",", cleaned);
            await _db.SaveChangesAsync();

            return Ok(cleaned);
        }

        private static List<string> Split(string? saved) =>
            string.IsNullOrWhiteSpace(saved)
                ? new List<string>()
                : saved.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                       .ToList();

        [GeneratedRegex("^#(?:[0-9a-f]{3}|[0-9a-f]{6})$")]
        private static partial Regex HexColor();
    }
}

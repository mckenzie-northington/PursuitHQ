"""
Point "is this overdue yet" at the student's clock instead of the server's.

Edits in place, and refuses to write a file if any anchor is missing, so a
file that has drifted since I last read it fails loudly instead of getting
half-patched. Line endings and the BOM are preserved exactly as found.

Run from the repo root:  python patch_tz.py
"""

import sys
from pathlib import Path

API = Path("Backend/PursuitHQ.API")


def edit(relative, replacements):
    path = API / relative
    raw = path.read_bytes()

    bom = raw.startswith(b"\xef\xbb\xbf")
    if bom:
        raw = raw[3:]

    crlf = b"\r\n" in raw
    text = raw.decode("utf-8").replace("\r\n", "\n")

    for old, new in replacements:
        if old not in text:
            sys.exit(f"ANCHOR NOT FOUND in {relative}:\n---\n{old}\n---")
        if text.count(old) != 1:
            sys.exit(f"ANCHOR NOT UNIQUE in {relative} ({text.count(old)}x):\n---\n{old}\n---")
        text = text.replace(old, new)

    out = text.replace("\n", "\r\n").encode("utf-8") if crlf else text.encode("utf-8")
    path.write_bytes((b"\xef\xbb\xbf" if bom else b"") + out)
    print(f"patched {relative}  ({len(replacements)} edits, {'CRLF' if crlf else 'LF'}{', BOM' if bom else ''})")


# ---------------------------------------------------------------- Assignments

edit("Controllers/AssignmentsController.cs", [
    (
        "        private readonly ApplicationDbContext _db;\n"
        "        private readonly ICreationNotifier _notifier;\n"
        "\n"
        "        public AssignmentsController(ApplicationDbContext db, ICreationNotifier notifier)\n"
        "        {\n"
        "            _db = db;\n"
        "            _notifier = notifier;\n"
        "        }",

        "        private readonly ApplicationDbContext _db;\n"
        "        private readonly ICreationNotifier _notifier;\n"
        "        private readonly IUserClock _clock;\n"
        "\n"
        "        public AssignmentsController(\n"
        "            ApplicationDbContext db, ICreationNotifier notifier, IUserClock clock)\n"
        "        {\n"
        "            _db = db;\n"
        "            _notifier = notifier;\n"
        "            _clock = clock;\n"
        "        }"
    ),
    (
        "            // Due dates are wall-clock, so \"overdue\" is a wall-clock question.\n"
        "            // DateTime.Now is the server's clock, which is the student's clock\n"
        "            // while the app runs on their laptop. Once this is deployed the\n"
        "            // server will be on UTC and this needs ApplicationUser.TimeZone\n"
        "            // instead - noted in docs/Deployment.md.\n"
        "            var now = DateTime.Now;",

        "            // Due dates are wall-clock, so \"overdue\" is a wall-clock question,\n"
        "            // and the wall clock that matters is the student's. See IUserClock.\n"
        "            var now = await _clock.LocalNowAsync(CurrentUserId);"
    ),
    (
        "            return Ok(ToDto(assignment));\n"
        "        }\n"
        "\n"
        "        [HttpPost]",

        "            return Ok(ToDto(assignment, await _clock.LocalNowAsync(CurrentUserId)));\n"
        "        }\n"
        "\n"
        "        [HttpPost]"
    ),
    (
        "            return CreatedAtAction(nameof(GetAssignment), new { id = assignment.Id }, ToDto(assignment));",

        "            return CreatedAtAction(\n"
        "                nameof(GetAssignment),\n"
        "                new { id = assignment.Id },\n"
        "                ToDto(assignment, await _clock.LocalNowAsync(CurrentUserId)));"
    ),
    (
        "            return Ok(ToDto(assignment));\n"
        "        }\n"
        "\n"
        "        /// <summary>\n"
        "        /// Flips just the status",

        "            return Ok(ToDto(assignment, await _clock.LocalNowAsync(CurrentUserId)));\n"
        "        }\n"
        "\n"
        "        /// <summary>\n"
        "        /// Flips just the status"
    ),
    (
        "            return Ok(ToDto(assignment));\n"
        "        }\n"
        "\n"
        "        [HttpDelete(\"{id:int}\")]",

        "            return Ok(ToDto(assignment, await _clock.LocalNowAsync(CurrentUserId)));\n"
        "        }\n"
        "\n"
        "        [HttpDelete(\"{id:int}\")]"
    ),
    (
        "        private static AssignmentDto ToDto(Assignment a) => new()",

        "        /// <summary>\n"
        "        /// Takes \"now\" rather than reading a clock, so that every caller has to\n"
        "        /// decide whose clock it is. That decision being invisible is what made\n"
        "        /// this wrong in the first place.\n"
        "        /// </summary>\n"
        "        private static AssignmentDto ToDto(Assignment a, DateTime now) => new()"
    ),
    (
        "            IsOverdue = a.DueDate < DateTime.Now && a.Status != AssignmentStatus.Completed\n"
        "        };",

        "            IsOverdue = a.DueDate < now && a.Status != AssignmentStatus.Completed\n"
        "        };"
    ),
])

# ------------------------------------------------------------------- Calendar

edit("Services/CalendarFeedService.cs", [
    (
        "        private readonly ApplicationDbContext _db;\n"
        "        private readonly ILogger<CalendarFeedService> _logger;\n"
        "\n"
        "        public CalendarFeedService(ApplicationDbContext db, ILogger<CalendarFeedService> logger)\n"
        "        {\n"
        "            _db = db;\n"
        "            _logger = logger;\n"
        "        }",

        "        private readonly ApplicationDbContext _db;\n"
        "        private readonly ILogger<CalendarFeedService> _logger;\n"
        "        private readonly IUserClock _clock;\n"
        "\n"
        "        public CalendarFeedService(\n"
        "            ApplicationDbContext db,\n"
        "            ILogger<CalendarFeedService> logger,\n"
        "            IUserClock clock)\n"
        "        {\n"
        "            _db = db;\n"
        "            _logger = logger;\n"
        "            _clock = clock;\n"
        "        }"
    ),
    (
        "            var today = DateOnly.FromDateTime(DateTime.Now);",

        "            var today = DateOnly.FromDateTime(await _clock.LocalNowAsync(userId));"
    ),
    (
        "            // Wall-clock, to match how due dates are stored. See the note in\n"
        "            // AssignmentsController.\n"
        "            var now = DateTime.Now;",

        "            // Wall-clock, to match how due dates are stored, and the student's\n"
        "            // wall clock rather than this machine's. See IUserClock.\n"
        "            var now = await _clock.LocalNowAsync(userId);"
    ),
])

# --------------------------------------------------------------- Notification
#
# It already got this right; the point of the edit is that the fallback
# behaviour now lives in exactly one place rather than two that can drift.

edit("Services/NotificationService.cs", [
    (
        "        private readonly ILogger<NotificationService> _logger;\n",

        "        private readonly ILogger<NotificationService> _logger;\n"
        "        private readonly IUserClock _clock;\n"
    ),
    (
        "            ILogger<NotificationService> logger)\n"
        "        {\n"
        "            _db = db;\n"
        "            _calendar = calendar;\n"
        "            _email = email;\n"
        "            _options = options.Value;\n"
        "            _logger = logger;\n"
        "        }",

        "            ILogger<NotificationService> logger,\n"
        "            IUserClock clock)\n"
        "        {\n"
        "            _db = db;\n"
        "            _calendar = calendar;\n"
        "            _email = email;\n"
        "            _options = options.Value;\n"
        "            _logger = logger;\n"
        "            _clock = clock;\n"
        "        }"
    ),
    (
        "                var localNow = LocalNow(row.TimeZone);",

        "                var localNow = _clock.LocalNow(row.TimeZone);"
    ),
    (
        "        /// <summary>\n"
        "        /// The current wall-clock time where this student is.\n"
        "        ///\n"
        "        /// An unknown or misspelled zone falls back to the server's own time\n"
        "        /// rather than throwing: a reminder an hour off is a nuisance, but one\n"
        "        /// bad row stopping the run for everybody is a real problem.\n"
        "        /// </summary>\n"
        "        private DateTime LocalNow(string? timeZoneId)\n"
        "        {\n"
        "            if (string.IsNullOrWhiteSpace(timeZoneId)) return DateTime.Now;\n"
        "\n"
        "            try\n"
        "            {\n"
        "                return TimeZoneInfo.ConvertTimeFromUtc(\n"
        "                    DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));\n"
        "            }\n"
        "            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)\n"
        "            {\n"
        "                _logger.LogWarning(\"Unknown time zone {TimeZone}; using server time.\", timeZoneId);\n"
        "                return DateTime.Now;\n"
        "            }\n"
        "        }\n",

        ""
    ),
])

# ------------------------------------------------------------------ Dashboard

edit("Controllers/DashboardController.cs", [
    (
        "        private readonly ApplicationDbContext _db;\n"
        "        private readonly ICalendarFeedService _feed;\n"
        "\n"
        "        public DashboardController(ApplicationDbContext db, ICalendarFeedService feed)\n"
        "        {\n"
        "            _db = db;\n"
        "            _feed = feed;\n"
        "        }",

        "        private readonly ApplicationDbContext _db;\n"
        "        private readonly ICalendarFeedService _feed;\n"
        "        private readonly IUserClock _clock;\n"
        "\n"
        "        public DashboardController(\n"
        "            ApplicationDbContext db, ICalendarFeedService feed, IUserClock clock)\n"
        "        {\n"
        "            _db = db;\n"
        "            _feed = feed;\n"
        "            _clock = clock;\n"
        "        }"
    ),
    (
        "            // Wall-clock, to match how due dates are stored. See the note in\n"
        "            // DatabaseDesign.md section 4a.\n"
        "            var now = DateTime.Now;",

        "            // Wall-clock, to match how due dates are stored (DatabaseDesign.md\n"
        "            // section 4a), in the student's own zone rather than this machine's.\n"
        "            // \"Today\" and the lookahead window both hang off this, so getting it\n"
        "            // from the server clock would shift the whole dashboard, not just the\n"
        "            // overdue flags.\n"
        "            var now = await _clock.LocalNowAsync(CurrentUserId, ct);"
    ),
])

# ------------------------------------------------------------------------ Auth
#
# A stored zone this machine cannot resolve does not fail loudly - it falls
# back to server time and quietly mis-times every reminder from then on. So it
# gets checked at the point of saving, while the student is still looking at
# the screen.

edit("Controllers/AuthController.cs", [
    (
        "                TimeZone = string.IsNullOrWhiteSpace(dto.TimeZone) ? \"America/New_York\" : dto.TimeZone,",

        "                // A zone this server cannot resolve is not worth failing a\n"
        "                // sign-up over: the default is wrong for some people, an\n"
        "                // account they cannot create is wrong for all of them.\n"
        "                TimeZone = IsResolvableTimeZone(dto.TimeZone) ? dto.TimeZone! : \"America/New_York\","
    ),
    (
        "            if (!string.IsNullOrWhiteSpace(dto.TimeZone))\n"
        "            {\n"
        "                user.TimeZone = dto.TimeZone;\n"
        "            }",

        "            if (!string.IsNullOrWhiteSpace(dto.TimeZone))\n"
        "            {\n"
        "                if (!IsResolvableTimeZone(dto.TimeZone))\n"
        "                {\n"
        "                    return BadRequest(new ApiErrorDto(\n"
        "                        \"UnknownTimeZone\", \"This server does not recognise that time zone.\"));\n"
        "                }\n"
        "\n"
        "                user.TimeZone = dto.TimeZone;\n"
        "            }"
    ),
    (
        "        private static UserProfileDto ToProfileDto(ApplicationUser user) => new()",

        "        /// <summary>\n"
        "        /// Changes only the time zone.\n"
        "        ///\n"
        "        /// Separate from UpdateMe because that one writes the whole profile from\n"
        "        /// whatever it is handed. A caller that knows only the zone - the prompt\n"
        "        /// that appears when the device disagrees - would blank the student's\n"
        "        /// name, major and graduation year on its way past.\n"
        "        /// </summary>\n"
        "        [HttpPut(\"me/timezone\")]\n"
        "        [Authorize]\n"
        "        public async Task<ActionResult<UserProfileDto>> UpdateMyTimeZone(UpdateTimeZoneDto dto)\n"
        "        {\n"
        "            var user = await GetCurrentUserAsync();\n"
        "            if (user is null) return Unauthorized();\n"
        "\n"
        "            if (!IsResolvableTimeZone(dto.TimeZone))\n"
        "            {\n"
        "                return BadRequest(new ApiErrorDto(\n"
        "                    \"UnknownTimeZone\", \"This server does not recognise that time zone.\"));\n"
        "            }\n"
        "\n"
        "            user.TimeZone = dto.TimeZone;\n"
        "\n"
        "            var result = await _userManager.UpdateAsync(user);\n"
        "            if (!result.Succeeded)\n"
        "            {\n"
        "                return BadRequest(new ApiErrorDto(\n"
        "                    \"UpdateFailed\", \"Could not update the time zone.\"));\n"
        "            }\n"
        "\n"
        "            return Ok(ToProfileDto(user));\n"
        "        }\n"
        "\n"
        "        /// <summary>\n"
        "        /// Whether this machine can actually resolve the id.\n"
        "        ///\n"
        "        /// The picker offers IANA names, which .NET resolves on Windows and Linux\n"
        "        /// alike so long as ICU is present - which it is unless the app is\n"
        "        /// published in globalization-invariant mode. That is exactly the case\n"
        "        /// worth catching here rather than at 3am in a reminder job.\n"
        "        /// </summary>\n"
        "        private static bool IsResolvableTimeZone(string? id)\n"
        "        {\n"
        "            if (string.IsNullOrWhiteSpace(id)) return false;\n"
        "\n"
        "            try\n"
        "            {\n"
        "                TimeZoneInfo.FindSystemTimeZoneById(id);\n"
        "                return true;\n"
        "            }\n"
        "            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)\n"
        "            {\n"
        "                return false;\n"
        "            }\n"
        "        }\n"
        "\n"
        "        private static UserProfileDto ToProfileDto(ApplicationUser user) => new()"
    ),
])

# -------------------------------------------------------------------- Startup

edit("Program.cs", [
    (
        "builder.Services.AddScoped<ICalendarFeedService, CalendarFeedService>();",

        "builder.Services.AddScoped<IUserClock, UserClock>();\n"
        "builder.Services.AddScoped<ICalendarFeedService, CalendarFeedService>();"
    ),
])

print("\nAll patched. Next: dotnet build.")

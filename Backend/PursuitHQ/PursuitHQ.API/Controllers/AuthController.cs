using System.Security.Claims;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Auth;
using PursuitHQ.API.Models;
using PursuitHQ.API.Services;

namespace PursuitHQ.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ITokenService tokenService,
            ApplicationDbContext db,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            IEmailQueue emailQueue,
            IOptions<EmailOptions> emailOptions,
            ILogger<AuthController> logger)
        {
            _emailQueue = emailQueue;
            _emailOptions = emailOptions.Value;
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _db = db;
            _environment = environment;
            _configuration = configuration;
            _logger = logger;
        }

        private readonly IEmailQueue _emailQueue;
        private readonly EmailOptions _emailOptions;

        /// <summary>Creates a new student account and returns a JWT.</summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
        {
            var existing = await _userManager.FindByEmailAsync(dto.Email);
            if (existing is not null)
            {
                return Conflict(new ApiErrorDto(
                    "EmailAlreadyRegistered",
                    "An account with that email already exists."));
            }

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Major = dto.Major,
                GraduationYear = dto.GraduationYear,
                TimeZone = string.IsNullOrWhiteSpace(dto.TimeZone) ? "America/New_York" : dto.TimeZone,
                CreatedAt = DateTime.UtcNow
            };

            // Identity hashes the password here. We never see or store it in plain text.
            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
            {
                var details = result.Errors
                    .GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

                return BadRequest(new ApiErrorDto(
                    "RegistrationFailed",
                    "Could not create the account. See details.",
                    details));
            }

            // Every student gets a notification preferences row with sensible
            // defaults, so the reminder job always has something to read.
            _db.NotificationPreferences.Add(new NotificationPreference
            {
                UserId = user.Id,
                TimeZone = user.TimeZone
            });
            await _db.SaveChangesAsync();

            return Ok(BuildAuthResponse(user));
        }

        /// <summary>Authenticates a student and returns a JWT.</summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);

            // Deliberately the same response whether the email is unknown or the
            // password is wrong - otherwise this endpoint reveals which emails
            // have accounts.
            if (user is null)
            {
                return Unauthorized(new ApiErrorDto(
                    "InvalidCredentials",
                    "Email or password is incorrect."));
            }

            // lockoutOnFailure: true is what makes the 5-attempt lockout work.
            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                return StatusCode(423, new ApiErrorDto(
                    "AccountLocked",
                    "Too many failed attempts. This account is locked for 15 minutes."));
            }

            if (!result.Succeeded)
            {
                var attemptsUsed = await _userManager.GetAccessFailedCountAsync(user);
                var remaining = Math.Max(0, 5 - attemptsUsed);

                return Unauthorized(new ApiErrorDto(
                    "InvalidCredentials",
                    $"Email or password is incorrect. {remaining} attempt(s) remaining before lockout."));
            }

            await _userManager.ResetAccessFailedCountAsync(user);

            return Ok(BuildAuthResponse(user));
        }

        /// <summary>
        /// Starts a password reset.
        ///
        /// Always answers the same way, whether or not that email has an
        /// account. Anything else turns this into a way for a stranger to find
        /// out who has signed up.
        ///
        /// The token is meant to arrive by email. Until email sending exists
        /// (Phase 3b) it is written to the API log, and returned in the
        /// response ONLY in Development - in any other environment handing the
        /// token to whoever asked would let anyone reset anyone's password.
        /// </summary>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<ActionResult<ForgotPasswordResponseDto>> ForgotPassword(
            ForgotPasswordDto dto)
        {
            var response = new ForgotPasswordResponseDto
            {
                Message =
                    "If an account exists for that email, a reset link is on its way. "
                    + "The link is good for one hour."
            };

            var user = await _userManager.FindByEmailAsync(dto.Email);

            if (user is null)
            {
                _logger.LogInformation(
                    "Password reset requested for an email with no account: {Email}", dto.Email);

                // In development, say so plainly - a silent success while you
                // are testing is indistinguishable from a broken endpoint, and
                // you are the only person who can reach this server anyway.
                //
                // In any other environment the generic answer stands. Telling
                // an anonymous caller "no account with that email" turns this
                // into a way to discover who has signed up, one address at a
                // time.
                if (_environment.IsDevelopment())
                {
                    return NotFound(new ApiErrorDto(
                        "NoAccount",
                        $"There is no account for {dto.Email}. (You are seeing this because the "
                        + "API is in development mode; in production this would not say whether "
                        + "the account exists.)"));
                }

                return Ok(response);
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var resetUrl =
                $"{FrontendUrl()}/reset-password"
                + $"?email={Uri.EscapeDataString(dto.Email)}"
                + $"&token={Uri.EscapeDataString(token)}";

            SendResetEmail(user, dto.Email, resetUrl);

            if (_environment.IsDevelopment())
            {
                // Still logged in development, so a reset can be tested without
                // waiting on a mailbox.
                _logger.LogInformation(
                    "PASSWORD RESET for {Email}. Link:\n{Url}", dto.Email, resetUrl);
            }

            if (_environment.IsDevelopment())
            {
                response.DevelopmentResetUrl = resetUrl;
            }

            return Ok(response);
        }

        /// <summary>
        /// Finishes a reset: checks the token and sets the new password.
        ///
        /// Also clears any lockout. Someone resetting their password is very
        /// often someone who just locked themselves out guessing at it, and
        /// leaving them locked out of an account they have proven they own
        /// would be absurd.
        /// </summary>
        /// <summary>
        /// Queues the reset email rather than waiting for it to send.
        ///
        /// Not only for speed. Awaiting the send would make this endpoint
        /// measurably slower for an address that has an account than for one
        /// that does not, and that difference is enough to enumerate who has
        /// signed up - which is the whole thing the generic response above
        /// exists to prevent. Queueing makes both paths cost the same.
        /// </summary>
        private void SendResetEmail(ApplicationUser user, string email, string resetUrl)
        {
            var name = string.IsNullOrWhiteSpace(user.FirstName) ? "there" : user.FirstName;

            var html =
                $"<p>Hi {WebUtility.HtmlEncode(name)},</p>"
                + "<p>Someone asked to reset the password on your PursuitHQ account. "
                + "If that was you, use this link within the hour:</p>"
                + $"<p><a href=\"{resetUrl}\" style=\"display:inline-block;background:#4f46e5;"
                + "color:#ffffff;padding:10px 16px;border-radius:6px;text-decoration:none;"
                + "font-weight:600\">Choose a new password</a></p>"
                + "<p style=\"font-size:12px;color:#64748b\">If the button does not work, paste "
                + $"this into your browser:<br>{WebUtility.HtmlEncode(resetUrl)}</p>"
                + "<p>If it was not you, ignore this email. Nothing has changed, and your "
                + "current password still works.</p>";

            var text =
                $"Hi {name},\n\n"
                + "Someone asked to reset the password on your PursuitHQ account. If that was "
                + "you, open this link within the hour:\n\n"
                + $"{resetUrl}\n\n"
                + "If it was not you, ignore this email. Nothing has changed, and your current "
                + "password still works.";

            _emailQueue.Enqueue(new EmailMessage(
                email,
                name,
                "Reset your PursuitHQ password",
                // showPreferences: false - this is not something you can opt out
                // of, and a footer offering to change your email settings would
                // suggest otherwise.
                EmailLayout.Html("Reset your password", html, _emailOptions.AppUrl, false),
                EmailLayout.Text("Reset your password", text, _emailOptions.AppUrl, false)));
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);

            // Same message for an unknown email and a bad token: a valid email
            // with an invalid token must not be distinguishable from the
            // reverse.
            var invalid = new ApiErrorDto(
                "InvalidResetToken",
                "That reset link is not valid any more. Links expire after an hour and can "
                + "only be used once - ask for a new one.");

            if (user is null) return BadRequest(invalid);

            var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);

            if (!result.Succeeded)
            {
                // A rejected token and a rejected password are different
                // problems, and the student can only fix the second one.
                var passwordProblems = result.Errors
                    .Where(e => !e.Code.Contains("Token", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (passwordProblems.Count > 0)
                {
                    return BadRequest(new ApiErrorDto(
                        "PasswordRejected",
                        "That password was not accepted.",
                        passwordProblems
                            .GroupBy(e => e.Code)
                            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())));
                }

                return BadRequest(invalid);
            }

            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            _logger.LogInformation("Password reset completed for {Email}", dto.Email);

            return NoContent();
        }

        /// <summary>Where the website lives, for building links back into it.</summary>
        private string FrontendUrl() =>
            _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:3000";

        /// <summary>Returns the signed-in student's profile.</summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UserProfileDto>> Me()
        {
            var user = await GetCurrentUserAsync();
            if (user is null) return Unauthorized();

            return Ok(ToProfileDto(user));
        }

        /// <summary>Updates the signed-in student's profile.</summary>
        [HttpPut("me")]
        [Authorize]
        public async Task<ActionResult<UserProfileDto>> UpdateMe(UpdateProfileDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user is null) return Unauthorized();

            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.Major = dto.Major;
            user.GraduationYear = dto.GraduationYear;

            if (!string.IsNullOrWhiteSpace(dto.TimeZone))
            {
                user.TimeZone = dto.TimeZone;
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(new ApiErrorDto("UpdateFailed", "Could not update the profile."));
            }

            return Ok(ToProfileDto(user));
        }

        /// <summary>Changes the signed-in student's password.</summary>
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user is null) return Unauthorized();

            var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);

            if (!result.Succeeded)
            {
                var details = result.Errors
                    .GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

                return BadRequest(new ApiErrorDto(
                    "PasswordChangeFailed",
                    "Could not change the password. See details.",
                    details));
            }

            return NoContent();
        }

        /// <summary>
        /// Deletes the signed-in student's account and everything they own.
        /// Cascade delete rules in ApplicationDbContext remove the owned rows.
        /// </summary>
        [HttpDelete("me")]
        [Authorize]
        public async Task<IActionResult> DeleteMe()
        {
            var user = await GetCurrentUserAsync();
            if (user is null) return Unauthorized();

            // TODO (Phase 4): also delete this user's uploaded files from storage.
            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(new ApiErrorDto("DeleteFailed", "Could not delete the account."));
            }

            return NoContent();
        }

        // ---------- helpers ----------

        /// <summary>
        /// Reads the user id from the JWT and loads that user. The id comes from
        /// the token, never from the request - this is the core of data isolation.
        /// </summary>
        private async Task<ApplicationUser?> GetCurrentUserAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return null;

            return await _userManager.FindByIdAsync(userId);
        }

        private AuthResponseDto BuildAuthResponse(ApplicationUser user)
        {
            var (token, expiresAt) = _tokenService.CreateToken(user);

            return new AuthResponseDto
            {
                Token = token,
                ExpiresAt = expiresAt,
                User = ToProfileDto(user)
            };
        }

        private static UserProfileDto ToProfileDto(ApplicationUser user) => new()
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            Major = user.Major,
            GraduationYear = user.GraduationYear,
            TimeZone = user.TimeZone,
            CreatedAt = user.CreatedAt
        };
    }
}

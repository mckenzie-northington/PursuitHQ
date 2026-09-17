using System.Security.Claims;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
        /// <summary>
        /// The youngest somebody may be to hold an account.
        ///
        /// Sixteen rather than thirteen: the rules protecting children are
        /// strict, vary by country and state, and are a poor fit for a project
        /// maintained by one person. Setting the line above where most of them
        /// begin is the proportionate answer for an app aimed at college
        /// students in the first place.
        /// </summary>
        private const int MinimumAge = 16;

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
            IAccountNotifier accountNotifier,
            IOptions<EmailOptions> emailOptions,
            ILogger<AuthController> logger)
        {
            _emailQueue = emailQueue;
            _accountNotifier = accountNotifier;
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
        private readonly IAccountNotifier _accountNotifier;
        private readonly EmailOptions _emailOptions;

        /// <summary>Creates a new student account and returns a JWT.</summary>
        [HttpPost("register")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
        {
            if (!dto.AcceptedTerms)
            {
                return BadRequest(new ApiErrorDto(
                    "TermsNotAccepted",
                    "You need to accept the terms and privacy policy to create an account."));
            }

            if (dto.DateOfBirth is not DateOnly birthday || !IsOldEnough(birthday))
            {
                // One message for "too young" and for "that date makes no
                // sense", so a refusal does not read as an invitation to try
                // again with a different year.
                return BadRequest(new ApiErrorDto(
                    "AgeRequirement",
                    $"You need to be at least {MinimumAge} to use PursuitHQ."));
            }

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
                // A zone this server cannot resolve is not worth failing a
                // sign-up over: the default is wrong for some people, an
                // account they cannot create is wrong for all of them.
                TimeZone = IsResolvableTimeZone(dto.TimeZone) ? dto.TimeZone! : "America/New_York",
                DateOfBirth = dto.DateOfBirth,
                TermsAcceptedAt = DateTime.UtcNow,
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

            // After the account is fully set up, and deliberately not awaited on
            // the critical path in any meaningful sense - the notifier queues and
            // returns. Registration must not fail because email is having a bad day.
            await _accountNotifier.AccountCreatedAsync(user.Email!, user.FirstName);

            return Ok(BuildAuthResponse(user));
        }

        /// <summary>Authenticates a student and returns a JWT.</summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);

            // An unknown email is told so plainly, rather than being folded into
            // "email or password is incorrect".
            //
            // The trade-off, stated so it is a decision and not an accident:
            // this does confirm whether an address has an account here, which a
            // stranger could use to check a list of addresses. It is allowed
            // because the alternative was people retyping a password they never
            // set on an account they never made, with nothing telling them so -
            // and because the "auth" rate limit caps how fast that list could be
            // worked through. Password reset stays deliberately vague, so the
            // two endpoints together still give nothing away quickly.
            if (user is null)
            {
                return Unauthorized(new ApiErrorDto(
                    "NoAccount",
                    "There is no account with that email address."));
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

            // The password was right. With two-step on, that is only half of
            // it: what goes back names the user and grants nothing at all
            // until a code arrives.
            if (await _userManager.GetTwoFactorEnabledAsync(user))
            {
                return Ok(new AuthResponseDto
                {
                    RequiresTwoFactor = true,
                    TwoFactorToken = _tokenService.CreateTwoFactorToken(user)
                });
            }

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
        [EnableRateLimiting("auth")]
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
        [EnableRateLimiting("auth")]
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
            user.School = string.IsNullOrWhiteSpace(dto.School) ? null : dto.School.Trim();

            // Anything outside the enum is treated as not answered rather than
            // stored: an int off the wire is not a level just because it fits.
            user.EducationLevel = Enum.IsDefined(dto.EducationLevel)
                ? dto.EducationLevel
                : EducationLevel.NotSet;

            user.IsDiscoverable = dto.IsDiscoverable;

            if (!string.IsNullOrWhiteSpace(dto.TimeZone))
            {
                if (!IsResolvableTimeZone(dto.TimeZone))
                {
                    return BadRequest(new ApiErrorDto(
                        "UnknownTimeZone", "This server does not recognise that time zone."));
                }

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
        public async Task<IActionResult> DeleteMe(
            [FromServices] IFileStorageService storage)
        {
            var user = await GetCurrentUserAsync();
            if (user is null) return Unauthorized();

            // Files first, while the rows that name them still exist. Deleting
            // the user first would cascade the rows away and leave every file
            // orphaned in storage with nothing left pointing at it - which is
            // how "delete my account" quietly becomes "keep my account's files".
            var keys = new List<string>();

            keys.AddRange(await _db.StudyMaterials
                .Where(x => x.UserId == user.Id && x.StoredPath != "")
                .Select(x => x.StoredPath)
                .ToListAsync());

            keys.AddRange(await _db.MessageAttachments
                .Where(x => x.Message!.SenderId == user.Id)
                .Select(x => x.StoragePath)
                .ToListAsync());

            if (!string.IsNullOrEmpty(user.PhotoPath)) keys.Add(user.PhotoPath);

            foreach (var key in keys.Distinct())
            {
                // DeleteAsync already swallows its own failures, so one missing
                // file cannot strand somebody in a half-deleted account.
                await storage.DeleteAsync(key);
            }

            // Read before the row is gone. After DeleteAsync there is nothing
            // left to look the address up from, and a deletion confirmation sent
            // nowhere is the one confirmation people actually go looking for.
            var email = user.Email;
            var firstName = user.FirstName;

            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(new ApiErrorDto("DeleteFailed", "Could not delete the account."));
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                await _accountNotifier.AccountDeletedAsync(email, firstName);
            }

            return NoContent();
        }

        /// <summary>
        /// Whether somebody born on this date has already had their birthday
        /// this year, rather than simply subtracting the years.
        /// </summary>
        private static bool IsOldEnough(DateOnly birthday)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // A date in the future, or one implying somebody older than anyone
            // alive, is a typo or a probe rather than an answer.
            if (birthday > today || birthday.Year < today.Year - 120) return false;

            var age = today.Year - birthday.Year;
            if (birthday > today.AddYears(-age)) age--;

            return age >= MinimumAge;
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

        // ---------- two-step verification ----------

        /// <summary>
        /// Finishes a sign-in that stopped for a code.
        /// </summary>
        [HttpPost("2fa/verify")]
        public async Task<ActionResult<AuthResponseDto>> VerifyTwoFactor(TwoFactorVerifyDto dto)
        {
            var userId = _tokenService.ReadTwoFactorToken(dto.TwoFactorToken);

            if (userId is null)
            {
                return Unauthorized(new ApiErrorDto(
                    "TwoFactorExpired",
                    "That sign-in attempt has expired. Enter your password again."));
            }

            var user = await _userManager.FindByIdAsync(userId);

            if (user is null || !await _userManager.GetTwoFactorEnabledAsync(user))
            {
                return Unauthorized(new ApiErrorDto(
                    "InvalidCredentials", "Sign in again."));
            }

            // The lockout that guards the password guards this too. Six digits is
            // a million combinations, which is nothing to a script - without a
            // limit on attempts the second factor would be decoration.
            if (await _userManager.IsLockedOutAsync(user))
            {
                return StatusCode(423, new ApiErrorDto(
                    "AccountLocked",
                    "Too many failed attempts. This account is locked for 15 minutes."));
            }

            var code = NormaliseCode(dto.Code);

            var accepted = await _userManager.VerifyTwoFactorTokenAsync(
                user, _userManager.Options.Tokens.AuthenticatorTokenProvider, code);

            if (!accepted)
            {
                // A recovery code is the way back in when the phone is gone.
                // Redeeming one spends it.
                var redeemed = await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, code);
                accepted = redeemed.Succeeded;
            }

            if (!accepted)
            {
                await _userManager.AccessFailedAsync(user);

                return Unauthorized(new ApiErrorDto(
                    "InvalidTwoFactorCode",
                    "That code is not right. Codes change every 30 seconds - check your app for the current one."));
            }

            await _userManager.ResetAccessFailedCountAsync(user);

            return Ok(BuildAuthResponse(user));
        }

        /// <summary>Whether two-step is on, and how many recovery codes are left.</summary>
        [HttpGet("2fa")]
        [Authorize]
        public async Task<ActionResult<TwoFactorStatusDto>> GetTwoFactorStatus()
        {
            var user = await GetCurrentUserAsync();
            if (user is null) return Unauthorized();

            return Ok(new TwoFactorStatusDto
            {
                Enabled = await _userManager.GetTwoFactorEnabledAsync(user),
                RecoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user)
            });
        }

        /// <summary>Hands out a fresh secret for an authenticator app.</summary>
        [HttpPost("2fa/setup")]
        [Authorize]
        public async Task<ActionResult<TwoFactorSetupDto>> SetUpTwoFactor()
        {
            var user = await GetCurrentUserAsync();
            if (user is null) return Unauthorized();

            if (await _userManager.GetTwoFactorEnabledAsync(user))
            {
                return BadRequest(new ApiErrorDto(
                    "AlreadyEnabled",
                    "Two-step verification is already on. Turn it off first to set up a new device."));
            }

            // A new secret every time setup is opened, so a QR code photographed
            // over someone's shoulder last month is already useless.
            await _userManager.ResetAuthenticatorKeyAsync(user);
            var key = await _userManager.GetAuthenticatorKeyAsync(user);

            if (string.IsNullOrEmpty(key))
            {
                return StatusCode(500, new ApiErrorDto(
                    "SetupFailed", "Could not create an authenticator key."));
            }

            return Ok(new TwoFactorSetupDto
            {
                SharedKey = GroupInFours(key),
                AuthenticatorUri = AuthenticatorUri(user.Email ?? user.UserName ?? "account", key)
            });
        }

        /// <summary>
        /// Turns it on, once a code proves the secret arrived intact.
        ///
        /// Verifying first matters: enabling on trust would lock out anyone whose
        /// scan silently failed, and they would not find out until next sign-in.
        /// </summary>
        [HttpPost("2fa/enable")]
        [Authorize]
        public async Task<ActionResult<RecoveryCodesDto>> EnableTwoFactor(TwoFactorCodeDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user is null) return Unauthorized();

            var valid = await _userManager.VerifyTwoFactorTokenAsync(
                user,
                _userManager.Options.Tokens.AuthenticatorTokenProvider,
                NormaliseCode(dto.Code));

            if (!valid)
            {
                return BadRequest(new ApiErrorDto(
                    "InvalidTwoFactorCode",
                    "That code is not right. Check your phone's clock is set automatically, then try the current code."));
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);

            // Handed over once. Identity stores only hashes, so they cannot be
            // shown again - which is the point, and why the page says so.
            var codes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

            return Ok(new RecoveryCodesDto { Codes = codes?.ToList() ?? new List<string>() });
        }

        /// <summary>Turns it off. Password required.</summary>
        [HttpPost("2fa/disable")]
        [Authorize]
        public async Task<IActionResult> DisableTwoFactor(TwoFactorPasswordDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user is null) return Unauthorized();

            if (!await _userManager.CheckPasswordAsync(user, dto.Password))
            {
                return BadRequest(new ApiErrorDto(
                    "InvalidCredentials", "That password is not right."));
            }

            await _userManager.SetTwoFactorEnabledAsync(user, false);

            // Throw the secret away as well. Turning it back on should mean
            // setting up a device you still have, not reviving a lost one.
            await _userManager.ResetAuthenticatorKeyAsync(user);

            return NoContent();
        }

        /// <summary>A new set of recovery codes, retiring the old ones.</summary>
        [HttpPost("2fa/recovery-codes")]
        [Authorize]
        public async Task<ActionResult<RecoveryCodesDto>> RegenerateRecoveryCodes(
            TwoFactorPasswordDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user is null) return Unauthorized();

            if (!await _userManager.CheckPasswordAsync(user, dto.Password))
            {
                return BadRequest(new ApiErrorDto(
                    "InvalidCredentials", "That password is not right."));
            }

            if (!await _userManager.GetTwoFactorEnabledAsync(user))
            {
                return BadRequest(new ApiErrorDto(
                    "NotEnabled", "Two-step verification is not on for this account."));
            }

            var codes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

            return Ok(new RecoveryCodesDto { Codes = codes?.ToList() ?? new List<string>() });
        }

        /// <summary>Codes get pasted and typed with spaces and dashes in them.</summary>
        private static string NormaliseCode(string code) =>
            code.Replace(" ", string.Empty).Replace("-", string.Empty).Trim();

        /// <summary>"ABCD EFGH IJKL" - far easier to type by hand without slipping.</summary>
        private static string GroupInFours(string key) =>
            string.Join(" ", Enumerable
                .Range(0, (key.Length + 3) / 4)
                .Select(i => key.Substring(i * 4, Math.Min(4, key.Length - i * 4))));

        /// <summary>
        /// The otpauth:// URI every authenticator app understands.
        ///
        /// Both halves of the label are escaped: an email containing a colon
        /// would otherwise split it and the app would show the wrong account.
        /// </summary>
        private static string AuthenticatorUri(string email, string key)
        {
            var issuer = Uri.EscapeDataString("PursuitHQ");
            var label = Uri.EscapeDataString(email);

            return $"otpauth://totp/{issuer}:{label}?secret={key}&issuer={issuer}&digits=6&period=30";
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

        /// <summary>
        /// Changes only the time zone.
        ///
        /// Separate from UpdateMe because that one writes the whole profile from
        /// whatever it is handed. A caller that knows only the zone - the prompt
        /// that appears when the device disagrees - would blank the student's
        /// name, major and graduation year on its way past.
        /// </summary>
        [HttpPut("me/timezone")]
        [Authorize]
        public async Task<ActionResult<UserProfileDto>> UpdateMyTimeZone(UpdateTimeZoneDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user is null) return Unauthorized();

            if (!IsResolvableTimeZone(dto.TimeZone))
            {
                return BadRequest(new ApiErrorDto(
                    "UnknownTimeZone", "This server does not recognise that time zone."));
            }

            user.TimeZone = dto.TimeZone;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(new ApiErrorDto(
                    "UpdateFailed", "Could not update the time zone."));
            }

            return Ok(ToProfileDto(user));
        }

        /// <summary>
        /// Whether this machine can actually resolve the id.
        ///
        /// The picker offers IANA names, which .NET resolves on Windows and Linux
        /// alike so long as ICU is present - which it is unless the app is
        /// published in globalization-invariant mode. That is exactly the case
        /// worth catching here rather than at 3am in a reminder job.
        /// </summary>
        private static bool IsResolvableTimeZone(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(id);
                return true;
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                return false;
            }
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
            School = user.School,
            EducationLevel = user.EducationLevel,
            IsDiscoverable = user.IsDiscoverable,
            HasPhoto = !string.IsNullOrEmpty(user.PhotoPath),
            CreatedAt = user.CreatedAt
        };
    }
}

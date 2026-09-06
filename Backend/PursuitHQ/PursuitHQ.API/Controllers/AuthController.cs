using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ITokenService tokenService,
            ApplicationDbContext db)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _db = db;
        }

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

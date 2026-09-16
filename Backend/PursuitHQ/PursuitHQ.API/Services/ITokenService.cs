using PursuitHQ.API.Models;

namespace PursuitHQ.API.Services
{
    public interface ITokenService
    {
        /// <summary>Creates a signed JWT for the given user.</summary>
        (string Token, DateTime ExpiresAt) CreateToken(ApplicationUser user);

        /// <summary>
        /// Creates the short-lived token that stands between a correct password
        /// and a finished login when two-factor is on.
        ///
        /// It is not a session and cannot be used as one - see the note on the
        /// audience in TokenService.
        /// </summary>
        string CreateTwoFactorToken(ApplicationUser user);

        /// <summary>
        /// Reads a two-factor token back, returning the user id it was issued
        /// for, or null if it is expired, tampered with, or an ordinary session
        /// token being passed off as one.
        /// </summary>
        string? ReadTwoFactorToken(string? token);
    }
}

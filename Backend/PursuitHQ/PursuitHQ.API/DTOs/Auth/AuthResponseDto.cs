namespace PursuitHQ.API.DTOs.Auth
{
    /// <summary>Returned on successful register or login.</summary>
    public class AuthResponseDto
    {
        /// <summary>
        /// Empty when <see cref="RequiresTwoFactor"/> is true. There is no
        /// session yet at that point - the password was right, and that is all
        /// that has been established.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        public UserProfileDto User { get; set; } = new();

        /// <summary>
        /// True when the password was correct but a code is still needed.
        /// Callers must check this before doing anything with Token.
        /// </summary>
        public bool RequiresTwoFactor { get; set; }

        /// <summary>
        /// The five-minute token to send back with the code. Not a session, and
        /// rejected by every authenticated endpoint - see TokenService.
        /// </summary>
        public string? TwoFactorToken { get; set; }
    }
}

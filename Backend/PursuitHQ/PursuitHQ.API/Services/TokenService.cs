using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// Builds the signed JWT the client sends on every subsequent request.
    ///
    /// The token carries the user's id as a claim. Controllers read that claim
    /// to scope every database query - which is why the client never sends a
    /// user id itself and cannot ask for someone else's data.
    /// </summary>
    public class TokenService : ITokenService
    {
        /// <summary>
        /// The audience on a half-finished login.
        ///
        /// This one line is what makes the two-factor step real. The JWT bearer
        /// handler is configured with ValidateAudience = true and the ordinary
        /// audience, so a token minted here is rejected by every authenticated
        /// endpoint in the app exactly as if it were forged. It can only be
        /// spent at the one endpoint that reads it by hand, and that endpoint
        /// will not hand back a session without a correct code.
        ///
        /// Getting this wrong - reusing the normal audience, or skipping
        /// audience validation - would mean a correct password alone still got
        /// you in, and the second factor would be decoration.
        /// </summary>
        private const string TwoFactorAudience = "PursuitHQ2FA";

        /// <summary>
        /// Long enough to go and find your phone, short enough that a token
        /// left on a shared machine is no use by the time anyone finds it.
        /// </summary>
        private static readonly TimeSpan TwoFactorWindow = TimeSpan.FromMinutes(5);

        private readonly IConfiguration _config;

        public TokenService(IConfiguration config)
        {
            _config = config;
        }

        public (string Token, DateTime ExpiresAt) CreateToken(ApplicationUser user)
        {
            var expiryMinutes = int.TryParse(_config["Jwt:ExpiryMinutes"], out var m) ? m : 60;
            var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

            var claims = new List<Claim>
            {
                // The user id. This is what every controller reads to scope queries.
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim("firstName", user.FirstName),
                new Claim("lastName", user.LastName),

                // Unique id for this specific token, so it can be revoked later if needed.
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            return (Write(claims, Audience, expiresAt), expiresAt);
        }

        public string CreateTwoFactorToken(ApplicationUser user)
        {
            // Only the id, and only for five minutes. Nothing here is useful to
            // anyone holding it: it names a user but grants nothing.
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            return Write(claims, TwoFactorAudience, DateTime.UtcNow.Add(TwoFactorWindow));
        }

        public string? ReadTwoFactorToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            try
            {
                var principal = new JwtSecurityTokenHandler().ValidateToken(
                    token,
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = Issuer,

                        // The whole point: a normal session token fails here, so
                        // one cannot be swapped in to skip the code.
                        ValidAudience = TwoFactorAudience,

                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
                        ClockSkew = TimeSpan.Zero
                    },
                    out _);

                return principal.FindFirstValue(ClaimTypes.NameIdentifier);
            }
            catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
            {
                // Expired, tampered with, or the wrong kind of token. All of
                // them mean the same thing to the caller: start again.
                return null;
            }
        }

        // ---------- helpers ----------

        private string Key =>
            _config["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "Jwt:Key is not configured. Set it with: dotnet user-secrets set \"Jwt:Key\" \"<32+ characters>\"");

        private string Issuer => _config["Jwt:Issuer"] ?? "PursuitHQ";

        private string Audience => _config["Jwt:Audience"] ?? "PursuitHQClient";

        private string Write(List<Claim> claims, string audience, DateTime expiresAt)
        {
            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

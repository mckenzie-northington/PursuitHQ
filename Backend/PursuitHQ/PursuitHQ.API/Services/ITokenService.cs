using PursuitHQ.API.Models;

namespace PursuitHQ.API.Services
{
    public interface ITokenService
    {
        /// <summary>Creates a signed JWT for the given user.</summary>
        (string Token, DateTime ExpiresAt) CreateToken(ApplicationUser user);
    }
}

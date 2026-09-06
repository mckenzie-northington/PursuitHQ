namespace PursuitHQ.API.DTOs.Auth
{
    /// <summary>Returned on successful register or login.</summary>
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public UserProfileDto User { get; set; } = new();
    }
}

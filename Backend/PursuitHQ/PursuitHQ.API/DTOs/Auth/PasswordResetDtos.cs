using System.ComponentModel.DataAnnotations;

namespace PursuitHQ.API.DTOs.Auth
{
    public class ForgotPasswordDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// The answer to a reset request.
    ///
    /// Deliberately says the same thing whether or not the email has an
    /// account. Telling an anonymous caller "no account with that email" turns
    /// this endpoint into a way to find out who has signed up.
    /// </summary>
    public class ForgotPasswordResponseDto
    {
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// The reset link, returned ONLY when the API is running in
        /// Development and email sending is not set up yet. It is null in any
        /// other environment - handing the token back to whoever asked would
        /// let anyone reset anyone's password.
        /// </summary>
        public string? DevelopmentResetUrl { get; set; }
    }

    public class ResetPasswordDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required, MinLength(8)]
        public string NewPassword { get; set; } = string.Empty;
    }
}

using System.ComponentModel.DataAnnotations;

namespace PursuitHQ.API.DTOs.Auth
{
    /// <summary>Where two-factor stands for the signed-in student.</summary>
    public class TwoFactorStatusDto
    {
        public bool Enabled { get; set; }

        /// <summary>
        /// How many single-use codes are left.
        ///
        /// Shown because running out quietly is how someone ends up locked out
        /// of their own account the day they lose their phone.
        /// </summary>
        public int RecoveryCodesLeft { get; set; }
    }

    /// <summary>What an authenticator app needs to start generating codes.</summary>
    public class TwoFactorSetupDto
    {
        /// <summary>The secret, grouped in fours, for typing in by hand.</summary>
        public string SharedKey { get; set; } = string.Empty;

        /// <summary>The otpauth:// URI the QR code encodes.</summary>
        public string AuthenticatorUri { get; set; } = string.Empty;
    }

    /// <summary>A code from the app, confirming the secret arrived intact.</summary>
    public class TwoFactorCodeDto
    {
        [Required(ErrorMessage = "Enter the code from your authenticator app.")]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>The second step of signing in.</summary>
    public class TwoFactorVerifyDto
    {
        [Required]
        [MaxLength(2000)]
        public string TwoFactorToken { get; set; } = string.Empty;

        /// <summary>Either a six-digit code or one of the recovery codes.</summary>
        [Required(ErrorMessage = "Enter your code.")]
        [MaxLength(40)]
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>
    /// Turning two-factor off, or minting fresh recovery codes.
    ///
    /// The password is required for both. Someone who walks up to an unlocked
    /// laptop should not be able to strip the second factor off the account, and
    /// a logged-in session alone proves nothing about who is at the keyboard.
    /// </summary>
    public class TwoFactorPasswordDto
    {
        [Required(ErrorMessage = "Enter your password.")]
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>The single-use codes, shown once and never again.</summary>
    public class RecoveryCodesDto
    {
        public List<string> Codes { get; set; } = new();
    }
}

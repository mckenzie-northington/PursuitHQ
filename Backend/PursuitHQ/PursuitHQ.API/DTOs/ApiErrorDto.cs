namespace PursuitHQ.API.DTOs
{
    /// <summary>
    /// The single error shape every failing endpoint returns, as specified in
    /// docs/ApiDesign.md. Consistent errors make the frontend far simpler.
    /// </summary>
    public class ApiErrorDto
    {
        public string Error { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, string[]>? Details { get; set; }

        public ApiErrorDto() { }

        public ApiErrorDto(string error, string message, Dictionary<string, string[]>? details = null)
        {
            Error = error;
            Message = message;
            Details = details;
        }
    }
}

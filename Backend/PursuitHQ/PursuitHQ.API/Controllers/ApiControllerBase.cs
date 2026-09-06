using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// Base class for every controller that works with a student's own data.
    ///
    /// [Authorize] here means derived controllers require a valid token by
    /// default - you have to opt out with [AllowAnonymous], rather than
    /// remembering to opt in. Forgetting to secure an endpoint is a much more
    /// common mistake than accidentally securing one.
    /// </summary>
    [ApiController]
    [Authorize]
    public abstract class ApiControllerBase : ControllerBase
    {
        /// <summary>
        /// The signed-in student's id, read from the JWT. Every query in every
        /// controller filters on this. The client never supplies a user id.
        /// </summary>
        protected string CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authorized request has no user id claim.");
    }
}

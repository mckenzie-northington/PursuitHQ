using PursuitHQ.API.Models;

namespace PursuitHQ.API.DTOs.Assignments
{
    /// <summary>
    /// Just the status. The calendar's checkbox needs to flip an assignment
    /// between Completed and NotStarted without knowing its title, due date or
    /// grade - and a full PUT would force the caller to send all of those back,
    /// which is how fields quietly get overwritten with stale values.
    /// </summary>
    public class UpdateAssignmentStatusDto
    {
        public AssignmentStatus Status { get; set; }
    }
}

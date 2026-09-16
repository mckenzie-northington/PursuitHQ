using PursuitHQ.API.DTOs.Students;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// Turns a user row into the shape another student is allowed to see.
    ///
    /// One function, used by every endpoint that returns a person, so there is a
    /// single place where "who may see the email" is decided. Every leak of this
    /// kind starts with a second mapper written in a hurry somewhere else.
    /// </summary>
    public static class StudentCardMapper
    {
        public static StudentCardDto ToCard(
            ApplicationUser user,
            ProfileVisibility visibility,
            Connection? connection,
            string viewerId)
        {
            var card = new StudentCardDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                School = user.School,
                EducationLevel = user.EducationLevel,
                HasPhoto = !string.IsNullOrEmpty(user.PhotoPath),
                Relationship = Describe(connection, viewerId),
                ConnectionId = connection?.Id
            };

            // The extra fields are added, never removed. Building the full shape
            // and then blanking it is how a field gets missed.
            if (visibility == ProfileVisibility.Full)
            {
                card.Email = user.Email;
                card.Major = user.Major;
                card.GraduationYear = user.GraduationYear;
            }

            return card;
        }

        /// <summary>
        /// Where the viewer stands, from the viewer's side.
        ///
        /// Direction matters for a pending request: one of them is waiting for
        /// an answer and the other owes one, and the screen reads completely
        /// differently depending on which you are.
        /// </summary>
        private static string Describe(Connection? connection, string viewerId)
        {
            if (connection is null) return "none";

            return connection.Status switch
            {
                ConnectionStatus.Accepted => "connected",
                ConnectionStatus.Declined => "declined",

                // Only the blocker ever sees this; the blocked person's request
                // does not get this far. See ConnectionService.VisibilityAsync.
                ConnectionStatus.Blocked =>
                    connection.BlockedById == viewerId ? "blocked" : "none",

                ConnectionStatus.Pending =>
                    connection.RequesterId == viewerId ? "pending_out" : "pending_in",

                _ => "none"
            };
        }
    }
}

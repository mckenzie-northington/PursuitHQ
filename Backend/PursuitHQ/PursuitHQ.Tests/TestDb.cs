using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.Models;
using PursuitHQ.API.Services;

namespace PursuitHQ.Tests
{
    /// <summary>
    /// A throwaway database for one test, plus the few fakes a controller needs
    /// to be constructed outside the web host.
    ///
    /// SQLite in memory rather than the EF in-memory provider: the latter is not
    /// a relational database and ignores foreign keys and unique indexes, so a
    /// test can pass against it while the same code fails against PostgreSQL.
    /// The connection is held open deliberately - an in-memory SQLite database
    /// exists only as long as a connection to it does.
    /// </summary>
    public sealed class TestDb : IDisposable
    {
        private readonly SqliteConnection _connection;

        public ApplicationDbContext Db { get; }

        public TestDb()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            Db = new ApplicationDbContext(options);

            // EnsureCreated, not Migrate: the migrations are PostgreSQL-specific.
            // The schema is built from the same model the app uses, which is what
            // matters here.
            Db.Database.EnsureCreated();
        }

        public ApplicationUser AddUser(string id, string firstName)
        {
            var user = new ApplicationUser
            {
                Id = id,
                UserName = $"{firstName.ToLowerInvariant()}@example.edu",
                Email = $"{firstName.ToLowerInvariant()}@example.edu",
                FirstName = firstName,
                LastName = "Test"
            };

            Db.Users.Add(user);
            Db.SaveChanges();

            return user;
        }

        /// <summary>
        /// A conversation with the given users as active members, and one message
        /// from the first of them. Returns the conversation id and message id.
        /// </summary>
        public (int ConversationId, int MessageId) AddConversation(
            string senderId, string body, params string[] memberIds)
        {
            var conversation = new Conversation { IsGroup = memberIds.Length > 2 };
            Db.Conversations.Add(conversation);
            Db.SaveChanges();

            foreach (var memberId in memberIds)
            {
                Db.ConversationMembers.Add(new ConversationMember
                {
                    ConversationId = conversation.Id,
                    UserId = memberId,
                    Status = MembershipStatus.Active,
                    Role = ConversationRole.Member
                });
            }

            var message = new Message
            {
                ConversationId = conversation.Id,
                SenderId = senderId,
                Body = body,
                Kind = MessageKind.Text
            };

            Db.Messages.Add(message);
            Db.SaveChanges();

            return (conversation.Id, message.Id);
        }

        public void Dispose()
        {
            Db.Dispose();
            _connection.Dispose();
        }
    }

    /// <summary>
    /// Says nobody is connected to anybody.
    ///
    /// Only the create-conversation path consults this, and these tests never
    /// take that path - they seed conversations directly. A fake that answers
    /// "no" keeps it honest: if a test ever starts passing because this said
    /// yes, the test is exercising something it did not mean to.
    /// </summary>
    public sealed class NoConnections : IConnectionService
    {
        public Task<Connection?> BetweenAsync(string a, string b, CancellationToken ct = default) =>
            Task.FromResult<Connection?>(null);

        public Task<bool> AreConnectedAsync(string a, string b, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<bool> IsBlockedAsync(string a, string b, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<ProfileVisibility> VisibilityAsync(
            string viewerId, string subjectId, CancellationToken ct = default) =>
            Task.FromResult(default(ProfileVisibility));
    }

    /// <summary>Throws if touched. No test here should reach file storage.</summary>
    public sealed class NoStorage : IFileStorageService
    {
        public Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default) =>
            throw new InvalidOperationException("A test reached file storage unexpectedly.");

        public Task<Stream> OpenAsync(string storedPath, CancellationToken ct = default) =>
            throw new InvalidOperationException("A test reached file storage unexpectedly.");

        public Task DeleteAsync(string storedPath, CancellationToken ct = default) =>
            throw new InvalidOperationException("A test reached file storage unexpectedly.");
    }

    public static class ControllerSetup
    {
        /// <summary>
        /// Puts a signed-in user behind a controller.
        ///
        /// This is the part that matters. Every controller reads CurrentUserId
        /// from the NameIdentifier claim and never from the request, so setting
        /// this claim is exactly equivalent to presenting that user's token.
        /// </summary>
        public static T AsUser<T>(this T controller, string userId) where T : ControllerBase
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "TestAuth");

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };

            return controller;
        }
    }
}

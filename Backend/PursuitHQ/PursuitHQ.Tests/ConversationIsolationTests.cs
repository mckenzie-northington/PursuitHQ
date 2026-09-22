using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Controllers;
using PursuitHQ.API.DTOs.Students;
using PursuitHQ.API.Models;
using Xunit;

namespace PursuitHQ.Tests
{
    /// <summary>
    /// The one invariant whose failure would be serious rather than
    /// embarrassing: a student can only reach conversations they are a member
    /// of.
    ///
    /// Every controller in this app derives the acting user from the JWT rather
    /// than from the request, and filters every query by it. That is a
    /// convention, held in place by nothing but care, and conventions are what
    /// quietly stop being true during a refactor six months from now. These
    /// tests are what notices.
    ///
    /// Note what an outsider gets: 404, never 403. "You are not allowed to see
    /// this" confirms the thing exists, which is a smaller leak than the content
    /// but a leak all the same. The assertions check for NotFound on purpose -
    /// if somebody ever "helpfully" changes these to Forbid, a test fails and
    /// the reason is written here.
    /// </summary>
    public class ConversationIsolationTests
    {
        private const string Alice = "user-alice";
        private const string Bob = "user-bob";
        private const string Mallory = "user-mallory";

        private static ConversationsController ControllerFor(TestDb fixture, string userId) =>
            new ConversationsController(fixture.Db, new NoConnections(), new NoStorage())
                .AsUser(userId);

        private static TestDb SeedAliceAndBobTalking(out int conversationId, out int messageId)
        {
            var fixture = new TestDb();

            fixture.AddUser(Alice, "Alice");
            fixture.AddUser(Bob, "Bob");
            fixture.AddUser(Mallory, "Mallory");

            (conversationId, messageId) = fixture.AddConversation(
                Alice, "meet me at the library at six", Alice, Bob);

            return fixture;
        }

        [Fact]
        public async Task A_member_can_read_the_conversation()
        {
            // The control. Without this, every test below could be passing
            // because the seed data is broken rather than because the check works.
            using var fixture = SeedAliceAndBobTalking(out var conversationId, out _);

            var result = await ControllerFor(fixture, Bob)
                .GetMessages(conversationId, before: null, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var messages = Assert.IsType<List<MessageDto>>(ok.Value);

            Assert.Single(messages);
            Assert.Equal("meet me at the library at six", messages[0].Body);
        }

        [Fact]
        public async Task An_outsider_cannot_read_the_messages()
        {
            using var fixture = SeedAliceAndBobTalking(out var conversationId, out _);

            var result = await ControllerFor(fixture, Mallory)
                .GetMessages(conversationId, before: null, CancellationToken.None);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task An_outsider_cannot_edit_a_message()
        {
            using var fixture = SeedAliceAndBobTalking(out var conversationId, out var messageId);

            var result = await ControllerFor(fixture, Mallory).EditMessage(
                conversationId,
                messageId,
                new EditMessageDto { Body = "meet me at the docks at midnight" },
                CancellationToken.None);

            Assert.IsType<NotFoundObjectResult>(result.Result);

            // Refused is not the same as unchanged. Check the row.
            var message = await fixture.Db.Messages.AsNoTracking()
                .FirstAsync(m => m.Id == messageId);

            Assert.Equal("meet me at the library at six", message.Body);
            Assert.Null(message.EditedAt);
        }

        [Fact]
        public async Task An_outsider_cannot_delete_a_message()
        {
            using var fixture = SeedAliceAndBobTalking(out var conversationId, out var messageId);

            var result = await ControllerFor(fixture, Mallory)
                .DeleteMessage(conversationId, messageId, CancellationToken.None);

            Assert.IsType<NotFoundObjectResult>(result);

            var message = await fixture.Db.Messages.AsNoTracking()
                .FirstAsync(m => m.Id == messageId);

            Assert.Null(message.DeletedAt);
            Assert.Equal("meet me at the library at six", message.Body);
        }

        [Fact]
        public async Task A_member_cannot_edit_somebody_elses_message()
        {
            // Being in the room is not the same as owning what was said in it.
            using var fixture = SeedAliceAndBobTalking(out var conversationId, out var messageId);

            var result = await ControllerFor(fixture, Bob).EditMessage(
                conversationId,
                messageId,
                new EditMessageDto { Body = "Alice said something she did not say" },
                CancellationToken.None);

            Assert.IsType<NotFoundObjectResult>(result.Result);

            var message = await fixture.Db.Messages.AsNoTracking()
                .FirstAsync(m => m.Id == messageId);

            Assert.Equal("meet me at the library at six", message.Body);
        }

        [Fact]
        public async Task A_member_who_left_cannot_read_the_conversation()
        {
            // Membership is a row with a status, not a row that gets removed, so
            // "left" and "never joined" are different states in the database and
            // only one of them is obvious to check for.
            using var fixture = SeedAliceAndBobTalking(out var conversationId, out _);

            var membership = await fixture.Db.ConversationMembers
                .FirstAsync(m => m.ConversationId == conversationId && m.UserId == Bob);

            membership.Status = MembershipStatus.Left;
            membership.LeftAt = DateTime.UtcNow;
            await fixture.Db.SaveChangesAsync();

            var result = await ControllerFor(fixture, Bob)
                .GetMessages(conversationId, before: null, CancellationToken.None);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task An_invited_but_not_joined_user_cannot_read_the_conversation()
        {
            using var fixture = SeedAliceAndBobTalking(out var conversationId, out _);

            fixture.Db.ConversationMembers.Add(new ConversationMember
            {
                ConversationId = conversationId,
                UserId = Mallory,
                Status = MembershipStatus.Invited,
                InvitedById = Alice
            });

            await fixture.Db.SaveChangesAsync();

            var result = await ControllerFor(fixture, Mallory)
                .GetMessages(conversationId, before: null, CancellationToken.None);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }
    }
}

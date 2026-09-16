using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PursuitHQ.API.Migrations
{
    /// <summary>
    /// Group ownership, invitations, replies, edits and mute.
    ///
    /// Hand-edited after scaffolding, and heavily. EF's diff saw a bool column
    /// disappear (IsAdmin) and a bool column appear (IsMuted) and decided it was
    /// a rename - so every admin would have become somebody with a muted
    /// conversation, and the admin list would have been gone. On top of that,
    /// both new enum columns default to 0, which here means "Member" and
    /// "Invited": every group would have been left ownerless, and every existing
    /// member of every conversation - including direct ones - would have been
    /// demoted to a pending invitee, which is to say every conversation in the
    /// app would have vanished from everybody's list.
    ///
    /// The order below is: add the new columns, derive their values while the
    /// old ones are still readable, then rename, then clear what the rename
    /// dragged across.
    /// </summary>
    public partial class AddGroupRolesAndInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- new columns first, so there is somewhere to put the old values
            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "ConversationMembers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ConversationMembers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "InvitedById",
                table: "ConversationMembers",
                type: "text",
                nullable: true);

            // --- derive the roles while IsAdmin still exists
            //
            // The person who created a group owns it; anyone who was already an
            // admin stays one. Direct conversations are skipped - roles mean
            // nothing when there are only two people and neither can remove the
            // other.
            migrationBuilder.Sql(@"
                UPDATE ""ConversationMembers"" m
                SET ""Role"" = CASE
                    WHEN m.""UserId"" = c.""CreatedById"" THEN 2
                    WHEN m.""IsAdmin"" THEN 1
                    ELSE 0
                END
                FROM ""Conversations"" c
                WHERE c.""Id"" = m.""ConversationId"" AND c.""IsGroup"";");

            // Status 0 is Invited. Everyone already in a conversation is Active,
            // or Left if they had already gone. Without this line every existing
            // conversation disappears from every list in the app.
            migrationBuilder.Sql(@"
                UPDATE ""ConversationMembers""
                SET ""Status"" = CASE WHEN ""LeftAt"" IS NULL THEN 1 ELSE 2 END;");

            // --- now the rename EF wanted, and the clean-up it needs
            migrationBuilder.RenameColumn(
                name: "IsAdmin",
                table: "ConversationMembers",
                newName: "IsMuted");

            // IsMuted has just inherited IsAdmin's values, so every admin would
            // start with that conversation silenced. Nobody has muted anything
            // yet; that is what false means.
            migrationBuilder.Sql(@"UPDATE ""ConversationMembers"" SET ""IsMuted"" = false;");

            // --- the purely additive rest
            migrationBuilder.AddColumn<DateTime>(
                name: "EditedAt",
                table: "Messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReplyToMessageId",
                table: "Messages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Conversations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoContentType",
                table: "Conversations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                table: "Conversations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReplyToMessageId",
                table: "Messages",
                column: "ReplyToMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMembers_UserId_Status",
                table: "ConversationMembers",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Messages_ReplyToMessageId",
                table: "Messages",
                column: "ReplyToMessageId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Messages_ReplyToMessageId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ReplyToMessageId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMembers_UserId_Status",
                table: "ConversationMembers");

            migrationBuilder.DropColumn(name: "EditedAt", table: "Messages");
            migrationBuilder.DropColumn(name: "Kind", table: "Messages");
            migrationBuilder.DropColumn(name: "ReplyToMessageId", table: "Messages");
            migrationBuilder.DropColumn(name: "Description", table: "Conversations");
            migrationBuilder.DropColumn(name: "PhotoContentType", table: "Conversations");
            migrationBuilder.DropColumn(name: "PhotoPath", table: "Conversations");

            // Renamed back first, then filled from Role, which has to happen
            // before Role is dropped. EF generated these the other way round.
            migrationBuilder.RenameColumn(
                name: "IsMuted",
                table: "ConversationMembers",
                newName: "IsAdmin");

            migrationBuilder.Sql(@"
                UPDATE ""ConversationMembers"" SET ""IsAdmin"" = (""Role"" >= 1);");

            migrationBuilder.DropColumn(name: "InvitedById", table: "ConversationMembers");
            migrationBuilder.DropColumn(name: "Role", table: "ConversationMembers");
            migrationBuilder.DropColumn(name: "Status", table: "ConversationMembers");
        }
    }
}

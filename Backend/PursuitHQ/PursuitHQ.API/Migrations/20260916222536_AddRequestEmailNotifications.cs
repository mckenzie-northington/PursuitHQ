using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PursuitHQ.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestEmailNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequestEmailsEnabled",
                table: "NotificationPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastTypingAt",
                table: "ConversationMembers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinnedAt",
                table: "ConversationMembers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestEmailsEnabled",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "LastTypingAt",
                table: "ConversationMembers");

            migrationBuilder.DropColumn(
                name: "PinnedAt",
                table: "ConversationMembers");
        }
    }
}

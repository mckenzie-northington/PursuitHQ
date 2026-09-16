using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PursuitHQ.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageEmailNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MessageEmailsEnabled",
                table: "NotificationPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMessageEmailAt",
                table: "ConversationMembers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MessageEmailsEnabled",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "LastMessageEmailAt",
                table: "ConversationMembers");
        }
    }
}

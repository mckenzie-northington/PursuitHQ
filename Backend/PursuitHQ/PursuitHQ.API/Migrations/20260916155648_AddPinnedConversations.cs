using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PursuitHQ.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPinnedConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "ConversationMembers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "ConversationMembers");
        }
    }
}

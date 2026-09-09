using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PursuitHQ.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SavedColors",
                table: "AspNetUsers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SavedColors",
                table: "AspNetUsers");
        }
    }
}

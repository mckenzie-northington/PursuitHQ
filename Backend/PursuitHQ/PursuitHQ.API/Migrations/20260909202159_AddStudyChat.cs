using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PursuitHQ.API.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudyConversations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    SourceMaterialIds = table.Column<string>(type: "text", nullable: true),
                    SourceNoteIds = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyConversations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyConversations_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudyMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConversationId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ArtifactKind = table.Column<int>(type: "integer", nullable: false),
                    ArtifactTitle = table.Column<string>(type: "text", nullable: true),
                    ArtifactContent = table.Column<string>(type: "text", nullable: true),
                    SavedStudyGuideId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyMessages_StudyConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "StudyConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyMessages_StudyGuides_SavedStudyGuideId",
                        column: x => x.SavedStudyGuideId,
                        principalTable: "StudyGuides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudyConversations_CourseId",
                table: "StudyConversations",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyConversations_UserId_CourseId",
                table: "StudyConversations",
                columns: new[] { "UserId", "CourseId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyMessages_ConversationId_CreatedAt",
                table: "StudyMessages",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyMessages_SavedStudyGuideId",
                table: "StudyMessages",
                column: "SavedStudyGuideId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudyMessages");

            migrationBuilder.DropTable(
                name: "StudyConversations");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PursuitHQ.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPracticeTests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SavedQuizId",
                table: "StudyMessages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Feedback",
                table: "QuizAnswers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyMessages_SavedQuizId",
                table: "StudyMessages",
                column: "SavedQuizId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudyMessages_Quizzes_SavedQuizId",
                table: "StudyMessages",
                column: "SavedQuizId",
                principalTable: "Quizzes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudyMessages_Quizzes_SavedQuizId",
                table: "StudyMessages");

            migrationBuilder.DropIndex(
                name: "IX_StudyMessages_SavedQuizId",
                table: "StudyMessages");

            migrationBuilder.DropColumn(
                name: "SavedQuizId",
                table: "StudyMessages");

            migrationBuilder.DropColumn(
                name: "Feedback",
                table: "QuizAnswers");
        }
    }
}

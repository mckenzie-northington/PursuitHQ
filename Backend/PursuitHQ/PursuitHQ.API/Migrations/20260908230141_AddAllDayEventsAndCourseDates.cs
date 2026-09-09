using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PursuitHQ.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAllDayEventsAndCourseDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                table: "Courses",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "Courses",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAllDay",
                table: "CalendarEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "IsAllDay",
                table: "CalendarEvents");
        }
    }
}

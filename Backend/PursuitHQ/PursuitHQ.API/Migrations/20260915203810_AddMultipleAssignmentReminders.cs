using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PursuitHQ.API.Migrations
{
    /// <summary>
    /// One reminder per assignment becomes several.
    ///
    /// Hand-edited after scaffolding. EF generated this as "drop the old column,
    /// then add the new one", which is correct as a schema change and wrong as a
    /// data change: every student's chosen reminder timing would have gone in
    /// the drop, and they would all have woken up on the default.
    ///
    /// The order below is add, carry the data across, then drop.
    /// </summary>
    public partial class AddMultipleAssignmentReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Added before anything is dropped, so there is somewhere to put the
            // existing values. The default is the app's own default rather than
            // an empty string: a row that somehow misses the copy below should
            // end up on 24 hours, not on no reminders at all.
            migrationBuilder.AddColumn<string>(
                name: "AssignmentReminderHours",
                table: "NotificationPreferences",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "24");

            // One value becomes a one-item list, so someone set to 12 hours stays
            // on 12 hours and simply gains the ability to add more.
            migrationBuilder.Sql(@"
                UPDATE ""NotificationPreferences""
                SET ""AssignmentReminderHours"" = ""AssignmentReminderHoursBefore""::text
                WHERE ""AssignmentReminderHoursBefore"" BETWEEN 1 AND 336;");

            // The reminder history is what stops an email going twice, and its key
            // now carries the interval: 'Assignment@24h' rather than 'Assignment'.
            // Without this, every assignment that has already had its reminder
            // would look unreminded on the next run and get a second email.
            migrationBuilder.Sql(@"
                UPDATE ""Notifications"" n
                SET ""RelatedEntityType"" =
                    'Assignment@' || p.""AssignmentReminderHoursBefore"" || 'h'
                FROM ""NotificationPreferences"" p
                WHERE p.""UserId"" = n.""UserId""
                  AND n.""RelatedEntityType"" = 'Assignment';");

            migrationBuilder.DropColumn(
                name: "AssignmentReminderHoursBefore",
                table: "NotificationPreferences");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignmentReminderHoursBefore",
                table: "NotificationPreferences",
                type: "integer",
                nullable: false,
                defaultValue: 24);

            // Going back, only the widest offset survives - there is nowhere to
            // put the rest. Guarded with a regex rather than a bare cast so a
            // malformed row cannot abort the whole rollback.
            migrationBuilder.Sql(@"
                UPDATE ""NotificationPreferences""
                SET ""AssignmentReminderHoursBefore"" =
                    CASE
                        WHEN split_part(""AssignmentReminderHours"", ',', 1) ~ '^[0-9]+$'
                        THEN split_part(""AssignmentReminderHours"", ',', 1)::int
                        ELSE 24
                    END;");

            migrationBuilder.Sql(@"
                UPDATE ""Notifications""
                SET ""RelatedEntityType"" = 'Assignment'
                WHERE ""RelatedEntityType"" LIKE 'Assignment@%';");

            migrationBuilder.DropColumn(
                name: "AssignmentReminderHours",
                table: "NotificationPreferences");
        }
    }
}

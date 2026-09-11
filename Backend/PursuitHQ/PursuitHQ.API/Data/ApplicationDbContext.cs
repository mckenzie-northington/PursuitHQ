using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Academic
        public DbSet<Course> Courses => Set<Course>();
        public DbSet<ClassSchedule> ClassSchedules => Set<ClassSchedule>();
        public DbSet<Assignment> Assignments => Set<Assignment>();
        public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();

        // Study materials
        public DbSet<MaterialFolder> MaterialFolders => Set<MaterialFolder>();
        public DbSet<StudyMaterial> StudyMaterials => Set<StudyMaterial>();
        public DbSet<Note> Notes => Set<Note>();

        // Study tools
        public DbSet<StudySession> StudySessions => Set<StudySession>();
        public DbSet<FlashcardDeck> FlashcardDecks => Set<FlashcardDeck>();
        public DbSet<Flashcard> Flashcards => Set<Flashcard>();
        public DbSet<Quiz> Quizzes => Set<Quiz>();
        public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
        public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
        public DbSet<QuizAnswer> QuizAnswers => Set<QuizAnswer>();
        public DbSet<StudyGuide> StudyGuides => Set<StudyGuide>();
        public DbSet<StudyConversation> StudyConversations => Set<StudyConversation>();
        public DbSet<StudyMessage> StudyMessages => Set<StudyMessage>();

        // Career
        public DbSet<JobApplication> JobApplications => Set<JobApplication>();
        public DbSet<Resume> Resumes => Set<Resume>();
        public DbSet<Goal> Goals => Set<Goal>();
        public DbSet<Skill> Skills => Set<Skill>();
        public DbSet<Certification> Certifications => Set<Certification>();

        // Notifications
        public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
        public DbSet<Notification> Notifications => Set<Notification>();

        /// <summary>
        /// Every DateTime column is PostgreSQL "timestamp with time zone", and
        /// Npgsql refuses to write a DateTime whose Kind is Unspecified to one.
        /// Unspecified is exactly what we get everywhere that matters: a
        /// datetime-local input sends "2026-09-08T09:00" with no offset, and
        /// DateOnly.ToDateTime() produces Unspecified too. Without this, every
        /// calendar query threw.
        ///
        /// The rule for this app is that a stored DateTime is a WALL-CLOCK
        /// time, not an instant. A 9am class is 9am; a paper due at 11:59pm is
        /// due at 11:59pm. So going in we only stamp the Kind so the driver
        /// will accept the value, and coming out we strip the Kind back to
        /// Unspecified.
        ///
        /// Stripping it on the way out is the half that matters to the UI:
        /// System.Text.Json writes a Utc DateTime with a trailing "Z", the
        /// browser would read that as an instant and shift it into local time,
        /// and an assignment due at 11:59pm would land on the wrong day in the
        /// calendar. Unspecified serializes with no suffix, which JavaScript
        /// parses as local time - the same wall clock we stored.
        ///
        /// A value that genuinely carries an offset (Kind = Local) is converted
        /// to UTC first, so it still means the same instant.
        /// </summary>
        protected override void ConfigureConventions(ModelConfigurationBuilder builder)
        {
            base.ConfigureConventions(builder);

            // Registering DateTime also covers DateTime? properties - EF wraps
            // the converter for nulls on its own.
            builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        }

        // Public because EF constructs it by reflection from the type argument
        // above.
        public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
        {
            public UtcDateTimeConverter() : base(
                toDb => ToUtc(toDb),
                fromDb => DateTime.SpecifyKind(fromDb, DateTimeKind.Unspecified))
            {
            }
        }

        private static DateTime ToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Required: sets up all of Identity's own tables.
            base.OnModelCreating(builder);

            // --- Study chat -------------------------------------------------
            // Deleting a conversation takes its messages with it: a message
            // outside its conversation means nothing.
            builder.Entity<StudyMessage>()
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            // A saved guide outlives the chat that produced it, so deleting the
            // conversation must not cascade into the library.
            builder.Entity<StudyMessage>()
                .HasOne(m => m.SavedStudyGuide)
                .WithMany()
                .HasForeignKey(m => m.SavedStudyGuideId)
                .OnDelete(DeleteBehavior.SetNull);

            // Same for a saved practice test.
            builder.Entity<StudyMessage>()
                .HasOne(m => m.SavedQuiz)
                .WithMany()
                .HasForeignKey(m => m.SavedQuizId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<StudyConversation>()
                .HasIndex(c => new { c.UserId, c.CourseId });

            builder.Entity<StudyMessage>()
                .HasIndex(m => new { m.ConversationId, m.CreatedAt });

            // --- Self-referencing folder tree -------------------------------
            // Deleting a folder must not cascade up into its parent, so the
            // child relationship is Restrict. Deleting a folder's children is
            // handled explicitly in the service layer.
            builder.Entity<MaterialFolder>()
                .HasOne(f => f.ParentFolder)
                .WithMany(f => f.ChildFolders)
                .HasForeignKey(f => f.ParentFolderId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- Quiz answers have two parents ------------------------------
            // Cascade from the attempt (deleting an attempt removes its answers),
            // but Restrict from the question so deleting a question does not
            // silently wipe historical attempt data.
            builder.Entity<QuizAnswer>()
                .HasOne(a => a.Attempt)
                .WithMany(at => at.Answers)
                .HasForeignKey(a => a.AttemptId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<QuizAnswer>()
                .HasOne(a => a.Question)
                .WithMany()
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- Generated study tools keep their source loosely ------------
            // Deleting the source file or note should not delete the flashcards,
            // quiz, or study guide the student generated from it.
            builder.Entity<FlashcardDeck>()
                .HasOne(d => d.SourceMaterial).WithMany()
                .HasForeignKey(d => d.SourceMaterialId).OnDelete(DeleteBehavior.SetNull);
            builder.Entity<FlashcardDeck>()
                .HasOne(d => d.SourceNote).WithMany()
                .HasForeignKey(d => d.SourceNoteId).OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Quiz>()
                .HasOne(q => q.SourceMaterial).WithMany()
                .HasForeignKey(q => q.SourceMaterialId).OnDelete(DeleteBehavior.SetNull);
            builder.Entity<Quiz>()
                .HasOne(q => q.SourceNote).WithMany()
                .HasForeignKey(q => q.SourceNoteId).OnDelete(DeleteBehavior.SetNull);

            builder.Entity<StudyGuide>()
                .HasOne(g => g.SourceMaterial).WithMany()
                .HasForeignKey(g => g.SourceMaterialId).OnDelete(DeleteBehavior.SetNull);
            builder.Entity<StudyGuide>()
                .HasOne(g => g.SourceNote).WithMany()
                .HasForeignKey(g => g.SourceNoteId).OnDelete(DeleteBehavior.SetNull);

            // --- One preferences row per student ----------------------------
            builder.Entity<NotificationPreference>()
                .HasIndex(p => p.UserId)
                .IsUnique();

            // --- Prevent duplicate reminders --------------------------------
            // NotificationService checks this before sending.
            builder.Entity<Notification>()
                .HasIndex(n => new { n.UserId, n.Type, n.RelatedEntityType, n.RelatedEntityId });

            // --- Indexes for the queries the app actually runs --------------
            builder.Entity<Course>().HasIndex(c => new { c.UserId, c.Semester });
            builder.Entity<Assignment>().HasIndex(a => a.DueDate);
            builder.Entity<CalendarEvent>().HasIndex(e => new { e.UserId, e.StartDateTime });
            builder.Entity<StudySession>().HasIndex(s => new { s.UserId, s.ScheduledDate });
            builder.Entity<JobApplication>().HasIndex(j => new { j.UserId, j.Status });
            builder.Entity<StudyMaterial>().HasIndex(m => new { m.CourseId, m.FolderId });
            builder.Entity<Note>().HasIndex(n => new { n.CourseId, n.FolderId });
        }
    }
}

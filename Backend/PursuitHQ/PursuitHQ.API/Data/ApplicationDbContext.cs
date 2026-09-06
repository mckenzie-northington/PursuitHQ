using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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

        // Career
        public DbSet<JobApplication> JobApplications => Set<JobApplication>();
        public DbSet<Resume> Resumes => Set<Resume>();
        public DbSet<Goal> Goals => Set<Goal>();
        public DbSet<Skill> Skills => Set<Skill>();
        public DbSet<Certification> Certifications => Set<Certification>();

        // Notifications
        public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
        public DbSet<Notification> Notifications => Set<Notification>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Required: sets up all of Identity's own tables.
            base.OnModelCreating(builder);

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

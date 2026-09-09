namespace PursuitHQ.API.Models
{
    public enum AssignmentStatus
    {
        NotStarted = 0,
        InProgress = 1,
        Completed = 2
    }

    public enum EventType
    {
        Work = 0,
        Club = 1,
        Appointment = 2,
        Personal = 3,
        Other = 4
    }

    public enum JobType
    {
        Internship = 0,
        PartTime = 1,
        FullTime = 2
    }

    public enum ApplicationStatus
    {
        Saved = 0,
        Applied = 1,
        Interview = 2,
        Offer = 3,
        Rejected = 4
    }

    public enum ApplicationSource
    {
        Manual = 0,
        Search = 1
    }

    public enum StudySessionStatus
    {
        Planned = 0,
        Completed = 1,
        Skipped = 2
    }

    public enum SkillLevel
    {
        Beginner = 0,
        Intermediate = 1,
        Advanced = 2
    }

    public enum NotificationType
    {
        AssignmentDue = 0,
        EventReminder = 1,
        DailyDigest = 2,
        WeeklyDigest = 3
    }

    public enum DeliveryStatus
    {
        Sent = 0,
        Failed = 1
    }

    public enum QuestionType
    {
        MultipleChoice = 0,
        TrueFalse = 1,
        ShortAnswer = 2
    }

    public enum StudyMessageRole
    {
        User = 0,
        Assistant = 1
    }

    /// <summary>Something a chat reply produced that is worth keeping.</summary>
    public enum StudyArtifactKind
    {
        None = 0,
        StudyGuide = 1,
        PracticeTest = 2
    }
}

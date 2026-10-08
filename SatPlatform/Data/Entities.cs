using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SatPlatform.Data;

public class User
{
    public int Id { get; set; }
    [MaxLength(120)] public string FullName { get; set; } = "";
    [MaxLength(20)] public string Phone { get; set; } = "";
    [MaxLength(160)] public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public long? TelegramChatId { get; set; }
    [MaxLength(64)] public string? TelegramUsername { get; set; }
    /// <summary>Teachers only: may create/edit questions in the Question Bank.</summary>
    public bool CanManageQuestions { get; set; }
    public bool NotifyTestResults { get; set; } = true;
    public bool NotifyAttendance { get; set; } = true;
    public bool NotifyHomework { get; set; } = true;
    public bool NotifyPracticeReminder { get; set; } = true;
    [MaxLength(16)] public string AvatarColor { get; set; } = "#0f8f7e";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAt { get; set; }

    public Student? Student { get; set; }
}

public class Student
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int? TargetScore { get; set; }
    public StudentStatus Status { get; set; } = StudentStatus.Active;
    /// <summary>Code a parent enters to link to this student.</summary>
    [MaxLength(12)] public string ParentCode { get; set; } = "";
    [MaxLength(500)] public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? OnboardedAt { get; set; }

    public List<Enrollment> Enrollments { get; set; } = [];
    public List<ParentStudent> Parents { get; set; } = [];
}

public class ParentStudent
{
    public int ParentUserId { get; set; }
    public User ParentUser { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
}

public class Group
{
    public int Id { get; set; }
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(80)] public string CourseType { get; set; } = "SAT";
    public int? TeacherUserId { get; set; }
    public User? TeacherUser { get; set; }
    public int? AssistantUserId { get; set; }
    public User? AssistantUser { get; set; }
    [MaxLength(60)] public string? Room { get; set; }
    /// <summary>Comma separated day numbers, Monday = 1 ... Sunday = 7.</summary>
    [MaxLength(30)] public string ScheduleDays { get; set; } = "";
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Enrollment> Enrollments { get; set; } = [];
}

public class Enrollment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
}

public class Attendance
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public DateOnly Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public int LateMinutes { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// ---------------------------------------------------------------- Content

public class Domain
{
    public int Id { get; set; }
    public Section Section { get; set; }
    [MaxLength(80)] public string Code { get; set; } = "";
    [MaxLength(120)] public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Skill> Skills { get; set; } = [];
}

public class Skill
{
    public int Id { get; set; }
    public int DomainId { get; set; }
    public Domain Domain { get; set; } = null!;
    [MaxLength(80)] public string Code { get; set; } = "";
    [MaxLength(120)] public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Question
{
    public int Id { get; set; }
    public Section Section { get; set; }
    public int DomainId { get; set; }
    public Domain Domain { get; set; } = null!;
    public int SkillId { get; set; }
    public Skill Skill { get; set; } = null!;
    public Difficulty Difficulty { get; set; }
    public QuestionType Type { get; set; }
    public QuestionStatus Status { get; set; } = QuestionStatus.Draft;
    /// <summary>Passage / context shown in the left panel (HTML).</summary>
    public string? Passage { get; set; }
    /// <summary>Question stem (HTML).</summary>
    public string Text { get; set; } = "";
    public string? Explanation { get; set; }
    [MaxLength(500)] public string? VideoUrl { get; set; }
    /// <summary>SPR: accepted answers separated by '|' (e.g. "2/3|0.666|0.667|.666|.667").</summary>
    [MaxLength(300)] public string? CorrectAnswer { get; set; }
    public int CurrentVersion { get; set; } = 1;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int AttemptCount { get; set; }
    public int CorrectCount { get; set; }
    public long TotalTimeSeconds { get; set; }
    [MaxLength(80)] public string? Tag { get; set; }

    public List<QuestionOption> Options { get; set; } = [];
    public List<QuestionVersion> Versions { get; set; } = [];
}

public class QuestionOption
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    [MaxLength(2)] public string Key { get; set; } = "A";
    public string Text { get; set; } = "";
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Frozen copy of a question as it was when attempts were taken against it.</summary>
public class QuestionVersion
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public int Version { get; set; }
    public QuestionType Type { get; set; }
    public string? Passage { get; set; }
    public string Text { get; set; } = "";
    public string? Explanation { get; set; }
    [MaxLength(500)] public string? VideoUrl { get; set; }
    [MaxLength(300)] public string? CorrectAnswer { get; set; }
    /// <summary>JSON array of {Key, Text, IsCorrect}.</summary>
    public string OptionsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class MediaFile
{
    public int Id { get; set; }
    [MaxLength(80)] public string Name { get; set; } = "";
    [MaxLength(255)] public string OriginalName { get; set; } = "";
    [MaxLength(120)] public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Data { get; set; } = [];
    public long Size { get; set; }
    public int UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ---------------------------------------------------------------- Tests

public class Test
{
    public int Id { get; set; }
    [MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(1000)] public string? Description { get; set; }
    public TestType Type { get; set; }
    public bool IsAdaptive { get; set; }
    public TestStatus Status { get; set; } = TestStatus.Draft;
    [MaxLength(40)] public string ScoringVersion { get; set; } = "SAT_2026_V1";
    public int BreakSeconds { get; set; } = 600;
    public bool ShuffleQuestions { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    /// <summary>Visible to every student without an assignment.</summary>
    public bool IsPublic { get; set; }

    public List<TestModule> Modules { get; set; } = [];
}

public class TestModule
{
    public int Id { get; set; }
    public int TestId { get; set; }
    public Test Test { get; set; } = null!;
    public Section Section { get; set; }
    public int ModuleNumber { get; set; }
    public ModuleRoute Route { get; set; }
    public int TimeLimitSeconds { get; set; }
    public int SortOrder { get; set; }
    public List<TestQuestion> Questions { get; set; } = [];

    [NotMapped]
    public string Title => (Section == Section.RW ? "Reading & Writing" : "Math") + " — Module " + ModuleNumber +
        (Route == ModuleRoute.Easy ? " (Easy)" : Route == ModuleRoute.Hard ? " (Hard)" : "");
}

public class TestQuestion
{
    public int Id { get; set; }
    public int TestModuleId { get; set; }
    public TestModule TestModule { get; set; } = null!;
    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public int Order { get; set; }
}

public class TestAssignment
{
    public int Id { get; set; }
    public int TestId { get; set; }
    public Test Test { get; set; } = null!;
    public int? StudentId { get; set; }
    public Student? Student { get; set; }
    public int? GroupId { get; set; }
    public Group? Group { get; set; }
    public int AssignedByUserId { get; set; }
    public DateTime StartAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndAt { get; set; }
    /// <summary>null = unlimited.</summary>
    public int? AttemptLimit { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TestAttempt
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int TestId { get; set; }
    public Test Test { get; set; } = null!;
    public int? AssignmentId { get; set; }
    public AttemptStatus Status { get; set; }
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime? BreakEndsAt { get; set; }
    public int CurrentModuleOrder { get; set; }
    public int? TotalScore { get; set; }
    public int? RwScore { get; set; }
    public int? MathScore { get; set; }
    public int RwRaw { get; set; }
    public int MathRaw { get; set; }
    public int RwMax { get; set; }
    public int MathMax { get; set; }
    public ModuleRoute? RwRoute { get; set; }
    public ModuleRoute? MathRoute { get; set; }
    [MaxLength(40)] public string ScoringVersion { get; set; } = "";
    public int? RandomSeed { get; set; }
    public bool IsPreview { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<AttemptModule> Modules { get; set; } = [];
    public List<AttemptQuestion> Questions { get; set; } = [];
}

public class AttemptModule
{
    public int Id { get; set; }
    public int AttemptId { get; set; }
    public TestAttempt Attempt { get; set; } = null!;
    public int? TestModuleId { get; set; }
    public Section Section { get; set; }
    public int ModuleNumber { get; set; }
    public ModuleRoute Route { get; set; }
    public int Order { get; set; }
    public int TimeLimitSeconds { get; set; }
    public AttemptModuleStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? DeadlineAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int? RawScore { get; set; }
    public int QuestionCount { get; set; }
}

public class AttemptQuestion
{
    public int Id { get; set; }
    public int AttemptId { get; set; }
    public TestAttempt Attempt { get; set; } = null!;
    public int AttemptModuleId { get; set; }
    public AttemptModule Module { get; set; } = null!;
    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public int QuestionVersionId { get; set; }
    public QuestionVersion QuestionVersion { get; set; } = null!;
    public int Order { get; set; }
    public bool IsFlagged { get; set; }
    [MaxLength(100)] public string? Answer { get; set; }
    public bool? IsCorrect { get; set; }
    public int TimeSpentSeconds { get; set; }
    public DateTime? AnsweredAt { get; set; }
    [MaxLength(10)] public string? EliminatedOptions { get; set; }
}

// ---------------------------------------------------------------- Practice

public class PracticeSession
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public PracticeMode Mode { get; set; }
    public Section? Section { get; set; }
    public int? DomainId { get; set; }
    public int? SkillId { get; set; }
    public Difficulty? Difficulty { get; set; }
    public int QuestionCount { get; set; }
    public int? TimeLimitSeconds { get; set; }
    public PracticeStatus Status { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeadlineAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int CorrectCount { get; set; }
    public int AnsweredCount { get; set; }
    public int? HomeworkId { get; set; }
    [MaxLength(160)] public string Title { get; set; } = "";

    public List<PracticeQuestion> Questions { get; set; } = [];
}

public class PracticeQuestion
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public PracticeSession Session { get; set; } = null!;
    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public int QuestionVersionId { get; set; }
    public QuestionVersion QuestionVersion { get; set; } = null!;
    public int Order { get; set; }
    [MaxLength(100)] public string? Answer { get; set; }
    public bool? IsCorrect { get; set; }
    public int TimeSpentSeconds { get; set; }
    public DateTime? AnsweredAt { get; set; }
    public bool IsFlagged { get; set; }
    [MaxLength(10)] public string? EliminatedOptions { get; set; }
}

public class StudentMistake
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public int SkillId { get; set; }
    public int MistakeCount { get; set; }
    public bool IsResolved { get; set; }
    public DateTime LastWrongAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    /// <summary>Last place the mistake was made: "attempt:12" or "practice:7".</summary>
    [MaxLength(40)] public string? LastSource { get; set; }
    [MaxLength(100)] public string? LastAnswer { get; set; }
}

public class StudentSkillStatistic
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int SkillId { get; set; }
    public Skill Skill { get; set; } = null!;
    public int AttemptedCount { get; set; }
    public int CorrectCount { get; set; }
    public long TotalTimeSeconds { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped] public double Accuracy => AttemptedCount == 0 ? 0 : CorrectCount * 100.0 / AttemptedCount;
    [NotMapped] public double AverageTime => AttemptedCount == 0 ? 0 : (double)TotalTimeSeconds / AttemptedCount;
}

public class ScoreHistory
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int AttemptId { get; set; }
    public TestType TestType { get; set; }
    public int TotalScore { get; set; }
    public int RwScore { get; set; }
    public int MathScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ---------------------------------------------------------------- Configuration

public class ScoringEntry
{
    public int Id { get; set; }
    [MaxLength(40)] public string ScoringVersion { get; set; } = "";
    public Section Section { get; set; }
    public ModuleRoute Route { get; set; }
    public int RawScore { get; set; }
    public int ScaledScore { get; set; }
}

public class AdaptiveRule
{
    public int Id { get; set; }
    public Section Section { get; set; }
    public int QuestionCount { get; set; }
    public int Cutoff { get; set; }
    public ModuleRoute LowRoute { get; set; } = ModuleRoute.Easy;
    public ModuleRoute HighRoute { get; set; } = ModuleRoute.Hard;
    public int DefaultModuleSeconds { get; set; }
}

public class AppSetting
{
    [Key, MaxLength(80)] public string Key { get; set; } = "";
    [MaxLength(2000)] public string Value { get; set; } = "";
}

// ---------------------------------------------------------------- Materials / homework

public class Material
{
    public int Id { get; set; }
    [MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(2000)] public string? Description { get; set; }
    public MaterialCategory Category { get; set; }
    public int? FileId { get; set; }
    public MediaFile? File { get; set; }
    [MaxLength(500)] public string? ExternalUrl { get; set; }
    public int? GroupId { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Homework
{
    public int Id { get; set; }
    [MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(2000)] public string? Description { get; set; }
    public HomeworkKind Kind { get; set; }
    public int? GroupId { get; set; }
    public Group? Group { get; set; }
    public int? StudentId { get; set; }
    public Student? Student { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public int? TestId { get; set; }
    public Section? Section { get; set; }
    public int? DomainId { get; set; }
    public int? SkillId { get; set; }
    public Difficulty? Difficulty { get; set; }
    public int QuestionCount { get; set; } = 20;
    public int? FileId { get; set; }
    public DateTime Deadline { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<HomeworkSubmission> Submissions { get; set; } = [];
}

public class HomeworkSubmission
{
    public int Id { get; set; }
    public int HomeworkId { get; set; }
    public Homework Homework { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public SubmissionStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? Score { get; set; }
    public int? MaxScore { get; set; }
    public int? PracticeSessionId { get; set; }
    public int? AttemptId { get; set; }
    public int? FileId { get; set; }
    [MaxLength(4000)] public string? TextAnswer { get; set; }
    [MaxLength(2000)] public string? TeacherComment { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

// ---------------------------------------------------------------- Notifications / audit / auth

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public NotificationType Type { get; set; }
    [MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(2000)] public string Body { get; set; } = "";
    [MaxLength(300)] public string? Link { get; set; }
    public bool IsRead { get; set; }
    public bool SentToTelegram { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    [MaxLength(60)] public string Action { get; set; } = "";
    [MaxLength(60)] public string EntityType { get; set; } = "";
    public int? EntityId { get; set; }
    [MaxLength(4000)] public string? Details { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PhoneVerification
{
    public int Id { get; set; }
    [MaxLength(20)] public string Phone { get; set; } = "";
    [MaxLength(8)] public string Code { get; set; } = "";
    [MaxLength(40)] public string LinkToken { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public int Attempts { get; set; }
    public bool Consumed { get; set; }
    /// <summary>What the code is for: "register", "login", "reset".</summary>
    [MaxLength(20)] public string Purpose { get; set; } = "register";
}

public class TelegramLink
{
    [Key, MaxLength(20)] public string Phone { get; set; } = "";
    public long ChatId { get; set; }
    [MaxLength(64)] public string? Username { get; set; }
    [MaxLength(120)] public string? FirstName { get; set; }
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One-time token that turns an interactive (SignalR) login into an auth cookie, or a Telegram magic link.</summary>
public class LoginToken
{
    [Key, MaxLength(64)] public string Token { get; set; } = "";
    public int UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Used { get; set; }
    [MaxLength(300)] public string? ReturnUrl { get; set; }
}

/// <summary>Conversation state of the Telegram bot for a chat (registration wizard).</summary>
public class BotState
{
    [Key] public long ChatId { get; set; }
    [MaxLength(40)] public string Step { get; set; } = "";
    [MaxLength(20)] public string? Phone { get; set; }
    [MaxLength(20)] public string? Role { get; set; }
    [MaxLength(120)] public string? FullName { get; set; }
    [MaxLength(20)] public string? ChildCode { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

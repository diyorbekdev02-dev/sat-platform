using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SatPlatform.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<ParentStudent> ParentStudents => Set<ParentStudent>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Attendance> Attendances => Set<Attendance>();

    public DbSet<Domain> Domains => Set<Domain>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<QuestionVersion> QuestionVersions => Set<QuestionVersion>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();

    public DbSet<Test> Tests => Set<Test>();
    public DbSet<TestModule> TestModules => Set<TestModule>();
    public DbSet<TestQuestion> TestQuestions => Set<TestQuestion>();
    public DbSet<TestAssignment> TestAssignments => Set<TestAssignment>();
    public DbSet<TestAttempt> TestAttempts => Set<TestAttempt>();
    public DbSet<AttemptModule> AttemptModules => Set<AttemptModule>();
    public DbSet<AttemptQuestion> AttemptQuestions => Set<AttemptQuestion>();

    public DbSet<PracticeSession> PracticeSessions => Set<PracticeSession>();
    public DbSet<PracticeQuestion> PracticeQuestions => Set<PracticeQuestion>();
    public DbSet<StudentMistake> StudentMistakes => Set<StudentMistake>();
    public DbSet<StudentSkillStatistic> StudentSkillStatistics => Set<StudentSkillStatistic>();
    public DbSet<ScoreHistory> ScoreHistories => Set<ScoreHistory>();

    public DbSet<ScoringEntry> ScoringEntries => Set<ScoringEntry>();
    public DbSet<AdaptiveRule> AdaptiveRules => Set<AdaptiveRule>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    public DbSet<Material> Materials => Set<Material>();
    public DbSet<Homework> Homeworks => Set<Homework>();
    public DbSet<HomeworkSubmission> HomeworkSubmissions => Set<HomeworkSubmission>();

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<StaffTask> StaffTasks => Set<StaffTask>();
    public DbSet<PhoneVerification> PhoneVerifications => Set<PhoneVerification>();
    public DbSet<TelegramLink> TelegramLinks => Set<TelegramLink>();
    public DbSet<LoginToken> LoginTokens => Set<LoginToken>();
    public DbSet<BotState> BotStates => Set<BotState>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Phone).IsUnique();
        b.Entity<User>().HasIndex(u => u.TelegramChatId);
        b.Entity<User>().HasOne(u => u.Student).WithOne(s => s.User).HasForeignKey<Student>(s => s.UserId);

        b.Entity<Student>().HasIndex(s => s.UserId).IsUnique();
        b.Entity<Payment>().HasOne(p => p.Student).WithMany().HasForeignKey(p => p.StudentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Payment>().HasIndex(p => new { p.StudentId, p.Date });
        b.Entity<Payment>().Property(p => p.Amount).HasPrecision(14, 2);
        b.Entity<Lead>().HasOne(l => l.ModeratorUser).WithMany().HasForeignKey(l => l.ModeratorUserId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<StaffTask>().HasOne(t => t.AssigneeUser).WithMany().HasForeignKey(t => t.AssigneeUserId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Student>().HasIndex(s => s.ParentCode).IsUnique();

        b.Entity<ParentStudent>().HasKey(p => new { p.ParentUserId, p.StudentId });
        b.Entity<ParentStudent>().HasOne(p => p.ParentUser).WithMany().HasForeignKey(p => p.ParentUserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ParentStudent>().HasOne(p => p.Student).WithMany(s => s.Parents).HasForeignKey(p => p.StudentId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Group>().HasOne(g => g.TeacherUser).WithMany().HasForeignKey(g => g.TeacherUserId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Group>().HasOne(g => g.AssistantUser).WithMany().HasForeignKey(g => g.AssistantUserId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<Enrollment>().HasIndex(e => new { e.StudentId, e.GroupId, e.Status });
        b.Entity<Attendance>().HasIndex(a => new { a.GroupId, a.StudentId, a.Date }).IsUnique();

        b.Entity<Domain>().HasIndex(d => d.Code).IsUnique();
        b.Entity<Skill>().HasIndex(s => s.Code).IsUnique();
        b.Entity<Skill>().HasOne(s => s.Domain).WithMany(d => d.Skills).HasForeignKey(s => s.DomainId);

        b.Entity<Question>().HasOne(q => q.Domain).WithMany().HasForeignKey(q => q.DomainId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Question>().HasOne(q => q.Skill).WithMany().HasForeignKey(q => q.SkillId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Question>().HasIndex(q => new { q.Section, q.DomainId, q.SkillId, q.Difficulty, q.Status });
        b.Entity<QuestionOption>().HasOne(o => o.Question).WithMany(q => q.Options).HasForeignKey(o => o.QuestionId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<QuestionVersion>().HasOne(v => v.Question).WithMany(q => q.Versions).HasForeignKey(v => v.QuestionId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<QuestionVersion>().HasIndex(v => new { v.QuestionId, v.Version }).IsUnique();
        b.Entity<MediaFile>().HasIndex(m => m.Name).IsUnique();

        b.Entity<TestModule>().HasOne(m => m.Test).WithMany(t => t.Modules).HasForeignKey(m => m.TestId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<TestQuestion>().HasOne(q => q.TestModule).WithMany(m => m.Questions).HasForeignKey(q => q.TestModuleId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<TestQuestion>().HasOne(q => q.Question).WithMany().HasForeignKey(q => q.QuestionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TestQuestion>().HasIndex(q => new { q.TestModuleId, q.QuestionId }).IsUnique();

        b.Entity<TestAssignment>().HasOne(a => a.Test).WithMany().HasForeignKey(a => a.TestId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<TestAssignment>().HasOne(a => a.Student).WithMany().HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<TestAssignment>().HasOne(a => a.Group).WithMany().HasForeignKey(a => a.GroupId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<TestAttempt>().HasOne(a => a.Student).WithMany().HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<TestAttempt>().HasOne(a => a.Test).WithMany().HasForeignKey(a => a.TestId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TestAttempt>().HasIndex(a => new { a.StudentId, a.Status });
        b.Entity<AttemptModule>().HasOne(m => m.Attempt).WithMany(a => a.Modules).HasForeignKey(m => m.AttemptId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AttemptQuestion>().HasOne(q => q.Attempt).WithMany(a => a.Questions).HasForeignKey(q => q.AttemptId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AttemptQuestion>().HasOne(q => q.Module).WithMany().HasForeignKey(q => q.AttemptModuleId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AttemptQuestion>().HasOne(q => q.Question).WithMany().HasForeignKey(q => q.QuestionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AttemptQuestion>().HasOne(q => q.QuestionVersion).WithMany().HasForeignKey(q => q.QuestionVersionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AttemptQuestion>().HasIndex(q => new { q.AttemptId, q.Order });

        b.Entity<PracticeSession>().HasOne(s => s.Student).WithMany().HasForeignKey(s => s.StudentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PracticeQuestion>().HasOne(q => q.Session).WithMany(s => s.Questions).HasForeignKey(q => q.SessionId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PracticeQuestion>().HasOne(q => q.Question).WithMany().HasForeignKey(q => q.QuestionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PracticeQuestion>().HasOne(q => q.QuestionVersion).WithMany().HasForeignKey(q => q.QuestionVersionId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<StudentMistake>().HasIndex(m => new { m.StudentId, m.QuestionId }).IsUnique();
        b.Entity<StudentMistake>().HasOne(m => m.Question).WithMany().HasForeignKey(m => m.QuestionId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<StudentSkillStatistic>().HasIndex(s => new { s.StudentId, s.SkillId }).IsUnique();
        b.Entity<ScoreHistory>().HasIndex(s => s.StudentId);

        b.Entity<ScoringEntry>().HasIndex(s => new { s.ScoringVersion, s.Section, s.Route, s.RawScore }).IsUnique();
        b.Entity<AdaptiveRule>().HasIndex(r => r.Section).IsUnique();

        b.Entity<Material>().HasOne(m => m.File).WithMany().HasForeignKey(m => m.FileId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Homework>().HasOne(h => h.CreatedByUser).WithMany().HasForeignKey(h => h.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Homework>().HasOne(h => h.Group).WithMany().HasForeignKey(h => h.GroupId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Homework>().HasOne(h => h.Student).WithMany().HasForeignKey(h => h.StudentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<HomeworkSubmission>().HasOne(s => s.Homework).WithMany(h => h.Submissions).HasForeignKey(s => s.HomeworkId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<HomeworkSubmission>().HasIndex(s => new { s.HomeworkId, s.StudentId }).IsUnique();

        b.Entity<Notification>().HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Notification>().HasIndex(n => new { n.UserId, n.IsRead });
        b.Entity<AuditLog>().HasIndex(a => a.CreatedAt);
        b.Entity<PhoneVerification>().HasIndex(v => v.Phone);
        b.Entity<PhoneVerification>().HasIndex(v => v.LinkToken);
    }
}

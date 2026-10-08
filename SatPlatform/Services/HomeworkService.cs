using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services;

public record HomeworkRow(Homework Homework, int Total, int Submitted, int Reviewed);
public record StudentHomework(Homework Homework, HomeworkSubmission? Submission);

public class HomeworkService(IDbContextFactory<AppDbContext> factory, PracticeService practice, AuditService audit, NotificationService notifications)
{
    public async Task<(int Id, string? Error)> CreateAsync(Homework h, int userId)
    {
        if (string.IsNullOrWhiteSpace(h.Title)) return (0, "Sarlavha kiriting.");
        if (h.GroupId is null && h.StudentId is null) return (0, "Guruh yoki o'quvchini tanlang.");
        if (h.Deadline <= DateTime.UtcNow) return (0, "Muddat kelajakda bo'lishi kerak.");
        if (h.Kind == HomeworkKind.Practice && h.QuestionCount is < 1 or > 60) return (0, "Savollar soni 1–60.");
        if (h.Kind == HomeworkKind.Test && h.TestId is null) return (0, "Testni tanlang.");
        await using var db = await factory.CreateDbContextAsync();
        if (h.Kind == HomeworkKind.Test)
        {
            var test = await db.Tests.FindAsync(h.TestId);
            if (test is null || test.Status != TestStatus.Published) return (0, "Faqat chop etilgan testni berish mumkin.");
        }
        h.CreatedByUserId = userId;
        db.Homeworks.Add(h);
        await db.SaveChangesAsync();

        var studentIds = h.StudentId is int sid ? [sid] : await db.Enrollments.Where(e => e.GroupId == h.GroupId && e.Status == EnrollmentStatus.Active).Select(e => e.StudentId).Distinct().ToListAsync();
        foreach (var s in studentIds) db.HomeworkSubmissions.Add(new HomeworkSubmission { HomeworkId = h.Id, StudentId = s, Status = SubmissionStatus.NotStarted });
        if (h.Kind == HomeworkKind.Test)
            db.TestAssignments.Add(new TestAssignment { TestId = h.TestId!.Value, StudentId = h.StudentId, GroupId = h.StudentId is null ? h.GroupId : null, AssignedByUserId = userId, StartAt = DateTime.UtcNow, EndAt = h.Deadline, AttemptLimit = 1 });
        await db.SaveChangesAsync();
        await audit.LogAsync(userId, "HomeworkCreated", "Homework", h.Id, h.Title);
        _ = notifications.HomeworkAssignedAsync(h.Id);
        return (h.Id, null);
    }

    public async Task<List<HomeworkRow>> ForStaffAsync(int userId, UserRole role, IEnumerable<int> groupScope)
    {
        await using var db = await factory.CreateDbContextAsync();
        var groups = groupScope.ToList();
        var q = db.Homeworks.AsNoTracking().Include(h => h.Group).Include(h => h.Student).ThenInclude(s => s!.User).Include(h => h.CreatedByUser).AsQueryable();
        if (role is UserRole.Teacher or UserRole.Assistant) q = q.Where(h => h.CreatedByUserId == userId || (h.GroupId != null && groups.Contains(h.GroupId.Value)));
        var list = await q.OrderByDescending(h => h.CreatedAt).Take(200).ToListAsync();
        var ids = list.Select(h => h.Id).ToList();
        var subs = await db.HomeworkSubmissions.Where(s => ids.Contains(s.HomeworkId)).GroupBy(s => s.HomeworkId)
            .Select(g => new { g.Key, T = g.Count(), S = g.Count(s => s.Status == SubmissionStatus.Submitted || s.Status == SubmissionStatus.Reviewed), R = g.Count(s => s.Status == SubmissionStatus.Reviewed) }).ToDictionaryAsync(x => x.Key);
        return list.Select(h => { subs.TryGetValue(h.Id, out var s); return new HomeworkRow(h, s?.T ?? 0, s?.S ?? 0, s?.R ?? 0); }).ToList();
    }

    public async Task<List<StudentHomework>> ForStudentAsync(int studentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var subs = await db.HomeworkSubmissions.AsNoTracking().Include(s => s.Homework).ThenInclude(h => h.CreatedByUser).Where(s => s.StudentId == studentId).OrderByDescending(s => s.Homework.Deadline).ToListAsync();
        return subs.Select(s => new StudentHomework(s.Homework, s)).ToList();
    }

    public async Task<(Homework Homework, List<HomeworkSubmission> Submissions)?> DetailAsync(int homeworkId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var h = await db.Homeworks.AsNoTracking().Include(x => x.Group).Include(x => x.Student).ThenInclude(s => s!.User).FirstOrDefaultAsync(x => x.Id == homeworkId);
        if (h is null) return null;
        var subs = await db.HomeworkSubmissions.AsNoTracking().Include(s => s.Student).ThenInclude(s => s.User).Where(s => s.HomeworkId == homeworkId).OrderBy(s => s.Student.User.FullName).ToListAsync();
        return (h, subs);
    }

    /// <summary>Student starts a practice-type homework: a practice session bound to the homework.</summary>
    public async Task<(int? SessionId, string? Error)> StartPracticeAsync(int homeworkId, int studentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var sub = await db.HomeworkSubmissions.Include(s => s.Homework).FirstOrDefaultAsync(s => s.HomeworkId == homeworkId && s.StudentId == studentId);
        if (sub is null) return (null, "Uy vazifasi topilmadi.");
        if (sub.Homework.Kind != HomeworkKind.Practice) return (null, "Bu vazifa mashq turida emas.");
        if (sub.Status is SubmissionStatus.Submitted or SubmissionStatus.Reviewed) return (sub.PracticeSessionId, "Vazifa allaqachon topshirilgan.");
        if (sub.Homework.Deadline < DateTime.UtcNow) return (null, "Vazifa muddati o'tgan.");
        if (sub.PracticeSessionId is int existing && await db.PracticeSessions.AnyAsync(p => p.Id == existing && p.Status == PracticeStatus.InProgress)) return (existing, null);
        var h = sub.Homework;
        var session = await practice.StartAsync(studentId, new PracticeRequest { Mode = PracticeMode.Homework, Section = h.Section, DomainId = h.DomainId, SkillId = h.SkillId, Difficulty = h.Difficulty, Count = h.QuestionCount, HomeworkId = h.Id, Title = h.Title });
        sub.Status = SubmissionStatus.InProgress; sub.StartedAt ??= DateTime.UtcNow; sub.PracticeSessionId = session.Id;
        await db.SaveChangesAsync();
        return (session.Id, null);
    }

    public async Task<string?> SubmitFileAsync(int homeworkId, int studentId, int? fileId, string? text)
    {
        await using var db = await factory.CreateDbContextAsync();
        var sub = await db.HomeworkSubmissions.Include(s => s.Homework).FirstOrDefaultAsync(s => s.HomeworkId == homeworkId && s.StudentId == studentId);
        if (sub is null) return "Uy vazifasi topilmadi.";
        if (sub.Status == SubmissionStatus.Reviewed) return "Vazifa allaqachon tekshirilgan.";
        if (fileId is null && string.IsNullOrWhiteSpace(text)) return "Fayl yoki matn kiriting.";
        sub.FileId = fileId ?? sub.FileId; sub.TextAnswer = text; sub.Status = SubmissionStatus.Submitted; sub.SubmittedAt = DateTime.UtcNow; sub.StartedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> ReviewAsync(int submissionId, int? score, int? maxScore, string? comment, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var sub = await db.HomeworkSubmissions.FirstOrDefaultAsync(s => s.Id == submissionId);
        if (sub is null) return "Topilmadi.";
        if (score is not null) sub.Score = score;
        if (maxScore is not null) sub.MaxScore = maxScore;
        sub.TeacherComment = comment; sub.Status = SubmissionStatus.Reviewed; sub.ReviewedByUserId = userId; sub.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        _ = notifications.HomeworkReviewedAsync(submissionId);
        return null;
    }

    public async Task DeleteAsync(int homeworkId, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var h = await db.Homeworks.FindAsync(homeworkId);
        if (h is null) return;
        db.Homeworks.Remove(h);
        await db.SaveChangesAsync();
        await audit.LogAsync(userId, "HomeworkDeleted", "Homework", homeworkId, h.Title);
    }
}

public class MaterialService(IDbContextFactory<AppDbContext> factory, AuditService audit)
{
    public async Task<List<Material>> ListAsync(MaterialCategory? category = null, IEnumerable<int>? groupIds = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.Materials.AsNoTracking().Include(m => m.File).AsQueryable();
        if (category is not null) q = q.Where(m => m.Category == category);
        if (groupIds is not null) { var l = groupIds.ToList(); q = q.Where(m => m.GroupId == null || l.Contains(m.GroupId.Value)); }
        return await q.OrderByDescending(m => m.CreatedAt).ToListAsync();
    }

    public async Task<(int Id, string? Error)> SaveAsync(Material m, int userId)
    {
        if (string.IsNullOrWhiteSpace(m.Title)) return (0, "Sarlavha kiriting.");
        if (m.FileId is null && string.IsNullOrWhiteSpace(m.ExternalUrl)) return (0, "Fayl yuklang yoki havola kiriting.");
        await using var db = await factory.CreateDbContextAsync();
        if (m.Id == 0) { m.CreatedByUserId = userId; db.Materials.Add(m); }
        else
        {
            var e = await db.Materials.FindAsync(m.Id);
            if (e is null) return (0, "Topilmadi.");
            e.Title = m.Title; e.Description = m.Description; e.Category = m.Category; e.FileId = m.FileId; e.ExternalUrl = m.ExternalUrl; e.GroupId = m.GroupId;
        }
        await db.SaveChangesAsync();
        await audit.LogAsync(userId, "MaterialSaved", "Material", m.Id, m.Title);
        return (m.Id, null);
    }

    public async Task DeleteAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var m = await db.Materials.FindAsync(id);
        if (m is null) return;
        db.Materials.Remove(m);
        if (m.FileId is int fid) await db.MediaFiles.Where(f => f.Id == fid).ExecuteDeleteAsync();
        await db.SaveChangesAsync();
        await audit.LogAsync(userId, "MaterialDeleted", "Material", id, m.Title);
    }
}

/// <summary>Daily Telegram practice reminder for students who did nothing today (at the configured local hour).</summary>
public class PracticeReminderJob(IDbContextFactory<AppDbContext> factory, SettingsService settings, AnalyticsService analytics, NotificationService notifications, ILogger<PracticeReminderJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        DateOnly? lastRun = null;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var hour = await settings.GetIntAsync(SettingKeys.PracticeReminderHour);
                var local = Tz.ToLocal(DateTime.UtcNow);
                var today = DateOnly.FromDateTime(local);
                if (local.Hour >= hour && lastRun != today)
                {
                    lastRun = today;
                    await RunAsync(ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning(ex, "Practice reminder job failed"); }
            try { await Task.Delay(TimeSpan.FromMinutes(5), ct); } catch (OperationCanceledException) { }
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync();
        var todayStart = Tz.ToUtc(Tz.Today.ToDateTime(TimeOnly.MinValue));
        var students = await db.Students.AsNoTracking().Include(s => s.User)
            .Where(s => s.Status == StudentStatus.Active && s.User.IsActive && s.User.TelegramChatId != null && s.User.NotifyPracticeReminder)
            .Where(s => !db.PracticeSessions.Any(p => p.StudentId == s.Id && p.StartedAt >= todayStart) && !db.TestAttempts.Any(a => a.StudentId == s.Id && a.StartedAt >= todayStart))
            .Where(s => db.StudentSkillStatistics.Any(x => x.StudentId == s.Id))
            .ToListAsync(ct);
        foreach (var s in students)
        {
            var rec = (await analytics.RecommendationsAsync(s.Id, 1)).FirstOrDefault();
            if (rec is null) continue;
            await notifications.PracticeReminderAsync(s.UserId, rec.Skill, rec.Questions);
        }
        logger.LogInformation("Practice reminders sent to {Count} students", students.Count);
    }
}

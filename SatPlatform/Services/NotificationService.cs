using System.Net;
using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;
using SatPlatform.Services.Telegram;

namespace SatPlatform.Services;

/// <summary>In-app notifications; mirrored to Telegram when the user has linked the bot and opted in.</summary>
public class NotificationService(IDbContextFactory<AppDbContext> factory, TelegramBotClient bot, IConfiguration config, ILogger<NotificationService> logger)
{
    private string PublicUrl => (config["App:PublicUrl"] ?? "").TrimEnd('/');
    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    public async Task SendAsync(int userId, NotificationType type, string title, string body, string? link = null, string? telegramHtml = null)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            var user = await db.Users.FindAsync(userId);
            if (user is null) return;
            var enabled = type switch
            {
                NotificationType.TestResult => user.NotifyTestResults,
                NotificationType.Attendance => user.NotifyAttendance,
                NotificationType.Homework or NotificationType.Assignment => user.NotifyHomework,
                NotificationType.PracticeReminder => user.NotifyPracticeReminder,
                _ => true
            };
            var n = new Notification { UserId = userId, Type = type, Title = title, Body = body, Link = link };
            db.Notifications.Add(n);
            await db.SaveChangesAsync();

            if (enabled && user.TelegramChatId is long chat && bot.IsConfigured)
            {
                var html = telegramHtml ?? $"<b>{H(title)}</b>\n{H(body)}";
                if (link is not null && PublicUrl.Length > 0) html += $"\n\n<a href=\"{PublicUrl}{link}\">Saytda ochish</a>";
                n.SentToTelegram = await bot.SendMessageAsync(chat, html);
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "Notification failed for user {UserId}", userId); }
    }

    public async Task<List<Notification>> ListAsync(int userId, int take = 30)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Notifications.AsNoTracking().Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).Take(take).ToListAsync();
    }

    public async Task<int> UnreadCountAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task MarkAllReadAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    // ---------------------------------------------------------------- domain events

    public async Task TestCompletedAsync(int attemptId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var a = await db.TestAttempts.AsNoTracking().Include(x => x.Test).Include(x => x.Student).ThenInclude(s => s.User).FirstOrDefaultAsync(x => x.Id == attemptId);
        if (a is null || a.IsPreview || a.TotalScore is null) return;
        var previous = await db.ScoreHistories.AsNoTracking().Where(s => s.StudentId == a.StudentId && s.AttemptId != attemptId && s.CreatedAt < a.CompletedAt)
            .OrderByDescending(s => s.CreatedAt).Select(s => (int?)s.TotalScore).FirstOrDefaultAsync();
        var diff = previous is null ? "" : $"\nOldingi: {previous}\nO'zgarish: {(a.TotalScore - previous >= 0 ? "+" : "")}{a.TotalScore - previous}";

        var body = $"{a.Test.Title}\nUmumiy: {a.TotalScore}\nR&W: {a.RwScore}  Math: {a.MathScore}{diff}";
        await SendAsync(a.Student.UserId, NotificationType.TestResult, "Test natijasi", body, $"/attempt/{attemptId}/result",
            $"📊 <b>SAT natija</b>\n{H(a.Test.Title)}\n\nUmumiy: <b>{a.TotalScore}</b>\nR&amp;W: {a.RwScore}  ·  Math: {a.MathScore}{H(diff)}");

        var parents = await db.ParentStudents.Where(p => p.StudentId == a.StudentId).Select(p => p.ParentUserId).ToListAsync();
        foreach (var pid in parents)
            await SendAsync(pid, NotificationType.TestResult, $"{a.Student.User.FullName}: test natijasi", body, "/parent",
                $"📊 <b>SAT Mock natijasi</b>\nO'quvchi: {H(a.Student.User.FullName)}\n{H(a.Test.Title)}\n\nUmumiy: <b>{a.TotalScore}</b>\nR&amp;W: {a.RwScore}  ·  Math: {a.MathScore}{H(diff)}");
    }

    public async Task AttendanceMarkedAsync(int studentId, int groupId, DateOnly date, AttendanceStatus status, int lateMinutes)
    {
        if (status is AttendanceStatus.Present or AttendanceStatus.Excused) return;
        await using var db = await factory.CreateDbContextAsync();
        var student = await db.Students.AsNoTracking().Include(s => s.User).FirstOrDefaultAsync(s => s.Id == studentId);
        var group = await db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == groupId);
        if (student is null || group is null) return;
        var time = group.StartTime is TimeOnly t ? t.ToString("HH:mm") + " dagi " : "";
        var text = status == AttendanceStatus.Absent
            ? $"⚠️ Farzandingiz {student.User.FullName} {date:dd.MM.yyyy} kuni {time}{group.Name} darsiga kelmadi."
            : $"⏰ Farzandingiz {student.User.FullName} {date:dd.MM.yyyy} kuni {time}{group.Name} darsiga {lateMinutes} daqiqa kechikdi.";
        var parents = await db.ParentStudents.Where(p => p.StudentId == studentId).Select(p => p.ParentUserId).ToListAsync();
        foreach (var pid in parents) await SendAsync(pid, NotificationType.Attendance, "Davomat", text, "/parent/attendance");
        await SendAsync(student.UserId, NotificationType.Attendance, "Davomat",
            status == AttendanceStatus.Absent ? $"{date:dd.MM.yyyy} — {group.Name} darsida yo'q deb belgilandingiz." : $"{date:dd.MM.yyyy} — {group.Name} darsiga {lateMinutes} daqiqa kechikdingiz.", "/profile");
    }

    public async Task HomeworkAssignedAsync(int homeworkId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var hw = await db.Homeworks.AsNoTracking().FirstOrDefaultAsync(h => h.Id == homeworkId);
        if (hw is null) return;
        var studentUserIds = hw.StudentId is int sid
            ? await db.Students.Where(s => s.Id == sid).Select(s => s.UserId).ToListAsync()
            : await db.Enrollments.Where(e => e.GroupId == hw.GroupId && e.Status == EnrollmentStatus.Active).Select(e => e.Student.UserId).Distinct().ToListAsync();
        foreach (var uid in studentUserIds)
            await SendAsync(uid, NotificationType.Homework, "Yangi uy vazifasi", $"{hw.Title}\nMuddat: {Tz.Fmt(hw.Deadline)}", "/homework",
                $"📋 <b>Yangi uy vazifasi</b>\n{H(hw.Title)}\nMuddat: {Tz.Fmt(hw.Deadline)}");
    }

    public async Task TestAssignedAsync(int assignmentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var a = await db.TestAssignments.AsNoTracking().Include(x => x.Test).FirstOrDefaultAsync(x => x.Id == assignmentId);
        if (a is null) return;
        var studentUserIds = a.StudentId is int sid
            ? await db.Students.Where(s => s.Id == sid).Select(s => s.UserId).ToListAsync()
            : a.GroupId is int gid
                ? await db.Enrollments.Where(e => e.GroupId == gid && e.Status == EnrollmentStatus.Active).Select(e => e.Student.UserId).Distinct().ToListAsync()
                : [];
        foreach (var uid in studentUserIds)
            await SendAsync(uid, NotificationType.Assignment, "Yangi test tayinlandi", $"{a.Test.Title}" + (a.EndAt is null ? "" : $"\nMuddat: {Tz.Fmt(a.EndAt)}"), "/tests",
                $"📝 <b>Yangi test tayinlandi</b>\n{H(a.Test.Title)}" + (a.EndAt is null ? "" : $"\nMuddat: {Tz.Fmt(a.EndAt)}"));
    }

    public async Task HomeworkReviewedAsync(int submissionId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var s = await db.HomeworkSubmissions.AsNoTracking().Include(x => x.Homework).Include(x => x.Student).FirstOrDefaultAsync(x => x.Id == submissionId);
        if (s is null) return;
        var body = $"{s.Homework.Title}\nBall: {s.Score}/{s.MaxScore}" + (string.IsNullOrWhiteSpace(s.TeacherComment) ? "" : $"\nIzoh: {s.TeacherComment}");
        await SendAsync(s.Student.UserId, NotificationType.Homework, "Uy vazifasi tekshirildi", body, "/homework");
    }

    public async Task PracticeReminderAsync(int userId, string skillName, int questions)
    {
        await SendAsync(userId, NotificationType.PracticeReminder, "Bugungi mashq", $"{skillName} — {questions} ta savol", "/practice",
            $"🎯 <b>Bugungi mashq</b>\n\n{H(skillName)} — {questions} ta savol\n\nBugun hali mashq qilmadingiz. 15 daqiqa yetadi!");
    }
}

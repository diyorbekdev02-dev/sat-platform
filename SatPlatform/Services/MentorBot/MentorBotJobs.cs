using System.Text;
using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services.MentorBot;

/// <summary>
/// Automatic mentor-bot messages, checked every minute: lesson reminders (1 hour and 10 minutes before), homework deadline reminders (24 h),
/// scheduled broadcasts, a daily risk report to admins (19:00) and a weekly report to parents (Sunday 18:00). Each message goes out once
/// (keys in <see cref="MentorReminderLog"/>).
/// </summary>
public class MentorBotJobs(MentorBotSender sender, IDbContextFactory<AppDbContext> factory, AcademyService academy, ILogger<MentorBotJobs> logger) : BackgroundService
{
    private static string H(string? s) => MentorBotSender.H(s);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(30), ct); } catch (OperationCanceledException) { return; }
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            if (!sender.IsConfigured) continue;
            try { await RunAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogWarning(ex, "Mentor bot jobs failed"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    public async Task RunAsync(CancellationToken ct)
    {
        await ScheduledBroadcastsAsync(ct);
        await LessonRemindersAsync(ct);
        await DeadlineRemindersAsync(ct);
        var now = Tz.ToLocal(DateTime.UtcNow);
        if (now.Hour == 19) await DailyRiskAsync(ct);
        if (now.DayOfWeek == DayOfWeek.Sunday && now.Hour == 18) await WeeklyParentReportAsync(ct);
        if (now.Hour == 3 && now.Minute < 2) await CleanupAsync(ct);
    }

    /// <summary>Marks a key as done; false if it was already sent.</summary>
    private static async Task<bool> OnceAsync(AppDbContext db, string key, CancellationToken ct)
    {
        if (await db.MentorReminderLogs.AnyAsync(x => x.Key == key, ct)) return false;
        db.MentorReminderLogs.Add(new MentorReminderLog { Key = key });
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return false; }
    }

    private async Task ScheduledBroadcastsAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var due = await db.MentorBroadcasts.Where(b => b.SentAt == null && b.ScheduledAt != null && b.ScheduledAt <= DateTime.UtcNow).Select(b => b.Id).ToListAsync(ct);
        foreach (var id in due) await sender.BroadcastAsync(id, ct);
    }

    private async Task LessonRemindersAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var now = Tz.ToLocal(DateTime.UtcNow);
        var today = DateOnly.FromDateTime(now);
        var groups = (await db.Groups.AsNoTracking().Where(g => g.IsActive && g.StartTime != null && g.ScheduleDays != "").ToListAsync(ct))
            .Where(g => AcademyService.HasClassOn(g, today)).ToList();
        foreach (var g in groups)
        {
            var start = today.ToDateTime(g.StartTime!.Value);
            var minutes = (start - now).TotalMinutes;
            int? slot = minutes is > 55 and <= 60 ? 60 : minutes is > 5 and <= 10 ? 10 : null;
            if (slot is null || !await OnceAsync(db, $"les:{g.Id}:{today:yyyyMMdd}:{slot}", ct)) continue;
            var users = await db.Enrollments.Where(e => e.GroupId == g.Id && e.Status == EnrollmentStatus.Active).Select(e => e.Student.UserId).ToListAsync(ct);
            var text = $"🔔 <b>{(slot == 60 ? "1 soatdan" : "10 daqiqadan")} keyin dars!</b>\n👥 {H(g.Name)}\n⏰ {g.StartTime:HH\\:mm}" + (string.IsNullOrWhiteSpace(g.Room) ? "" : $"\n🚪 {H(g.Room)}");
            foreach (var u in users) await sender.SendToUserAsync(u, text, null, ct);
        }
    }

    private async Task DeadlineRemindersAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var from = DateTime.UtcNow.AddHours(23); var to = DateTime.UtcNow.AddHours(24);
        var subs = await db.HomeworkSubmissions.AsNoTracking().Where(s => (s.Status == SubmissionStatus.NotStarted || s.Status == SubmissionStatus.InProgress) && s.Homework.Deadline > from && s.Homework.Deadline <= to)
            .Select(s => new { s.Id, s.HomeworkId, s.Homework.Title, s.Homework.Deadline, s.Student.UserId }).ToListAsync(ct);
        foreach (var s in subs)
        {
            if (!await OnceAsync(db, $"hw:{s.Id}", ct)) continue;
            await sender.SendToUserAsync(s.UserId, $"⏰ <b>Vazifa muddati ertaga tugaydi</b>\n📝 {H(s.Title)}\nMuddat: {Tz.Fmt(s.Deadline)}",
                new { inline_keyboard = new[] { new[] { new { text = "📤 Ochish", callback_data = $"sh:{s.HomeworkId}" } } } }, ct);
        }
    }

    private async Task DailyRiskAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (!await OnceAsync(db, $"risk:{Tz.Today:yyyyMMdd}", ct)) return;
        var all = await db.Students.Where(s => s.Status == StudentStatus.Active).Select(s => s.Id).ToListAsync(ct);
        var rows = await MentorBotService.RiskAsync(db, all, ct);
        if (rows.Count == 0) return;
        var sb = new StringBuilder($"⚠️ <b>Kunlik xavf signali</b> ({rows.Count})\n");
        foreach (var r in rows.Take(40)) sb.Append($"\n• <b>{H(r.Name)}</b> — {H(r.Reason)}");
        var admins = await db.Users.Where(u => u.IsActive && ((u.BotRole ?? u.Role) == UserRole.Admin || (u.BotRole ?? u.Role) == UserRole.SuperAdmin)).Select(u => u.Id).ToListAsync(ct);
        foreach (var a in admins) await sender.SendToUserAsync(a, sb.ToString(), null, ct);
        // teachers get only their own students
        var teachers = await db.Groups.Where(g => g.IsActive).SelectMany(g => new[] { g.TeacherUserId, g.AssistantUserId }).Where(x => x != null).Distinct().ToListAsync(ct);
        foreach (var t in teachers.OfType<int>())
        {
            var user = await db.Users.FindAsync([t], ct);
            if (user is null || !user.IsActive || admins.Contains(t)) continue;
            var scope = (await academy.StudentScopeAsync(t, user.BotRole ?? user.Role)).ToHashSet();
            var mine = rows.Where(r => scope.Contains(r.StudentId)).ToList();
            if (mine.Count == 0) continue;
            await sender.SendToUserAsync(t, "⚠️ <b>Xavf signali</b>\n" + string.Join("\n", mine.Select(r => $"• <b>{H(r.Name)}</b> — {H(r.Reason)}")), null, ct);
        }
    }

    private async Task WeeklyParentReportAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var week = Tz.Today.AddDays(-6);
        var links = await db.ParentStudents.AsNoTracking().Where(p => p.ParentUser.IsActive && p.Student.Status == StudentStatus.Active)
            .Select(p => new { p.ParentUserId, p.StudentId, p.Student.User.FullName }).ToListAsync(ct);
        foreach (var l in links)
        {
            if (!await OnceAsync(db, $"wk:{Tz.Today:yyyyMMdd}:{l.ParentUserId}:{l.StudentId}", ct)) continue;
            var att = await db.Attendances.AsNoTracking().Where(a => a.StudentId == l.StudentId && a.Date >= week).ToListAsync(ct);
            var weekStart = DateTime.UtcNow.AddDays(-7);
            var subs = await db.HomeworkSubmissions.AsNoTracking().Include(s => s.Homework).Where(s => s.StudentId == l.StudentId && s.Homework.Deadline >= weekStart && s.Homework.Deadline <= DateTime.UtcNow).ToListAsync(ct);
            var graded = subs.Where(s => s.Score is not null && s.MaxScore is > 0).ToList();
            var quiz = await db.MentorQuizResults.AsNoTracking().Where(q => q.StudentId == l.StudentId && q.CreatedAt >= weekStart).ToListAsync(ct);
            var bal = await db.Payments.Where(p => p.StudentId == l.StudentId).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
            var text = $"🗞 <b>Haftalik hisobot</b> — {H(l.FullName)}\n{week:dd.MM}–{Tz.Today:dd.MM}\n" +
                       $"\n🗓 Davomat: {(att.Count == 0 ? "dars bo'lmagan" : $"{att.Count(a => a.Status is AttendanceStatus.Present or AttendanceStatus.Late)}/{att.Count} ({string.Join(" ", att.OrderBy(a => a.Date).Select(a => a.Date.ToString("dd.MM") + (a.Status switch { AttendanceStatus.Present => "✅", AttendanceStatus.Absent => "❌", AttendanceStatus.Late => "⏱", _ => "🟡" })))})")}" +
                       $"\n📝 Vazifalar: {subs.Count(s => s.Status >= SubmissionStatus.Submitted)}/{subs.Count} topshirilgan" +
                       (graded.Count > 0 ? $", o'rtacha baho {graded.Average(s => s.Score!.Value * 100.0 / s.MaxScore!.Value):0}%" : "") +
                       (quiz.Count > 0 ? $"\n🧠 Mini testlar: {string.Join(", ", quiz.Select(q => $"{q.Correct}/{q.Total}"))}" : "") +
                       $"\n💳 Balans: {Labels.Money(bal)} so'm{(bal < 0 ? " 🔴" : "")}";
            await sender.SendToUserAsync(l.ParentUserId, text, null, ct);
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var old = DateTime.UtcNow.AddDays(-90);
        await db.MentorBotMessages.Where(m => m.CreatedAt < old).ExecuteDeleteAsync(ct);
        await db.MentorReminderLogs.Where(m => m.SentAt < old).ExecuteDeleteAsync(ct);
    }
}

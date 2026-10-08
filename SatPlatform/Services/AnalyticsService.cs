using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services;

public record SkillStat(int SkillId, string Skill, string Domain, Section Section, int Attempted, int Correct, double Accuracy, double AvgTime, string Level);
public record Recommendation(int SkillId, string Skill, string Domain, Section Section, double Accuracy, int Attempted, int Questions, Difficulty Difficulty, string Reason, double Priority);
public record ScorePoint(int AttemptId, DateTime Date, int Total, int Rw, int Math, TestType Type, string Title);
public record TimeStats(double AvgAll, double AvgCorrect, double AvgWrong, Dictionary<Difficulty, double> ByDifficulty, List<(string Skill, double Avg)> BySkill, int Answered);

public class StudentDashboard
{
    public int? CurrentScore { get; set; }
    public int? CurrentRw { get; set; }
    public int? CurrentMath { get; set; }
    public int? TargetScore { get; set; }
    public int? Improvement { get; set; }
    public int? FirstScore { get; set; }
    public double OverallAccuracy { get; set; }
    public int QuestionsSolved { get; set; }
    public int Streak { get; set; }
    public double? AttendanceRate { get; set; }
    public int UnresolvedMistakes { get; set; }
    public int PracticeCount { get; set; }
    public int TestCount { get; set; }
    public bool PracticedToday { get; set; }
    public List<SkillStat> WeakSkills { get; set; } = [];
    public List<Recommendation> Recommendations { get; set; } = [];
    public List<ScorePoint> RecentScores { get; set; } = [];
    public bool HasDiagnostic { get; set; }
}

public class AnalyticsService(IDbContextFactory<AppDbContext> factory, SettingsService settings)
{
    public async Task<(int Weak, int Strong, int Min)> ThresholdsAsync() =>
        (await settings.GetIntAsync(SettingKeys.WeakThreshold), await settings.GetIntAsync(SettingKeys.StrongThreshold), await settings.GetIntAsync(SettingKeys.MinSkillSample));

    public async Task<List<SkillStat>> SkillStatsAsync(int studentId, Section? section = null)
    {
        var (weak, strong, _) = await ThresholdsAsync();
        await using var db = await factory.CreateDbContextAsync();
        var q = db.StudentSkillStatistics.AsNoTracking().Include(s => s.Skill).ThenInclude(s => s.Domain).Where(s => s.StudentId == studentId);
        if (section is not null) q = q.Where(s => s.Skill.Domain.Section == section);
        var rows = await q.ToListAsync();
        return rows.Select(s => new SkillStat(s.SkillId, s.Skill.Name, s.Skill.Domain.Name, s.Skill.Domain.Section, s.AttemptedCount, s.CorrectCount, Math.Round(s.Accuracy, 1), Math.Round(s.AverageTime),
                s.Accuracy < weak ? "weak" : s.Accuracy > strong ? "strong" : "medium"))
            .OrderBy(s => s.Section).ThenBy(s => s.Domain).ThenBy(s => s.Accuracy).ToList();
    }

    public async Task<List<ScorePoint>> ScoresAsync(int studentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ScoreHistories.AsNoTracking().Where(s => s.StudentId == studentId).OrderBy(s => s.CreatedAt)
            .Select(s => new ScorePoint(s.AttemptId, s.CreatedAt, s.TotalScore, s.RwScore, s.MathScore, s.TestType, db.TestAttempts.Where(a => a.Id == s.AttemptId).Select(a => a.Test.Title).FirstOrDefault() ?? ""))
            .ToListAsync();
    }

    /// <summary>Weak skills first, weighted by how slow and how often wrong the student is there.</summary>
    public async Task<List<Recommendation>> RecommendationsAsync(int studentId, int take = 3)
    {
        var (weak, strong, min) = await ThresholdsAsync();
        var count = await settings.GetIntAsync(SettingKeys.RecommendedQuestionCount);
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.StudentSkillStatistics.AsNoTracking().Include(s => s.Skill).ThenInclude(s => s.Domain).Where(s => s.StudentId == studentId && s.AttemptedCount >= Math.Min(min, 3)).ToListAsync();
        if (rows.Count == 0) return [];
        var mistakes = await db.StudentMistakes.Where(m => m.StudentId == studentId && !m.IsResolved).GroupBy(m => m.SkillId).Select(g => new { SkillId = g.Key, C = g.Sum(m => m.MistakeCount), Recent = g.Max(m => m.LastWrongAt) }).ToListAsync();
        var avgTimeAll = rows.Sum(r => r.TotalTimeSeconds) / (double)Math.Max(1, rows.Sum(r => r.AttemptedCount));
        var available = await db.Questions.Where(q => q.Status == QuestionStatus.Active).GroupBy(q => q.SkillId).Select(g => new { SkillId = g.Key, C = g.Count() }).ToDictionaryAsync(x => x.SkillId, x => x.C);

        var list = new List<Recommendation>();
        foreach (var r in rows)
        {
            if (!available.TryGetValue(r.SkillId, out var avail) || avail == 0) continue;
            var m = mistakes.FirstOrDefault(x => x.SkillId == r.SkillId);
            var accuracyGap = Math.Max(0, 100 - r.Accuracy) / 100.0;          // 0..1
            var slow = avgTimeAll > 0 ? Math.Clamp((r.AverageTime - avgTimeAll) / avgTimeAll, 0, 1) : 0; // slower than own average
            var wrong = m is null ? 0 : Math.Min(1, m.C / 10.0);
            var recency = m is not null && m.Recent > DateTime.UtcNow.AddDays(-7) ? 0.15 : 0;
            var sample = Math.Min(1, r.AttemptedCount / (double)Math.Max(1, min));
            var priority = (accuracyGap * 0.6 + slow * 0.2 + wrong * 0.2 + recency) * (0.5 + 0.5 * sample);
            var reason = r.Accuracy < weak ? $"Aniqlik {r.Accuracy:0}% — zaif" : r.Accuracy <= strong ? $"Aniqlik {r.Accuracy:0}% — mustahkamlash kerak" : $"Aniqlik {r.Accuracy:0}%";
            if (slow > 0.3) reason += ", sekin";
            if (m is not null && m.C >= 3) reason += $", {m.C} ta xato";
            var difficulty = r.Accuracy < weak ? (r.Accuracy < 30 ? Difficulty.Easy : Difficulty.Medium) : r.Accuracy > strong ? Difficulty.Hard : Difficulty.Medium;
            list.Add(new Recommendation(r.SkillId, r.Skill.Name, r.Skill.Domain.Name, r.Skill.Domain.Section, Math.Round(r.Accuracy, 1), r.AttemptedCount, Math.Min(count, avail), difficulty, reason, priority));
        }
        return list.OrderByDescending(x => x.Priority).Take(take).ToList();
    }

    public async Task<TimeStats> TimeStatsAsync(int studentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var a = await db.AttemptQuestions.AsNoTracking().Where(q => q.Attempt.StudentId == studentId && q.AnsweredAt != null && !q.Attempt.IsPreview)
            .Select(q => new { q.IsCorrect, q.TimeSpentSeconds, q.Question.Difficulty, Skill = q.Question.Skill.Name }).ToListAsync();
        var p = await db.PracticeQuestions.AsNoTracking().Where(q => q.Session.StudentId == studentId && q.AnsweredAt != null)
            .Select(q => new { q.IsCorrect, q.TimeSpentSeconds, q.Question.Difficulty, Skill = q.Question.Skill.Name }).ToListAsync();
        var all = a.Concat(p).Where(x => x.TimeSpentSeconds > 0).ToList();
        double Avg(IEnumerable<int> xs) { var l = xs.ToList(); return l.Count == 0 ? 0 : Math.Round(l.Average()); }
        return new TimeStats(Avg(all.Select(x => x.TimeSpentSeconds)), Avg(all.Where(x => x.IsCorrect == true).Select(x => x.TimeSpentSeconds)), Avg(all.Where(x => x.IsCorrect == false).Select(x => x.TimeSpentSeconds)),
            Enum.GetValues<Difficulty>().ToDictionary(d => d, d => Avg(all.Where(x => x.Difficulty == d).Select(x => x.TimeSpentSeconds))),
            all.GroupBy(x => x.Skill).Select(g => (g.Key, Avg(g.Select(x => x.TimeSpentSeconds)))).OrderByDescending(x => x.Item2).Take(12).ToList(), all.Count);
    }

    public async Task<int> StreakAsync(int studentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var since = DateTime.UtcNow.AddDays(-120);
        var days = (await db.PracticeSessions.Where(s => s.StudentId == studentId && s.Status == PracticeStatus.Completed && s.StartedAt > since).Select(s => s.StartedAt).ToListAsync())
            .Concat(await db.TestAttempts.Where(a => a.StudentId == studentId && a.Status == AttemptStatus.Completed && !a.IsPreview && a.StartedAt > since).Select(a => a.StartedAt).ToListAsync())
            .Select(d => DateOnly.FromDateTime(Tz.ToLocal(d))).ToHashSet();
        var today = Tz.Today;
        var day = days.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (days.Contains(day)) { streak++; day = day.AddDays(-1); }
        return streak;
    }

    public async Task<StudentDashboard> DashboardAsync(int studentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var d = new StudentDashboard();
        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId);
        d.TargetScore = student?.TargetScore;
        var scores = await ScoresAsync(studentId);
        d.RecentScores = scores.TakeLast(6).ToList();
        if (scores.Count > 0)
        {
            var last = scores[^1];
            d.CurrentScore = last.Total; d.CurrentRw = last.Rw; d.CurrentMath = last.Math;
            d.FirstScore = scores[0].Total;
            if (scores.Count > 1) d.Improvement = last.Total - scores[^2].Total;
        }
        var stat = await db.StudentSkillStatistics.Where(s => s.StudentId == studentId).GroupBy(_ => 1).Select(g => new { A = g.Sum(s => s.AttemptedCount), C = g.Sum(s => s.CorrectCount) }).FirstOrDefaultAsync();
        d.QuestionsSolved = stat?.A ?? 0;
        d.OverallAccuracy = stat is null || stat.A == 0 ? 0 : Math.Round(stat.C * 100.0 / stat.A, 1);
        d.UnresolvedMistakes = await db.StudentMistakes.CountAsync(m => m.StudentId == studentId && !m.IsResolved);
        d.PracticeCount = await db.PracticeSessions.CountAsync(s => s.StudentId == studentId && s.Status == PracticeStatus.Completed);
        d.TestCount = await db.TestAttempts.CountAsync(a => a.StudentId == studentId && a.Status == AttemptStatus.Completed && !a.IsPreview);
        d.HasDiagnostic = await db.TestAttempts.AnyAsync(a => a.StudentId == studentId && a.Status == AttemptStatus.Completed && !a.IsPreview && a.Test.Type == TestType.Diagnostic);
        var todayStart = Tz.ToUtc(Tz.Today.ToDateTime(TimeOnly.MinValue));
        d.PracticedToday = await db.PracticeSessions.AnyAsync(s => s.StudentId == studentId && s.StartedAt >= todayStart) || await db.TestAttempts.AnyAsync(a => a.StudentId == studentId && a.StartedAt >= todayStart && !a.IsPreview);
        d.Streak = await StreakAsync(studentId);
        var att = await db.Attendances.Where(a => a.StudentId == studentId).GroupBy(_ => 1).Select(g => new { T = g.Count(), P = g.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late) }).FirstOrDefaultAsync();
        d.AttendanceRate = att is null || att.T == 0 ? null : Math.Round(att.P * 100.0 / att.T);
        var (_, _, min) = await ThresholdsAsync();
        d.WeakSkills = (await SkillStatsAsync(studentId)).Where(s => s.Level == "weak" && s.Attempted >= min).OrderBy(s => s.Accuracy).Take(6).ToList();
        d.Recommendations = await RecommendationsAsync(studentId);
        return d;
    }

    /// <summary>Progress to target: (current − 400) / (target − 400).</summary>
    public static int? TargetProgress(int? current, int? target) =>
        current is null || target is null || target <= 400 ? null : (int)Math.Clamp(Math.Round((current.Value - 400) * 100.0 / (target.Value - 400)), 0, 100);

    // ---------------------------------------------------------------- staff analytics

    public record StudentSummary(int StudentId, int UserId, string Name, string Phone, StudentStatus Status, int? Score, int? Target, double? Accuracy, double? Attendance, int Tests, int Practices, DateTime? LastActive, string Groups, string? WeakestSkill, double? WeakestAccuracy);

    public async Task<List<StudentSummary>> StudentSummariesAsync(IEnumerable<int>? studentIds = null, int? groupId = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.Students.AsNoTracking().Include(s => s.User).Include(s => s.Enrollments.Where(e => e.Status == EnrollmentStatus.Active)).ThenInclude(e => e.Group).AsQueryable();
        if (studentIds is not null) { var ids = studentIds.ToList(); q = q.Where(s => ids.Contains(s.Id)); }
        if (groupId is not null) q = q.Where(s => s.Enrollments.Any(e => e.GroupId == groupId && e.Status == EnrollmentStatus.Active));
        var students = await q.ToListAsync();
        var sids = students.Select(s => s.Id).ToList();
        var lastScores = await db.ScoreHistories.Where(h => sids.Contains(h.StudentId)).GroupBy(h => h.StudentId).Select(g => new { StudentId = g.Key, Score = g.OrderByDescending(h => h.CreatedAt).Select(h => h.TotalScore).First() }).ToDictionaryAsync(x => x.StudentId, x => x.Score);
        var acc = await db.StudentSkillStatistics.Where(s => sids.Contains(s.StudentId)).GroupBy(s => s.StudentId).Select(g => new { StudentId = g.Key, A = g.Sum(s => s.AttemptedCount), C = g.Sum(s => s.CorrectCount) }).ToDictionaryAsync(x => x.StudentId, x => x.A == 0 ? (double?)null : Math.Round(x.C * 100.0 / x.A));
        var att = await db.Attendances.Where(a => sids.Contains(a.StudentId)).GroupBy(a => a.StudentId).Select(g => new { StudentId = g.Key, T = g.Count(), P = g.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late) }).ToDictionaryAsync(x => x.StudentId, x => x.T == 0 ? (double?)null : Math.Round(x.P * 100.0 / x.T));
        var tests = await db.TestAttempts.Where(a => sids.Contains(a.StudentId) && a.Status == AttemptStatus.Completed && !a.IsPreview).GroupBy(a => a.StudentId).Select(g => new { StudentId = g.Key, C = g.Count(), Last = g.Max(a => a.StartedAt) }).ToDictionaryAsync(x => x.StudentId);
        var practices = await db.PracticeSessions.Where(p => sids.Contains(p.StudentId) && p.Status == PracticeStatus.Completed).GroupBy(p => p.StudentId).Select(g => new { StudentId = g.Key, C = g.Count(), Last = g.Max(p => p.StartedAt) }).ToDictionaryAsync(x => x.StudentId);
        var (_, _, min) = await ThresholdsAsync();
        var weakest = (await db.StudentSkillStatistics.Where(s => sids.Contains(s.StudentId) && s.AttemptedCount >= min).Select(s => new { s.StudentId, Skill = s.Skill.Name, s.AttemptedCount, s.CorrectCount }).ToListAsync())
            .GroupBy(s => s.StudentId).ToDictionary(g => g.Key, g => g.OrderBy(s => s.CorrectCount * 1.0 / s.AttemptedCount).First());

        return students.Select(s =>
        {
            tests.TryGetValue(s.Id, out var t); practices.TryGetValue(s.Id, out var p);
            var last = new[] { t?.Last, p?.Last, s.User.LastSeenAt }.Where(x => x is not null).Max();
            weakest.TryGetValue(s.Id, out var w);
            return new StudentSummary(s.Id, s.UserId, s.User.FullName, s.User.Phone, s.Status, lastScores.TryGetValue(s.Id, out var sc) ? sc : (int?)null, s.TargetScore,
                acc.GetValueOrDefault(s.Id), att.GetValueOrDefault(s.Id), t?.C ?? 0, p?.C ?? 0, last, string.Join(", ", s.Enrollments.Select(e => e.Group.Name)),
                w?.Skill, w is null ? null : Math.Round(w.CorrectCount * 100.0 / w.AttemptedCount));
        }).OrderBy(s => s.Name).ToList();
    }

    public record SkillAggregate(int SkillId, string Skill, string Domain, Section Section, int Attempted, double Accuracy, int Students);

    /// <summary>Skill accuracy aggregated over a set of students (group / center).</summary>
    public async Task<List<SkillAggregate>> GroupSkillsAsync(IEnumerable<int>? studentIds)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.StudentSkillStatistics.AsNoTracking().AsQueryable();
        if (studentIds is not null) { var ids = studentIds.ToList(); q = q.Where(s => ids.Contains(s.StudentId)); }
        var rows = await q.GroupBy(s => new { s.SkillId, Skill = s.Skill.Name, Domain = s.Skill.Domain.Name, s.Skill.Domain.Section })
            .Select(g => new { g.Key, A = g.Sum(s => s.AttemptedCount), C = g.Sum(s => s.CorrectCount), N = g.Count() }).ToListAsync();
        return rows.Where(r => r.A > 0).Select(r => new SkillAggregate(r.Key.SkillId, r.Key.Skill, r.Key.Domain, r.Key.Section, r.A, Math.Round(r.C * 100.0 / r.A, 1), r.N)).OrderBy(r => r.Accuracy).ToList();
    }

    public record QuestionAnalytics(int Id, Section Section, string Skill, Difficulty Difficulty, int Attempts, int Correct, double Accuracy, double AvgTime, string Preview);

    public async Task<List<QuestionAnalytics>> QuestionAnalyticsAsync(Section? section, int minAttempts, string sort)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.Questions.AsNoTracking().Where(x => x.AttemptCount >= minAttempts);
        if (section is not null) q = q.Where(x => x.Section == section);
        var rows = await q.Select(x => new { x.Id, x.Section, Skill = x.Skill.Name, x.Difficulty, x.AttemptCount, x.CorrectCount, x.TotalTimeSeconds, x.Text }).Take(2000).ToListAsync();
        var list = rows.Select(x => new QuestionAnalytics(x.Id, x.Section, x.Skill, x.Difficulty, x.AttemptCount, x.CorrectCount, x.AttemptCount == 0 ? 0 : Math.Round(x.CorrectCount * 100.0 / x.AttemptCount, 1),
            x.AttemptCount == 0 ? 0 : Math.Round((double)x.TotalTimeSeconds / x.AttemptCount), ContentService.Preview(x.Text, 90)));
        return (sort switch { "hardest" => list.OrderBy(x => x.Accuracy), "easiest" => list.OrderByDescending(x => x.Accuracy), "slowest" => list.OrderByDescending(x => x.AvgTime), _ => list.OrderByDescending(x => x.Attempts) }).Take(200).ToList();
    }

    public record CenterStats(int Students, int Teachers, int Groups, int TestsToday, int QuestionsToday, double? AvgScore, double? AvgAttendance, int ActiveQuestions, int PublishedTests);

    public async Task<CenterStats> CenterAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var todayStart = Tz.ToUtc(Tz.Today.ToDateTime(TimeOnly.MinValue));
        var lastScores = await db.ScoreHistories.GroupBy(h => h.StudentId).Select(g => g.OrderByDescending(h => h.CreatedAt).Select(h => h.TotalScore).First()).ToListAsync();
        var att = await db.Attendances.Where(a => a.Date >= Tz.Today.AddDays(-30)).GroupBy(_ => 1).Select(g => new { T = g.Count(), P = g.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late) }).FirstOrDefaultAsync();
        return new CenterStats(
            await db.Students.CountAsync(s => s.Status == StudentStatus.Active),
            await db.Users.CountAsync(u => u.Role == UserRole.Teacher && u.IsActive),
            await db.Groups.CountAsync(g => g.IsActive),
            await db.TestAttempts.CountAsync(a => a.StartedAt >= todayStart && !a.IsPreview),
            await db.AttemptQuestions.CountAsync(q => q.AnsweredAt >= todayStart) + await db.PracticeQuestions.CountAsync(q => q.AnsweredAt >= todayStart),
            lastScores.Count == 0 ? null : Math.Round(lastScores.Average()),
            att is null || att.T == 0 ? null : Math.Round(att.P * 100.0 / att.T),
            await db.Questions.CountAsync(q => q.Status == QuestionStatus.Active),
            await db.Tests.CountAsync(t => t.Status == TestStatus.Published));
    }

    public record GroupPerf(int GroupId, string Group, string? Teacher, int Students, double? AvgScore, double? AvgAccuracy, double? Attendance);

    public async Task<List<GroupPerf>> GroupPerformanceAsync(IEnumerable<int>? groupIds = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var gq = db.Groups.AsNoTracking().Include(g => g.TeacherUser).Where(g => g.IsActive);
        if (groupIds is not null) { var ids = groupIds.ToList(); gq = gq.Where(g => ids.Contains(g.Id)); }
        var groups = await gq.ToListAsync();
        var result = new List<GroupPerf>();
        foreach (var g in groups)
        {
            var sids = await db.Enrollments.Where(e => e.GroupId == g.Id && e.Status == EnrollmentStatus.Active).Select(e => e.StudentId).ToListAsync();
            var scores = sids.Count == 0 ? [] : await db.ScoreHistories.Where(h => sids.Contains(h.StudentId)).GroupBy(h => h.StudentId).Select(x => x.OrderByDescending(h => h.CreatedAt).Select(h => h.TotalScore).First()).ToListAsync();
            var acc = sids.Count == 0 ? null : await db.StudentSkillStatistics.Where(s => sids.Contains(s.StudentId)).GroupBy(_ => 1).Select(x => new { A = x.Sum(s => s.AttemptedCount), C = x.Sum(s => s.CorrectCount) }).FirstOrDefaultAsync();
            var att = await db.Attendances.Where(a => a.GroupId == g.Id).GroupBy(_ => 1).Select(x => new { T = x.Count(), P = x.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late) }).FirstOrDefaultAsync();
            result.Add(new GroupPerf(g.Id, g.Name, g.TeacherUser?.FullName, sids.Count, scores.Count == 0 ? null : Math.Round(scores.Average()),
                acc is null || acc.A == 0 ? null : Math.Round(acc.C * 100.0 / acc.A), att is null || att.T == 0 ? null : Math.Round(att.P * 100.0 / att.T)));
        }
        return result.OrderByDescending(r => r.AvgScore ?? 0).ToList();
    }
}

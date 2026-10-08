using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services;

public class PracticeRequest
{
    public PracticeMode Mode { get; set; } = PracticeMode.Custom;
    public Section? Section { get; set; }
    public int? DomainId { get; set; }
    public int? SkillId { get; set; }
    public Difficulty? Difficulty { get; set; }
    public int Count { get; set; } = 15;
    public int? TimeLimitMinutes { get; set; }
    public int? HomeworkId { get; set; }
    public string? Title { get; set; }
}

public record MistakeGroup(int SkillId, string Skill, string Domain, Section Section, int Count, int Unresolved);

/// <summary>Practice sessions: question selection, answering (with instant feedback), finishing and the mistake bank.</summary>
public class PracticeService(IDbContextFactory<AppDbContext> factory, StatisticsService stats, SettingsService settings, ILogger<PracticeService> logger)
{
    public async Task<PracticeSession> StartAsync(int studentId, PracticeRequest r)
    {
        if (r.Count is < 1 or > 60) throw new AppException("Savollar soni 1 dan 60 gacha bo'lishi kerak.");
        await using var db = await factory.CreateDbContextAsync();

        var active = await db.PracticeSessions.FirstOrDefaultAsync(s => s.StudentId == studentId && s.Status == PracticeStatus.InProgress);
        if (active is not null)
        {
            if (r.HomeworkId is not null && active.HomeworkId == r.HomeworkId) return active;
            // an abandoned session from a closed tab: close it so the student is not stuck
            active.Status = PracticeStatus.Abandoned; active.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var questionIds = await SelectAsync(db, studentId, r);
        if (questionIds.Count == 0) throw new AppException("Bu tanlov bo'yicha savollar topilmadi. Boshqa mavzu yoki qiyinlik tanlang.");

        var session = new PracticeSession
        {
            StudentId = studentId, Mode = r.Mode, Section = r.Section, DomainId = r.DomainId, SkillId = r.SkillId, Difficulty = r.Difficulty,
            QuestionCount = questionIds.Count, TimeLimitSeconds = r.TimeLimitMinutes is int m and > 0 ? m * 60 : null, Status = PracticeStatus.InProgress,
            HomeworkId = r.HomeworkId, Title = r.Title ?? await TitleAsync(db, r)
        };
        if (session.TimeLimitSeconds is int tl) session.DeadlineAt = DateTime.UtcNow.AddSeconds(tl);
        db.PracticeSessions.Add(session);
        await db.SaveChangesAsync();
        var order = 0;
        foreach (var qid in questionIds)
            db.PracticeQuestions.Add(new PracticeQuestion { SessionId = session.Id, QuestionId = qid, QuestionVersionId = await ContentService.CurrentVersionIdAsync(db, qid), Order = order++ });
        await db.SaveChangesAsync();
        return session;
    }

    private static async Task<string> TitleAsync(AppDbContext db, PracticeRequest r)
    {
        if (r.SkillId is int sid) return (await db.Skills.Where(s => s.Id == sid).Select(s => s.Name).FirstOrDefaultAsync()) ?? Labels.PracticeMode(r.Mode);
        if (r.DomainId is int did) return (await db.Domains.Where(d => d.Id == did).Select(d => d.Name).FirstOrDefaultAsync()) ?? Labels.PracticeMode(r.Mode);
        if (r.Section is Section s) return $"{Labels.Section(s)} — {Labels.PracticeMode(r.Mode)}";
        return Labels.PracticeMode(r.Mode);
    }

    /// <summary>Picks questions: filters, prefers never-seen, then wrong-before, avoids recently-correct ones.</summary>
    private async Task<List<int>> SelectAsync(AppDbContext db, int studentId, PracticeRequest r)
    {
        var q = db.Questions.AsNoTracking().Where(x => x.Status == QuestionStatus.Active);
        if (r.Mode == PracticeMode.Mistake)
        {
            var mistakeQ = db.StudentMistakes.Where(m => m.StudentId == studentId && !m.IsResolved);
            if (r.Section is not null) mistakeQ = mistakeQ.Where(m => m.Question.Section == r.Section);
            if (r.SkillId is not null) mistakeQ = mistakeQ.Where(m => m.SkillId == r.SkillId);
            var mistakes = await mistakeQ.OrderByDescending(m => m.MistakeCount).ThenByDescending(m => m.LastWrongAt).Select(m => new { m.QuestionId, m.SkillId }).Take(r.Count * 2).ToListAsync();
            var chosen = mistakes.Select(m => m.QuestionId).Distinct().Take((r.Count + 1) / 2).ToList();
            // top up with fresh questions of the same skills and similar difficulty
            var skills = mistakes.Select(m => m.SkillId).Distinct().ToList();
            if (skills.Count > 0 && chosen.Count < r.Count)
            {
                var fresh = await q.Where(x => skills.Contains(x.SkillId) && !chosen.Contains(x.Id)).Select(x => x.Id).ToListAsync();
                chosen.AddRange(fresh.OrderBy(_ => Random.Shared.Next()).Take(r.Count - chosen.Count));
            }
            return chosen.OrderBy(_ => Random.Shared.Next()).ToList();
        }

        if (r.Mode == PracticeMode.Weakness)
        {
            var weak = await WeakSkillIdsAsync(db, studentId, r.Section);
            if (weak.Count == 0) throw new AppException("Hozircha zaif skilllar aniqlanmagan — avval diagnostik test yoki bir nechta mashq ishlang.");
            q = q.Where(x => weak.Contains(x.SkillId));
        }
        else
        {
            if (r.Section is not null) q = q.Where(x => x.Section == r.Section);
            if (r.DomainId is not null) q = q.Where(x => x.DomainId == r.DomainId);
            if (r.SkillId is not null) q = q.Where(x => x.SkillId == r.SkillId);
        }
        if (r.Difficulty is not null) q = q.Where(x => x.Difficulty == r.Difficulty);

        var candidates = await q.Select(x => new { x.Id, x.SkillId }).ToListAsync();
        if (candidates.Count == 0) return [];
        var ids = candidates.Select(c => c.Id).ToList();

        var recentCorrect = await db.PracticeQuestions.Where(p => p.Session.StudentId == studentId && p.IsCorrect == true && ids.Contains(p.QuestionId) && p.AnsweredAt > DateTime.UtcNow.AddDays(-14)).Select(p => p.QuestionId).Distinct().ToListAsync();
        recentCorrect.AddRange(await db.AttemptQuestions.Where(a => a.Attempt.StudentId == studentId && a.IsCorrect == true && ids.Contains(a.QuestionId) && a.AnsweredAt > DateTime.UtcNow.AddDays(-14)).Select(a => a.QuestionId).Distinct().ToListAsync());
        var seen = await db.PracticeQuestions.Where(p => p.Session.StudentId == studentId && ids.Contains(p.QuestionId)).Select(p => p.QuestionId).Distinct().ToListAsync();
        seen.AddRange(await db.AttemptQuestions.Where(a => a.Attempt.StudentId == studentId && ids.Contains(a.QuestionId)).Select(a => a.QuestionId).Distinct().ToListAsync());
        var wrongBefore = await db.StudentMistakes.Where(m => m.StudentId == studentId && !m.IsResolved && ids.Contains(m.QuestionId)).Select(m => m.QuestionId).ToListAsync();

        var recentSet = recentCorrect.ToHashSet(); var seenSet = seen.ToHashSet(); var wrongSet = wrongBefore.ToHashSet();
        var rnd = Random.Shared;
        // tier 0: never seen, tier 1: wrong before, tier 2: seen (not recently correct), tier 3: recently correct
        var ordered = candidates
            .Select(c => (c.Id, c.SkillId, Tier: !seenSet.Contains(c.Id) ? 0 : wrongSet.Contains(c.Id) ? 1 : !recentSet.Contains(c.Id) ? 2 : 3, R: rnd.Next()))
            .OrderBy(x => x.Tier).ThenBy(x => x.R).ToList();

        // spread across skills when the request spans many skills
        var picked = new List<int>();
        var perSkill = new Dictionary<int, int>();
        var skillCount = ordered.Select(o => o.SkillId).Distinct().Count();
        var cap = skillCount > 1 ? Math.Max(1, (int)Math.Ceiling(r.Count * 1.5 / skillCount)) : int.MaxValue;
        foreach (var o in ordered)
        {
            if (picked.Count >= r.Count) break;
            var n = perSkill.GetValueOrDefault(o.SkillId);
            if (n >= cap) continue;
            picked.Add(o.Id); perSkill[o.SkillId] = n + 1;
        }
        foreach (var o in ordered) { if (picked.Count >= r.Count) break; if (!picked.Contains(o.Id)) picked.Add(o.Id); }
        return picked.OrderBy(_ => rnd.Next()).ToList();
    }

    public async Task<List<int>> WeakSkillIdsAsync(AppDbContext db, int studentId, Section? section)
    {
        var weak = await settings.GetIntAsync(SettingKeys.WeakThreshold);
        var min = await settings.GetIntAsync(SettingKeys.MinSkillSample);
        var q = db.StudentSkillStatistics.Where(s => s.StudentId == studentId && s.AttemptedCount >= min);
        if (section is not null) q = q.Where(s => s.Skill.Domain.Section == section);
        var rows = await q.Select(s => new { s.SkillId, s.AttemptedCount, s.CorrectCount }).ToListAsync();
        return rows.Where(r => r.CorrectCount * 100.0 / r.AttemptedCount < weak).Select(r => r.SkillId).ToList();
    }

    // ---------------------------------------------------------------- session flow

    public async Task<PracticeSession?> GetAsync(int sessionId, int? studentId, bool staff = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var s = await db.PracticeSessions.Include(x => x.Questions.OrderBy(q => q.Order)).ThenInclude(q => q.QuestionVersion)
            .Include(x => x.Questions).ThenInclude(q => q.Question).ThenInclude(q => q.Skill).ThenInclude(s => s.Domain)
            .FirstOrDefaultAsync(x => x.Id == sessionId && (staff || x.StudentId == studentId));
        if (s is null) return null;
        if (s.Status == PracticeStatus.InProgress && s.DeadlineAt is DateTime d && d < DateTime.UtcNow)
        {
            await FinishInternalAsync(db, s);
        }
        return s;
    }

    public async Task<PracticeSession?> ActiveAsync(int studentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.PracticeSessions.AsNoTracking().FirstOrDefaultAsync(s => s.StudentId == studentId && s.Status == PracticeStatus.InProgress);
    }

    /// <summary>Answers one question; returns correctness immediately (practice gives instant feedback).</summary>
    public async Task<(bool? Correct, string? Error)> AnswerAsync(int sessionId, int studentId, int practiceQuestionId, string? answer, int seconds)
    {
        await using var db = await factory.CreateDbContextAsync();
        var s = await db.PracticeSessions.FirstOrDefaultAsync(x => x.Id == sessionId && x.StudentId == studentId);
        if (s is null) return (null, "Mashq topilmadi.");
        if (s.Status != PracticeStatus.InProgress) return (null, "Mashq yakunlangan.");
        if (s.DeadlineAt is DateTime d && d < DateTime.UtcNow) { await FinishInternalAsync(db, s); return (null, "Vaqt tugadi."); }
        var q = await db.PracticeQuestions.Include(x => x.QuestionVersion).FirstOrDefaultAsync(x => x.Id == practiceQuestionId && x.SessionId == sessionId);
        if (q is null) return (null, "Savol topilmadi.");
        if (q.AnsweredAt is not null) return (q.IsCorrect, null);
        if (string.IsNullOrWhiteSpace(answer)) return (null, "Javobni tanlang.");
        if (q.QuestionVersion.Type == QuestionType.SPR && ScoringService.ValidateSprInput(answer) is string err) return (null, err);
        q.Answer = answer.Trim();
        q.IsCorrect = ScoringService.IsCorrect(q.QuestionVersion, q.Answer);
        q.AnsweredAt = DateTime.UtcNow;
        q.TimeSpentSeconds += Math.Clamp(seconds, 0, 3600);
        s.AnsweredCount++;
        if (q.IsCorrect == true) s.CorrectCount++;
        await db.SaveChangesAsync();
        return (q.IsCorrect, null);
    }

    public async Task FlagAsync(int sessionId, int studentId, int practiceQuestionId, bool flagged)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.PracticeQuestions.Where(q => q.Id == practiceQuestionId && q.SessionId == sessionId && q.Session.StudentId == studentId).ExecuteUpdateAsync(s => s.SetProperty(q => q.IsFlagged, flagged));
    }

    public async Task EliminateAsync(int sessionId, int studentId, int practiceQuestionId, string eliminated)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.PracticeQuestions.Where(q => q.Id == practiceQuestionId && q.SessionId == sessionId && q.Session.StudentId == studentId).ExecuteUpdateAsync(s => s.SetProperty(q => q.EliminatedOptions, eliminated.Length > 10 ? eliminated[..10] : eliminated));
    }

    public async Task<string?> FinishAsync(int sessionId, int studentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var s = await db.PracticeSessions.FirstOrDefaultAsync(x => x.Id == sessionId && x.StudentId == studentId);
        if (s is null) return "Mashq topilmadi.";
        if (s.Status != PracticeStatus.InProgress) return null;
        await FinishInternalAsync(db, s);
        return null;
    }

    private async Task FinishInternalAsync(AppDbContext db, PracticeSession s)
    {
        var qs = await db.PracticeQuestions.Include(q => q.QuestionVersion).Include(q => q.Question).Where(q => q.SessionId == s.Id).ToListAsync();
        var answered = qs.Where(q => q.AnsweredAt is not null).ToList();
        s.AnsweredCount = answered.Count;
        s.CorrectCount = answered.Count(q => q.IsCorrect == true);
        s.Status = PracticeStatus.Completed;
        s.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (answered.Count > 0)
        {
            await stats.RecordAsync(db, s.StudentId, answered.Select(q => (q.QuestionId, q.Question.SkillId, q.IsCorrect == true, q.TimeSpentSeconds, q.Answer)), $"practice:{s.Id}");
            await db.SaveChangesAsync();
        }

        if (s.HomeworkId is int hid)
        {
            var sub = await db.HomeworkSubmissions.FirstOrDefaultAsync(x => x.HomeworkId == hid && x.StudentId == s.StudentId);
            if (sub is not null && sub.Status != SubmissionStatus.Reviewed)
            {
                sub.Status = SubmissionStatus.Submitted; sub.SubmittedAt = DateTime.UtcNow; sub.Score = s.CorrectCount; sub.MaxScore = qs.Count; sub.PracticeSessionId = s.Id;
                await db.SaveChangesAsync();
            }
        }
    }

    public async Task<List<PracticeSession>> HistoryAsync(int studentId, int take = 30)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.PracticeSessions.AsNoTracking().Where(s => s.StudentId == studentId && s.Status != PracticeStatus.Abandoned).OrderByDescending(s => s.StartedAt).Take(take).ToListAsync();
    }

    // ---------------------------------------------------------------- mistakes

    public async Task<List<MistakeGroup>> MistakeGroupsAsync(int studentId, Section? section, bool unresolvedOnly)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.StudentMistakes.AsNoTracking().Where(m => m.StudentId == studentId);
        if (section is not null) q = q.Where(m => m.Question.Section == section);
        if (unresolvedOnly) q = q.Where(m => !m.IsResolved);
        var rows = await q.GroupBy(m => new { m.SkillId, Skill = m.Question.Skill.Name, Domain = m.Question.Domain.Name, m.Question.Section })
            .Select(g => new MistakeGroup(g.Key.SkillId, g.Key.Skill, g.Key.Domain, g.Key.Section, g.Count(), g.Count(m => !m.IsResolved))).ToListAsync();
        return rows.OrderByDescending(r => r.Unresolved).ThenByDescending(r => r.Count).ToList();
    }

    public async Task<List<StudentMistake>> MistakesAsync(int studentId, Section? section, int? skillId, string sort, bool unresolvedOnly)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.StudentMistakes.AsNoTracking().Include(m => m.Question).ThenInclude(q => q.Skill).ThenInclude(s => s.Domain).Where(m => m.StudentId == studentId);
        if (section is not null) q = q.Where(m => m.Question.Section == section);
        if (skillId is not null) q = q.Where(m => m.SkillId == skillId);
        if (unresolvedOnly) q = q.Where(m => !m.IsResolved);
        q = sort == "repeated" ? q.OrderByDescending(m => m.MistakeCount).ThenByDescending(m => m.LastWrongAt) : q.OrderByDescending(m => m.LastWrongAt);
        return await q.Take(200).ToListAsync();
    }

    public async Task<(StudentMistake Mistake, QuestionVersion Version)?> MistakeDetailAsync(int studentId, int questionId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var m = await db.StudentMistakes.AsNoTracking().Include(x => x.Question).ThenInclude(q => q.Skill).ThenInclude(s => s.Domain).FirstOrDefaultAsync(x => x.StudentId == studentId && x.QuestionId == questionId);
        if (m is null) return null;
        var vid = await ContentService.CurrentVersionIdAsync(db, questionId);
        var v = await db.QuestionVersions.AsNoTracking().FirstAsync(x => x.Id == vid);
        return (m, v);
    }

    public async Task<int> UnresolvedMistakeCountAsync(int studentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.StudentMistakes.CountAsync(m => m.StudentId == studentId && !m.IsResolved);
    }

    /// <summary>Closes timed practice sessions whose deadline passed and abandons sessions idle for a day.</summary>
    public async Task SweepAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var expired = await db.PracticeSessions.Where(s => s.Status == PracticeStatus.InProgress && ((s.DeadlineAt != null && s.DeadlineAt < now) || s.StartedAt < now.AddHours(-24))).Take(100).ToListAsync(ct);
        foreach (var s in expired)
        {
            try { if (s.DeadlineAt is not null && s.DeadlineAt < now) await FinishInternalAsync(db, s); else { s.Status = PracticeStatus.Abandoned; s.CompletedAt = now; await db.SaveChangesAsync(ct); } }
            catch (Exception ex) { logger.LogWarning(ex, "Practice sweep failed for {Id}", s.Id); }
        }
    }
}

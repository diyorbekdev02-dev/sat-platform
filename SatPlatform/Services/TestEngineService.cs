using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services;

public record AvailableTest(Test Test, TestAssignment? Assignment, int AttemptsUsed, int? AttemptLimit, TestAttempt? ActiveAttempt, TestAttempt? LastCompleted, bool Expired, bool NotStarted)
{
    public bool CanStart => !Expired && !NotStarted && (AttemptLimit is null || AttemptsUsed < AttemptLimit);
}

public record ModuleView(AttemptModule Module, List<AttemptQuestion> Questions);

/// <summary>State of one test session as the UI sees it.</summary>
public record AttemptState(TestAttempt Attempt, AttemptModule? CurrentModule, List<AttemptQuestion> CurrentQuestions, DateTime ServerNow)
{
    public int SecondsLeft => CurrentModule?.DeadlineAt is DateTime d ? Math.Max(0, (int)(d - ServerNow).TotalSeconds) : 0;
    public int BreakSecondsLeft => Attempt.BreakEndsAt is DateTime b ? Math.Max(0, (int)(b - ServerNow).TotalSeconds) : 0;
}

/// <summary>The SAT test engine: sessions, timers, answers, adaptive routing, breaks, auto-submit and scoring.</summary>
public class TestEngineService(IDbContextFactory<AppDbContext> factory, ScoringService scoring, StatisticsService stats, AuditService audit, NotificationService notifications, ILogger<TestEngineService> logger)
{
    /// <summary>attempt id → circuit id that currently owns the test window (one active session per attempt).</summary>
    private static readonly ConcurrentDictionary<int, (string Circuit, DateTime Seen)> ActiveSessions = new();

    public bool TryAcquireSession(int attemptId, string circuitId)
    {
        var now = DateTime.UtcNow;
        var entry = ActiveSessions.AddOrUpdate(attemptId, (circuitId, now),
            (_, old) => old.Circuit == circuitId || now - old.Seen > TimeSpan.FromSeconds(20) ? (circuitId, now) : old);
        return entry.Circuit == circuitId;
    }

    public void Heartbeat(int attemptId, string circuitId)
    {
        if (ActiveSessions.TryGetValue(attemptId, out var e) && e.Circuit == circuitId) ActiveSessions[attemptId] = (circuitId, DateTime.UtcNow);
    }

    public void ReleaseSession(int attemptId, string circuitId)
    {
        if (ActiveSessions.TryGetValue(attemptId, out var e) && e.Circuit == circuitId) ActiveSessions.TryRemove(attemptId, out _);
    }

    // ---------------------------------------------------------------- what a student can take

    public async Task<List<AvailableTest>> AvailableAsync(int studentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var groupIds = await db.Enrollments.Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Active).Select(e => e.GroupId).ToListAsync();
        var assignments = await db.TestAssignments.AsNoTracking().Include(a => a.Test)
            .Where(a => a.Test.Status == TestStatus.Published && (a.StudentId == studentId || (a.GroupId != null && groupIds.Contains(a.GroupId.Value))))
            .OrderByDescending(a => a.CreatedAt).ToListAsync();
        var publicTests = await db.Tests.AsNoTracking().Where(t => t.Status == TestStatus.Published && t.IsPublic).OrderByDescending(t => t.PublishedAt).ToListAsync();
        var attempts = await db.TestAttempts.AsNoTracking().Where(a => a.StudentId == studentId && !a.IsPreview).OrderByDescending(a => a.StartedAt).ToListAsync();

        var list = new List<AvailableTest>();
        foreach (var a in assignments)
        {
            var mine = attempts.Where(x => x.AssignmentId == a.Id).ToList();
            list.Add(new AvailableTest(a.Test, a, mine.Count(x => x.Status is not AttemptStatus.Abandoned), a.AttemptLimit,
                mine.FirstOrDefault(x => x.Status is AttemptStatus.InProgress or AttemptStatus.Break),
                mine.FirstOrDefault(x => x.Status is AttemptStatus.Completed), a.EndAt is not null && a.EndAt < now, a.StartAt > now));
        }
        foreach (var t in publicTests)
        {
            if (list.Any(x => x.Test.Id == t.Id)) continue;
            var mine = attempts.Where(x => x.TestId == t.Id && x.AssignmentId == null).ToList();
            list.Add(new AvailableTest(t, null, mine.Count(x => x.Status is not AttemptStatus.Abandoned), null,
                mine.FirstOrDefault(x => x.Status is AttemptStatus.InProgress or AttemptStatus.Break), mine.FirstOrDefault(x => x.Status == AttemptStatus.Completed), false, false));
        }
        return list;
    }

    public async Task<List<TestAttempt>> AttemptsAsync(int studentId, int take = 50)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.TestAttempts.AsNoTracking().Include(a => a.Test).Where(a => a.StudentId == studentId && !a.IsPreview).OrderByDescending(a => a.StartedAt).Take(take).ToListAsync();
    }

    public async Task<List<TestAttempt>> AttemptsForAssignmentAsync(int assignmentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.TestAttempts.AsNoTracking().Include(a => a.Student).ThenInclude(s => s.User).Where(a => a.AssignmentId == assignmentId).OrderByDescending(a => a.StartedAt).ToListAsync();
    }

    public async Task<List<TestAttempt>> AttemptsForTestAsync(int testId, int take = 200)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.TestAttempts.AsNoTracking().Include(a => a.Student).ThenInclude(s => s.User).Where(a => a.TestId == testId && !a.IsPreview).OrderByDescending(a => a.StartedAt).Take(take).ToListAsync();
    }

    public async Task<TestAttempt?> ActiveAttemptAsync(int studentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.TestAttempts.AsNoTracking().Include(a => a.Test).FirstOrDefaultAsync(a => a.StudentId == studentId && !a.IsPreview && (a.Status == AttemptStatus.InProgress || a.Status == AttemptStatus.Break));
    }

    // ---------------------------------------------------------------- start

    /// <summary>Staff preview: runs the test exactly as a student would, on a hidden student record of the staff user.</summary>
    public async Task<TestAttempt> StartPreviewAsync(int userId, int testId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var student = await db.Students.FirstOrDefaultAsync(s => s.UserId == userId);
        if (student is null)
        {
            student = new Student { UserId = userId, Status = StudentStatus.Applicant, ParentCode = await AuthService.UniqueParentCodeAsync(db) };
            db.Students.Add(student);
            await db.SaveChangesAsync();
        }
        // one preview at a time per user
        foreach (var old in await db.TestAttempts.Where(a => a.StudentId == student.Id && a.IsPreview && (a.Status == AttemptStatus.InProgress || a.Status == AttemptStatus.Break)).ToListAsync())
        { old.Status = AttemptStatus.Abandoned; old.CompletedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync();
        return await StartAsync(student.Id, testId, null, preview: true);
    }

    /// <summary>Creates the attempt with Module 1 of every section. Module 2 instances are created after routing.</summary>
    public async Task<TestAttempt> StartAsync(int studentId, int testId, int? assignmentId, bool preview = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var test = await db.Tests.Include(t => t.Modules.OrderBy(m => m.SortOrder)).ThenInclude(m => m.Questions.OrderBy(q => q.Order)).FirstOrDefaultAsync(t => t.Id == testId) ?? throw new AppException("Test topilmadi.");
        if (!preview)
        {
            if (test.Status != TestStatus.Published) throw new AppException("Bu test hali chop etilmagan.");
            var active = await db.TestAttempts.FirstOrDefaultAsync(a => a.StudentId == studentId && !a.IsPreview && (a.Status == AttemptStatus.InProgress || a.Status == AttemptStatus.Break));
            if (active is not null)
            {
                if (active.TestId == testId) return active;
                throw new AppException("Sizda boshqa test davom etmoqda. Avval uni yakunlang.");
            }
            TestAssignment? assignment = null;
            if (assignmentId is not null)
            {
                assignment = await db.TestAssignments.FindAsync(assignmentId) ?? throw new AppException("Tayinlov topilmadi.");
                if (assignment.TestId != testId) throw new AppException("Tayinlov boshqa testga tegishli.");
                var now = DateTime.UtcNow;
                if (assignment.StartAt > now) throw new AppException("Test hali boshlanmagan.");
                if (assignment.EndAt is not null && assignment.EndAt < now) throw new AppException("Test muddati tugagan.");
                if (assignment.StudentId is not null && assignment.StudentId != studentId) throw new AppException("Bu test sizga tayinlanmagan.");
                if (assignment.GroupId is not null && !await db.Enrollments.AnyAsync(e => e.StudentId == studentId && e.GroupId == assignment.GroupId && e.Status == EnrollmentStatus.Active))
                    throw new AppException("Bu test sizning guruhingizga tayinlanmagan.");
                if (assignment.AttemptLimit is int limit)
                {
                    var used = await db.TestAttempts.CountAsync(a => a.StudentId == studentId && a.AssignmentId == assignmentId && a.Status != AttemptStatus.Abandoned);
                    if (used >= limit) throw new AppException("Urinishlar soni tugagan.");
                }
            }
            else if (!test.IsPublic) throw new AppException("Bu test sizga tayinlanmagan.");
        }

        var attempt = new TestAttempt
        {
            StudentId = studentId, TestId = testId, AssignmentId = assignmentId, Status = AttemptStatus.InProgress, ScoringVersion = test.ScoringVersion,
            IsPreview = preview, RandomSeed = test.ShuffleQuestions ? Random.Shared.Next() : null
        };
        var order = 0;
        foreach (var section in test.Modules.Select(m => m.Section).Distinct())
        {
            var m1 = test.Modules.First(m => m.Section == section && m.ModuleNumber == 1);
            attempt.Modules.Add(new AttemptModule { TestModuleId = m1.Id, Section = section, ModuleNumber = 1, Route = ModuleRoute.Standard, Order = order++, TimeLimitSeconds = m1.TimeLimitSeconds, QuestionCount = m1.Questions.Count });
            // a non-adaptive second module is known up front
            var m2 = test.Modules.FirstOrDefault(m => m.Section == section && m.ModuleNumber == 2 && m.Route == ModuleRoute.Standard);
            if (!test.IsAdaptive && m2 is not null)
                attempt.Modules.Add(new AttemptModule { TestModuleId = m2.Id, Section = section, ModuleNumber = 2, Route = ModuleRoute.Standard, Order = order++, TimeLimitSeconds = m2.TimeLimitSeconds, QuestionCount = m2.Questions.Count });
            else if (test.IsAdaptive)
                attempt.Modules.Add(new AttemptModule { Section = section, ModuleNumber = 2, Route = ModuleRoute.Standard, Order = order++, TimeLimitSeconds = m1.TimeLimitSeconds, Status = AttemptModuleStatus.Pending });
        }
        db.TestAttempts.Add(attempt);
        await db.SaveChangesAsync();

        // materialize every module whose test module is known
        foreach (var am in attempt.Modules.Where(m => m.TestModuleId is not null))
            await MaterializeAsync(db, attempt, am, test.Modules.First(m => m.Id == am.TestModuleId));
        await db.SaveChangesAsync();

        await StartModuleAsync(db, attempt, attempt.Modules.OrderBy(m => m.Order).First());
        await db.SaveChangesAsync();
        if (!preview) await audit.LogAsync(null, "AttemptStarted", "TestAttempt", attempt.Id, $"student {studentId} test {testId}");
        return attempt;
    }

    private static async Task MaterializeAsync(AppDbContext db, TestAttempt attempt, AttemptModule am, TestModule tm)
    {
        var questions = tm.Questions.OrderBy(q => q.Order).ToList();
        if (attempt.RandomSeed is int seed) questions = questions.OrderBy(_ => new Random(seed + am.Order).Next()).ToList();
        var order = 0;
        foreach (var tq in questions)
        {
            var versionId = await ContentService.CurrentVersionIdAsync(db, tq.QuestionId);
            db.AttemptQuestions.Add(new AttemptQuestion { AttemptId = attempt.Id, AttemptModuleId = am.Id, QuestionId = tq.QuestionId, QuestionVersionId = versionId, Order = order++ });
        }
        am.QuestionCount = questions.Count;
        am.TestModuleId = tm.Id;
        am.TimeLimitSeconds = tm.TimeLimitSeconds;
    }

    private static Task StartModuleAsync(AppDbContext db, TestAttempt attempt, AttemptModule m)
    {
        var now = DateTime.UtcNow;
        m.Status = AttemptModuleStatus.InProgress; m.StartedAt = now; m.DeadlineAt = now.AddSeconds(m.TimeLimitSeconds);
        attempt.CurrentModuleOrder = m.Order; attempt.Status = AttemptStatus.InProgress; attempt.BreakEndsAt = null; attempt.UpdatedAt = now;
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------- state

    public async Task<AttemptState?> StateAsync(int attemptId, int? studentId, bool allowStaff = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var attempt = await db.TestAttempts.Include(a => a.Test).Include(a => a.Modules.OrderBy(m => m.Order)).FirstOrDefaultAsync(a => a.Id == attemptId);
        if (attempt is null) return null;
        if (!allowStaff && attempt.StudentId != studentId) return null;
        await EnforceDeadlinesAsync(db, attempt);
        var current = attempt.Modules.FirstOrDefault(m => m.Order == attempt.CurrentModuleOrder);
        var questions = current is null ? [] : await db.AttemptQuestions.Include(q => q.QuestionVersion).Where(q => q.AttemptModuleId == current.Id).OrderBy(q => q.Order).ToListAsync();
        return new AttemptState(attempt, current, questions, DateTime.UtcNow);
    }

    /// <summary>Server-side deadline enforcement: expired module → finish it (routing / break / submit); expired break → next module.</summary>
    private async Task EnforceDeadlinesAsync(AppDbContext db, TestAttempt attempt)
    {
        var now = DateTime.UtcNow;
        if (attempt.Status == AttemptStatus.Break && attempt.BreakEndsAt is DateTime b && b <= now)
        {
            await AdvanceAsync(db, attempt);
        }
        if (attempt.Status == AttemptStatus.InProgress)
        {
            var current = attempt.Modules.FirstOrDefault(m => m.Order == attempt.CurrentModuleOrder);
            if (current is { Status: AttemptModuleStatus.InProgress, DeadlineAt: DateTime d } && d.AddSeconds(2) <= now)
                await FinishModuleAsync(db, attempt, current, auto: true);
        }
    }

    // ---------------------------------------------------------------- answering

    public async Task<string?> AnswerAsync(int attemptId, int studentId, int attemptQuestionId, string? answer, int timeSpentDelta)
    {
        await using var db = await factory.CreateDbContextAsync();
        var attempt = await db.TestAttempts.Include(a => a.Modules).FirstOrDefaultAsync(a => a.Id == attemptId && a.StudentId == studentId);
        if (attempt is null) return "Urinish topilmadi.";
        if (attempt.Status != AttemptStatus.InProgress) return "Test faol emas.";
        var q = await db.AttemptQuestions.Include(x => x.Module).Include(x => x.QuestionVersion).FirstOrDefaultAsync(x => x.Id == attemptQuestionId && x.AttemptId == attemptId);
        if (q is null) return "Savol topilmadi.";
        if (q.Module.Order != attempt.CurrentModuleOrder || q.Module.Status != AttemptModuleStatus.InProgress) return "Bu modul yakunlangan.";
        if (q.Module.DeadlineAt is DateTime d && d.AddSeconds(3) < DateTime.UtcNow) { await FinishModuleAsync(db, attempt, q.Module, auto: true); return "Vaqt tugadi."; }

        if (q.QuestionVersion.Type == QuestionType.SPR && ScoringService.ValidateSprInput(answer) is string err) return err;
        q.Answer = string.IsNullOrWhiteSpace(answer) ? null : answer.Trim();
        q.AnsweredAt = q.Answer is null ? null : DateTime.UtcNow;
        q.IsCorrect = q.Answer is null ? null : ScoringService.IsCorrect(q.QuestionVersion, q.Answer);
        q.TimeSpentSeconds += Math.Clamp(timeSpentDelta, 0, 3600);
        attempt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return null;
    }

    public async Task TimeAsync(int attemptId, int studentId, int attemptQuestionId, int seconds)
    {
        if (seconds <= 0) return;
        await using var db = await factory.CreateDbContextAsync();
        await db.AttemptQuestions.Where(q => q.Id == attemptQuestionId && q.AttemptId == attemptId && q.Attempt.StudentId == studentId)
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.TimeSpentSeconds, q => q.TimeSpentSeconds + Math.Clamp(seconds, 0, 3600)));
    }

    public async Task FlagAsync(int attemptId, int studentId, int attemptQuestionId, bool flagged)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.AttemptQuestions.Where(q => q.Id == attemptQuestionId && q.AttemptId == attemptId && q.Attempt.StudentId == studentId)
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.IsFlagged, flagged));
    }

    public async Task EliminateAsync(int attemptId, int studentId, int attemptQuestionId, string eliminated)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.AttemptQuestions.Where(q => q.Id == attemptQuestionId && q.AttemptId == attemptId && q.Attempt.StudentId == studentId)
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.EliminatedOptions, eliminated.Length > 10 ? eliminated[..10] : eliminated));
    }

    // ---------------------------------------------------------------- module flow

    /// <summary>Student pressed "Finish module" on the review screen.</summary>
    public async Task<string?> FinishModuleAsync(int attemptId, int studentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var attempt = await db.TestAttempts.Include(a => a.Test).Include(a => a.Modules.OrderBy(m => m.Order)).FirstOrDefaultAsync(a => a.Id == attemptId && a.StudentId == studentId);
        if (attempt is null) return "Urinish topilmadi.";
        if (attempt.Status != AttemptStatus.InProgress) return "Test faol emas.";
        var current = attempt.Modules.FirstOrDefault(m => m.Order == attempt.CurrentModuleOrder && m.Status == AttemptModuleStatus.InProgress);
        if (current is null) return "Faol modul yo'q.";
        await FinishModuleAsync(db, attempt, current, auto: false);
        return null;
    }

    private async Task FinishModuleAsync(AppDbContext db, TestAttempt attempt, AttemptModule current, bool auto)
    {
        if (!db.Entry(attempt).Reference(a => a.Test).IsLoaded) await db.Entry(attempt).Reference(a => a.Test).LoadAsync();
        var now = DateTime.UtcNow;
        var qs = await db.AttemptQuestions.Include(q => q.QuestionVersion).Where(q => q.AttemptModuleId == current.Id).ToListAsync();
        foreach (var q in qs) q.IsCorrect = q.Answer is null ? false : ScoringService.IsCorrect(q.QuestionVersion, q.Answer);
        current.RawScore = qs.Count(q => q.IsCorrect == true);
        current.Status = AttemptModuleStatus.Completed;
        current.EndedAt = now;

        // adaptive routing: module 1 decides which module 2 the student gets
        if (attempt.Test.IsAdaptive && current.ModuleNumber == 1)
        {
            var route = await scoring.RouteForAsync(current.Section, current.RawScore.Value, qs.Count);
            var next = attempt.Modules.First(m => m.Section == current.Section && m.ModuleNumber == 2);
            var tm = await db.TestModules.Include(m => m.Questions).FirstOrDefaultAsync(m => m.TestId == attempt.TestId && m.Section == current.Section && m.ModuleNumber == 2 && m.Route == route)
                     ?? await db.TestModules.Include(m => m.Questions).FirstAsync(m => m.TestId == attempt.TestId && m.Section == current.Section && m.ModuleNumber == 2);
            next.Route = tm.Route;
            await MaterializeAsync(db, attempt, next, tm);
            if (current.Section == Section.RW) attempt.RwRoute = tm.Route; else attempt.MathRoute = tm.Route;
        }
        await db.SaveChangesAsync();

        var nextModule = attempt.Modules.Where(m => m.Order > current.Order).OrderBy(m => m.Order).FirstOrDefault();
        if (nextModule is null)
        {
            attempt.Status = auto ? AttemptStatus.AutoSubmitted : AttemptStatus.Scoring;
            await db.SaveChangesAsync();
            await ScoreAsync(db, attempt);
            return;
        }
        if (nextModule.Section != current.Section && attempt.Test.BreakSeconds > 0)
        {
            attempt.Status = AttemptStatus.Break;
            attempt.BreakEndsAt = now.AddSeconds(attempt.Test.BreakSeconds);
            attempt.UpdatedAt = now;
            await db.SaveChangesAsync();
            return;
        }
        await StartModuleAsync(db, attempt, nextModule);
        await db.SaveChangesAsync();
    }

    /// <summary>Break over (timer or "Skip break").</summary>
    public async Task<string?> ResumeFromBreakAsync(int attemptId, int studentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var attempt = await db.TestAttempts.Include(a => a.Test).Include(a => a.Modules.OrderBy(m => m.Order)).FirstOrDefaultAsync(a => a.Id == attemptId && a.StudentId == studentId);
        if (attempt is null) return "Urinish topilmadi.";
        if (attempt.Status != AttemptStatus.Break) return null;
        await AdvanceAsync(db, attempt);
        return null;
    }

    private static async Task AdvanceAsync(AppDbContext db, TestAttempt attempt)
    {
        var next = attempt.Modules.Where(m => m.Status == AttemptModuleStatus.Pending && m.Order > attempt.CurrentModuleOrder).OrderBy(m => m.Order).FirstOrDefault();
        if (next is null) { attempt.Status = AttemptStatus.Scoring; await db.SaveChangesAsync(); return; }
        await StartModuleAsync(db, attempt, next);
        await db.SaveChangesAsync();
    }

    /// <summary>Student gives up (or admin ends a preview). Answers are kept but no score is produced.</summary>
    public async Task AbandonAsync(int attemptId, int? studentId, bool staff = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var attempt = await db.TestAttempts.FirstOrDefaultAsync(a => a.Id == attemptId && (staff || a.StudentId == studentId));
        if (attempt is null || attempt.Status is AttemptStatus.Completed) return;
        attempt.Status = AttemptStatus.Abandoned; attempt.CompletedAt = DateTime.UtcNow; attempt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        ActiveSessions.TryRemove(attemptId, out _);
    }

    // ---------------------------------------------------------------- scoring

    private async Task ScoreAsync(AppDbContext db, TestAttempt attempt)
    {
        var modules = attempt.Modules;
        var qs = await db.AttemptQuestions.Include(q => q.QuestionVersion).Include(q => q.Question).Where(q => q.AttemptId == attempt.Id).ToListAsync();
        foreach (var q in qs) q.IsCorrect = q.Answer is null ? false : ScoringService.IsCorrect(q.QuestionVersion, q.Answer);

        var rw = qs.Where(q => modules.First(m => m.Id == q.AttemptModuleId).Section == Section.RW).ToList();
        var math = qs.Where(q => modules.First(m => m.Id == q.AttemptModuleId).Section == Section.Math).ToList();
        attempt.RwRaw = rw.Count(q => q.IsCorrect == true); attempt.RwMax = rw.Count;
        attempt.MathRaw = math.Count(q => q.IsCorrect == true); attempt.MathMax = math.Count;

        var scaledType = attempt.Test.Type is TestType.Diagnostic or TestType.FullMock or TestType.SectionTest;
        if (scaledType)
        {
            attempt.RwScore = rw.Count > 0 ? await scoring.ScaledAsync(attempt.ScoringVersion, Section.RW, attempt.RwRoute ?? ModuleRoute.Standard, attempt.RwRaw, attempt.RwMax) : null;
            attempt.MathScore = math.Count > 0 ? await scoring.ScaledAsync(attempt.ScoringVersion, Section.Math, attempt.MathRoute ?? ModuleRoute.Standard, attempt.MathRaw, attempt.MathMax) : null;
            attempt.TotalScore = (attempt.RwScore ?? 0) + (attempt.MathScore ?? 0);
            if (attempt.RwScore is null || attempt.MathScore is null) attempt.TotalScore = null; // section-only test: no total
        }
        attempt.Status = AttemptStatus.Completed;
        attempt.CompletedAt = DateTime.UtcNow;
        attempt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        ActiveSessions.TryRemove(attempt.Id, out _);

        if (attempt.IsPreview) return;

        if (attempt.TotalScore is int total && attempt.RwScore is int rws && attempt.MathScore is int ms)
            db.ScoreHistories.Add(new ScoreHistory { StudentId = attempt.StudentId, AttemptId = attempt.Id, TestType = attempt.Test.Type, TotalScore = total, RwScore = rws, MathScore = ms });
        await db.SaveChangesAsync();

        await stats.RecordAsync(db, attempt.StudentId, qs.Select(q => (q.QuestionId, q.Question.SkillId, q.IsCorrect == true, q.TimeSpentSeconds, q.Answer)), $"attempt:{attempt.Id}");
        await db.SaveChangesAsync();

        if (attempt.AssignmentId is int aid)
        {
            // homework of kind Test: close the submission
            var hw = await db.HomeworkSubmissions.Include(s => s.Homework).FirstOrDefaultAsync(s => s.StudentId == attempt.StudentId && s.Homework.TestId == attempt.TestId && s.Status != SubmissionStatus.Reviewed);
            if (hw is not null) { hw.Status = SubmissionStatus.Submitted; hw.SubmittedAt = DateTime.UtcNow; hw.AttemptId = attempt.Id; hw.Score = attempt.RwRaw + attempt.MathRaw; hw.MaxScore = attempt.RwMax + attempt.MathMax; await db.SaveChangesAsync(); }
        }
        await audit.LogAsync(null, "AttemptScored", "TestAttempt", attempt.Id, $"total {attempt.TotalScore} rw {attempt.RwScore} math {attempt.MathScore}");
        _ = notifications.TestCompletedAsync(attempt.Id);
    }

    /// <summary>Admin: recompute raw and scaled scores with the current tables (audited).</summary>
    public async Task<string?> RecalculateAsync(int attemptId, int adminUserId, string reason)
    {
        await using var db = await factory.CreateDbContextAsync();
        var attempt = await db.TestAttempts.Include(a => a.Test).Include(a => a.Modules).FirstOrDefaultAsync(a => a.Id == attemptId);
        if (attempt is null) return "Urinish topilmadi.";
        if (attempt.Status != AttemptStatus.Completed) return "Faqat yakunlangan urinishni qayta hisoblash mumkin.";
        var old = (attempt.TotalScore, attempt.RwScore, attempt.MathScore);
        scoring.Invalidate();
        var qs = await db.AttemptQuestions.Include(q => q.QuestionVersion).Where(q => q.AttemptId == attempt.Id).ToListAsync();
        foreach (var q in qs) q.IsCorrect = q.Answer is null ? false : ScoringService.IsCorrect(q.QuestionVersion, q.Answer);
        var rw = qs.Where(q => attempt.Modules.First(m => m.Id == q.AttemptModuleId).Section == Section.RW).ToList();
        var math = qs.Where(q => attempt.Modules.First(m => m.Id == q.AttemptModuleId).Section == Section.Math).ToList();
        attempt.RwRaw = rw.Count(q => q.IsCorrect == true); attempt.MathRaw = math.Count(q => q.IsCorrect == true);
        if (attempt.Test.Type is TestType.Diagnostic or TestType.FullMock or TestType.SectionTest)
        {
            attempt.RwScore = rw.Count > 0 ? await scoring.ScaledAsync(attempt.ScoringVersion, Section.RW, attempt.RwRoute ?? ModuleRoute.Standard, attempt.RwRaw, attempt.RwMax) : null;
            attempt.MathScore = math.Count > 0 ? await scoring.ScaledAsync(attempt.ScoringVersion, Section.Math, attempt.MathRoute ?? ModuleRoute.Standard, attempt.MathRaw, attempt.MathMax) : null;
            attempt.TotalScore = attempt.RwScore is null || attempt.MathScore is null ? null : attempt.RwScore + attempt.MathScore;
        }
        var history = await db.ScoreHistories.FirstOrDefaultAsync(h => h.AttemptId == attemptId);
        if (history is not null && attempt.TotalScore is int t) { history.TotalScore = t; history.RwScore = attempt.RwScore ?? 0; history.MathScore = attempt.MathScore ?? 0; }
        attempt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync(adminUserId, "ScoreRecalculated", "TestAttempt", attemptId, $"old {old.TotalScore}/{old.RwScore}/{old.MathScore} → new {attempt.TotalScore}/{attempt.RwScore}/{attempt.MathScore}; reason: {reason}");
        return null;
    }

    // ---------------------------------------------------------------- result / review

    public async Task<TestAttempt?> ResultAsync(int attemptId, int? studentId, bool staff = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var a = await db.TestAttempts.AsNoTracking().Include(x => x.Test).Include(x => x.Modules.OrderBy(m => m.Order)).Include(x => x.Student).ThenInclude(s => s.User)
            .FirstOrDefaultAsync(x => x.Id == attemptId && (staff || x.StudentId == studentId));
        return a;
    }

    public async Task<List<AttemptQuestion>> ReviewAsync(int attemptId, int? studentId, bool staff = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var ok = await db.TestAttempts.AnyAsync(a => a.Id == attemptId && (staff || a.StudentId == studentId) && (a.Status == AttemptStatus.Completed || staff));
        if (!ok) return [];
        return await db.AttemptQuestions.AsNoTracking().Include(q => q.QuestionVersion).Include(q => q.Question).ThenInclude(q => q.Skill).ThenInclude(s => s.Domain).Include(q => q.Module)
            .Where(q => q.AttemptId == attemptId).OrderBy(q => q.Module.Order).ThenBy(q => q.Order).ToListAsync();
    }

    public async Task<int?> PreviousTotalAsync(int studentId, int attemptId, DateTime before)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ScoreHistories.Where(s => s.StudentId == studentId && s.AttemptId != attemptId && s.CreatedAt < before).OrderByDescending(s => s.CreatedAt).Select(s => (int?)s.TotalScore).FirstOrDefaultAsync();
    }

    /// <summary>Background sweep: finishes modules/breaks whose deadline passed while nobody was connected.</summary>
    public async Task SweepAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var ids = await db.TestAttempts.Where(a => (a.Status == AttemptStatus.InProgress || a.Status == AttemptStatus.Break) && a.UpdatedAt < now.AddSeconds(-5)).Select(a => a.Id).Take(200).ToListAsync(ct);
        foreach (var id in ids)
        {
            try
            {
                var attempt = await db.TestAttempts.Include(a => a.Test).Include(a => a.Modules.OrderBy(m => m.Order)).FirstAsync(a => a.Id == id, ct);
                await EnforceDeadlinesAsync(db, attempt);
                // abandon sessions nobody touched for 12 hours (e.g. closed laptop during a break)
                if (attempt.Status is AttemptStatus.InProgress or AttemptStatus.Break && attempt.UpdatedAt < now.AddHours(-12))
                {
                    var current = attempt.Modules.FirstOrDefault(m => m.Order == attempt.CurrentModuleOrder);
                    if (current is not null && current.Status == AttemptModuleStatus.InProgress) await FinishModuleAsync(db, attempt, current, auto: true);
                    else { attempt.Status = AttemptStatus.Abandoned; attempt.CompletedAt = now; await db.SaveChangesAsync(ct); }
                }
            }
            catch (Exception ex) { logger.LogWarning(ex, "Sweep failed for attempt {Id}", id); }
            db.ChangeTracker.Clear();
        }
    }
}

public class AttemptWatchdog(TestEngineService engine, PracticeService practice, ILogger<AttemptWatchdog> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), ct);
        while (!ct.IsCancellationRequested)
        {
            try { await engine.SweepAsync(ct); await practice.SweepAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning(ex, "Watchdog sweep error"); }
            try { await Task.Delay(TimeSpan.FromSeconds(10), ct); } catch (OperationCanceledException) { }
        }
    }
}

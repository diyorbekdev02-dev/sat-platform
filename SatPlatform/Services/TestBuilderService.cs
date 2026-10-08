using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services;

public record TestRow(int Id, string Title, TestType Type, bool IsAdaptive, TestStatus Status, int QuestionCount, int ModuleCount, DateTime UpdatedAt, int AttemptCount, bool IsPublic);

public class AutoGenerateRule
{
    public Dictionary<int, int> SkillPercents { get; set; } = new();      // skillId → % (optional; empty = any skill of the section)
    public Dictionary<int, int> DomainPercents { get; set; } = new();     // domainId → %
    public int EasyPercent { get; set; } = 20;
    public int MediumPercent { get; set; } = 50;
    public int HardPercent { get; set; } = 30;
    public int Count { get; set; } = 22;
    public bool Replace { get; set; } = true;
}

/// <summary>Admin side of tests: structure, questions, validation, publishing, assignments.</summary>
public class TestBuilderService(IDbContextFactory<AppDbContext> factory, ScoringService scoring, SettingsService settings, AuditService audit, NotificationService notifications)
{
    public async Task<List<TestRow>> ListAsync(TestStatus? status = null, TestType? type = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.Tests.AsNoTracking().AsQueryable();
        if (status is not null) q = q.Where(t => t.Status == status);
        if (type is not null) q = q.Where(t => t.Type == type);
        return await q.OrderByDescending(t => t.UpdatedAt)
            .Select(t => new TestRow(t.Id, t.Title, t.Type, t.IsAdaptive, t.Status, t.Modules.Sum(m => m.Questions.Count), t.Modules.Count, t.UpdatedAt,
                db.TestAttempts.Count(a => a.TestId == t.Id && !a.IsPreview), t.IsPublic))
            .ToListAsync();
    }

    public async Task<Test?> GetAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Tests.AsNoTracking()
            .Include(t => t.Modules.OrderBy(m => m.SortOrder)).ThenInclude(m => m.Questions.OrderBy(q => q.Order)).ThenInclude(q => q.Question).ThenInclude(q => q.Skill)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    /// <summary>Creates the test with its module skeleton according to type/adaptive flags.</summary>
    public async Task<int> CreateAsync(string title, TestType type, bool adaptive, Section? section, string? description, int userId, int? moduleSeconds = null)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new AppException("Test nomini kiriting.");
        await using var db = await factory.CreateDbContextAsync();
        var rwSec = moduleSeconds ?? await settings.GetIntAsync(SettingKeys.RwModuleSeconds);
        var mathSec = moduleSeconds ?? await settings.GetIntAsync(SettingKeys.MathModuleSeconds);
        var test = new Test
        {
            Title = title.Trim(), Type = type, Description = description, CreatedByUserId = userId,
            IsAdaptive = adaptive && type is TestType.Diagnostic or TestType.FullMock or TestType.SectionTest,
            ScoringVersion = await settings.GetAsync(SettingKeys.DefaultScoringVersion),
            BreakSeconds = await settings.GetIntAsync(SettingKeys.BreakSeconds),
            IsPublic = type == TestType.Diagnostic
        };
        var order = 0;
        void AddSection(Section s)
        {
            var secs = s == Section.RW ? rwSec : mathSec;
            test.Modules.Add(new TestModule { Section = s, ModuleNumber = 1, Route = ModuleRoute.Standard, TimeLimitSeconds = secs, SortOrder = order++ });
            if (test.IsAdaptive)
            {
                test.Modules.Add(new TestModule { Section = s, ModuleNumber = 2, Route = ModuleRoute.Easy, TimeLimitSeconds = secs, SortOrder = order++ });
                test.Modules.Add(new TestModule { Section = s, ModuleNumber = 2, Route = ModuleRoute.Hard, TimeLimitSeconds = secs, SortOrder = order++ });
            }
            else if (type is TestType.Diagnostic or TestType.FullMock or TestType.SectionTest)
                test.Modules.Add(new TestModule { Section = s, ModuleNumber = 2, Route = ModuleRoute.Standard, TimeLimitSeconds = secs, SortOrder = order++ });
        }
        switch (type)
        {
            case TestType.Diagnostic or TestType.FullMock: AddSection(Section.RW); AddSection(Section.Math); break;
            case TestType.SectionTest: AddSection(section ?? Section.RW); break;
            default:
                var s = section ?? Section.Math;
                test.Modules.Add(new TestModule { Section = s, ModuleNumber = 1, Route = ModuleRoute.Standard, TimeLimitSeconds = s == Section.RW ? rwSec : mathSec, SortOrder = 0 });
                break;
        }
        db.Tests.Add(test);
        await db.SaveChangesAsync();
        await audit.LogAsync(userId, "TestCreated", "Test", test.Id, title);
        return test.Id;
    }

    public async Task UpdateAsync(int id, string title, string? description, int breakSeconds, bool shuffle, bool isPublic, string scoringVersion, Dictionary<int, int> moduleSeconds, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var t = await db.Tests.Include(x => x.Modules).FirstOrDefaultAsync(x => x.Id == id) ?? throw new AppException("Test topilmadi.");
        if (string.IsNullOrWhiteSpace(title)) throw new AppException("Test nomini kiriting.");
        t.Title = title.Trim(); t.Description = description; t.BreakSeconds = Math.Max(0, breakSeconds); t.ShuffleQuestions = shuffle; t.IsPublic = isPublic; t.ScoringVersion = scoringVersion; t.UpdatedAt = DateTime.UtcNow;
        foreach (var m in t.Modules) if (moduleSeconds.TryGetValue(m.Id, out var sec) && sec > 0) m.TimeLimitSeconds = sec;
        await db.SaveChangesAsync();
        await audit.LogAsync(userId, "TestUpdated", "Test", id);
    }

    public async Task<string?> AddQuestionsAsync(int moduleId, IEnumerable<int> questionIds, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var module = await db.TestModules.Include(m => m.Questions).Include(m => m.Test).FirstOrDefaultAsync(m => m.Id == moduleId) ?? throw new AppException("Modul topilmadi.");
        var existing = module.Questions.Select(q => q.QuestionId).ToHashSet();
        var order = module.Questions.Count == 0 ? 0 : module.Questions.Max(q => q.Order) + 1;
        var skipped = 0;
        var ids = questionIds.Distinct().ToList();
        var valid = await db.Questions.Where(q => ids.Contains(q.Id) && q.Section == module.Section && q.Status != QuestionStatus.Archived).Select(q => q.Id).ToListAsync();
        foreach (var id in ids)
        {
            if (existing.Contains(id)) { skipped++; continue; }
            if (!valid.Contains(id)) { skipped++; continue; }
            module.Questions.Add(new TestQuestion { QuestionId = id, Order = order++ });
            existing.Add(id);
        }
        module.Test.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return skipped > 0 ? $"{skipped} ta savol qo'shilmadi (allaqachon modulda, boshqa bo'limdan yoki arxivlangan)." : null;
    }

    public async Task RemoveQuestionAsync(int moduleId, int questionId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var tq = await db.TestQuestions.Include(q => q.TestModule).FirstOrDefaultAsync(q => q.TestModuleId == moduleId && q.QuestionId == questionId);
        if (tq is null) return;
        db.TestQuestions.Remove(tq);
        await db.SaveChangesAsync();
        await Renumber(db, moduleId);
    }

    public async Task MoveQuestionAsync(int moduleId, int questionId, int delta)
    {
        await using var db = await factory.CreateDbContextAsync();
        var list = await db.TestQuestions.Where(q => q.TestModuleId == moduleId).OrderBy(q => q.Order).ToListAsync();
        var i = list.FindIndex(q => q.QuestionId == questionId);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= list.Count) return;
        (list[i], list[j]) = (list[j], list[i]);
        for (var k = 0; k < list.Count; k++) list[k].Order = k;
        await db.SaveChangesAsync();
    }

    public async Task ReorderAsync(int moduleId, List<int> questionIdsInOrder)
    {
        await using var db = await factory.CreateDbContextAsync();
        var list = await db.TestQuestions.Where(q => q.TestModuleId == moduleId).ToListAsync();
        var order = 0;
        foreach (var id in questionIdsInOrder) { var q = list.FirstOrDefault(x => x.QuestionId == id); if (q is not null) q.Order = order++; }
        foreach (var q in list.Where(x => !questionIdsInOrder.Contains(x.QuestionId)).OrderBy(x => x.Order)) q.Order = order++;
        await db.SaveChangesAsync();
    }

    private static async Task Renumber(AppDbContext db, int moduleId)
    {
        var list = await db.TestQuestions.Where(q => q.TestModuleId == moduleId).OrderBy(q => q.Order).ToListAsync();
        for (var k = 0; k < list.Count; k++) list[k].Order = k;
        await db.SaveChangesAsync();
    }

    /// <summary>Fills a module from the bank following percentage rules. Returns an error when the bank is too small.</summary>
    public async Task<string?> AutoGenerateAsync(int moduleId, AutoGenerateRule rule, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var module = await db.TestModules.Include(m => m.Questions).Include(m => m.Test).ThenInclude(t => t.Modules).ThenInclude(m => m.Questions).FirstOrDefaultAsync(m => m.Id == moduleId) ?? throw new AppException("Modul topilmadi.");
        if (rule.Count <= 0) return "Savollar soni noto'g'ri.";
        if (rule.EasyPercent + rule.MediumPercent + rule.HardPercent != 100) return "Qiyinlik foizlari yig'indisi 100 bo'lishi kerak.";

        var usedInTest = module.Test.Modules.Where(m => m.Id != moduleId).SelectMany(m => m.Questions).Select(q => q.QuestionId).ToHashSet();
        var current = rule.Replace ? [] : module.Questions.Select(q => q.QuestionId).ToHashSet();

        var pool = await db.Questions.AsNoTracking().Where(q => q.Section == module.Section && q.Status == QuestionStatus.Active).Select(q => new { q.Id, q.SkillId, q.DomainId, q.Difficulty }).ToListAsync();
        pool = pool.Where(q => !usedInTest.Contains(q.Id) && !current.Contains(q.Id)).ToList();

        // difficulty bias by route: easy module → more easy, hard module → more hard
        var want = new Dictionary<Difficulty, int>
        {
            [Difficulty.Easy] = (int)Math.Round(rule.Count * rule.EasyPercent / 100.0),
            [Difficulty.Medium] = (int)Math.Round(rule.Count * rule.MediumPercent / 100.0),
        };
        want[Difficulty.Hard] = rule.Count - want[Difficulty.Easy] - want[Difficulty.Medium];

        // group targets (skills first, otherwise domains, otherwise free)
        var groupKey = rule.SkillPercents.Count > 0 ? "skill" : rule.DomainPercents.Count > 0 ? "domain" : "none";
        var percents = groupKey == "skill" ? rule.SkillPercents : rule.DomainPercents;
        var groupTargets = new Dictionary<int, int>();
        if (groupKey != "none")
        {
            if (percents.Values.Sum() != 100) return "Mavzu foizlari yig'indisi 100 bo'lishi kerak.";
            var assigned = 0;
            foreach (var (k, p) in percents) { var n = (int)Math.Floor(rule.Count * p / 100.0); groupTargets[k] = n; assigned += n; }
            foreach (var k in percents.OrderByDescending(p => p.Value).Select(p => p.Key)) { if (assigned >= rule.Count) break; groupTargets[k]++; assigned++; }
        }

        var rnd = new Random();
        var picked = new List<int>();
        int GroupOf(dynamic q) => groupKey == "skill" ? q.SkillId : q.DomainId;
        var remainingDiff = new Dictionary<Difficulty, int>(want);

        IEnumerable<int> groups = groupKey == "none" ? [0] : groupTargets.Keys;
        foreach (var g in groups)
        {
            var need = groupKey == "none" ? rule.Count : groupTargets[g];
            var candidates = pool.Where(q => groupKey == "none" || GroupOf(q) == g).OrderBy(_ => rnd.Next()).ToList();
            // take by difficulty quota, then fill with whatever is left in the group
            foreach (var diff in new[] { Difficulty.Hard, Difficulty.Medium, Difficulty.Easy })
            {
                var share = (int)Math.Round(need * (double)want[diff] / rule.Count);
                foreach (var q in candidates.Where(q => q.Difficulty == diff && !picked.Contains(q.Id)).Take(Math.Min(share, remainingDiff[diff])))
                { picked.Add(q.Id); remainingDiff[diff]--; }
            }
            var still = need - candidates.Count(q => picked.Contains(q.Id));
            foreach (var q in candidates.Where(q => !picked.Contains(q.Id)).Take(Math.Max(0, still))) picked.Add(q.Id);
        }
        // final top-up from the whole pool
        foreach (var q in pool.Where(q => !picked.Contains(q.Id)).OrderBy(_ => rnd.Next()).Take(Math.Max(0, rule.Count - picked.Count))) picked.Add(q.Id);

        if (picked.Count < rule.Count)
            return $"Bu qoidalarga mos savollar yetarli emas.\nMavjud: {picked.Count}\nKerak: {rule.Count}";

        if (rule.Replace) { db.TestQuestions.RemoveRange(module.Questions); module.Questions.Clear(); }
        var order = module.Questions.Count == 0 ? 0 : module.Questions.Max(q => q.Order) + 1;
        foreach (var id in picked.OrderBy(_ => rnd.Next())) module.Questions.Add(new TestQuestion { QuestionId = id, Order = order++ });
        module.Test.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync(userId, "TestAutoGenerated", "TestModule", moduleId, $"{picked.Count} questions");
        return null;
    }

    // ---------------------------------------------------------------- validation / publish

    public async Task<List<string>> ValidateAsync(int testId)
    {
        var t = await GetAsync(testId);
        if (t is null) return ["Test topilmadi."];
        var errors = new List<string>();
        if (t.Modules.Count == 0) errors.Add("Testda modul yo'q.");
        var rwRule = await scoring.RuleAsync(Section.RW);
        var mathRule = await scoring.RuleAsync(Section.Math);
        foreach (var m in t.Modules)
        {
            if (m.Questions.Count == 0) { errors.Add($"{m.Title}: savollar yo'q."); continue; }
            if (m.TimeLimitSeconds <= 0) errors.Add($"{m.Title}: vaqt belgilanmagan.");
            var incomplete = m.Questions.Where(q => q.Question.Status != QuestionStatus.Active).ToList();
            if (incomplete.Count > 0) errors.Add($"{m.Title}: {incomplete.Count} ta savol faol emas (#{string.Join(", #", incomplete.Take(5).Select(q => q.QuestionId))}).");
            var wrongSection = m.Questions.Count(q => q.Question.Section != m.Section);
            if (wrongSection > 0) errors.Add($"{m.Title}: {wrongSection} ta savol boshqa bo'limdan.");
            var dup = m.Questions.GroupBy(q => q.QuestionId).Count(g => g.Count() > 1);
            if (dup > 0) errors.Add($"{m.Title}: takrorlangan savollar bor.");
        }
        if (t.IsAdaptive)
        {
            foreach (var s in t.Modules.Select(m => m.Section).Distinct())
            {
                var m1 = t.Modules.FirstOrDefault(m => m.Section == s && m.ModuleNumber == 1);
                var easy = t.Modules.FirstOrDefault(m => m.Section == s && m.ModuleNumber == 2 && m.Route == ModuleRoute.Easy);
                var hard = t.Modules.FirstOrDefault(m => m.Section == s && m.ModuleNumber == 2 && m.Route == ModuleRoute.Hard);
                if (m1 is null || easy is null || hard is null) errors.Add($"{Labels.Section(s)}: adaptiv test uchun Module 1, Module 2 Easy va Module 2 Hard bo'lishi kerak.");
                var rule = s == Section.RW ? rwRule : mathRule;
                if (m1 is not null && m1.Questions.Count != rule.QuestionCount) errors.Add($"{m1.Title}: {m1.Questions.Count} ta savol, adaptiv qoida bo'yicha {rule.QuestionCount} ta kutilmoqda (routing cutoff shunga moslab o'lchanadi).");
            }
        }
        var crossModuleDup = t.Modules.SelectMany(m => m.Questions.Select(q => new { q.QuestionId, m.Section, m.ModuleNumber, m.Route }))
            .GroupBy(x => x.QuestionId).Where(g => g.Select(x => (x.ModuleNumber, x.Route)).Distinct().Count() > 1 && g.Any(x => x.ModuleNumber == 1)).Count();
        if (crossModuleDup > 0) errors.Add($"{crossModuleDup} ta savol Module 1 va Module 2 da takrorlanadi.");
        if (string.IsNullOrWhiteSpace(t.ScoringVersion)) errors.Add("Scoring version tanlanmagan.");
        return errors;
    }

    public async Task<List<string>> PublishAsync(int testId, int userId)
    {
        var errors = await ValidateAsync(testId);
        if (errors.Count > 0) return errors;
        await using var db = await factory.CreateDbContextAsync();
        var t = await db.Tests.FindAsync(testId) ?? throw new AppException("Test topilmadi.");
        t.Status = TestStatus.Published; t.PublishedAt = DateTime.UtcNow; t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync(userId, "TestPublished", "Test", testId, t.Title);
        return [];
    }

    public async Task SetStatusAsync(int testId, TestStatus status, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var t = await db.Tests.FindAsync(testId) ?? throw new AppException("Test topilmadi.");
        t.Status = status; t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync(userId, status == TestStatus.Archived ? "TestArchived" : "TestStatusChanged", "Test", testId, status.ToString());
    }

    public async Task<string?> DeleteAsync(int testId, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (await db.TestAttempts.AnyAsync(a => a.TestId == testId && !a.IsPreview)) return "Bu test ishlangan — o'chirib bo'lmaydi, arxivlang.";
        var t = await db.Tests.FindAsync(testId);
        if (t is null) return null;
        await db.TestAttempts.Where(a => a.TestId == testId).ExecuteDeleteAsync();
        db.Tests.Remove(t);
        await db.SaveChangesAsync();
        await audit.LogAsync(userId, "TestDeleted", "Test", testId, t.Title);
        return null;
    }

    public async Task<int> DuplicateAsync(int testId, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var t = await db.Tests.AsNoTracking().Include(x => x.Modules).ThenInclude(m => m.Questions).FirstOrDefaultAsync(x => x.Id == testId) ?? throw new AppException("Test topilmadi.");
        var copy = new Test
        {
            Title = t.Title + " (nusxa)", Description = t.Description, Type = t.Type, IsAdaptive = t.IsAdaptive, ScoringVersion = t.ScoringVersion, BreakSeconds = t.BreakSeconds,
            ShuffleQuestions = t.ShuffleQuestions, CreatedByUserId = userId,
            Modules = t.Modules.Select(m => new TestModule { Section = m.Section, ModuleNumber = m.ModuleNumber, Route = m.Route, TimeLimitSeconds = m.TimeLimitSeconds, SortOrder = m.SortOrder,
                Questions = m.Questions.Select(q => new TestQuestion { QuestionId = q.QuestionId, Order = q.Order }).ToList() }).ToList()
        };
        db.Tests.Add(copy);
        await db.SaveChangesAsync();
        return copy.Id;
    }

    // ---------------------------------------------------------------- assignments

    public async Task<int> AssignAsync(int testId, int? studentId, int? groupId, DateTime startAt, DateTime? endAt, int? attemptLimit, int userId)
    {
        if (studentId is null && groupId is null) throw new AppException("Guruh yoki o'quvchini tanlang.");
        if (endAt is not null && endAt <= startAt) throw new AppException("Tugash vaqti boshlanishdan keyin bo'lishi kerak.");
        await using var db = await factory.CreateDbContextAsync();
        var test = await db.Tests.FindAsync(testId) ?? throw new AppException("Test topilmadi.");
        if (test.Status != TestStatus.Published) throw new AppException("Faqat chop etilgan testni tayinlash mumkin.");
        var a = new TestAssignment { TestId = testId, StudentId = studentId, GroupId = groupId, StartAt = startAt, EndAt = endAt, AttemptLimit = attemptLimit, AssignedByUserId = userId };
        db.TestAssignments.Add(a);
        await db.SaveChangesAsync();
        await audit.LogAsync(userId, "TestAssigned", "TestAssignment", a.Id, $"test {testId} → {(studentId is not null ? "student " + studentId : "group " + groupId)}");
        _ = notifications.TestAssignedAsync(a.Id);
        return a.Id;
    }

    public async Task<List<TestAssignment>> AssignmentsAsync(int? testId = null, int? groupId = null, int? byUserId = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.TestAssignments.AsNoTracking().Include(a => a.Test).Include(a => a.Group).Include(a => a.Student).ThenInclude(s => s!.User).AsQueryable();
        if (testId is not null) q = q.Where(a => a.TestId == testId);
        if (groupId is not null) q = q.Where(a => a.GroupId == groupId);
        if (byUserId is not null) q = q.Where(a => a.AssignedByUserId == byUserId);
        return await q.OrderByDescending(a => a.CreatedAt).ToListAsync();
    }

    public async Task DeleteAssignmentAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.TestAssignments.Where(a => a.Id == id).ExecuteDeleteAsync();
        await audit.LogAsync(userId, "TestAssignmentDeleted", "TestAssignment", id);
    }
}

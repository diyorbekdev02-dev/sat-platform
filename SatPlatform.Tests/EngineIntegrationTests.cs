using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SatPlatform.Data;
using SatPlatform.Services;
using SatPlatform.Services.Telegram;
using Xunit;

namespace SatPlatform.Tests;

/// <summary>
/// Runs the real services against a dedicated PostgreSQL database (SAT_TEST_DB connection string, default: local docker
/// container, database "satplatform_test"). Skipped when the database is unreachable.
/// </summary>
public class EngineFixture : IAsyncLifetime
{
    public ServiceProvider Services { get; private set; } = null!;
    public bool Available { get; private set; }
    public int StudentId { get; private set; }
    public int AdminId { get; private set; }

    public async Task InitializeAsync()
    {
        var cs = Environment.GetEnvironmentVariable("SAT_TEST_DB") ?? "Host=localhost;Port=5433;Database=satplatform_test;Username=postgres;Password=postgres";
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:Demo"] = "true", ["Seed:AdminPassword"] = "Admin123!", ["Seed:DemoPassword"] = "Demo123!" }).Build();
        var sc = new ServiceCollection();
        sc.AddSingleton<IConfiguration>(config);
        sc.AddSingleton<IHostEnvironment>(new TestEnv());
        sc.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        sc.AddDbContextFactory<AppDbContext>(o => o.UseNpgsql(cs));
        sc.AddSingleton<SettingsService>(); sc.AddSingleton<AuditService>(); sc.AddSingleton<ScoringService>(); sc.AddSingleton<ContentService>();
        sc.AddSingleton<StatisticsService>(); sc.AddSingleton<TestBuilderService>(); sc.AddSingleton<TestEngineService>(); sc.AddSingleton<PracticeService>();
        sc.AddSingleton<AnalyticsService>(); sc.AddSingleton<AcademyService>(); sc.AddSingleton<HomeworkService>(); sc.AddSingleton<NotificationService>();
        sc.AddSingleton<SeedService>(); sc.AddSingleton<AuthService>();
        sc.Configure<TelegramOptions>(_ => { });
        sc.AddHttpClient<TelegramBotClient>();
        Services = sc.BuildServiceProvider();
        try
        {
            var factory = Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using (var db = await factory.CreateDbContextAsync())
            {
                await db.Database.EnsureDeletedAsync();
                await db.Database.MigrateAsync();
            }
            await Services.GetRequiredService<SeedService>().RunAsync();
            await using (var db = await factory.CreateDbContextAsync())
            {
                StudentId = await db.Students.Where(s => s.User.Phone == "+998902000000").Select(s => s.Id).FirstAsync();
                AdminId = await db.Users.Where(u => u.Role == UserRole.SuperAdmin).Select(u => u.Id).FirstAsync();
            }
            Available = true;
        }
        catch (Exception ex) { Console.WriteLine("Integration DB unavailable: " + ex.Message); Available = false; }
    }

    public Task DisposeAsync() { Services?.Dispose(); return Task.CompletedTask; }

    private class TestEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "SatPlatform.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}

[CollectionDefinition("engine")]
public class EngineCollection : ICollectionFixture<EngineFixture>;

[Collection("engine")]
public class EngineIntegrationTests(EngineFixture f)
{
    private T Get<T>() where T : notnull => f.Services.GetRequiredService<T>();

    private async Task<int> FullMockIdAsync()
    {
        var db = await Get<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        return await db.Tests.Where(t => t.Type == TestType.FullMock && t.Status == TestStatus.Published).Select(t => t.Id).FirstAsync();
    }

    private static string CorrectAnswer(QuestionVersion v) => v.Type == QuestionType.SPR ? v.CorrectAnswer!.Split('|')[0] : ScoringService.Options(v).First(o => o.IsCorrect).Key;

    [Fact]
    public async Task Perfect_run_routes_to_hard_and_scores_1600()
    {
        if (!f.Available) return;
        var engine = Get<TestEngineService>();
        await CleanupAsync();
        var attempt = await engine.StartAsync(f.StudentId, await FullMockIdAsync(), null);
        Assert.Equal(AttemptStatus.InProgress, attempt.Status);
        Assert.Equal(4, attempt.Modules.Count);

        // R&W M1
        var state = (await engine.StateAsync(attempt.Id, f.StudentId))!;
        Assert.Equal(Section.RW, state.CurrentModule!.Section);
        Assert.Equal(27, state.CurrentQuestions.Count);
        Assert.True(state.SecondsLeft is > 1900 and <= 1920);
        foreach (var q in state.CurrentQuestions) Assert.Null(await engine.AnswerAsync(attempt.Id, f.StudentId, q.Id, CorrectAnswer(q.QuestionVersion), 30));
        Assert.Null(await engine.FinishModuleAsync(attempt.Id, f.StudentId));

        // R&W M2 must be the Hard route
        state = (await engine.StateAsync(attempt.Id, f.StudentId))!;
        Assert.Equal(AttemptStatus.InProgress, state.Attempt.Status);
        Assert.Equal(2, state.CurrentModule!.ModuleNumber);
        Assert.Equal(ModuleRoute.Hard, state.CurrentModule.Route);
        Assert.Equal(27, state.CurrentQuestions.Count);
        foreach (var q in state.CurrentQuestions) await engine.AnswerAsync(attempt.Id, f.StudentId, q.Id, CorrectAnswer(q.QuestionVersion), 20);
        await engine.FinishModuleAsync(attempt.Id, f.StudentId);

        // break between sections
        state = (await engine.StateAsync(attempt.Id, f.StudentId))!;
        Assert.Equal(AttemptStatus.Break, state.Attempt.Status);
        Assert.True(state.BreakSecondsLeft is > 500 and <= 600);
        await engine.ResumeFromBreakAsync(attempt.Id, f.StudentId);

        // Math M1 + M2 Hard
        for (var m = 0; m < 2; m++)
        {
            state = (await engine.StateAsync(attempt.Id, f.StudentId))!;
            Assert.Equal(Section.Math, state.CurrentModule!.Section);
            Assert.Equal(22, state.CurrentQuestions.Count);
            if (m == 1) Assert.Equal(ModuleRoute.Hard, state.CurrentModule.Route);
            foreach (var q in state.CurrentQuestions) await engine.AnswerAsync(attempt.Id, f.StudentId, q.Id, CorrectAnswer(q.QuestionVersion), 25);
            await engine.FinishModuleAsync(attempt.Id, f.StudentId);
        }

        var result = (await engine.ResultAsync(attempt.Id, f.StudentId))!;
        Assert.Equal(AttemptStatus.Completed, result.Status);
        Assert.Equal(54, result.RwRaw); Assert.Equal(44, result.MathRaw);
        Assert.Equal(800, result.RwScore); Assert.Equal(800, result.MathScore); Assert.Equal(1600, result.TotalScore);

        // statistics, score history and no mistakes
        var db = await Get<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        Assert.True(await db.ScoreHistories.AnyAsync(s => s.AttemptId == attempt.Id && s.TotalScore == 1600));
        Assert.Equal(98, await db.StudentSkillStatistics.Where(s => s.StudentId == f.StudentId).SumAsync(s => s.AttemptedCount));
        Assert.False(await db.StudentMistakes.AnyAsync(m => m.StudentId == f.StudentId && !m.IsResolved));
        var dash = await Get<AnalyticsService>().DashboardAsync(f.StudentId);
        Assert.Equal(1600, dash.CurrentScore);
    }

    [Fact]
    public async Task Blank_module_1_routes_to_easy_and_unanswered_count_as_wrong()
    {
        if (!f.Available) return;
        var engine = Get<TestEngineService>();
        await CleanupAsync();
        var attempt = await engine.StartAsync(f.StudentId, await FullMockIdAsync(), null);
        await engine.FinishModuleAsync(attempt.Id, f.StudentId);
        var state = (await engine.StateAsync(attempt.Id, f.StudentId))!;
        Assert.Equal(ModuleRoute.Easy, state.CurrentModule!.Route);
        Assert.Equal(27, state.CurrentQuestions.Count);
        // cannot answer a question of a finished module
        var m1 = await (await Get<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()).AttemptQuestions.FirstAsync(q => q.AttemptId == attempt.Id && q.Module.ModuleNumber == 1);
        Assert.NotNull(await engine.AnswerAsync(attempt.Id, f.StudentId, m1.Id, "A", 1));
        await engine.AbandonAsync(attempt.Id, f.StudentId);
        Assert.Equal(AttemptStatus.Abandoned, (await engine.ResultAsync(attempt.Id, f.StudentId))!.Status);
    }

    [Fact]
    public async Task Expired_module_is_auto_submitted_by_the_sweep()
    {
        if (!f.Available) return;
        var engine = Get<TestEngineService>();
        await CleanupAsync();
        var attempt = await engine.StartAsync(f.StudentId, await FullMockIdAsync(), null);
        var factory = Get<IDbContextFactory<AppDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            // pretend every module deadline passed long ago
            await db.AttemptModules.Where(m => m.AttemptId == attempt.Id && m.Status == AttemptModuleStatus.InProgress).ExecuteUpdateAsync(s => s.SetProperty(m => m.DeadlineAt, DateTime.UtcNow.AddMinutes(-5)));
            await db.TestAttempts.Where(a => a.Id == attempt.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.UpdatedAt, DateTime.UtcNow.AddMinutes(-5)));
        }
        await engine.SweepAsync(CancellationToken.None);
        var state = (await engine.StateAsync(attempt.Id, f.StudentId))!;
        // module 1 closed → module 2 (easy) started automatically
        Assert.Equal(2, state.CurrentModule!.ModuleNumber);
        Assert.Equal(ModuleRoute.Easy, state.CurrentModule.Route);
        await engine.AbandonAsync(attempt.Id, f.StudentId);
    }

    [Fact]
    public async Task Another_student_cannot_touch_the_attempt()
    {
        if (!f.Available) return;
        var engine = Get<TestEngineService>();
        await CleanupAsync();
        var attempt = await engine.StartAsync(f.StudentId, await FullMockIdAsync(), null);
        var state = (await engine.StateAsync(attempt.Id, f.StudentId))!;
        var other = f.StudentId + 1;
        Assert.Null(await engine.StateAsync(attempt.Id, other));
        Assert.NotNull(await engine.AnswerAsync(attempt.Id, other, state.CurrentQuestions[0].Id, "A", 1));
        Assert.NotNull(await engine.FinishModuleAsync(attempt.Id, other));
        Assert.Empty(await engine.ReviewAsync(attempt.Id, other));
        await engine.AbandonAsync(attempt.Id, f.StudentId);
    }

    [Fact]
    public async Task Attempt_limit_is_enforced()
    {
        if (!f.Available) return;
        var engine = Get<TestEngineService>(); var builder = Get<TestBuilderService>();
        await CleanupAsync();
        var testId = await FullMockIdAsync();
        var assignmentId = await builder.AssignAsync(testId, f.StudentId, null, DateTime.UtcNow.AddMinutes(-1), null, 1, f.AdminId);
        var a1 = await engine.StartAsync(f.StudentId, testId, assignmentId);
        await engine.AbandonAsync(a1.Id, f.StudentId);
        var ex = await Assert.ThrowsAsync<AppException>(() => engine.StartAsync(f.StudentId, testId, assignmentId));
        Assert.Contains("Urinishlar", ex.Message);
    }

    [Fact]
    public async Task Question_edit_creates_a_new_version_and_old_attempts_keep_the_snapshot()
    {
        if (!f.Available) return;
        var engine = Get<TestEngineService>(); var content = Get<ContentService>();
        await CleanupAsync();
        var attempt = await engine.StartAsync(f.StudentId, await FullMockIdAsync(), null);
        var state = (await engine.StateAsync(attempt.Id, f.StudentId))!;
        var aq = state.CurrentQuestions[0];
        var q = (await content.GetAsync(aq.QuestionId))!;
        var edit = QuestionEdit.From(q);
        edit.Text = "<p>EDITED " + edit.Text + "</p>";
        var (_, errors) = await content.SaveAsync(edit, f.AdminId);
        Assert.Empty(errors);
        var updated = (await content.GetAsync(aq.QuestionId))!;
        Assert.Equal(q.CurrentVersion + 1, updated.CurrentVersion);
        var frozen = await content.GetVersionAsync(aq.QuestionVersionId);
        Assert.DoesNotContain("EDITED", frozen!.Text);
        await engine.AbandonAsync(attempt.Id, f.StudentId);
    }

    [Fact]
    public async Task Practice_records_mistakes_and_mistake_practice_uses_them()
    {
        if (!f.Available) return;
        var practice = Get<PracticeService>();
        await CleanupAsync();
        var session = await practice.StartAsync(f.StudentId, new PracticeRequest { Mode = PracticeMode.Custom, Section = Section.Math, Count = 6 });
        Assert.Equal(6, session.QuestionCount);
        var full = (await practice.GetAsync(session.Id, f.StudentId))!;
        var wrong = 0;
        foreach (var pq in full.Questions)
        {
            var answer = pq.QuestionVersion.Type == QuestionType.SPR ? "99999" : "A";
            var (correct, err) = await practice.AnswerAsync(session.Id, f.StudentId, pq.Id, answer, 10);
            Assert.Null(err);
            if (correct == false) wrong++;
        }
        await practice.FinishAsync(session.Id, f.StudentId);
        Assert.True(wrong > 0);
        Assert.Equal(wrong, await practice.UnresolvedMistakeCountAsync(f.StudentId));
        var mistakeSession = await practice.StartAsync(f.StudentId, new PracticeRequest { Mode = PracticeMode.Mistake, Count = 4 });
        Assert.True(mistakeSession.QuestionCount >= 1);
        await practice.FinishAsync(mistakeSession.Id, f.StudentId);
    }

    [Fact]
    public async Task Test_builder_validation_blocks_incomplete_tests()
    {
        if (!f.Available) return;
        var builder = Get<TestBuilderService>();
        var id = await builder.CreateAsync("Validation test", TestType.FullMock, true, null, null, f.AdminId);
        var errors = await builder.PublishAsync(id, f.AdminId);
        Assert.NotEmpty(errors);
        var t = (await builder.GetAsync(id))!;
        Assert.Equal(6, t.Modules.Count);
        foreach (var m in t.Modules) Assert.Null(await builder.AutoGenerateAsync(m.Id, new AutoGenerateRule { Count = m.Section == Section.RW ? 27 : 22 }, f.AdminId));
        Assert.Empty(await builder.PublishAsync(id, f.AdminId));
        // every module filled, no question used twice between module 1 and module 2
        t = (await builder.GetAsync(id))!;
        foreach (var s in new[] { Section.RW, Section.Math })
        {
            var m1 = t.Modules.First(m => m.Section == s && m.ModuleNumber == 1).Questions.Select(q => q.QuestionId).ToHashSet();
            foreach (var m2 in t.Modules.Where(m => m.Section == s && m.ModuleNumber == 2)) Assert.Empty(m2.Questions.Where(q => m1.Contains(q.QuestionId)));
        }
        Assert.NotNull(await builder.AutoGenerateAsync(t.Modules[0].Id, new AutoGenerateRule { Count = 500 }, f.AdminId));
    }

    [Fact]
    public async Task Teacher_scope_excludes_other_groups()
    {
        if (!f.Available) return;
        var academy = Get<AcademyService>();
        var db = await Get<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        var teacher = await db.Users.FirstAsync(u => u.Phone == "+998901111111");
        var other = await db.Users.FirstAsync(u => u.Phone == "+998901111112");
        var groups = await academy.GroupScopeAsync(teacher.Id, UserRole.Teacher);
        Assert.Single(groups);
        Assert.False((await academy.GroupScopeAsync(other.Id, UserRole.Teacher)).Intersect(groups).Any());
        var students = await academy.StudentScopeAsync(teacher.Id, UserRole.Teacher);
        Assert.Equal(6, students.Count);
        Assert.Contains(f.StudentId, students);
        Assert.False(await academy.CanSeeStudentAsync(other.Id, UserRole.Teacher, f.StudentId));
        Assert.Equal(await db.Students.CountAsync(), (await academy.StudentScopeAsync(f.AdminId, UserRole.SuperAdmin)).Count);
    }

    [Fact]
    public async Task Registration_and_parent_link_flow()
    {
        if (!f.Available) return;
        var auth = Get<AuthService>();
        var phone = "+99891" + Random.Shared.Next(1000000, 9999999);
        var req = await auth.RequestCodeAsync(phone, "register");
        Assert.NotNull(req.DevCode);
        var r = await auth.RegisterAsync(new AuthService.RegisterRequest(phone, req.DevCode!, "Test Student", "secret1", UserRole.Student, 1350, null));
        Assert.Null(r.Error);
        Assert.NotNull(r.User!.Student);
        var code = r.User.Student!.ParentCode;
        var parentPhone = "+99893" + Random.Shared.Next(1000000, 9999999);
        var preq = await auth.RequestCodeAsync(parentPhone, "register");
        var p = await auth.RegisterAsync(new AuthService.RegisterRequest(parentPhone, preq.DevCode!, "Test Parent", "secret1", UserRole.Parent, null, "WRONG1"));
        Assert.NotNull(p.Error);
        p = await auth.RegisterAsync(new AuthService.RegisterRequest(parentPhone, preq.DevCode!, "Test Parent", "secret1", UserRole.Parent, null, code));
        Assert.Null(p.Error);
        var children = await Get<AcademyService>().ChildrenOfAsync(p.User!.Id);
        Assert.Single(children);
        Assert.Equal(r.User.Student.Id, children[0].Id);
        // password login + lockout message on wrong password
        Assert.Null((await auth.LoginWithPasswordAsync(phone, "secret1")).Error);
        Assert.NotNull((await auth.LoginWithPasswordAsync(phone, "nope")).Error);
        // duplicate registration refused
        Assert.NotNull((await auth.RequestCodeAsync(phone, "register")).Error);
        // one-time login token works once
        var token = await auth.IssueLoginTokenAsync(r.User.Id);
        Assert.NotNull((await auth.ConsumeLoginTokenAsync(token)).User);
        Assert.Null((await auth.ConsumeLoginTokenAsync(token)).User);
    }

    private async Task CleanupAsync()
    {
        var factory = Get<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.TestAttempts.Where(a => a.StudentId == f.StudentId && (a.Status == AttemptStatus.InProgress || a.Status == AttemptStatus.Break)).ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, AttemptStatus.Abandoned));
        await db.PracticeSessions.Where(a => a.StudentId == f.StudentId && a.Status == PracticeStatus.InProgress).ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, PracticeStatus.Abandoned));
        await db.StudentMistakes.Where(m => m.StudentId == f.StudentId).ExecuteDeleteAsync();
        await db.StudentSkillStatistics.Where(m => m.StudentId == f.StudentId).ExecuteDeleteAsync();
        await db.ScoreHistories.Where(m => m.StudentId == f.StudentId).ExecuteDeleteAsync();
        await db.TestAssignments.Where(a => a.StudentId == f.StudentId).ExecuteDeleteAsync();
    }
}

using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services;

/// <summary>First-run data: SAT taxonomy, scoring table, adaptive rules, settings, the super admin and (optionally) demo content.</summary>
public class SeedService(IDbContextFactory<AppDbContext> factory, IConfiguration config, IHostEnvironment env, ContentService content, TestBuilderService builder, ILogger<SeedService> logger)
{
    public static readonly (Section Section, string Domain, string DomainCode, (string Code, string Name)[] Skills)[] Taxonomy =
    [
        (Section.RW, "Information and Ideas", "INFORMATION_AND_IDEAS", [("CENTRAL_IDEAS", "Central Ideas and Details"), ("INFERENCES", "Inferences"), ("COMMAND_OF_EVIDENCE", "Command of Evidence")]),
        (Section.RW, "Craft and Structure", "CRAFT_AND_STRUCTURE", [("WORDS_IN_CONTEXT", "Words in Context"), ("TEXT_STRUCTURE", "Text Structure and Purpose"), ("CROSS_TEXT", "Cross-Text Connections")]),
        (Section.RW, "Expression of Ideas", "EXPRESSION_OF_IDEAS", [("RHETORICAL_SYNTHESIS", "Rhetorical Synthesis"), ("TRANSITIONS", "Transitions")]),
        (Section.RW, "Standard English Conventions", "STANDARD_ENGLISH", [("BOUNDARIES", "Boundaries"), ("FORM_STRUCTURE_SENSE", "Form, Structure, and Sense")]),
        (Section.Math, "Algebra", "ALGEBRA", [("LINEAR_EQUATIONS", "Linear Equations in One Variable"), ("LINEAR_EQUATIONS_TWO", "Linear Equations in Two Variables"), ("LINEAR_FUNCTIONS", "Linear Functions"), ("SYSTEMS", "Systems of Two Linear Equations"), ("LINEAR_INEQUALITIES", "Linear Inequalities")]),
        (Section.Math, "Advanced Math", "ADVANCED_MATH", [("EQUIVALENT_EXPRESSIONS", "Equivalent Expressions"), ("NONLINEAR_EQUATIONS", "Nonlinear Equations and Systems"), ("NONLINEAR_FUNCTIONS", "Nonlinear Functions")]),
        (Section.Math, "Problem-Solving and Data Analysis", "PROBLEM_SOLVING", [("RATIOS_RATES", "Ratios, Rates, and Units"), ("PERCENTAGES", "Percentages"), ("ONE_VARIABLE_DATA", "One-Variable Data"), ("TWO_VARIABLE_DATA", "Two-Variable Data"), ("PROBABILITY", "Probability"), ("SAMPLE_STATISTICS", "Inference from Sample Statistics")]),
        (Section.Math, "Geometry and Trigonometry", "GEOMETRY_TRIG", [("AREA_VOLUME", "Area and Volume"), ("LINES_ANGLES_TRIANGLES", "Lines, Angles, and Triangles"), ("RIGHT_TRIANGLES_TRIG", "Right Triangles and Trigonometry"), ("CIRCLES", "Circles")]),
    ];

    public async Task RunAsync()
    {
        await using var db = await factory.CreateDbContextAsync();

        // taxonomy
        var sort = 0;
        foreach (var (section, domainName, domainCode, skills) in Taxonomy)
        {
            var d = await db.Domains.Include(x => x.Skills).FirstOrDefaultAsync(x => x.Code == domainCode);
            if (d is null) { d = new Domain { Section = section, Code = domainCode, Name = domainName, SortOrder = sort }; db.Domains.Add(d); }
            sort++;
            var ss = 0;
            foreach (var (code, name) in skills)
            {
                if (d.Skills.All(s => s.Code != code)) d.Skills.Add(new Skill { Code = code, Name = name, SortOrder = ss });
                ss++;
            }
        }
        await db.SaveChangesAsync();

        // settings
        foreach (var (k, v) in SettingKeys.Defaults)
            if (!await db.AppSettings.AnyAsync(s => s.Key == k)) db.AppSettings.Add(new AppSetting { Key = k, Value = v });
        var centerName = config["App:CenterName"];
        if (!string.IsNullOrWhiteSpace(centerName))
        {
            var row = await db.AppSettings.FindAsync(SettingKeys.CenterName);
            if (row is not null && row.Value == SettingKeys.Defaults[SettingKeys.CenterName]) row.Value = centerName;
        }
        await db.SaveChangesAsync();

        // adaptive rules
        if (!await db.AdaptiveRules.AnyAsync())
        {
            db.AdaptiveRules.Add(new AdaptiveRule { Section = Section.RW, QuestionCount = 27, Cutoff = 15, DefaultModuleSeconds = 1920 });
            db.AdaptiveRules.Add(new AdaptiveRule { Section = Section.Math, QuestionCount = 22, Cutoff = 12, DefaultModuleSeconds = 2100 });
            await db.SaveChangesAsync();
        }

        // scoring table
        if (!await db.ScoringEntries.AnyAsync())
        {
            db.ScoringEntries.AddRange(ScoringService.DefaultTable("SAT_2026_V1"));
            await db.SaveChangesAsync();
        }

        // super admin
        var adminPhone = PhoneFormat.Normalize(config["Seed:AdminPhone"] ?? "+998900000000");
        var adminPassword = config["Seed:AdminPassword"];
        if (!await db.Users.AnyAsync(u => u.Role == UserRole.SuperAdmin))
        {
            if (string.IsNullOrEmpty(adminPassword)) { adminPassword = env.IsDevelopment() ? "Admin123!" : Ids.Code(10); }
            var admin = new User { FullName = config["Seed:AdminName"] ?? "Super Admin", Phone = adminPhone, Role = UserRole.SuperAdmin, AvatarColor = "#0f8f7e" };
            admin.PasswordHash = AuthService.HashPassword(admin, adminPassword);
            db.Users.Add(admin);
            await db.SaveChangesAsync();
            logger.LogWarning("Super admin created: {Phone} / {Password}  ← change the password after the first login", adminPhone, adminPassword);
        }

        var demo = config.GetValue<bool?>("Seed:Demo") ?? true;
        if (demo && !await db.Questions.AnyAsync()) await SeedDemoAsync(db);
    }

    private async Task SeedDemoAsync(AppDbContext db)
    {
        logger.LogInformation("Seeding demo content (question bank, tests, users, groups)...");
        var admin = await db.Users.FirstAsync(u => u.Role == UserRole.SuperAdmin);
        var skills = await db.Skills.Include(s => s.Domain).ToDictionaryAsync(s => s.Code);

        // ---- question bank ----
        var items = DemoContent.Generate(new Random(20261008));
        var count = 0;
        foreach (var it in items)
        {
            if (!skills.TryGetValue(it.Skill, out var skill)) { logger.LogWarning("Demo: unknown skill {Skill}", it.Skill); continue; }
            var edit = new QuestionEdit
            {
                SkillId = skill.Id, DomainId = skill.DomainId, Section = skill.Domain.Section, Difficulty = it.Difficulty, Type = it.Options is null ? QuestionType.SPR : QuestionType.MultipleChoice,
                Status = QuestionStatus.Active, Passage = it.Passage, Text = it.Text, Explanation = it.Explanation, CorrectAnswer = it.Answer, Tag = "demo",
                Options = it.Options?.Select((o, i) => new OptionEdit(((char)('A' + i)).ToString()) { Text = o, IsCorrect = i == it.Correct }).ToList() ?? []
            };
            var (_, errors) = await content.SaveAsync(edit, admin.Id);
            if (errors.Count == 0) count++; else logger.LogWarning("Demo question skipped: {Errors}", string.Join(" ", errors));
        }
        logger.LogInformation("Demo: {Count} questions created", count);

        // ---- demo users ----
        var password = config["Seed:DemoPassword"] ?? (env.IsDevelopment() ? "Demo123!" : Ids.Code(10));
        User Make(string name, string phone, UserRole role, bool questions = false)
        {
            var u = new User { FullName = name, Phone = phone, Role = role, AvatarColor = AuthService.ColorFor(phone), CanManageQuestions = questions };
            u.PasswordHash = AuthService.HashPassword(u, password);
            db.Users.Add(u);
            return u;
        }
        var teacher = Make("Dilnoza Karimova", "+998901111111", UserRole.Teacher, questions: true);
        var teacher2 = Make("Jasur Rahimov", "+998901111112", UserRole.Teacher);
        var assistant = Make("Malika Yusupova", "+998901111113", UserRole.Assistant);
        var adminUser = Make("Aziz Admin", "+998901111110", UserRole.Admin);
        string[] studentNames = ["Ali Valiyev", "Vali Hasanov", "Hasan Karimov", "Bekzod Tursunov", "Madina Azimova", "Nilufar Saidova", "Sardor Umarov", "Zarina Qodirova", "Javohir Toshev", "Kamola Nazarova"];
        var students = new List<Student>();
        for (var i = 0; i < studentNames.Length; i++)
        {
            var u = Make(studentNames[i], $"+99890200000{i}", UserRole.Student);
            u.Student = new Student { TargetScore = 1300 + i * 20, ParentCode = Ids.Code(6) };
            students.Add(u.Student);
        }
        var parent = Make("Valiyev Bobur", "+998903000000", UserRole.Parent);
        await db.SaveChangesAsync();
        db.ParentStudents.Add(new ParentStudent { ParentUserId = parent.Id, StudentId = students[0].Id });

        var g1 = new Group { Name = "SAT Intensive A", TeacherUserId = teacher.Id, AssistantUserId = assistant.Id, Room = "201", ScheduleDays = "1,3,5", StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(20, 0) };
        var g2 = new Group { Name = "SAT Foundation B", TeacherUserId = teacher2.Id, Room = "202", ScheduleDays = "2,4,6", StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(18, 0) };
        db.Groups.AddRange(g1, g2);
        await db.SaveChangesAsync();
        for (var i = 0; i < students.Count; i++)
            db.Enrollments.Add(new Enrollment { StudentId = students[i].Id, GroupId = i < 6 ? g1.Id : g2.Id, StartDate = Tz.Today.AddDays(-30) });
        await db.SaveChangesAsync();
        logger.LogWarning("Demo users created (password: {Password}): admin +998901111110, teacher +998901111111, student +998902000000, parent +998903000000", password);

        // ---- tests ----
        var diag = await builder.CreateAsync("Diagnostik test", TestType.Diagnostic, adaptive: true, null, "Boshlang'ich darajangizni aniqlash uchun to'liq adaptiv SAT.", admin.Id);
        await FillAsync(diag, admin.Id);
        var mock = await builder.CreateAsync("SAT Mock Test #1", TestType.FullMock, adaptive: true, null, "To'liq SAT formatidagi sinov testi.", admin.Id);
        await FillAsync(mock, admin.Id);
        var mathSec = await builder.CreateAsync("Math Section Test", TestType.SectionTest, adaptive: true, Section.Math, "Faqat Math bo'limi (2 modul, adaptiv).", admin.Id);
        await FillAsync(mathSec, admin.Id);
        var topic = await builder.CreateAsync("Algebra — Topic Test", TestType.TopicTest, adaptive: false, Section.Math, "Algebra domeni bo'yicha 15 ta savol.", admin.Id, moduleSeconds: 20 * 60);
        await FillTopicAsync(topic, admin.Id);
        foreach (var id in new[] { diag, mock, mathSec, topic })
        {
            var errors = await builder.PublishAsync(id, admin.Id);
            if (errors.Count > 0) logger.LogWarning("Demo test {Id} not published: {Errors}", id, string.Join(" | ", errors));
        }
        await using var db2 = await factory.CreateDbContextAsync();
        await db2.Tests.Where(t => t.Id == mock || t.Id == mathSec || t.Id == topic).ExecuteUpdateAsync(s => s.SetProperty(t => t.IsPublic, true));
        db2.TestAssignments.Add(new TestAssignment { TestId = mock, GroupId = g1.Id, AssignedByUserId = teacher.Id, StartAt = DateTime.UtcNow, EndAt = DateTime.UtcNow.AddDays(14), AttemptLimit = 2 });
        await db2.SaveChangesAsync();
    }

    private async Task FillAsync(int testId, int userId)
    {
        var t = await builder.GetAsync(testId);
        if (t is null) return;
        foreach (var m in t.Modules)
        {
            var rule = new AutoGenerateRule { Count = m.Section == Section.RW ? 27 : 22 };
            (rule.EasyPercent, rule.MediumPercent, rule.HardPercent) = m.Route switch { ModuleRoute.Easy => (50, 40, 10), ModuleRoute.Hard => (10, 40, 50), _ => (25, 50, 25) };
            var err = await builder.AutoGenerateAsync(m.Id, rule, userId);
            if (err is not null) logger.LogWarning("Demo auto-generate {Module}: {Err}", m.Title, err.Replace('\n', ' '));
        }
    }

    private async Task FillTopicAsync(int testId, int userId)
    {
        var t = await builder.GetAsync(testId);
        if (t is null) return;
        await using var db = await factory.CreateDbContextAsync();
        var algebra = await db.Domains.FirstAsync(d => d.Code == "ALGEBRA");
        var rule = new AutoGenerateRule { Count = 15, DomainPercents = new() { [algebra.Id] = 100 } };
        var err = await builder.AutoGenerateAsync(t.Modules[0].Id, rule, userId);
        if (err is not null) logger.LogWarning("Demo topic test: {Err}", err.Replace('\n', ' '));
    }
}

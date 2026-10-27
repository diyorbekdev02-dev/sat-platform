using SatPlatform.Data;
using SatPlatform.Services;
using Xunit;

namespace SatPlatform.Tests;

public class SprAnswerTests
{
    [Theory]
    [InlineData("2/3", "2/3", true)]
    [InlineData("2/3", ".667", true)]
    [InlineData("2/3", "0.667", true)]
    [InlineData("2/3", ".666", true)]
    [InlineData("2/3", "0.66", false)]      // only two decimals: not precise enough
    [InlineData("2/3", "4/6", true)]        // equivalent fraction
    [InlineData("-2", "-2", true)]
    [InlineData("-2", "2", false)]
    [InlineData("3.5", "7/2", true)]
    [InlineData("3.5", "3.50", true)]
    [InlineData("12", " 12 ", true)]
    [InlineData("12", "12.0", true)]
    [InlineData("1/3|0.333", "0.333", true)]
    [InlineData("5", "", false)]
    [InlineData("5", "abc", false)]
    [InlineData("0.25", "1/4", true)]
    [InlineData("9999", "9999", true)]
    public void Spr_matching(string spec, string answer, bool expected) => Assert.Equal(expected, ScoringService.IsSprCorrect(spec, answer));

    [Theory]
    [InlineData("3/4", null)]
    [InlineData("-2.5", null)]
    [InlineData("123456", "Ko'pi bilan 5 ta belgi kiriting (masalan 0.667 yoki 2/3).")]
    [InlineData("-123456", "Ko'pi bilan 6 ta belgi kiriting (masalan 0.667 yoki 2/3).")]
    [InlineData("1/0", "Javob son, kasr (3/4) yoki o'nlik (0.75) bo'lishi kerak.")]
    [InlineData("x", "Javob son, kasr (3/4) yoki o'nlik (0.75) bo'lishi kerak.")]
    public void Spr_input_validation(string input, string? expectedError) => Assert.Equal(expectedError, ScoringService.ValidateSprInput(input));

    [Fact]
    public void MultipleChoice_checks_key_case_insensitive()
    {
        var v = new QuestionVersion { Type = QuestionType.MultipleChoice, OptionsJson = "[{\"Key\":\"A\",\"Text\":\"x\",\"IsCorrect\":false},{\"Key\":\"B\",\"Text\":\"y\",\"IsCorrect\":true}]" };
        Assert.True(ScoringService.IsCorrect(v, "B"));
        Assert.True(ScoringService.IsCorrect(v, "b"));
        Assert.False(ScoringService.IsCorrect(v, "A"));
        Assert.False(ScoringService.IsCorrect(v, null));
    }
}

public class ScaledScoreTests
{
    [Fact]
    public void Linear_fallback_covers_200_to_800()
    {
        Assert.Equal(200, ScoringService.Linear(ModuleRoute.Hard, 0, 54));
        Assert.Equal(800, ScoringService.Linear(ModuleRoute.Hard, 54, 54));
        Assert.True(ScoringService.Linear(ModuleRoute.Easy, 54, 54) < 800);
        Assert.Equal(200, ScoringService.Linear(ModuleRoute.Easy, 0, 54));
    }

    [Fact]
    public void Default_table_is_monotonic_and_bounded()
    {
        var table = ScoringService.DefaultTable("T").ToList();
        foreach (var g in table.GroupBy(e => (e.Section, e.Route)))
        {
            var ordered = g.OrderBy(e => e.RawScore).ToList();
            Assert.Equal(200, ordered.First().ScaledScore);
            Assert.True(ordered.Last().ScaledScore <= 800);
            for (var i = 1; i < ordered.Count; i++) Assert.True(ordered[i].ScaledScore >= ordered[i - 1].ScaledScore, $"{g.Key} not monotonic at {i}");
            if (g.Key.Route == ModuleRoute.Hard) Assert.Equal(800, ordered.Last().ScaledScore);
            if (g.Key.Route == ModuleRoute.Easy) Assert.True(ordered.Last().ScaledScore < 800);
        }
        Assert.Equal(55, table.Count(e => e.Section == Section.RW && e.Route == ModuleRoute.Hard));
        Assert.Equal(45, table.Count(e => e.Section == Section.Math && e.Route == ModuleRoute.Hard));
    }

    [Fact]
    public void Table_parser_accepts_common_separators()
    {
        var rows = ScoringService.ParseTable("0,200\n1;210\n2\t220\n3 230\nbad line\n");
        Assert.Equal(4, rows.Count);
        Assert.Equal((3, 230), rows[3]);
    }
}

public class DemoContentTests
{
    [Fact]
    public void Every_generated_question_has_exactly_one_correct_answer_and_an_explanation()
    {
        var items = DemoContent.Generate(new Random(1));
        Assert.True(items.Count > 200);
        foreach (var q in items)
        {
            Assert.False(string.IsNullOrWhiteSpace(q.Text));
            Assert.False(string.IsNullOrWhiteSpace(q.Explanation));
            if (q.Options is null)
            {
                Assert.False(string.IsNullOrWhiteSpace(q.Answer));
                foreach (var a in q.Answer!.Split('|')) Assert.NotNull(ScoringService.ParseNumber(ScoringService.NormalizeSpr(a)!));
            }
            else
            {
                Assert.Equal(4, q.Options.Length);
                Assert.Equal(4, q.Options.Distinct().Count());
                Assert.InRange(q.Correct, 0, 3);
            }
        }
    }

    [Fact]
    public void Generated_bank_covers_every_skill_of_the_taxonomy()
    {
        var items = DemoContent.Generate(new Random(2));
        var skills = SeedService.Taxonomy.SelectMany(t => t.Skills.Select(s => s.Code)).ToHashSet();
        foreach (var s in skills) Assert.True(items.Any(i => i.Skill == s), $"no demo questions for {s}");
        foreach (var i in items) Assert.Contains(i.Skill, skills);
    }
}

public class AuthHelperTests
{
    [Theory]
    [InlineData("+998 90 123 45 67", "+998901234567")]
    [InlineData("901234567", "+998901234567")]
    [InlineData("0901234567", "+998901234567")]
    [InlineData("998901234567", "+998901234567")]
    public void Phone_normalization(string input, string expected) => Assert.Equal(expected, AuthService.NormalizePhone(input));

    [Fact]
    public void Password_rules()
    {
        Assert.NotNull(AuthService.ValidatePassword("12345"));
        Assert.Null(AuthService.ValidatePassword("123456"));
        Assert.NotNull(AuthService.ValidatePassword(""));
    }

    [Fact]
    public void Target_progress_formula()
    {
        Assert.Equal(78, AnalyticsService.TargetProgress(1180, 1400));
        Assert.Equal(100, AnalyticsService.TargetProgress(1500, 1400));
        Assert.Null(AnalyticsService.TargetProgress(null, 1400));
    }
}

public class KeepAliveScheduleTests
{
    private static DateTime TashkentUtc(int hour, int minute = 0) => Tz.ToUtc(new DateTime(2026, 10, 26, hour, minute, 0));

    [Theory]
    [InlineData(null, 0, 24)]
    [InlineData("", 0, 24)]
    [InlineData("07-23", 7, 23)]
    [InlineData(" 8 - 22 ", 8, 22)]
    [InlineData("22-06", 22, 6)]
    [InlineData("garbage", 0, 24)]
    [InlineData("7-7", 0, 24)]
    [InlineData("-1-30", 0, 24)]
    public void Parse(string? text, int start, int end) => Assert.Equal(new KeepAliveSchedule(start, end), KeepAliveSchedule.Parse(text));

    [Theory]
    [InlineData("07-23", 6, 59, false)]
    [InlineData("07-23", 7, 0, true)]
    [InlineData("07-23", 15, 0, true)]
    [InlineData("07-23", 22, 59, true)]
    [InlineData("07-23", 23, 0, false)]
    [InlineData("07-23", 2, 0, false)]
    [InlineData("22-06", 23, 0, true)]
    [InlineData("22-06", 3, 0, true)]
    [InlineData("22-06", 6, 0, false)]
    [InlineData("22-06", 12, 0, false)]
    [InlineData("0-24", 12, 0, true)]
    public void Is_active_in_tashkent_local_time(string text, int hour, int minute, bool active)
        => Assert.Equal(active, KeepAliveSchedule.Parse(text).IsActive(TashkentUtc(hour, minute)));

    [Fact]
    public void Wait_until_next_start()
    {
        var s = KeepAliveSchedule.Parse("07-23");
        Assert.Equal(TimeSpan.FromHours(4), s.UntilNextStart(TashkentUtc(3)));
        Assert.Equal(TimeSpan.FromHours(8), s.UntilNextStart(TashkentUtc(23)));
        Assert.Equal(TimeSpan.FromDays(1), s.UntilNextStart(TashkentUtc(7)));
    }
}

public class PhoneFormatTests
{
    [Theory]
    [InlineData("+998 90 123 45 67", true)]
    [InlineData("90-123-45-67", true)]
    [InlineData("(90) 123 45 67", true)]
    [InlineData("0901234567", true)]
    [InlineData("998941234567", true)]
    [InlineData("+998 33 123 45 67", true)]
    [InlineData("+998 12 345 67 89", false)]   // no such operator code
    [InlineData("+998 90 123 45", false)]      // too short
    [InlineData("+998 90 123 45 678", false)]  // too long
    [InlineData("+7 912 345 67 89", false)]    // not Uzbekistan
    [InlineData("+1 202 555 0143", false)]
    [InlineData("", false)]
    [InlineData("abc", false)]
    public void Only_uzbek_mobile_numbers_are_valid(string input, bool valid) => Assert.Equal(valid, PhoneFormat.IsValid(input));

    [Theory]
    [InlineData("+998901234567", "+998 90 123 45 67")]
    [InlineData("901234567", "+998 90 123 45 67")]
    [InlineData("+7 912 345 67 89", "+7 912 345 67 89")]   // foreign input is left as typed
    [InlineData("", "")]
    public void Display_is_grouped(string input, string shown) => Assert.Equal(shown, PhoneFormat.Display(input));

    [Fact]
    public void Search_matches_digits_only()
    {
        Assert.True(PhoneFormat.Matches("+998901234567", "90 123"));
        Assert.True(PhoneFormat.Matches("+998901234567", "+998 90"));
        Assert.False(PhoneFormat.Matches("+998901234567", "Ali"));   // a name must not match every phone
        Assert.False(PhoneFormat.Matches("+998901234567", ""));
    }
}

public class PageHelpTests
{
    // pages that deliberately have no «Qo'llanma» button: public, error, the manual itself, and the exam window
    private static readonly string[] NoHelp = ["/", "/Error", "/login", "/register", "/forbidden", "/not-found", "/help", "/attempt/{id}"];

    [Theory]
    [InlineData("/teacher/groups/12", "/teacher/groups/{id}")]
    [InlineData("/dashboard?x=1", "/dashboard")]
    [InlineData("/admin/questions/new", "/admin/questions/{id}")]
    [InlineData("/admin/questions/345", "/admin/questions/{id}")]
    [InlineData("/practice/new", "/practice/new")]
    [InlineData("/attempt/7/result", "/attempt/{id}/result")]
    public void Route_key(string path, string key) => Assert.Equal(key, PageHelp.Key(path));

    [Fact]
    public void Every_routable_page_has_a_help_topic()
    {
        var pages = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SatPlatform", "Components", "Pages"));
        var routes = Directory.EnumerateFiles(pages, "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f), "^@page \"([^\"]+)\"", System.Text.RegularExpressions.RegexOptions.Multiline).Select(m => m.Groups[1].Value))
            .Select(r => System.Text.RegularExpressions.Regex.Replace(r, @"\{[A-Za-z]+(:int)?\}", "{id}"))
            .Distinct().ToList();
        Assert.True(routes.Count > 30);
        var missing = routes.Where(r => !NoHelp.Contains(r) && PageHelp.Find(r.Replace("{id}", "1")) is null).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void Help_text_is_complete()
    {
        var t = PageHelp.Find("/admin/tests/5")!;
        Assert.NotEmpty(t.Intro);
        Assert.All(t.Sections, s => { Assert.NotEmpty(s.Heading); Assert.NotEmpty(s.Items); });
    }
}

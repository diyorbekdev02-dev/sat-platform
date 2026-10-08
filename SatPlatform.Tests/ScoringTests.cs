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

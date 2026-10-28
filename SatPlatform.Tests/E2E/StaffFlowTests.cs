using Microsoft.Playwright;
using Xunit;

namespace SatPlatform.Tests.E2E;

[Collection("e2e")]
public class StaffFlowTests(BrowserFixture f)
{
    private static readonly string Password = Environment.GetEnvironmentVariable("SAT_E2E_PASSWORD") ?? "Demo123!";
    private static readonly string AdminPhone = Environment.GetEnvironmentVariable("SAT_E2E_ADMIN_PHONE") ?? "+998900000000";
    private static readonly string AdminPassword = Environment.GetEnvironmentVariable("SAT_E2E_ADMIN_PASSWORD") ?? "Admin123!";

    [Fact]
    public async Task Teacher_dashboard_groups_students_and_attendance()
    {
        if (!f.Available) return;
        var page = await f.NewPageAsync();
        await page.LoginAsync("+998901111111", Password, "/teacher");
        await page.WaitForSelectorAsync("text=Mening guruhlarim");
        await page.GoAsync("/teacher/groups");
        await page.ClickAsync(".card:has-text('SAT Intensive A')");
        await page.WaitForSelectorAsync("h1:has-text('SAT Intensive A')");
        Assert.True(await page.Locator("table tbody tr").CountAsync() >= 5);
        // teacher must not see the other teacher's group
        await page.GoAsync("/teacher/groups");
        Assert.Equal(0, await page.Locator(".card:has-text('SAT Foundation B')").CountAsync());

        await page.GoAsync("/teacher/attendance");
        await page.WaitForSelectorAsync(".att-grid tbody tr");
        var rows = page.Locator(".att-grid tbody tr");
        // cell cycle: empty -> present -> absent -> late
        var first = rows.Nth(0).Locator("button.att-cell").Last;
        await first.ClickAsync(); await first.ClickAsync();
        var second = rows.Nth(1).Locator("button.att-cell").Last;
        await second.ClickAsync(); await second.ClickAsync(); await second.ClickAsync();
        await page.ClickAsync("button:has-text('Saqlash')");
        await page.WaitForSelectorAsync(".toast:has-text('Davomat saqlandi')");
        await page.ReloadAsync();
        await page.WaitForSelectorAsync(".att-grid tbody tr");
        Assert.Contains("absent", await page.Locator(".att-grid tbody tr").Nth(0).Locator("button.att-cell").Last.GetAttributeAsync("class"));
        Assert.Contains("late", await page.Locator(".att-grid tbody tr").Nth(1).Locator("button.att-cell").Last.GetAttributeAsync("class"));
    }

    [Fact]
    public async Task Teacher_creates_homework_and_student_sees_it()
    {
        if (!f.Available) return;
        var page = await f.NewPageAsync();
        await page.LoginAsync("+998901111111", Password, "/teacher");
        await page.GoAsync("/teacher/homework/new");
        await page.WaitForSelectorAsync("input[placeholder*='Algebra']");
        var title = "E2E vazifa " + DateTime.Now.Ticks % 10000;
        await page.FillAsync("input[placeholder*='Algebra']", title);
        await page.SelectOptionAsync("select.input.mt-s", new SelectOptionValue { Label = "SAT Intensive A" });
        await page.ClickAsync("button:has-text('Vazifa berish')");
        await page.WaitForURLAsync(u => System.Text.RegularExpressions.Regex.IsMatch(u, @"/teacher/homework/\d+$"));
        await page.WaitForSelectorAsync($"h1:has-text('{title}')");

        var student = await f.NewPageAsync();
        await student.LoginAsync("+998902000000", Password, "/dashboard");
        await student.GoAsync("/homework");
        await student.WaitForSelectorAsync($"h2:has-text('{title}')");
        await student.GoAsync("/notifications");
        await student.WaitForSelectorAsync("text=Yangi uy vazifasi");
    }

    [Fact]
    public async Task Admin_creates_question_builds_test_and_publishes()
    {
        if (!f.Available) return;
        var page = await f.NewPageAsync();
        await page.LoginAsync(AdminPhone, AdminPassword, "/admin");
        await page.WaitForSelectorAsync("text=Savollar banki");

        // new question (SPR, so no rich-text options needed)
        await page.GoAsync("/admin/questions/new");
        await page.WaitForSelectorAsync(".ql-editor");
        await page.SelectOptionAsync("select.input >> nth=1", new SelectOptionValue { Label = "Algebra" });
        await page.SelectOptionAsync("select.input >> nth=2", new SelectOptionValue { Label = "Linear Equations in One Variable" });
        await page.SelectOptionAsync("select.input >> nth=4", "SPR");
        var editors = page.Locator(".ql-editor");
        await editors.Nth(1).FillAsync("E2E: If 3x + 1 = 10, what is x?");
        await page.FillAsync("input[placeholder*='2/3']", "3");
        await editors.Nth(2).FillAsync("3x = 9, so x = 3.");
        await page.SelectOptionAsync("select.input >> nth=5", "Active");
        await page.ClickAsync("button:has-text('Saqlash')");
        await page.WaitForURLAsync(u => System.Text.RegularExpressions.Regex.IsMatch(u, @"/admin/questions/\d+$"));
        await page.WaitForSelectorAsync(".toast:has-text('Saqlandi')");

        // new topic test, auto-generate, publish
        await page.GoAsync("/admin/tests");
        await page.ClickAsync("button:has-text('Yangi test')");
        var name = "E2E Topic " + DateTime.Now.Ticks % 10000;
        await page.FillAsync(".modal input[placeholder*='Mock']", name);
        await page.SelectOptionAsync(".modal select.input >> nth=0", "TopicTest");
        await page.ClickAsync(".modal button:has-text('Yaratish')");
        await page.WaitForURLAsync(u => System.Text.RegularExpressions.Regex.IsMatch(u, @"/admin/tests/\d+$"));
        await page.WaitForSelectorAsync($"h1:has-text('{name}')");
        await page.ClickAsync("button:has-text('Avto-generatsiya')");
        await page.FillAsync(".modal input[type=number] >> nth=0", "10");
        await page.ClickAsync(".modal button:has-text('Generatsiya')");
        await page.WaitForSelectorAsync(".toast:has-text('generatsiya')");
        await page.WaitForSelectorAsync(".module-q");
        Assert.Equal(10, await page.Locator(".module-q").CountAsync());
        await page.ClickAsync("button:has-text('Chop etish')");
        await page.WaitForSelectorAsync(".badge:has-text('Chop etilgan')");

        // preview runs the student UI
        await page.ClickAsync("button:has-text('Preview')");
        await page.WaitForURLAsync(u => System.Text.RegularExpressions.Regex.IsMatch(u, @"/attempt/\d+$"));
        await page.WaitForSelectorAsync(".test-header .sub:has-text('PREVIEW')");
    }

    [Fact]
    public async Task Admin_pages_render_without_errors()
    {
        if (!f.Available) return;
        var page = await f.NewPageAsync();
        await page.LoginAsync(AdminPhone, AdminPassword, "/admin");
        foreach (var (path, title) in new[] { ("/admin/questions", "Savollar banki"), ("/admin/tests", "Testlar"), ("/admin/materials", "Materiallar"), ("/admin/students", "O'quvchilar"), ("/admin/users", "Xodimlar"), ("/admin/groups", "Guruhlar"), ("/admin/analytics", "Tahlil"), ("/admin/settings", "Sozlamalar"), ("/admin/audit", "Audit"), ("/teacher/assign", "Test tayinlash"), ("/help", "Qo'llanma") })
        {
            await page.GoAsync(path);
            await page.WaitForFunctionAsync("t => (document.querySelector('#page-title')?.textContent || '').includes(t)", title);
            await page.WaitForTimeoutAsync(300);
            Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync(), $"error banner on {path}");
        }
        // settings tabs
        await page.GoAsync("/admin/settings");
        await page.ClickAsync("button.tab:has-text('Scoring')");
        await page.WaitForSelectorAsync("table tbody tr");
        await page.ClickAsync("button.tab:has-text('Domain')");
        await page.WaitForSelectorAsync("text=Linear Functions");
    }

    [Fact]
    public async Task Parent_sees_child_progress()
    {
        if (!f.Available) return;
        var page = await f.NewPageAsync();
        await page.LoginAsync("+998903000000", Password, "/parent");
        await page.WaitForSelectorAsync("h1:has-text('Ali Valiyev')");
        await page.ClickAsync("button.tab:has-text('Davomat')");
        await page.WaitForSelectorAsync("table.table, .empty");
        await page.GoAsync("/admin");
        await page.WaitForSelectorAsync("text=ruxsatingiz yo'q");
    }

    [Fact]
    public async Task Registration_with_dev_code_creates_student()
    {
        if (!f.Available) return;
        var page = await f.NewPageAsync();
        await page.GoAsync("/register");
        var phone = "+99890" + (DateTime.UtcNow.Ticks % 10000000).ToString("0000000");
        await page.FillAsync("input.phone-input", phone);
        await page.ClickAsync("button:has-text('Davom etish')");
        await page.WaitForSelectorAsync(".dev-code");
        var code = (await page.TextContentAsync(".dev-code"))!.Trim();
        await page.FillAsync("input.code-input", code);
        await page.ClickAsync("button:has-text('Tasdiqlash')");
        await page.WaitForSelectorAsync("input[placeholder='Ali Valiyev']");
        await page.FillAsync("input[placeholder='Ali Valiyev']", "E2E Student");
        var pws = page.Locator("input[type=password]");
        await pws.Nth(0).FillAsync("Secret123");
        await pws.Nth(1).FillAsync("Secret123");
        await page.ClickAsync("button.btn.primary:has-text(\"Ro'yxatdan o'tish\")");
        await page.WaitForURLAsync(u => u.Contains("/dashboard"), new() { Timeout = 30000 });
        await page.WaitForSelectorAsync("text=Xush kelibsiz, E2E");
    }
}

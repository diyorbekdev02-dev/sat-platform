using Microsoft.Playwright;
using Xunit;

namespace SatPlatform.Tests.E2E;

[Collection("e2e")]
public class StudentFlowTests(BrowserFixture f)
{
    private const string StudentPhone = "+998902000000";
    private static readonly string Password = Environment.GetEnvironmentVariable("SAT_E2E_PASSWORD") ?? "Demo123!";

    [Fact]
    public async Task Login_wrong_password_shows_error()
    {
        if (!f.Available) return;
        var page = await f.NewPageAsync();
        await page.GoAsync("/login");
        await page.FillAsync("input.phone-input", StudentPhone);
        await page.FillAsync("input[type=password]", "wrong-pass");
        await page.ClickAsync("button.btn.primary:has-text('Kirish')");
        await page.WaitForSelectorAsync(".error");
        Assert.Contains("Parol noto'g'ri", await page.TextContentAsync(".error"));
    }

    [Fact]
    public async Task Unauthenticated_user_is_redirected_to_login()
    {
        if (!f.Available) return;
        var page = await f.NewPageAsync();
        await page.GoAsync("/dashboard");
        await page.WaitForURLAsync(u => u.Contains("/login"));
    }

    [Fact]
    public async Task Student_cannot_open_admin_pages()
    {
        if (!f.Available) return;
        var page = await f.NewPageAsync();
        await page.LoginAsync(StudentPhone, Password, "/dashboard");
        await page.GoAsync("/admin/questions");
        await page.WaitForSelectorAsync("text=ruxsatingiz yo'q");
    }

    [Fact]
    public async Task Dashboard_tests_practice_mistakes_analytics_pages_render()
    {
        if (!f.Available) return;
        var page = await f.NewPageAsync();
        await page.LoginAsync(StudentPhone, Password, "/dashboard");
        Assert.Equal("Bosh sahifa", await page.Title());
        await page.WaitForSelectorAsync("text=Joriy SAT ball");

        foreach (var (path, title) in new[] { ("/tests", "Testlar"), ("/practice", "Mashq"), ("/mistakes", "Xatolarim"), ("/analytics", "Mening progressim"), ("/materials", "Materiallar"), ("/homework", "Uy vazifasi"), ("/profile", "Profil"), ("/notifications", "Bildirishnomalar") })
        {
            await page.GoAsync(path);
            await page.WaitForFunctionAsync("t => (document.querySelector('#page-title')?.textContent || '').includes(t)", title);
            Assert.Null(await page.QuerySelectorAsync("#blazor-error-ui[style*='display: block']"));
        }
    }

    [Fact]
    public async Task Practice_session_answers_and_result()
    {
        if (!f.Available) return;
        var page = await f.NewPageAsync();
        await page.LoginAsync(StudentPhone, Password, "/dashboard");
        await page.GoAsync("/practice/new?section=1&count=5");
        await page.WaitForSelectorAsync("button:has-text('Mashqni boshlash')");
        await page.ClickAsync("button:has-text('Mashqni boshlash')");
        await page.WaitForURLAsync(u => System.Text.RegularExpressions.Regex.IsMatch(u, @"/practice/\d+$"));

        for (var i = 0; i < 5; i++)
        {
            await page.WaitForSelectorAsync(".q-content");
            var option = await page.QuerySelectorAsync(".option");
            if (option is not null)
            {
                await option.ClickAsync();
                await page.ClickAsync("button:has-text('Javobni tasdiqlash')");
            }
            else
            {
                await page.FillAsync(".spr input", "1");
                await page.Keyboard.PressAsync("Tab");
                await page.ClickAsync("button:has-text('Javobni tasdiqlash')");
            }
            await page.WaitForSelectorAsync(".feedback");
            if (i < 4) await page.ClickAsync("button:has-text('Keyingi')");
        }
        await page.ClickAsync("footer button:has-text('Yakunlash')");
        await page.WaitForURLAsync(u => u.EndsWith("/result"));
        await page.WaitForSelectorAsync("text=MASHQ YAKUNLANDI");
        var badge = await page.TextContentAsync(".badge:has-text('ta savol')");
        Assert.Contains("5 ta savol", badge);
    }

    [Fact]
    public async Task Full_adaptive_test_runs_through_modules_break_and_result()
    {
        if (!f.Available) return;
        var page = await f.NewPageAsync();
        await page.LoginAsync(StudentPhone, Password, "/dashboard");
        await page.EnsureNoActiveAttemptAsync();
        await page.GoAsync("/tests");
        // the Math section test is public and short (2 modules): take it
        var card = page.Locator(".card", new() { HasText = "Math Section Test" });
        var cont = card.Locator("a:has-text('Davom ettirish')");
        if (await cont.CountAsync() > 0) await cont.First.ClickAsync();
        else await card.Locator("a:has-text('Testni boshlash')").ClickAsync();
        await page.WaitForURLAsync(u => u.Contains("/start") || u.Contains("/attempt/"), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        if (page.Url.Contains("/start"))
        {
            await page.WaitForSelectorAsync("button:has-text('Testni boshlash'):not([disabled])");
            await page.ClickAsync("button:has-text('Testni boshlash')");
        }
        await page.WaitForURLAsync(u => System.Text.RegularExpressions.Regex.IsMatch(u, @"/attempt/\d+$"));

        for (var module = 0; module < 2; module++)
        {
            await page.WaitForSelectorAsync(".test-header .title:has-text('Module')");
            // answer the first 12 questions (enough to be routed to Hard when correct, but we just pick A / 1)
            for (var i = 0; i < 12; i++)
            {
                await page.WaitForSelectorAsync(".q-content");
                var option = await page.QuerySelectorAsync(".option");
                if (option is not null) await option.ClickAsync();
                else { await page.FillAsync(".spr input", "1"); await page.Keyboard.PressAsync("Tab"); }
                await page.ClickAsync("footer button:has-text('Next'), footer button:has-text('Review')");
            }
            // flag + grid work
            await page.ClickAsync(".flag-btn");
            await page.WaitForSelectorAsync(".flag-btn.on");
            await page.ClickAsync("footer button:has-text('Question')");
            await page.WaitForSelectorAsync(".qgrid-popover");
            Assert.True(await page.Locator(".qgrid-popover .qcell.answered").CountAsync() >= 12);
            await page.ClickAsync(".qgrid-popover button:has-text('Modulni yakunlash')");
            await page.WaitForSelectorAsync(".modal:has-text('Module Review')");
            await page.ClickAsync(".modal button:has-text('Modulni yakunlash')");
            await page.WaitForTimeoutAsync(1500);
        }
        await page.WaitForSelectorAsync("text=Test yakunlandi", new() { Timeout = 30000 });
        await page.ClickAsync("a:has-text(\"Natijani ko'rish\")");
        await page.WaitForSelectorAsync("text=TEST COMPLETE");
        Assert.Contains("Math ball", await page.TextContentAsync(".card"));
        await page.ClickAsync("a:has-text(\"Javoblarni ko'rish\")");
        await page.WaitForSelectorAsync(".qcell");
        Assert.True(await page.Locator(".qcell").CountAsync() >= 24);
    }

    [Fact]
    public async Task Second_tab_is_locked_out_of_an_active_test()
    {
        if (!f.Available) return;
        var page = await f.NewPageAsync();
        await page.LoginAsync(StudentPhone, Password, "/dashboard");
        await page.EnsureNoActiveAttemptAsync();
        await page.GoAsync("/tests");
        var card = page.Locator(".card", new() { HasText = "Diagnostik test" });
        var cont = card.Locator("a:has-text('Davom ettirish')");
        if (await cont.CountAsync() > 0) await cont.First.ClickAsync();
        else { await card.Locator("a:has-text('Testni boshlash')").ClickAsync(); await page.WaitForURLAsync(u => u.Contains("/start"), new() { WaitUntil = WaitUntilState.DOMContentLoaded }); await page.WaitForSelectorAsync("button:has-text('Testni boshlash'):not([disabled])"); await page.ClickAsync("button:has-text('Testni boshlash')"); }
        await page.WaitForURLAsync(u => System.Text.RegularExpressions.Regex.IsMatch(u, @"/attempt/\d+$"));
        await page.WaitForSelectorAsync(".test-header");
        var second = await page.Context.NewPageAsync();
        await second.GotoAsync(page.Url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await second.WaitForSelectorAsync("text=boshqa oynada ochiq");
        await second.CloseAsync();
        await page.EnsureNoActiveAttemptAsync();
    }
}

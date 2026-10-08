using Microsoft.Playwright;
using Xunit;

namespace SatPlatform.Tests.E2E;

/// <summary>Visual smoke test: screenshots of the main screens (desktop + phone) saved to %TEMP%\sat-shots.</summary>
[Collection("e2e")]
public class ScreenshotTests(BrowserFixture f)
{
    [Fact]
    public async Task Capture_main_screens()
    {
        if (!f.Available || Environment.GetEnvironmentVariable("SAT_SHOTS") != "1") return;
        var dir = Path.Combine(Path.GetTempPath(), "sat-shots");
        Directory.CreateDirectory(dir);
        var page = await f.NewPageAsync();
        await page.GoAsync("/login");
        await page.ScreenshotAsync(new() { Path = Path.Combine(dir, "01-login.png") });
        await page.LoginAsync("+998902000000", "Demo123!", "/dashboard");
        await page.WaitForSelectorAsync("text=Joriy SAT ball");
        await page.ScreenshotAsync(new() { Path = Path.Combine(dir, "02-dashboard.png"), FullPage = true });
        await page.GoAsync("/analytics"); await page.WaitForSelectorAsync(".tabs"); await page.WaitForTimeoutAsync(500);
        await page.ScreenshotAsync(new() { Path = Path.Combine(dir, "03-analytics.png"), FullPage = true });
        await page.GoAsync("/practice"); await page.WaitForSelectorAsync(".card");
        await page.ScreenshotAsync(new() { Path = Path.Combine(dir, "04-practice.png"), FullPage = true });
        await page.EnsureNoActiveAttemptAsync();
        await page.GoAsync("/tests");
        var card = page.Locator(".card", new() { HasText = "Diagnostik test" });
        await card.Locator("a:has-text('Testni boshlash')").ClickAsync();
        await page.WaitForURLAsync(u => u.Contains("/start"), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForSelectorAsync("button:has-text('Testni boshlash'):not([disabled])");
        await page.ScreenshotAsync(new() { Path = Path.Combine(dir, "05-pretest.png"), FullPage = true });
        await page.ClickAsync("button:has-text('Testni boshlash')");
        await page.WaitForURLAsync(u => u.Contains("/attempt/"), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForSelectorAsync(".q-content"); await page.WaitForTimeoutAsync(800);
        await page.ScreenshotAsync(new() { Path = Path.Combine(dir, "06-test.png") });
        await page.ClickAsync("footer button:has-text('Question')"); await page.WaitForTimeoutAsync(300);
        await page.ScreenshotAsync(new() { Path = Path.Combine(dir, "07-test-grid.png") });
        await page.EnsureNoActiveAttemptAsync();

        var phone = await f.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, IsMobile = true, HasTouch = true });
        var p = await phone.NewPageAsync();
        p.Dialog += async (_, d) => { try { await d.AcceptAsync(); } catch { } };
        await p.LoginAsync("+998902000000", "Demo123!", "/dashboard");
        await p.WaitForSelectorAsync("text=Joriy SAT ball");
        await p.ScreenshotAsync(new() { Path = Path.Combine(dir, "08-mobile-dashboard.png"), FullPage = true });
        await p.ClickAsync(".menu-btn"); await p.WaitForTimeoutAsync(300);
        await p.ScreenshotAsync(new() { Path = Path.Combine(dir, "09-mobile-menu.png") });

        var admin = await f.NewPageAsync();
        await admin.LoginAsync("+998900000000", "Admin123!", "/admin");
        await admin.WaitForSelectorAsync("text=Savollar banki");
        await admin.ScreenshotAsync(new() { Path = Path.Combine(dir, "10-admin.png"), FullPage = true });
        await admin.GoAsync("/admin/questions/1"); await admin.WaitForSelectorAsync(".ql-editor"); await admin.WaitForTimeoutAsync(800);
        await admin.ScreenshotAsync(new() { Path = Path.Combine(dir, "11-question-edit.png"), FullPage = true });
        await admin.GoAsync("/admin/tests/2"); await admin.WaitForSelectorAsync(".module-card");
        await admin.ScreenshotAsync(new() { Path = Path.Combine(dir, "12-test-builder.png"), FullPage = true });
        var teacher = await f.NewPageAsync();
        await teacher.LoginAsync("+998901111111", "Demo123!", "/teacher");
        await teacher.WaitForSelectorAsync("text=Mening guruhlarim");
        await teacher.ScreenshotAsync(new() { Path = Path.Combine(dir, "13-teacher.png"), FullPage = true });
    }
}

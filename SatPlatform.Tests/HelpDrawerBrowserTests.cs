using Microsoft.Playwright;
using Xunit;

namespace SatPlatform.Tests;

/// <summary>Logs in to a running app and opens the per-page guide (opt-in: SAT_BROWSER=1, SAT_URL=http://localhost:5100, SAT_SHOTS=folder for screenshots).</summary>
public class HelpDrawerBrowserTests
{
    [Theory]
    [InlineData("+998901111111", "/teacher/attendance", "Davomat")]
    [InlineData("+998901111110", "/admin/settings", "Sozlamalar")]
    [InlineData("+998902000000", "/dashboard", "Bosh sahifa")]
    [InlineData("+998903000000", "/parent", "Farzandim")]
    public async Task Guide_button_opens_this_pages_help(string phone, string path, string title)
    {
        if (Environment.GetEnvironmentVariable("SAT_BROWSER") != "1") return;
        var url = Environment.GetEnvironmentVariable("SAT_URL") ?? "http://localhost:5100";
        using var pw = await Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1280, Height = 800 } });

        await page.GotoAsync(url + "/login");
        await page.WaitForSelectorAsync("input[data-phone]");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForTimeoutAsync(1500);                                              // let the Blazor circuit connect before typing
        await page.FillAsync("input[data-phone]", "");
        await page.Locator("input[data-phone]").PressSequentiallyAsync(phone[4..]);     // typed without +998: the mask adds it
        Assert.Equal("+998 " + string.Join(' ', new[] { phone[4..6], phone[6..9], phone[9..11], phone[11..13] }), await page.InputValueAsync("input[data-phone]"));
        await page.FillAsync("input[type=password]", "demo123");
        await page.ClickAsync("button.btn.primary.lg");
        await page.WaitForSelectorAsync(".sidebar", new() { Timeout = 15000 });

        await page.GotoAsync(url + path);
        await page.WaitForSelectorAsync("html[data-ready='1']");                // circuit connected
        await page.WaitForSelectorAsync(".help-btn");
        await page.ClickAsync(".help-btn");
        var drawer = page.Locator(".help-drawer");
        await drawer.WaitForAsync();
        Assert.Contains(title, await drawer.Locator("h2").InnerTextAsync());
        Assert.True(await drawer.Locator(".help-section").CountAsync() >= 2);
        await page.WaitForTimeoutAsync(500);                                            // slide-in animation
        if (Environment.GetEnvironmentVariable("SAT_SHOTS") is { Length: > 0 } dir)
            await page.ScreenshotAsync(new() { Path = Path.Combine(dir, $"help{path.Replace('/', '-')}.png") });

        await page.Keyboard.PressAsync("Escape");
        await page.WaitForSelectorAsync(".help-drawer", new() { State = WaitForSelectorState.Detached });
    }
}

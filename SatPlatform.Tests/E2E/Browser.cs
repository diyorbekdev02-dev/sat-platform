using Microsoft.Playwright;
using Xunit;

namespace SatPlatform.Tests.E2E;

/// <summary>One Chromium instance per test class; the app must already be running at BaseUrl (SAT_E2E_URL).</summary>
public class BrowserFixture : IAsyncLifetime
{
    public static readonly string BaseUrl = Environment.GetEnvironmentVariable("SAT_E2E_URL") ?? "http://localhost:5100";
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    public bool Available { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            Available = (await http.GetAsync(BaseUrl + "/healthz")).IsSuccessStatusCode;
        }
        catch { Available = false; }
        if (!Available) return;
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    public async Task<IPage> NewPageAsync()
    {
        var ctx = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1366, Height = 900 }, Locale = "uz-UZ" });
        ctx.SetDefaultTimeout(20000);
        var page = await ctx.NewPageAsync();
        page.Console += (_, m) => { if (m.Type == "error") Console.WriteLine("[browser] " + m.Text); };
        page.Dialog += async (_, d) => { try { await d.AcceptAsync(); } catch { } };
        return page;
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        Playwright?.Dispose();
    }
}

[CollectionDefinition("e2e")]
public class E2ECollection : ICollectionFixture<BrowserFixture>;

public static class PageExt
{
    /// <summary>Navigates and waits until the Blazor circuit is interactive (the layouts set html[data-ready]).</summary>
    public static async Task GoAsync(this IPage page, string path)
    {
        await page.GotoAsync(BrowserFixture.BaseUrl + path, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForSelectorAsync("html[data-ready]", new() { State = WaitForSelectorState.Attached, Timeout = 30000 });
    }

    public static async Task LoginAsync(this IPage page, string phone, string password, string expectedPath)
    {
        await page.GoAsync("/login");
        await page.FillAsync("input.phone-input", phone);
        await page.FillAsync("input[type=password]", password);
        await page.ClickAsync("button.btn.primary:has-text('Kirish')");
        await page.WaitForURLAsync(u => u.Contains(expectedPath), new() { Timeout = 30000, WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForSelectorAsync("html[data-ready]", new() { State = WaitForSelectorState.Attached, Timeout = 30000 });
    }

    public static async Task<string> Title(this IPage page) => (await page.TextContentAsync("#page-title"))?.Trim() ?? "";

    /// <summary>Abandons whatever test attempt the logged-in student has in progress (tests are order-independent).</summary>
    public static async Task EnsureNoActiveAttemptAsync(this IPage page)
    {
        for (var i = 0; i < 3; i++)
        {
            await page.GoAsync("/tests");
            await page.WaitForSelectorAsync(".card, .empty");
            var cont = page.Locator("a:has-text('Davom ettirish')");
            if (await cont.CountAsync() == 0) return;
            await cont.First.ClickAsync();
            await page.WaitForURLAsync(u => u.Contains("/attempt/"), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.WaitForSelectorAsync(".test-header, .break-screen, .center-box");
            if (await page.Locator(".test-header button:has-text('Tools')").CountAsync() > 0)
            {
                await page.ClickAsync(".test-header button:has-text('Tools')");
                await page.ClickAsync(".tools-menu button:has-text('Testdan chiqish')");
                await page.WaitForURLAsync(u => u.EndsWith("/tests"), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            }
            else if (await page.Locator("button:has-text('Tanaffusni tugatish')").CountAsync() > 0)
            {
                await page.ClickAsync("button:has-text('Tanaffusni tugatish')");
                await page.WaitForSelectorAsync(".test-header button:has-text('Tools')");
                await page.ClickAsync(".test-header button:has-text('Tools')");
                await page.ClickAsync(".tools-menu button:has-text('Testdan chiqish')");
                await page.WaitForURLAsync(u => u.EndsWith("/tests"), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            }
        }
    }
}

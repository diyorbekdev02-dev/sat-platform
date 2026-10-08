using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace SatPlatform.Tests.E2E;

/// <summary>Diagnostics (opt-in with SAT_DIAG=1): print what a page shows instead of asserting.</summary>
[Collection("e2e")]
public class DiagTests(BrowserFixture f, ITestOutputHelper output)
{
    private static string Phone => Environment.GetEnvironmentVariable("SAT_DIAG_PHONE") ?? "+998902000002";
    private static string Password => Environment.GetEnvironmentVariable("SAT_E2E_PASSWORD") ?? "Demo123!";
    private static string Short(string s) => s.Replace("\n", " | ").Substring(0, Math.Min(400, s.Length));

    [Fact]
    public async Task Login_page_diagnostics()
    {
        if (!f.Available) return;
        var page = await f.NewPageAsync();
        await page.GoAsync("/login");
        output.WriteLine("URL: " + page.Url);
        output.WriteLine("window.Blazor: " + await page.EvaluateAsync<bool>("() => !!window.Blazor"));
    }

    [Fact]
    public async Task Test_start_diagnostics()
    {
        if (!f.Available || Environment.GetEnvironmentVariable("SAT_DIAG") != "1") return;
        var page = await f.NewPageAsync();
        page.Console += (_, m) => output.WriteLine($"console[{m.Type}] {m.Text}");
        await page.LoginAsync(Phone, Password, "/dashboard");
        await page.EnsureNoActiveAttemptAsync();
        await page.GoAsync("/tests");
        var card = page.Locator(".card", new() { HasText = "Math Section Test" });
        output.WriteLine("card text: " + Short(await card.InnerTextAsync()));
        await card.Locator("a:has-text('Testni boshlash')").ClickAsync();
        await page.WaitForURLAsync(u => u.Contains("/start"), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForSelectorAsync("button:has-text('Testni boshlash'):not([disabled])");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await page.ClickAsync("button:has-text('Testni boshlash')");
        for (var i = 0; i < 30; i++)
        {
            await page.WaitForTimeoutAsync(1000);
            if (await page.Locator(".test-header").CountAsync() > 0) { output.WriteLine($"test header after {sw.Elapsed.TotalSeconds:0.0}s"); break; }
            if (i % 5 == 4) output.WriteLine($"{sw.Elapsed.TotalSeconds:0}s url={page.Url} body={Short(await page.InnerTextAsync("body"))}");
        }
        await page.ScreenshotAsync(new() { Path = Path.Combine(Path.GetTempPath(), "sat-diag-start.png") });
        await page.EnsureNoActiveAttemptAsync();
    }

    [Fact]
    public async Task Practice_spr_diagnostics()
    {
        if (!f.Available || Environment.GetEnvironmentVariable("SAT_DIAG") != "1") return;
        var page = await f.NewPageAsync();
        page.Console += (_, m) => output.WriteLine($"console[{m.Type}] {m.Text}");
        await page.LoginAsync(Phone, Password, "/dashboard");
        await page.GoAsync("/practice/new?section=1&count=5");
        if (Environment.GetEnvironmentVariable("SAT_DIAG_DOMAIN") != "none") await page.ClickAsync(".chips button:has-text('" + (Environment.GetEnvironmentVariable("SAT_DIAG_DOMAIN") ?? "Problem-Solving") + "')");
        if (Environment.GetEnvironmentVariable("SAT_DIAG_DOMAIN") != "none") await page.ClickAsync(".chips button:has-text('" + (Environment.GetEnvironmentVariable("SAT_DIAG_SKILL") ?? "Probability") + "')");
        await page.ClickAsync("button:has-text('Mashqni boshlash')");
        await page.WaitForURLAsync(u => System.Text.RegularExpressions.Regex.IsMatch(u, @"/practice/\d+$"), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForSelectorAsync(".q-content");
        output.WriteLine("url " + page.Url);
        for (var k = 0; k < 4 && await page.Locator(".spr input").CountAsync() == 0; k++) { await page.ClickAsync("footer button:has-text('Keyingi')"); await page.WaitForTimeoutAsync(600); }
        output.WriteLine("options: " + await page.Locator(".option").CountAsync() + " spr: " + await page.Locator(".spr input").CountAsync());
        if (await page.Locator(".spr input").CountAsync() > 0)
        {
            await page.FillAsync(".spr input", Environment.GetEnvironmentVariable("SAT_DIAG_ANSWER") ?? "1");
            if (Environment.GetEnvironmentVariable("SAT_DIAG_KEY") == "none") { } else await page.Keyboard.PressAsync(Environment.GetEnvironmentVariable("SAT_DIAG_KEY") ?? "Tab");
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            while (sw2.Elapsed.TotalSeconds < 25 && await page.Locator("button:has-text('Javobni tasdiqlash')").GetAttributeAsync("disabled") is not null) await page.WaitForTimeoutAsync(250);
            output.WriteLine($"enabled after {sw2.Elapsed.TotalSeconds:0.0}s");
            output.WriteLine("after blur: value=" + await page.InputValueAsync(".spr input") + " btn-disabled=" + await page.Locator("button:has-text('Javobni tasdiqlash')").GetAttributeAsync("disabled") + " err=" + await page.Locator(".spr .error").CountAsync());
            output.WriteLine("panel: " + Short(await page.InnerTextAsync("#question-panel")));
        }
        else
        {
            await page.ClickAsync(".option >> nth=0");
            await page.WaitForTimeoutAsync(3000);
            output.WriteLine("after click: btn count=" + await page.Locator("button:has-text('Javobni tasdiqlash')").CountAsync() + " selected=" + await page.Locator(".option.selected").CountAsync());
            output.WriteLine("panel: " + Short(await page.InnerTextAsync("#question-panel")));
        }
        await page.ScreenshotAsync(new() { Path = Path.Combine(Path.GetTempPath(), "sat-diag-practice.png") });
    }
}

using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace SatPlatform.Tests.E2E;

[Collection("e2e")]
public class DiagTests(BrowserFixture f, ITestOutputHelper output)
{
    [Fact]
    public async Task Login_page_diagnostics()
    {
        if (!f.Available) return;
        var page = await f.NewPageAsync();
        var failed = new List<string>();
        page.RequestFailed += (_, r) => failed.Add(r.Url + " :: " + r.Failure);
        page.Console += (_, m) => output.WriteLine($"console[{m.Type}] {m.Text}");
        try { await page.GotoAsync(BrowserFixture.BaseUrl + "/login", new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 }); }
        catch (Exception ex) { output.WriteLine("goto: " + ex.Message); }
        await page.WaitForTimeoutAsync(8000);
        output.WriteLine("URL: " + page.Url);
        output.WriteLine("failed requests: " + string.Join("\n", failed));
        var html = await page.ContentAsync();
        output.WriteLine("HTML length " + html.Length);
        output.WriteLine(html.Length > 3000 ? html[..3000] : html);
        var hasBlazor = await page.EvaluateAsync<bool>("() => !!window.Blazor");
        output.WriteLine("window.Blazor: " + hasBlazor);
        await page.ScreenshotAsync(new() { Path = Path.Combine(Path.GetTempPath(), "sat-login.png") });
    }
}

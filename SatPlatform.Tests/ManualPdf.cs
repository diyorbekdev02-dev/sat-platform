using Microsoft.Playwright;
using Xunit;

namespace SatPlatform.Tests;

/// <summary>Renders docs/qollanma.html into SatPlatform/wwwroot/qollanma.pdf (opt-in: SAT_MANUAL=1).</summary>
public class ManualPdf
{
    [Fact]
    public async Task Generate_manual_pdf()
    {
        if (Environment.GetEnvironmentVariable("SAT_MANUAL") != "1") return;
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var html = Path.Combine(root, "docs", "qollanma.html");
        var pdf = Path.Combine(root, "SatPlatform", "wwwroot", "qollanma.pdf");
        Assert.True(File.Exists(html), html);
        using var pw = await Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(new Uri(html).AbsoluteUri, new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.PdfAsync(new()
        {
            Path = pdf, Format = "A4", PrintBackground = true, PreferCSSPageSize = true, DisplayHeaderFooter = true,
            HeaderTemplate = "<div></div>",
            FooterTemplate = "<div style=\"width:100%;font-size:8px;color:#5f6b85;text-align:center;font-family:Arial\">SAT Platform — Foydalanuvchi qo'llanmasi · <span class=\"pageNumber\"></span> / <span class=\"totalPages\"></span></div>"
        });
        Assert.True(new FileInfo(pdf).Length > 50_000);
    }
}

public class ManualPreview
{
    [Fact]
    public async Task Screenshot_manual_html()
    {
        if (Environment.GetEnvironmentVariable("SAT_MANUAL") != "1") return;
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        using var pw = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 800, Height = 1100 } });
        await page.GotoAsync(new Uri(Path.Combine(root, "docs", "qollanma.html")).AbsoluteUri, new() { WaitUntil = Microsoft.Playwright.WaitUntilState.NetworkIdle });
        await page.EmulateMediaAsync(new() { Media = Microsoft.Playwright.Media.Print });
        await page.EvaluateAsync("() => window.scrollTo(0, 2600)");
        await page.ScreenshotAsync(new() { Path = Path.Combine(Path.GetTempPath(), "sat-manual-1.png") });
        await page.EvaluateAsync("() => window.scrollTo(0, 9000)");
        await page.ScreenshotAsync(new() { Path = Path.Combine(Path.GetTempPath(), "sat-manual-2.png") });
    }
}

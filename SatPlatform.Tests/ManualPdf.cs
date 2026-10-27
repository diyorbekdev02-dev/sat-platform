using Microsoft.Playwright;
using Xunit;

namespace SatPlatform.Tests;

/// <summary>Renders docs/qollanma.html into one PDF per role: SatPlatform/wwwroot/qollanma-{student,teacher,admin,parent}.pdf (opt-in: SAT_MANUAL=1).</summary>
public class ManualPdf
{
    private static readonly (string Role, string Title)[] Roles =
    [
        ("student", "O'quvchi uchun qo'llanma"),
        ("teacher", "O'qituvchi va assistent uchun qo'llanma"),
        ("admin", "Administrator uchun qo'llanma"),
        ("parent", "Ota-ona uchun qo'llanma"),
    ];

    // Keeps the cover, the common sections and the one section whose data-for equals the role; renumbers headings and rebuilds the contents list.
    private const string Filter = @"([role, title]) => {
        document.getElementById('roles').textContent = title;
        let keep = true;
        for (const el of [...document.body.children]) {
            if (el.classList.contains('cover')) { keep = true; continue; }
            if (el.tagName === 'H2') keep = !el.dataset.for || el.dataset.for === role;
            if (!keep) el.remove();
        }
        const heads = [...document.body.querySelectorAll('h2')].filter(h => h.id !== 'toc-title');
        const titles = heads.map(h => h.textContent.replace(/^\d+\.\s*/, ''));
        heads.forEach((h, i) => h.textContent = (i + 1) + '. ' + titles[i]);
        document.querySelector('ol.toc').innerHTML = titles.map(t => '<li>' + t + '</li>').join('');
    }";

    [Fact]
    public async Task Generate_manual_pdf()
    {
        if (Environment.GetEnvironmentVariable("SAT_MANUAL") != "1") return;
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var html = Path.Combine(root, "docs", "qollanma.html");
        Assert.True(File.Exists(html), html);
        using var pw = await Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
        foreach (var (role, title) in Roles)
        {
            var pdf = Path.Combine(root, "SatPlatform", "wwwroot", $"qollanma-{role}.pdf");
            var page = await browser.NewPageAsync();
            await page.GotoAsync(new Uri(html).AbsoluteUri, new() { WaitUntil = WaitUntilState.NetworkIdle });
            await page.EvaluateAsync(Filter, new[] { role, title });
            var heads = await page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('h2')].map(h => h.textContent)");
            // exactly one role-specific chapter ("... uchun") survives, and it is this role's
            var roleHeads = heads.Where(h => h.EndsWith(" uchun")).ToArray();
            Assert.Single(roleHeads);
            Assert.StartsWith(title.Split(" uchun")[0], roleHeads[0].Substring(roleHeads[0].IndexOf(' ') + 1));
            Assert.Equal(role == "admin", heads.Any(h => h.Contains("Demo hisoblar")));
            await page.PdfAsync(new()
            {
                Path = pdf, Format = "A4", PrintBackground = true, PreferCSSPageSize = true, DisplayHeaderFooter = true,
                HeaderTemplate = "<div></div>",
                FooterTemplate = "<div style=\"width:100%;font-size:8px;color:#5f6b85;text-align:center;font-family:Arial\">SAT Platform — " + title + " · <span class=\"pageNumber\"></span> / <span class=\"totalPages\"></span></div>"
            });
            await page.CloseAsync();
            Assert.True(new FileInfo(pdf).Length > 30_000, pdf);
        }
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

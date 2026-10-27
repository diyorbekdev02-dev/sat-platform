using Microsoft.Playwright;
using Xunit;

namespace SatPlatform.Tests;

/// <summary>Types into a real &lt;input data-phone&gt; with the shipped app.js (opt-in: SAT_BROWSER=1).</summary>
public class PhoneMaskBrowserTests
{
    [Theory]
    [InlineData("901234567", "+998 90 123 45 67")]
    [InlineData("90123", "+998 90 123")]
    [InlineData("90 123-45-67 99", "+998 90 123 45 67")]      // separators ignored, more than 9 digits cut
    public async Task Typing_is_formatted(string typed, string shown)
    {
        if (Environment.GetEnvironmentVariable("SAT_BROWSER") != "1") return;
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        using var pw = await Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='p' data-phone value='+998 '>");
        await page.AddScriptTagAsync(new() { Path = Path.Combine(root, "SatPlatform", "wwwroot", "app.js") });
        await page.FocusAsync("#p");
        await page.EvaluateAsync("() => { const e = document.getElementById('p'); e.setSelectionRange(e.value.length, e.value.length); }");
        await page.Keyboard.TypeAsync(typed);
        Assert.Equal(shown, await page.InputValueAsync("#p"));
    }

    [Theory]
    [InlineData("0901234567")]
    [InlineData("901234567")]
    [InlineData("998901234567")]
    [InlineData("+998901234567")]
    [InlineData("+998 (90) 123-45-67")]
    public async Task Pasted_number_in_any_format_is_normalised(string pasted)
    {
        if (Environment.GetEnvironmentVariable("SAT_BROWSER") != "1") return;
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        using var pw = await Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='p' data-phone value='+998 '>");
        await page.AddScriptTagAsync(new() { Path = Path.Combine(root, "SatPlatform", "wwwroot", "app.js") });
        await page.EvaluateAsync("(v) => { const e = document.getElementById('p'); e.value = v; e.dispatchEvent(new Event('input', { bubbles: true })); }", pasted);
        Assert.Equal("+998 90 123 45 67", await page.InputValueAsync("#p"));
    }

    [Fact]
    public async Task Prefix_cannot_be_erased()
    {
        if (Environment.GetEnvironmentVariable("SAT_BROWSER") != "1") return;
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        using var pw = await Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='p' data-phone value='+998 90 1'>");
        await page.AddScriptTagAsync(new() { Path = Path.Combine(root, "SatPlatform", "wwwroot", "app.js") });
        await page.FocusAsync("#p");
        await page.EvaluateAsync("() => { const e = document.getElementById('p'); e.setSelectionRange(e.value.length, e.value.length); }");
        for (var i = 0; i < 12; i++) await page.Keyboard.PressAsync("Backspace");
        Assert.Equal("+998 ", await page.InputValueAsync("#p"));
    }
}

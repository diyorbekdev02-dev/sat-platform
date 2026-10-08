using Microsoft.Playwright;
using Xunit;

namespace SatPlatform.Tests.E2E;

[Collection("e2e")]
public class SprPracticeTests(BrowserFixture f)
{
    private static readonly string Password = Environment.GetEnvironmentVariable("SAT_E2E_PASSWORD") ?? "Demo123!";

    [Fact]
    public async Task Spr_questions_can_be_answered_in_practice()
    {
        if (!f.Available) return;
        var page = await f.NewPageAsync();
        await page.LoginAsync("+998902000001", Password, "/dashboard");
        // Probability questions are all SPR in the demo bank
        await page.GoAsync("/practice/new?section=1&count=3");
        await page.ClickAsync(".chips button:has-text('Problem-Solving')");
        await page.ClickAsync(".chips button:has-text('Probability')");
        await page.ClickAsync("button:has-text('Mashqni boshlash')");
        await page.WaitForURLAsync(u => System.Text.RegularExpressions.Regex.IsMatch(u, @"/practice/\d+$"), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        for (var i = 0; i < 3; i++)
        {
            await page.WaitForSelectorAsync(".spr input");
            await page.FillAsync(".spr input", "1/2");
            await page.Keyboard.PressAsync("Tab");
            await page.ClickAsync("button:has-text('Javobni tasdiqlash')");
            await page.WaitForSelectorAsync(".feedback");
            if (i < 2) await page.ClickAsync("button:has-text('Keyingi')");
        }
        await page.ClickAsync("footer button:has-text('Yakunlash')");
        await page.WaitForURLAsync(u => u.EndsWith("/result"), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForSelectorAsync("text=MASHQ YAKUNLANDI");
    }
}

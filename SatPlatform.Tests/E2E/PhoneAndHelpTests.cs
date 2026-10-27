using Microsoft.Playwright;
using Xunit;

namespace SatPlatform.Tests.E2E;

[Collection("e2e")]
public class PhoneMaskTests(BrowserFixture f)
{
    private const string Input = "input[data-phone]";

    private async Task<IPage> OpenLoginAsync()
    {
        var page = await f.NewPageAsync();
        await page.GoAsync("/login");
        await page.FocusAsync(Input);
        await page.Keyboard.PressAsync("End");
        return page;
    }

    [Theory]
    [InlineData("901234567", "+998 90 123 45 67")]
    [InlineData("90123", "+998 90 123")]
    [InlineData("90 123-45-67 99", "+998 90 123 45 67")]      // separators ignored, more than 9 digits cut
    public async Task Typing_is_formatted(string typed, string shown)
    {
        if (!f.Available) return;
        var page = await OpenLoginAsync();
        await page.Keyboard.TypeAsync(typed);
        Assert.Equal(shown, await page.InputValueAsync(Input));
    }

    [Theory]
    [InlineData("0901234567")]
    [InlineData("901234567")]
    [InlineData("998901234567")]
    [InlineData("+998901234567")]
    [InlineData("+998 (90) 123-45-67")]
    public async Task Pasted_number_in_any_format_is_normalised(string pasted)
    {
        if (!f.Available) return;
        var page = await OpenLoginAsync();
        await page.FillAsync(Input, pasted);                 // Fill = replace the whole value + input event, like a paste
        Assert.Equal("+998 90 123 45 67", await page.InputValueAsync(Input));
    }

    [Fact]
    public async Task Prefix_cannot_be_erased()
    {
        if (!f.Available) return;
        var page = await OpenLoginAsync();
        await page.Keyboard.TypeAsync("901");
        for (var i = 0; i < 12; i++) await page.Keyboard.PressAsync("Backspace");
        Assert.Equal("+998 ", await page.InputValueAsync(Input));
    }
}

[Collection("e2e")]
public class HelpDrawerTests(BrowserFixture f)
{
    private static readonly string Password = Environment.GetEnvironmentVariable("SAT_E2E_PASSWORD") ?? "Demo123!";

    [Theory]
    [InlineData("+998901111111", "/teacher", "/teacher/attendance", "Davomat")]
    [InlineData("+998901111110", "/admin", "/admin/settings", "Sozlamalar")]
    [InlineData("+998902000000", "/dashboard", "/dashboard", "Bosh sahifa")]
    [InlineData("+998903000000", "/parent", "/parent", "Farzandim")]
    public async Task Guide_button_opens_this_pages_help(string phone, string home, string path, string title)
    {
        if (!f.Available) return;
        var page = await f.NewPageAsync();
        await page.LoginAsync(phone, Password, home);
        await page.GoAsync(path);
        await page.ClickAsync(".help-btn");

        var drawer = page.Locator(".help-drawer");
        await drawer.WaitForAsync();
        Assert.Contains(title, await drawer.Locator("h2").InnerTextAsync());
        Assert.True(await drawer.Locator(".help-section").CountAsync() >= 2);

        await page.Keyboard.PressAsync("Escape");
        await page.WaitForSelectorAsync(".help-drawer", new() { State = WaitForSelectorState.Detached });
    }
}

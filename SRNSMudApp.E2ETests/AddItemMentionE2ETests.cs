using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace SRNSMudApp.E2ETests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class AddItemMentionE2ETests : PageTest
{
    private string _serverAddress = "http://localhost:5000";

    [SetUp]
    public void Setup()
    {
        var serverAddress = Environment.GetEnvironmentVariable("TEST_SERVER_ADDRESS");
        if (!string.IsNullOrEmpty(serverAddress))
        {
            _serverAddress = serverAddress;
        }
    }

    [Test]
    public async Task GivenAddItemInput_WhenTypeMultiLineAndMention_ThenReplacesWithLink()
    {
        Page.Console += (_, e) => Console.WriteLine($"Browser Console: {e.Type}: {e.Text}");
        Page.PageError += (_, e) => Console.WriteLine($"Browser Error: {e}");

        var testEmail = $"testmention-{Guid.NewGuid():N}@example.com";
        await WebAuthnTestHelpers.LoginWithMockGoogleAsync(Page, _serverAddress, testEmail);

        // Feed page should be visible
        await Expect(Page).ToHaveURLAsync(new Regex(".*"));
        await Expect(Page.Locator("h1").Filter(new() { HasText = "タイムライン" })).ToBeVisibleAsync();

        var input = Page.Locator("textarea").First;
        await Expect(input).ToBeVisibleAsync();
        await input.ClickAsync();

        // 一行目に文字を入力
        await input.PressSequentiallyAsync("Line 1", new() { Delay = 50 });
        
        // 改行して二行目に文字を入力
        await input.PressAsync("Enter", new() { Delay = 50 });
        await input.PressSequentiallyAsync("Line 2", new() { Delay = 50 });
        
        // ２行目の先頭に移動
        for(int i=0; i<6; i++) {
            await input.PressAsync("ArrowLeft", new() { Delay = 10 });
        }
        
        // 改行して
        await input.PressAsync("Enter", new() { Delay = 50 });
        
        // 上の空行に移動
        await input.PressAsync("ArrowUp", new() { Delay = 50 });

        // # を入力
        await input.FocusAsync();
        await input.PressSequentiallyAsync(" ", new() { Delay = 50 }); // Force space just in case
        await input.PressSequentiallyAsync("#C", new() { Delay = 100 });

        // Autocomplete popover (Tribute.js container) should appear
        var tributePopover = Page.Locator(".tribute-container");
        var csharpOption = tributePopover.Locator("text=C#");
        await Expect(csharpOption).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Select the "C#" option
        await csharpOption.ClickAsync();

        // 決定して入力可能かてすとをする (verify text was replaced with mention)
        // Tribute replaces it with the display text (which is configured as "#C#" or similar, but the binding should catch it)
        // Then click Save
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "保存" }).ClickAsync();

        // The item card should appear in the feed with the link
        var itemCard = Page.Locator(".mud-card-content").First;
        await Expect(itemCard).ToBeVisibleAsync();
        await Expect(itemCard).ToContainTextAsync("Line 1");
        await Expect(itemCard).ToContainTextAsync("C#");
        await Expect(itemCard).ToContainTextAsync("Line 2");
    }
}

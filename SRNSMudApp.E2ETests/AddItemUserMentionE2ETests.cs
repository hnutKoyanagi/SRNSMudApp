using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

using NUnit.Framework;

namespace SRNSMudApp.E2ETests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class AddItemUserMentionE2ETests : PageTest
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
    public async Task GivenAddItemInput_WhenTypeMultiLineAndUserMention_ThenReplacesWithLink()
    {
        Page.Console += (_, e) => Console.WriteLine($"Browser Console: {e.Type}: {e.Text}");
        Page.PageError += (_, e) => Console.WriteLine($"Browser Error: {e}");

        var testEmail = $"testusermention-{Guid.NewGuid():N}@example.com";
        await WebAuthnTestHelpers.LoginWithMockGoogleAsync(Page, _serverAddress, testEmail);

        // Feed page should be visible
        await Expect(Page).ToHaveURLAsync(new Regex(".*"));
        await Expect(Page.Locator("h1").Filter(new() { HasText = "タイムライン" })).ToBeVisibleAsync();

        var input = Page.Locator("textarea").First;
        await Expect(input).ToBeVisibleAsync();
        await input.ClickAsync();

        // Type @ mention
        await input.PressSequentiallyAsync("Hello @", new() { Delay = 50 });

        // Autocomplete popover (Tribute.js container) should appear
        var tributePopover = Page.Locator(".tribute-container");

        // At least one user option should be visible (current user or mock users)
        var userOption = tributePopover.Locator("li").First;
        await Expect(userOption).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Select the option
        await userOption.ClickAsync();

        // Click Save
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "保存" }).ClickAsync();

        // The item card should appear in the feed with the link
        var itemCard = Page.Locator(".mud-card-content").First;
        await Expect(itemCard).ToBeVisibleAsync();
        await Expect(itemCard).ToContainTextAsync("Hello");
    }
}
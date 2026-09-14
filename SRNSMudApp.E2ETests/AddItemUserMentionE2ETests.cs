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

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _serverAddress = SharedTestServerFixture.ServerAddress;
    }

    [Test]
    public async Task GivenAddItemInput_WhenTypeMultiLineAndUserMention_ThenReplacesWithLink()
    {
        Page.Console += (_, e) => Console.WriteLine($"Browser Console: {e.Type}: {e.Text}");
        Page.PageError += (_, e) => Console.WriteLine($"Browser Error: {e}");

        var testEmail = $"testusermention-{Guid.NewGuid():N}@example.com";
        await WebAuthnTestHelpers.LoginWithMockGoogleAsync(Page, _serverAddress, testEmail);

        // Feed page should be visible
        await Expect(Page).ToHaveURLAsync(new Regex(@"^" + Regex.Escape(_serverAddress) + @"/?$"));
        await Expect(Page.Locator("h5").Filter(new() { HasText = "タイムライン" })).ToBeVisibleAsync();

        // Navigate to Item List to find the textarea
        await Page.GotoAsync($"{_serverAddress}/Item/ItemList");

        var input = Page.Locator("#add-item-textarea");
        await Expect(input).ToBeVisibleAsync();
        await input.ClickAsync(new LocatorClickOptions { Force = true });

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
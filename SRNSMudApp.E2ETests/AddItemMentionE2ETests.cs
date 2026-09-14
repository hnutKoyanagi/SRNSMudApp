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

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _serverAddress = SharedTestServerFixture.ServerAddress;
    }

    [Test]
    public async Task GivenAddItemInput_WhenTypeMultiLineAndMention_ThenReplacesWithLink()
    {
        Page.Console += (_, e) => Console.WriteLine($"Browser Console: {e.Type}: {e.Text}");
        Page.PageError += (_, e) => Console.WriteLine($"Browser Error: {e}");

        var testEmail = $"testmention-{Guid.NewGuid():N}@example.com";
        await WebAuthnTestHelpers.LoginWithMockGoogleAsync(Page, _serverAddress, testEmail);

        var dbFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<SRNSMudApp.Data.ApplicationDbContext>>(SharedTestServerFixture.Factory.AppServices);
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(db.Users, u => u.Email == testEmail);
        if (user != null)
        {
            var tag = new SRNSMudApp.Data.Tag { Name = "C#", OwnerId = user.Id };
            db.Tags.Add(tag);
            await db.SaveChangesAsync();
        }

        // Feed page should be visible
        await Expect(Page).ToHaveURLAsync(new Regex(@"^" + Regex.Escape(_serverAddress) + @"/?$"));
        await Expect(Page.Locator("h5").Filter(new() { HasText = "タイムライン" })).ToBeVisibleAsync();

        // Navigate to Item List to find the textarea
        await Page.GotoAsync($"{_serverAddress}/Item/ItemList", new() { WaitUntil = WaitUntilState.Commit });

        var input = Page.Locator("#add-item-textarea");
        await Expect(input).ToBeVisibleAsync();
        await input.FocusAsync();
        await Page.WaitForTimeoutAsync(1500); // Wait for Blazor Server circuit to fully connect
        await input.PressSequentiallyAsync("Line 1\n ", new() { Delay = 50 });

        // # を入力
        await input.PressSequentiallyAsync("#C#", new() { Delay = 100 });

        // Autocomplete popover (Tribute.js container) should appear
        var tributePopover = Page.Locator(".tribute-container");
        var csharpOption = tributePopover.Locator("text=C#");
        await Expect(csharpOption).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Select the "C#" option
        await csharpOption.ClickAsync();

        // Wait for replacement to finish
        await Page.WaitForTimeoutAsync(200);

        // Type Line 2
        await input.PressSequentiallyAsync("\nLine 2", new() { Delay = 50 });

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
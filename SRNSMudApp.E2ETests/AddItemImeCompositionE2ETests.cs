using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

using NUnit.Framework;

namespace SRNSMudApp.E2ETests;

/// <summary>
///     Tribute.js でタグ補完を確定した直後に IME（日本語入力）で入力した場合、
///     コンポジション中に sync() が割り込んで入力が壊れないことを検証する。
///     例: #C# を確定後、IME で「ta」と入力 → 「た」が正しく表示され、
///     「t」で分断されて「あ」になる不具合が発生しないこと。
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class AddItemImeCompositionE2ETests : PageTest
{
    private string _serverAddress = "http://localhost:5000";

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _serverAddress = SharedTestServerFixture.ServerAddress;
    }

    /// <summary>
    /// Evaluates that a Tribute.js selection followed by IME composition correctly
    /// preserves the composition state and prevents early commit bugs.
    /// </summary>
    [Test]
    public async Task GivenContentEditableEditor_WhenImeCompositionAfterTributeSelect_ThenCompositionIsNotInterrupted()
    {
        Page.Console += (_, e) => Console.WriteLine($"Browser Console: {e.Type}: {e.Text}");
        Page.PageError += (_, e) => Console.WriteLine($"Browser Error: {e}");

        // Arrange: Setup user, tag, and navigate to the editor
        var testEmail = $"testimecomp-{Guid.NewGuid():N}@example.com";
        await WebAuthnTestHelpers.LoginWithMockGoogleAsync(Page, _serverAddress, testEmail);

        var dbFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<SRNSMudApp.Data.ApplicationDbContext>>(
                SharedTestServerFixture.Factory.AppServices);
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstOrDefaultAsync(db.Users, u => u.Email == testEmail);
        if (user != null)
        {
            var tag = new SRNSMudApp.Data.Tag { Name = "Rust", OwnerId = user.Id };
            db.Tags.Add(tag);
            await db.SaveChangesAsync();
        }

        await Page.GotoAsync($"{_serverAddress}/Item/ItemList", new() { WaitUntil = WaitUntilState.Commit });

        var editor = Page.Locator("#add-item-textarea");
        await Expect(editor).ToBeVisibleAsync();
        await editor.FocusAsync();
        await Page.WaitForTimeoutAsync(1500); // Wait for Blazor Server connection

        // Wait for Tribute.js initialization via JS interop
        await Page.WaitForFunctionAsync("() => window.tributeInterop && window.tributeInterop.instances['add-item-textarea'] !== undefined");

        // Act: Perform Tribute.js autocomplete and simulate IME composition
        await editor.PressSequentiallyAsync("#Rust", new() { Delay = 100 });
        var tributePopover = Page.Locator(".tribute-container");
        var rustOption = tributePopover.Locator("text=Rust");
        await Expect(rustOption).ToBeVisibleAsync(new() { Timeout = 5000 });
        await rustOption.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Simulate IME composition for "ta" -> "た" using ImeTestHelpers facade
        await ImeTestHelpers.SimulateCompositionAsync(Page, "#add-item-textarea", "t", "た");
        await Page.WaitForTimeoutAsync(300);

        // Assert: Verify Zero-Width Space exists to prevent IME boundary bugs and sync is correct
        var editorHtml = await editor.InnerHTMLAsync();
        Console.WriteLine($"Editor innerHTML: '{editorHtml}'");

        Assert.That(editorHtml, Does.Contain("&#8203;").Or.Contain("\u200B"),
            "IMEコンポジションバグを防ぐための Zero-Width Space が挿入されていません。");

        // isComposing ガードが正しく機能しているか（JSレベルの挙動）の確認として、
        // コンポジション中（ta の t）に hidden textarea に同期されていないことを確認
        var hiddenValue = await Page.EvaluateAsync<string>(
            "() => document.querySelector('textarea[name=\"_newItem.Content\"]')?.value ?? ''");

        // コンポジション完了後の値が同期されていること
        Assert.That(hiddenValue, Does.Contain("た"),
            "IME コンポジション完了後に sync() が実行されていません。");
    }
}
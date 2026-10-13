#region

using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

#endregion

namespace SRNSMudApp.E2ETests;

/// <summary>
///     Blazor InteractiveServer (SignalR) 接続下での主要ページのボタン操作・MudDialog ポップアップ・タブ切り替えを
///     実ブラウザ (Playwright) 上で検証する E2E テスト。
/// </summary>
[TestFixture]
public class InteractivePageDialogE2ETests : PageTest
{
    private string _serverAddress = "";

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _serverAddress = SharedTestServerFixture.ServerAddress;
    }

    [Test]
    public async Task UserGroupList_ClickCreateButton_OpensDialog_AndCreatesGroup()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var email = $"e2e_ug_{uniqueId}@example.com";

        await WebAuthnTestHelpers.LoginWithMockGoogleAsync(Page, _serverAddress, email);

        _ = await Page.GotoAsync($"{_serverAddress}/UserGroup/List");

        // 初回表示（空状態）の確認
        await Expect(Page.GetByText("グループがまだありません"))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10000 });

        // 「グループを作成」ボタンをクリックし、SignalR 経由で MudDialog が開くまで待機
        var createButton = Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "グループを作成" });
        await Expect(createButton).ToBeVisibleAsync();

        // InteractiveServer 回線確立直後のクリック取りこぼしを防ぐため、ダイアログ表示をリトライ確認
        var dialogTitle = Page.GetByText("新規グループ作成").Last;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            await createButton.ClickAsync();
            try
            {
                await Expect(Page.GetByLabel("グループ名"))
                    .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 2000 });
                break;
            }
            catch when (attempt < 4)
            {
                await Task.Delay(500);
            }
        }

        await Expect(dialogTitle).ToBeVisibleAsync();

        // グループ名を入力して保存
        var groupName = $"Team_{uniqueId}";
        var nameInput = Page.GetByLabel("グループ名");
        await nameInput.FillAsync(groupName);
        await nameInput.BlurAsync();

        var saveButton = Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "保存" });
        await Expect(saveButton).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = 5000 });
        await saveButton.ClickAsync();

        // 作成されたグループが一覧およびスナックバーに表示されることを確認
        await Expect(Page.GetByText(groupName, new PageGetByTextOptions { Exact = true }))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10000 });
        await Expect(Page.GetByText($"グループ「{groupName}」を作成しました。"))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10000 });
    }

    [Test]
    public async Task ContractManagement_SwitchesTabsInteractively()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var email = $"e2e_cm_{uniqueId}@example.com";

        await WebAuthnTestHelpers.LoginWithMockGoogleAsync(Page, _serverAddress, email);

        _ = await Page.GotoAsync($"{_serverAddress}/Contract/ContractManagement");

        await Expect(Page.GetByText("現在、受信したコントラクトの提案はありません。"))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10000 });

        var outboxTab = Page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = "送信済み (Outbox)" });
        await Expect(outboxTab).ToBeVisibleAsync();

        for (int attempt = 0; attempt < 5; attempt++)
        {
            await outboxTab.ClickAsync();
            try
            {
                await Expect(Page.GetByText("現在、送信したコントラクトの提案はありません。"))
                    .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 2000 });
                break;
            }
            catch when (attempt < 4)
            {
                await Task.Delay(500);
            }
        }

        await Expect(Page.GetByText("現在、送信したコントラクトの提案はありません。")).ToBeVisibleAsync();
    }
}
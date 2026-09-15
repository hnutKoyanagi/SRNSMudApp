using System.Security.Claims;

using Bunit;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Item;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Item;

public sealed class AddItemTests : IAsyncLifetime
{
    private const string ExistingUserId = "test-user-id";
    private const string TestContent = "Test Item Content";

    private readonly BunitContext _ctx = new();
    private readonly Mock<IItemCardDataProvider> _itemCardDataMock = new();
    private readonly Mock<IUserDataProvider> _userDataProviderMock = new();
    private readonly Mock<ITagSearchQueryService> _tagSearchMock = new();

    public AddItemTests()
    {
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _itemCardDataMock.Object);

        Claim[] claims = [new(ClaimTypes.NameIdentifier, ExistingUserId), new(ClaimTypes.Name, "testuser")];
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var authState = new AuthenticationState(new ClaimsPrincipal(identity));
        _ = _ctx.Services.AddCascadingValue(_ => Task.FromResult(authState));
        _ = _ctx.Services.AddAuthorizationCore();

        _ = _ctx.Services.AddScoped(_ => _userDataProviderMock.Object);
        _ = _ctx.Services.AddScoped(_ => _tagSearchMock.Object);

        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Render<MudPopoverProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public void Save_SubmitsForm_AndCallsCreateItemAsyncWithCorrectParameters()
    {
        var onItemAddedCalled = false;
        _ = _itemCardDataMock
            .Setup(d => d.CreateItemAsync(It.IsAny<SRNSMudApp.Data.Item>(), It.IsAny<IReadOnlyCollection<int>?>()))
            .Returns(Task.CompletedTask);

        IRenderedComponent<AddItem> cut = _ctx.Render<AddItem>(parameters => parameters
            .Add(p => p.OnItemAdded, () => onItemAddedCalled = true));

        cut.WaitForState(() => cut.FindAll("form").Count > 0);
        cut.Find("textarea").Input(TestContent);


        cut.Find("form").Submit();

        _itemCardDataMock.Verify(d => d.CreateItemAsync(
            It.Is<SRNSMudApp.Data.Item>(i => i.Content == TestContent && i.OwnerId == ExistingUserId),
            It.IsAny<IReadOnlyCollection<int>?>()), Times.Once);

        Assert.True(onItemAddedCalled);
    }

    [Fact]
    public void CommandEnter_SavesItem()
    {
        _ = _itemCardDataMock
            .Setup(d => d.CreateItemAsync(It.IsAny<SRNSMudApp.Data.Item>(), It.IsAny<IReadOnlyCollection<int>?>()))
            .Returns(Task.CompletedTask);

        IRenderedComponent<AddItem> cut = _ctx.Render<AddItem>();

        cut.WaitForState(() => cut.FindAll("form").Count > 0);
        cut.Find("textarea").Input(TestContent);
        cut.Find("textarea").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs
        {
            Key = "Enter",
            MetaKey = true
        });

        _itemCardDataMock.Verify(d => d.CreateItemAsync(
            It.Is<SRNSMudApp.Data.Item>(i => i.Content == TestContent && i.OwnerId == ExistingUserId),
            It.IsAny<IReadOnlyCollection<int>?>()), Times.Once);
    }

    [Fact]
    public void Input_UrlInContent_RendersUrlPreviewCardWithLoadPreview()
    {
        const string inputContent = "Check this out https://example.com";
        IRenderedComponent<AddItem> cut = _ctx.Render<AddItem>();

        cut.WaitForState(() => cut.FindAll("form").Count > 0);
        cut.Find("textarea").Input(inputContent);



        var previewCard = cut.FindComponent<SRNSMudApp.Components.UI.UrlPreviewCard>();
        Assert.NotNull(previewCard);
        Assert.Equal("https://example.com", previewCard.Instance.Url);
        Assert.NotNull(previewCard.Instance.LoadPreview);
    }

    [Fact]
    public void Save_WhenPrivateModeEnabled_CreatesItemWithIsPrivateTrue()
    {
        _ = _itemCardDataMock
            .Setup(d => d.CreateItemAsync(It.IsAny<SRNSMudApp.Data.Item>(), It.IsAny<IReadOnlyCollection<int>?>()))
            .Returns(Task.CompletedTask);

        IRenderedComponent<AddItem> cut = _ctx.Render<AddItem>();

        cut.WaitForState(() => cut.FindAll("form").Count > 0);
        cut.Find("textarea").Input(TestContent);



        // プライベートモードのスイッチ (MudSwitch) を ON に切り替える
        var switchInput = cut.Find("input[type='checkbox']");
        switchInput.Change(true);

        cut.Find("form").Submit();

        _itemCardDataMock.Verify(d => d.CreateItemAsync(
            It.Is<SRNSMudApp.Data.Item>(i => i.Content == TestContent && i.OwnerId == ExistingUserId && i.IsPrivate),
            It.IsAny<IReadOnlyCollection<int>?>()), Times.Once);
    }

    [Fact]
    public void Render_ContainsContentEditableEditor()
    {
        IRenderedComponent<AddItem> cut = _ctx.Render<AddItem>();

        cut.WaitForState(() => cut.FindAll("form").Count > 0);

        var editor = cut.Find("#add-item-textarea");
        Assert.NotNull(editor);
        Assert.Equal("true", editor.GetAttribute("contenteditable"));
        Assert.Equal("新しいアイテムのコンテンツを入力...", editor.GetAttribute("data-placeholder"));
    }

    [Fact]
    public void Input_InternalUrlInContent_DoesNotRenderBottomPreviewCards()
    {
        const string inputContent = "Check tag /TagDetail/1 and user /User/UserDetail/user1";
        IRenderedComponent<AddItem> cut = _ctx.Render<AddItem>();

        cut.WaitForState(() => cut.FindAll("form").Count > 0);
        cut.Find("textarea").Input(inputContent);

        // 内部リンクはエディタ内でインライン表示されるため、bottomの外部プレビューカードセクションには描画されない
        var previewCards = cut.FindComponents<SRNSMudApp.Components.UI.UrlPreviewCard>();
        Assert.Empty(previewCards);
    }

    [Fact]
    public async Task SearchTags_InvokesTagSearchQueryService_AndReturnsFormattedMentionItems()
    {
        var sampleTags = new List<SRNSMudApp.Data.Tag>
        {
            new()
            {
                Id = 10,
                Name = "C#",
                OwnerId = "owner-1",
                Owner = new ApplicationUser { Id = "owner-1", UserName = "Alice" }
            },
            new()
            {
                Id = 20,
                Name = "Blazor",
                OwnerId = "owner-2",
                Owner = new ApplicationUser { Id = "owner-2", UserName = "Bob" }
            },
            new()
            {
                Id = 30,
                Name = "SystemTag",
                OwnerId = "system",
                IsSystem = true
            }
        };
        _ = _tagSearchMock
            .Setup(s => s.SearchTagsWithFallbackAsync("test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleTags);

        IRenderedComponent<AddItem> cut = _ctx.Render<AddItem>();
        cut.WaitForState(() => cut.FindAll("form").Count > 0);

        var results = (await cut.Instance.SearchTags("test")).ToList();

        Assert.Equal(3, results.Count);
        Assert.Equal("C# : Alice", results[0].name);
        Assert.Equal("/TagDetail/10", results[0].replacement);
        Assert.Equal("Blazor : Bob", results[1].name);
        Assert.Equal("/TagDetail/20", results[1].replacement);
        Assert.Equal("SystemTag : system", results[2].name);
        Assert.Equal("/TagDetail/30", results[2].replacement);
    }

    [Fact]
    public async Task SearchUsers_InvokesUserDataProvider_AndReturnsFormattedMentionItems()
    {
        var sampleUsers = new List<ApplicationUser>
        {
            new() { Id = "user-1", UserName = "Alice" },
            new() { Id = "user-2", UserName = "Bob" }
        };
        _ = _userDataProviderMock
            .Setup(u => u.SearchUsersAsync("al", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleUsers);

        IRenderedComponent<AddItem> cut = _ctx.Render<AddItem>();
        cut.WaitForState(() => cut.FindAll("form").Count > 0);

        var results = (await cut.Instance.SearchUsers("al")).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal("@Alice", results[0].name);
        Assert.Equal("/User/UserDetail/user-1", results[0].replacement);
        Assert.Equal("@Bob", results[1].name);
        Assert.Equal("/User/UserDetail/user-2", results[1].replacement);
    }

    [Fact]
    public async Task SubmitForm_WhenInvokedDirectly_SavesItem()
    {
        _ = _itemCardDataMock
            .Setup(d => d.CreateItemAsync(It.IsAny<SRNSMudApp.Data.Item>(), It.IsAny<IReadOnlyCollection<int>?>()))
            .Returns(Task.CompletedTask);

        IRenderedComponent<AddItem> cut = _ctx.Render<AddItem>();
        cut.WaitForState(() => cut.FindAll("form").Count > 0);

        cut.Find("textarea").Input(TestContent);

        // JS側から Cmd+Enter 等で呼ばれる SubmitForm() を直接テスト
        await cut.Instance.SubmitForm();

        _itemCardDataMock.Verify(d => d.CreateItemAsync(
            It.Is<SRNSMudApp.Data.Item>(i => i.Content == TestContent && i.OwnerId == ExistingUserId),
            It.IsAny<IReadOnlyCollection<int>?>()), Times.Once);
    }

    [Fact]
    public void Input_UserMentionInContent_UpdatesNotificationTargets_AndSavesRecipients()
    {
        var mentionedUser = new ApplicationUser { Id = "target-user-1", UserName = "TargetUser" };
        _ = _userDataProviderMock
            .Setup(u => u.GetUsersByIdsAsync(It.Is<IEnumerable<string>>(ids => ids.Contains("target-user-1"))))
            .ReturnsAsync([mentionedUser]);

        _ = _itemCardDataMock
            .Setup(d => d.CreateItemAsync(It.IsAny<SRNSMudApp.Data.Item>(), It.IsAny<IReadOnlyCollection<int>?>()))
            .Returns(Task.CompletedTask);

        IRenderedComponent<AddItem> cut = _ctx.Render<AddItem>();
        cut.WaitForState(() => cut.FindAll("form").Count > 0);

        // メンションを含むテキストを入力
        cut.Find("textarea").Input("Hello /User/UserDetail/target-user-1 please check");

        // 通知先パネルが表示されるのを待機
        cut.WaitForState(() => cut.FindAll(".mud-expand-panel").Count > 0);
        Assert.Contains("通知先 (1)", cut.Markup);

        cut.Find("form").Submit();

        _itemCardDataMock.Verify(d => d.CreateItemAsync(
            It.Is<SRNSMudApp.Data.Item>(i =>
                i.NotificationRecipients != null &&
                i.NotificationRecipients.Count == 1 &&
                i.NotificationRecipients.Any(r => r.RecipientUserId == "target-user-1")),
            It.IsAny<IReadOnlyCollection<int>?>()), Times.Once);
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}
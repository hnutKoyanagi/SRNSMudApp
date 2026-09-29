using System.Security.Claims;

using Bunit;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor.Services;

using SRNSMudApp.Components.UI;
using SRNSMudApp.Data;
using SRNSMudApp.Services;
using SRNSMudApp.Tests.TestSupport;

namespace SRNSMudApp.Tests.Components.UI;

/// <summary>
///     ItemCard においてリプライスレッドを展開する前（初期表示時）に
///     リプライ件数が正しくボタンに表示されることを検証するテスト。
/// </summary>
public sealed class ItemCardReplyCountTests : IAsyncLifetime
{
    private const string UserId = "test-user-1";
    private readonly BunitContext _ctx = new();
    private readonly Mock<IItemReplyService> _itemReplyServiceMock = new();
    private readonly Mock<IItemTagService> _itemTagServiceMock = new();

    public ItemCardReplyCountTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ctx.Services.AddAuthorizationCore();

        AuthenticationState authState = BunitTestSetup.CreateAuthState(UserId);
        Mock<AuthenticationStateProvider> authMock = new();
        _ = authMock.Setup(p => p.GetAuthenticationStateAsync()).ReturnsAsync(authState);
        _ctx.Services.AddScoped(_ => authMock.Object);
        _itemReplyServiceMock.Setup(s => s.GetItemRepliesAsync(It.IsAny<int>())).ReturnsAsync([]);
        _itemReplyServiceMock.Setup(s => s.GetOptedOutUsersAsync(It.IsAny<int>())).ReturnsAsync([]);
        _ctx.Services.AddScoped(_ => _itemReplyServiceMock.Object);
        _ctx.Services.AddScoped(_ => _itemTagServiceMock.Object);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public void WhenItemHasReplies_ShowsReplyCountBeforeClicking()
    {
        var item = new SRNSMudApp.Data.Item
        {
            Id = 42,
            Content = "Test item content",
            OwnerId = UserId
        };

        _itemReplyServiceMock
            .Setup(s => s.GetItemReplyCountAsync(item.Id))
            .ReturnsAsync(3);

        IRenderedComponent<ItemCard> cut = _ctx.Render<ItemCard>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.CurrentUserId, UserId));

        // リプライボタンをクリックする前の初期描画状態で「リプライ (3)」が表示されていることを検証
        cut.WaitForState(() => cut.Markup.Contains("リプライ (3)"));
        Assert.Contains("リプライ (3)", cut.Markup);
    }

    [Fact]
    public void WhenItemHasNoReplies_ShowsDefaultReplyTextWithoutCount()
    {
        var item = new SRNSMudApp.Data.Item
        {
            Id = 43,
            Content = "Item without replies",
            OwnerId = UserId
        };

        _itemReplyServiceMock
            .Setup(s => s.GetItemReplyCountAsync(item.Id))
            .ReturnsAsync(0);

        IRenderedComponent<ItemCard> cut = _ctx.Render<ItemCard>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.CurrentUserId, UserId));

        cut.WaitForState(() => cut.Markup.Contains("リプライ"));
        Assert.DoesNotContain("リプライ (", cut.Markup);
    }

    [Fact]
    public void WhenFocusChanges_DoesNotRequeryItemMetadata()
    {
        var item = new SRNSMudApp.Data.Item
        {
            Id = 44,
            Content = "Item for focus change test",
            OwnerId = UserId
        };

        _itemReplyServiceMock
            .Setup(s => s.GetItemReplyCountAsync(item.Id))
            .ReturnsAsync(0);

        IRenderedComponent<ItemCard> cut = _ctx.Render<ItemCard>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.CurrentUserId, UserId)
            .Add(p => p.IsFocused, false));

        cut.WaitForState(() => cut.Markup.Contains("リプライ"));

        // フォーカス状態を変更して再レンダリング
        cut.Render(parameters => parameters.Add(p => p.IsFocused, true));
        cut.Render(parameters => parameters.Add(p => p.IsFocused, false));

        // 初期化時の1回のみ呼ばれ、フォーカス変更による再問い合わせが発生しないことを検証
        _itemReplyServiceMock.Verify(s => s.GetItemReplyCountAsync(item.Id), Times.Once);
        _itemTagServiceMock.Verify(s => s.GetTaggingRequestsForItemAsync(item.Id), Times.Once);
    }

    [Fact]
    public void WhenParentItemIsPrivate_ItemCard_SubmitsReplyWithSameUserGroupByDefault()
    {
        var item = new SRNSMudApp.Data.Item
        {
            Id = 50,
            Content = "Private parent item",
            OwnerId = UserId,
            IsPrivate = true,
            TargetUserGroupId = 99,
            TargetUserGroup = new UserGroup { Id = 99, Name = "Alpha", OwnerId = UserId }
        };

        var replyItem = new SRNSMudApp.Data.Item
        {
            Id = 51,
            ParentItemId = 50,
            Content = "Reply to private item",
            OwnerId = UserId,
            IsPrivate = true,
            TargetUserGroupId = 99
        };

        _ = _itemReplyServiceMock
            .Setup(s => s.GetItemReplyCountAsync(item.Id))
            .ReturnsAsync(0);

        _ = _itemReplyServiceMock
            .Setup(s => s.AddItemReplyAsync(item.Id, "Reply to private item", UserId, It.IsAny<IEnumerable<string>>(), true, 99))
            .ReturnsAsync(replyItem);

        IRenderedComponent<ItemCard> cut = _ctx.Render<ItemCard>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.CurrentUserId, UserId));

        cut.WaitForState(() => cut.Markup.Contains("リプライ"));

        // リプライ展開ボタンをクリック
        var replyToggleBtn = cut.Find($"[data-testid='reply-toggle-button-{item.Id}']");
        replyToggleBtn.Click();

        // プライベートトグルが表示され、デフォルトでグループ名が含まれていること
        Assert.NotEmpty(cut.FindAll($"[data-testid='reply-private-toggle-{item.Id}']"));
        Assert.Contains("Alpha", cut.Markup);

        // リプライを入力して送信
        var replyInput = cut.Find($"[data-testid='reply-input-form-{item.Id}'] textarea");
        replyInput.Input("Reply to private item");

        var submitBtn = cut.Find($"[data-testid='reply-submit-button-{item.Id}']");
        submitBtn.Click();

        // 同一グループ限定プライベートで送信されたこと
        _itemReplyServiceMock.Verify(s => s.AddItemReplyAsync(item.Id, "Reply to private item", UserId, It.IsAny<IEnumerable<string>>(), true, 99), Times.Once);
    }

    [Fact]
    public void WhenParentItemIsPrivate_ItemCard_CanToggleOffPrivate_AndSubmitPublicReply()
    {
        var item = new SRNSMudApp.Data.Item
        {
            Id = 60,
            Content = "Private parent item",
            OwnerId = UserId,
            IsPrivate = true,
            TargetUserGroupId = 99,
            TargetUserGroup = new UserGroup { Id = 99, Name = "Alpha", OwnerId = UserId }
        };

        var publicReplyItem = new SRNSMudApp.Data.Item
        {
            Id = 61,
            ParentItemId = 60,
            Content = "Public reply to private item",
            OwnerId = UserId,
            IsPrivate = false,
            TargetUserGroupId = null
        };

        _ = _itemReplyServiceMock
            .Setup(s => s.GetItemReplyCountAsync(item.Id))
            .ReturnsAsync(0);

        _ = _itemReplyServiceMock
            .Setup(s => s.AddItemReplyAsync(item.Id, "Public reply to private item", UserId, It.IsAny<IEnumerable<string>>(), false, null))
            .ReturnsAsync(publicReplyItem);

        IRenderedComponent<ItemCard> cut = _ctx.Render<ItemCard>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.CurrentUserId, UserId));

        cut.WaitForState(() => cut.Markup.Contains("リプライ"));

        // リプライ展開
        var replyToggleBtn = cut.Find($"[data-testid='reply-toggle-button-{item.Id}']");
        replyToggleBtn.Click();

        // スイッチをOFFに切り替え
        var switchInput = cut.Find($"[data-testid='reply-private-toggle-{item.Id}'] input[type='checkbox']");
        switchInput.Change(false);

        // リプライを入力して送信
        var replyInput = cut.Find($"[data-testid='reply-input-form-{item.Id}'] textarea");
        replyInput.Input("Public reply to private item");

        var submitBtn = cut.Find($"[data-testid='reply-submit-button-{item.Id}']");
        submitBtn.Click();

        // 公開で送信されたこと
        _itemReplyServiceMock.Verify(s => s.AddItemReplyAsync(item.Id, "Public reply to private item", UserId, It.IsAny<IEnumerable<string>>(), false, null), Times.Once);
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}
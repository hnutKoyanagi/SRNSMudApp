using Bunit;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Item;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Item;

public sealed class ItemEditDialogTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly Mock<IItemCardDataProvider> _itemCardDataMock = new();
    private readonly IRenderedComponent<MudDialogProvider> _dialogProvider;

    public ItemEditDialogTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _itemCardDataMock.Object);
        _dialogProvider = _ctx.Render<MudDialogProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Render_WithUrlInContent_DisplaysUrlPreviewCardWithLoadPreview()
    {
        var item = new SRNSMudApp.Data.Item
        {
            Id = 1,
            OwnerId = "test-user-id",
            Content = "Editing item with link https://example.com"
        };
        var parameters = new DialogParameters<ItemEditDialog>
        {
            { x => x.Item, item }
        };

        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();
        _ = await dialogService.ShowAsync<ItemEditDialog>("アイテムの編集", parameters);

        _dialogProvider.WaitForState(() => _dialogProvider.FindAll("textarea").Count > 0);

        var previewCard = _dialogProvider.FindComponent<SRNSMudApp.Components.UI.UrlPreviewCard>();
        Assert.NotNull(previewCard);
        Assert.Equal("https://example.com", previewCard.Instance.Url);
        Assert.NotNull(previewCard.Instance.LoadPreview);
    }

    [Fact]
    public void ParsePillsToHtml_ConvertsTagsAndUserMentionsToPillHtml()
    {
        const string input = "Hello /TagDetail/10 and /User/UserDetail/user-1 & <special>";
        var html = ItemEditDialog.ParsePillsToHtml(input);

        Assert.Contains("class=\"internal-link-preview-pill\"", html);
        Assert.Contains("data-url=\"/TagDetail/10\"", html);
        Assert.Contains(">#タグ</span>&#8203;&nbsp;", html);
        Assert.Contains("data-url=\"/User/UserDetail/user-1\"", html);
        Assert.Contains(">@ユーザー</span>&#8203;&nbsp;", html);
        // HTML 特殊文字がエスケープされていること
        Assert.Contains("&amp;&nbsp;&lt;special&gt;", html);
    }

    [Fact]
    public async Task Render_InitializesContentEditableContainer_AndHiddenTextarea()
    {
        var item = new SRNSMudApp.Data.Item
        {
            Id = 42,
            OwnerId = "test-user-id",
            Content = "Initial content"
        };
        var parameters = new DialogParameters<ItemEditDialog>
        {
            { x => x.Item, item }
        };

        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();
        _ = await dialogService.ShowAsync<ItemEditDialog>("アイテムの編集", parameters);

        _dialogProvider.WaitForState(() => _dialogProvider.FindAll("#edit-item-textarea").Count > 0);

        var editor = _dialogProvider.Find("#edit-item-textarea");
        Assert.Equal("true", editor.GetAttribute("contenteditable"));

        var hiddenTextarea = _dialogProvider.Find("#edit-item-textarea-hidden");
        Assert.Equal("Initial content", hiddenTextarea.GetAttribute("value"));
    }

    [Fact]
    public async Task Save_WhenContentValid_CallsUpdateItemContentAsync()
    {
        _itemCardDataMock
            .Setup(d => d.UpdateItemContentAsync(100, "New updated content"))
            .ReturnsAsync(true);

        var item = new SRNSMudApp.Data.Item
        {
            Id = 100,
            OwnerId = "test-user-id",
            Content = "Old content"
        };
        var parameters = new DialogParameters<ItemEditDialog>
        {
            { x => x.Item, item }
        };

        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();
        _ = await dialogService.ShowAsync<ItemEditDialog>("アイテムの編集", parameters);

        _dialogProvider.WaitForState(() => _dialogProvider.FindAll("textarea").Count > 0);

        // テキストエリアを変更
        var textarea = _dialogProvider.Find("#edit-item-textarea-hidden");
        textarea.Input("New updated content");

        // 保存ボタンをクリック
        var saveButton = _dialogProvider.FindAll("button").First(b => b.TextContent.Contains("保存"));
        saveButton.Click();

        _itemCardDataMock.Verify(d => d.UpdateItemContentAsync(100, "New updated content"), Times.Once);
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}
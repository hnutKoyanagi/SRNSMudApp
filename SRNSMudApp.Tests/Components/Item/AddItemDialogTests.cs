using System.Security.Claims;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Item;
using SRNSMudApp.Services;

namespace SRNSMudApp.Tests.Components.Item;

/// <summary>
///     <see cref="AddItemDialog" /> の単体テスト。
///     タイトルバーの閉じるボタン、保存ボタンの配置およびダイアログキャンセル動作を検証する。
/// </summary>
public sealed class AddItemDialogTests : IAsyncLifetime
{
    private const string ExistingUserId = "test-user-id";
    private readonly BunitContext _ctx = new();
    private readonly Mock<IItemCardDataProvider> _itemCardDataMock = new();
    private readonly Mock<IUserDataProvider> _userDataProviderMock = new();
    private readonly Mock<ITagSearchQueryService> _tagSearchMock = new();
    private readonly Mock<ITagSuggestionService> _tagSuggestionMock = new();

    public AddItemDialogTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _itemCardDataMock.Object);

        Claim[] claims = [new(ClaimTypes.NameIdentifier, ExistingUserId), new(ClaimTypes.Name, "testuser")];
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var authState = new AuthenticationState(new ClaimsPrincipal(identity));
        _ = _ctx.Services.AddCascadingValue(_ => Task.FromResult(authState));
        _ = _ctx.Services.AddAuthorizationCore();

        _ = _ctx.Services.AddScoped(_ => _userDataProviderMock.Object);
        _ = _ctx.Services.AddScoped(_ => _tagSearchMock.Object);

        _tagSuggestionMock
            .Setup(s => s.SuggestTagsAsync(It.IsAny<string>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _ = _ctx.Services.AddScoped(_ => _tagSuggestionMock.Object);

        _ = _ctx.Render<MudPopoverProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _ctx.DisposeAsync().AsTask();

    [Fact]
    public async Task Render_DisplaysTitleCloseButtonAndSubmitButtonWithFormAttribute()
    {
        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();
        _ = await dialogService.ShowAsync<AddItemDialog>("アイテムを追加");

        host.WaitForAssertion(() =>
        {
            var closeButton = host.Find("button[aria-label='閉じる']");
            Assert.NotNull(closeButton);

            var submitButton = host.Find("button[type='submit'][form='add-item-dialog-form']");
            Assert.NotNull(submitButton);
            Assert.Contains("保存", submitButton.TextContent);

            var addItemComponent = host.FindComponent<AddItem>();
            Assert.NotNull(addItemComponent);
            Assert.True(addItemComponent.Instance.HideSubmitButton);
            Assert.Equal("add-item-dialog-form", addItemComponent.Instance.FormId);
            Assert.Equal("add-item-textarea-dialog", addItemComponent.Instance.TextareaId);
        });
    }

    [Fact]
    public async Task CloseButton_CancelsDialog()
    {
        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();
        var dialogRef = await dialogService.ShowAsync<AddItemDialog>("アイテムを追加");

        host.WaitForAssertion(() =>
        {
            var closeButton = host.Find("button[aria-label='閉じる']");
            Assert.NotNull(closeButton);
        });

        var closeButton = host.Find("button[aria-label='閉じる']");
        closeButton.Click();

        var result = await dialogRef.Result;
        Assert.NotNull(result);
        Assert.True(result.Canceled);
    }

    private sealed class DialogHost : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
        }
    }
}
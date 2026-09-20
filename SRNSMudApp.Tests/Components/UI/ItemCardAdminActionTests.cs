#region

using Bunit;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor.Services;

using SRNSMudApp.Components.UI;
using SRNSMudApp.Data;
using SRNSMudApp.Services;
using SRNSMudApp.Tests.TestSupport;

#endregion

namespace SRNSMudApp.Tests.Components.UI;

/// <summary>
///     ItemCard における管理者の強制削除・非公開化アクションのコンポーネントテスト (bUnit)。
/// </summary>
public sealed class ItemCardAdminActionTests : IAsyncLifetime
{
    private const string AdminUserId = "admin-user-id";
    private const string RegularUserId = "regular-user-id";
    private const string AuthorUserId = "author-user-id";

    private readonly BunitContext _ctx = new();
    private readonly Mock<IItemCardDataProvider> _itemCardDataMock = new();

    public ItemCardAdminActionTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddAuthorizationCore();

        // IItemCardDataProvider を上書き登録
        _ = _ctx.Services.AddScoped(_ => _itemCardDataMock.Object);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    [Fact]
    public void WhenUserIsAdmin_RendersAdminDeleteAndHideButtons_EvenOnOthersItem()
    {
        // Arrange
        AuthenticationState authState = BunitTestSetup.CreateAuthState(AdminUserId, "Admin");
        Mock<AuthenticationStateProvider> authMock = new();
        _ = authMock.Setup(p => p.GetAuthenticationStateAsync()).ReturnsAsync(authState);
        _ = _ctx.Services.AddScoped(_ => authMock.Object);

        var item = new SRNSMudApp.Data.Item
        {
            Id = 101,
            Content = "Other user's post",
            OwnerId = AuthorUserId,
            IsAdminHidden = false
        };

        // Act
        IRenderedComponent<ItemCard> cut = _ctx.Render<ItemCard>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.CurrentUserId, AdminUserId)
            .Add(p => p.IsAdmin, true));

        // Assert
        Assert.Contains($"admin-delete-button-{item.Id}", cut.Markup);
        Assert.Contains($"admin-hide-button-{item.Id}", cut.Markup);
    }

    [Fact]
    public void WhenUserIsNotAdmin_DoesNotRenderAdminDeleteOrHideButtons_OnOthersItem()
    {
        // Arrange
        AuthenticationState authState = BunitTestSetup.CreateAuthState(RegularUserId);
        Mock<AuthenticationStateProvider> authMock = new();
        _ = authMock.Setup(p => p.GetAuthenticationStateAsync()).ReturnsAsync(authState);
        _ = _ctx.Services.AddScoped(_ => authMock.Object);

        var item = new SRNSMudApp.Data.Item
        {
            Id = 102,
            Content = "Other user's post",
            OwnerId = AuthorUserId,
            IsAdminHidden = false
        };

        // Act
        IRenderedComponent<ItemCard> cut = _ctx.Render<ItemCard>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.CurrentUserId, RegularUserId)
            .Add(p => p.IsAdmin, false));

        // Assert
        Assert.DoesNotContain($"admin-delete-button-{item.Id}", cut.Markup);
        Assert.DoesNotContain($"admin-hide-button-{item.Id}", cut.Markup);
    }

    [Fact]
    public async Task WhenAdminClicksHideButton_CallsSetAdminHiddenAsync()
    {
        // Arrange
        var item = new SRNSMudApp.Data.Item
        {
            Id = 103,
            Content = "Target post to hide",
            OwnerId = AuthorUserId,
            IsAdminHidden = false
        };

        _itemCardDataMock
            .Setup(d => d.SetAdminHiddenAsync(item.Id, true, It.IsAny<string?>(), AdminUserId))
            .ReturnsAsync(true);

        IRenderedComponent<ItemCard> cut = _ctx.Render<ItemCard>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.CurrentUserId, AdminUserId)
            .Add(p => p.IsAdmin, true));

        // Act: 非公開化ボタンをクリック
        var hideButton = cut.Find($"button[data-testid='admin-hide-button-{item.Id}']");
        await cut.InvokeAsync(() => hideButton.Click());

        // Assert
        _itemCardDataMock.Verify(d => d.SetAdminHiddenAsync(item.Id, true, It.IsAny<string?>(), AdminUserId), Times.Once);
    }

    [Fact]
    public async Task WhenAdminClicksDeleteButton_CallsDeleteItemByAdminAsync()
    {
        // Arrange
        var item = new SRNSMudApp.Data.Item
        {
            Id = 104,
            Content = "Target post to delete",
            OwnerId = AuthorUserId,
            IsAdminHidden = false
        };

        _itemCardDataMock
            .Setup(d => d.DeleteItemByAdminAsync(item.Id, AdminUserId))
            .ReturnsAsync(true);

        IRenderedComponent<ItemCard> cut = _ctx.Render<ItemCard>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.CurrentUserId, AdminUserId)
            .Add(p => p.IsAdmin, true));

        // Act: 強制削除ボタンをクリック
        var deleteButton = cut.Find($"button[data-testid='admin-delete-button-{item.Id}']");
        await cut.InvokeAsync(() => deleteButton.Click());

        // Assert
        _itemCardDataMock.Verify(d => d.DeleteItemByAdminAsync(item.Id, AdminUserId), Times.Once);
    }
}
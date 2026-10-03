using System.Security.Claims;

using Bunit;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Pages;
using SRNSMudApp.Data;
using SRNSMudApp.Models;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Dialogs;

namespace SRNSMudApp.Tests.Components.Pages;

/// <summary>
///     <see cref="Home" /> コンポーネントのモバイル向け機能（FABボタン表示・ダイアログ起動）のテスト。
/// </summary>
public sealed class HomeMobileTests : IAsyncLifetime
{
    private const string CurrentUserId = "test-user-id";
    private readonly BunitContext _ctx = new();
    private readonly Mock<IHomeDataProvider> _homeDataMock = new();
    private readonly Mock<ISystemTagEnsurer> _systemTagEnsurerMock = new();
    private readonly Mock<IDialogLauncher> _dialogLauncherMock = new();

    public HomeMobileTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();

        _homeDataMock.Setup(d => d.GetFollowedTagIdsAsync(CurrentUserId))
            .ReturnsAsync([1]);
        _homeDataMock.Setup(d => d.GetTagsAndRelationsAsync())
            .ReturnsAsync((new List<global::SRNSMudApp.Data.Tag>(), new List<global::SRNSMudApp.Data.TagRelationToTag>()));
        _homeDataMock.Setup(d => d.LoadTimelineAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new HomeTimelinePage([], 0));

        var homeViewModel = new HomeViewModel(_homeDataMock.Object, _systemTagEnsurerMock.Object);
        _ = _ctx.Services.AddScoped(_ => homeViewModel);
        _ = _ctx.Services.AddScoped(_ => _dialogLauncherMock.Object);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _ctx.DisposeAsync().AsTask();

    [Fact]
    public void Home_WhenLoggedIn_RendersMobileFab()
    {
        // Arrange
        Claim[] claims = [new(ClaimTypes.NameIdentifier, CurrentUserId), new(ClaimTypes.Name, "testuser")];
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var authState = new AuthenticationState(new ClaimsPrincipal(identity));
        _ = _ctx.Services.AddCascadingValue(_ => Task.FromResult(authState));
        _ = _ctx.Services.AddAuthorizationCore();

        // Act
        var cut = _ctx.Render<Home>();

        // Assert: MudFab が描画され、アイテム追加ラベルを持つ
        var fab = cut.Find("button[aria-label='アイテムを追加']");
        Assert.NotNull(fab);
    }

    [Fact]
    public void Home_WhenNotLoggedIn_DoesNotRenderMobileFab()
    {
        // Arrange
        var identity = new ClaimsIdentity(); // 未認証
        var authState = new AuthenticationState(new ClaimsPrincipal(identity));
        _ = _ctx.Services.AddCascadingValue(_ => Task.FromResult(authState));
        _ = _ctx.Services.AddAuthorizationCore();

        // Act
        var cut = _ctx.Render<Home>();

        // Assert: 未ログイン時は FAB が描画されない
        Assert.Empty(cut.FindAll("button[aria-label='アイテムを追加']"));
    }

    [Fact]
    public async Task Home_MobileFab_Click_InvokesShowAddItemDialogAsync()
    {
        // Arrange
        Claim[] claims = [new(ClaimTypes.NameIdentifier, CurrentUserId), new(ClaimTypes.Name, "testuser")];
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var authState = new AuthenticationState(new ClaimsPrincipal(identity));
        _ = _ctx.Services.AddCascadingValue(_ => Task.FromResult(authState));
        _ = _ctx.Services.AddAuthorizationCore();

        var dialogRefMock = new Mock<IDialogReference>();
        dialogRefMock.Setup(d => d.Result).ReturnsAsync(DialogResult.Cancel());

        _dialogLauncherMock
            .Setup(l => l.ShowAsync(typeof(global::SRNSMudApp.Components.Item.AddItemDialog), string.Empty, It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()))
            .ReturnsAsync(dialogRefMock.Object);

        var cut = _ctx.Render<Home>();

        // Act
        var fab = cut.Find("button[aria-label='アイテムを追加']");
        fab.Click();

        // Assert: ダイアログランチャー経由で AddItemDialog が呼ばれたことを確認
        _dialogLauncherMock.Verify(
            l => l.ShowAsync(
                typeof(global::SRNSMudApp.Components.Item.AddItemDialog),
                string.Empty,
                It.IsAny<DialogParameters>(),
                It.Is<DialogOptions>(o => o.FullScreen == true)),
            Times.Once);
    }
}
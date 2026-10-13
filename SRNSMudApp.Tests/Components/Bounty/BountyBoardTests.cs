using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Bounty;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Dialogs;

namespace SRNSMudApp.Tests.Components.Bounty;

/// <summary>
///     バウンティ掲示板画面 (<see cref="BountyBoard" />) の bUnit UI 操作テスト。
/// </summary>
public sealed class BountyBoardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly Mock<IBountyDataProvider> _bountyDataMock = new();
    private readonly Mock<IDialogLauncher> _dialogLauncherMock = new();

    public BountyBoardTests()
    {
        _ = _ctx.Services.AddAuth("user-1", "User");
        _ = _ctx.Services.AddMudServices().AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _bountyDataMock.Object);
        _ = _ctx.Services.AddScoped(_ => _dialogLauncherMock.Object);

        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Render<MudPopoverProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    private IRenderedComponent<BountyBoard> RenderBountyBoard()
    {
        AuthenticationStateProvider authProvider = _ctx.Services.GetRequiredService<AuthenticationStateProvider>();
        Task<AuthenticationState> authStateTask = authProvider.GetAuthenticationStateAsync();
        return _ctx.Render<BountyBoard>(parameters => parameters.AddCascadingValue(authStateTask));
    }

    [Fact]
    public void BountyBoard_WhenEmpty_RendersEmptyAlert()
    {
        // Arrange
        _ = _bountyDataMock.Setup(x => x.GetActiveBountiesAsync())
            .ReturnsAsync(new BountyBoardData([], []));

        // Act
        IRenderedComponent<BountyBoard> cut = RenderBountyBoard();

        // Assert
        Assert.Contains("Bounty Board", cut.Markup);
        Assert.Contains("現在、募集中のバウンティはありません。", cut.Markup);
    }

    [Fact]
    public async Task ClickingCreateBountyButton_OpensBountyCreateDialogViaLauncher_AndReloadsOnSuccess()
    {
        // Arrange
        _ = _bountyDataMock.Setup(x => x.GetActiveBountiesAsync())
            .ReturnsAsync(new BountyBoardData([], []));

        var dialogRefMock = new Mock<IDialogReference>();
        _ = dialogRefMock.Setup(r => r.Result)
            .ReturnsAsync(DialogResult.Ok(true));

        _ = _dialogLauncherMock
            .Setup(d => d.ShowAsync(
                typeof(BountyCreateDialog),
                "タグ付けの依頼を作成",
                null,
                It.IsAny<DialogOptions>()))
            .ReturnsAsync(dialogRefMock.Object);

        IRenderedComponent<BountyBoard> cut = RenderBountyBoard();

        // Act
        IElement createBtn = cut.FindAll("button").First(b => b.TextContent.Contains("タグ付けを依頼する"));
        await cut.InvokeAsync(() => createBtn.Click());

        // Assert
        _dialogLauncherMock.Verify(
            d => d.ShowAsync(
                typeof(BountyCreateDialog),
                "タグ付けの依頼を作成",
                null,
                It.IsAny<DialogOptions>()),
            Times.Once);
        _bountyDataMock.Verify(x => x.GetActiveBountiesAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task ClickingFulfillButton_OnForeignBounty_OpensFulfillBountyDialogViaLauncher()
    {
        // Arrange
        var ownBounty = new TaggingRequestEntity
        {
            Id = 1,
            OwnerId = "user-1",
            RequesterUserId = "user-1",
            RequestedTag = new SRNSMudApp.Data.Tag { Id = 10, Name = "OwnTag", OwnerId = "user-1" },
            TargetItem = new SRNSMudApp.Data.Item { Id = 100, Content = "OwnItemContent", OwnerId = "user-1" },
            Payload = new GratisPayload("please")
        };

        var foreignBounty = new TaggingRequestEntity
        {
            Id = 2,
            OwnerId = "user-2",
            RequesterUserId = "user-2",
            RequestedTag = new SRNSMudApp.Data.Tag { Id = 20, Name = "ForeignTag", OwnerId = "user-2" },
            TargetItem = new SRNSMudApp.Data.Item { Id = 200, Content = "ForeignItemContent", OwnerId = "user-2" },
            Payload = new BountyPayload(42)
        };

        var rewardAsset = new RightAsset
        {
            Id = 42,
            OwnerId = "user-2",
            TargetTag = new SRNSMudApp.Data.Tag { Id = 30, Name = "RewardTag", OwnerId = "user-2" }
        };

        _ = _bountyDataMock.Setup(x => x.GetActiveBountiesAsync())
            .ReturnsAsync(new BountyBoardData(
                [ownBounty, foreignBounty],
                new Dictionary<int, RightAsset> { { 42, rewardAsset } }));

        var dialogRefMock = new Mock<IDialogReference>();
        _ = dialogRefMock.Setup(r => r.Result)
            .ReturnsAsync(DialogResult.Ok(true));

        _ = _dialogLauncherMock
            .Setup(d => d.ShowAsync(
                typeof(FulfillBountyDialog),
                "依頼を叶える",
                It.IsAny<DialogParameters>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync(dialogRefMock.Object);

        IRenderedComponent<BountyBoard> cut = RenderBountyBoard();

        // Assert initial render of both cards
        Assert.Contains("OwnTag", cut.Markup);
        Assert.Contains("自分の依頼", cut.Markup);
        Assert.Contains("ForeignTag", cut.Markup);
        Assert.Contains("報酬: RewardTag の権利", cut.Markup);

        // Act: click "依頼を完了する"
        IElement fulfillBtn = cut.FindAll("button").First(b => b.TextContent.Contains("依頼を完了する"));
        await cut.InvokeAsync(() => fulfillBtn.Click());

        // Assert
        _dialogLauncherMock.Verify(
            d => d.ShowAsync(
                typeof(FulfillBountyDialog),
                "依頼を叶える",
                It.Is<DialogParameters>(p => p != null && ReferenceEquals(p["Bounty"], foreignBounty)),
                It.IsAny<DialogOptions>()),
            Times.Once);
        _bountyDataMock.Verify(x => x.GetActiveBountiesAsync(), Times.Exactly(2));
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}
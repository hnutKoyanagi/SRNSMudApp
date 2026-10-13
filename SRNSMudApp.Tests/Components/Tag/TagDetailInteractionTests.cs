using AngleSharp.Dom;

using Bunit;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Contract;
using SRNSMudApp.Components.Tag;
using SRNSMudApp.Components.UI;
using SRNSMudApp.Data;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Dialogs;
using SRNSMudApp.Tests.TestSupport;

using TagEntity = SRNSMudApp.Data.Tag;

namespace SRNSMudApp.Tests.Components.Tag;

/// <summary>
/// <see cref="TagDetail"/> 画面におけるフォロー／アンフォロー切り替え、編集ダイアログ起動、
/// 操作権限リクエスト、および通報ダイアログ起動の UI インタラクション単体テスト。
/// </summary>
public sealed class TagDetailInteractionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly Mock<ITagDetailDataProvider> _tagDetailDataMock = new();
    private readonly Mock<ITagLockService> _tagLockServiceMock = new();
    private readonly Mock<ITagContentProposalService> _contentProposalMock = new();
    private readonly Mock<ITagNameProposalService> _nameProposalMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();
    private readonly Mock<IDialogLauncher> _dialogLauncherMock = new();

    public TagDetailInteractionTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ = _ctx.Services.AddMudServices();
        _ = _ctx.Services.AddMockSrnsServices();
        _ = _ctx.Services.AddScoped(_ => _tagDetailDataMock.Object);
        _ = _ctx.Services.AddScoped(_ => _tagLockServiceMock.Object);
        _ = _ctx.Services.AddScoped(_ => _contentProposalMock.Object);
        _ = _ctx.Services.AddScoped(_ => _nameProposalMock.Object);
        _ = _ctx.Services.AddScoped(_ => _snackbarMock.Object);
        _ = _ctx.Services.AddScoped(_ => _dialogLauncherMock.Object);
        _ = _ctx.Services.AddAuthorizationCore();
        _ = _ctx.Services.AddAuth("test-user");
        _ = _ctx.Render<MudPopoverProvider>();

        _ = _contentProposalMock.Setup(s => s.GetPendingProposalsForTagAsync(It.IsAny<int>()))
            .ReturnsAsync([]);
        _ = _nameProposalMock.Setup(s => s.GetPendingProposalsForTagAsync(It.IsAny<int>()))
            .ReturnsAsync([]);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _ctx.DisposeAsync().AsTask();

    private static TagDetailPageData CreatePageData(TagEntity tag, bool isFollowing = false) =>
        new(tag, isFollowing, [], [], [], [], []);

    private IRenderedComponent<TagDetail> RenderTagDetail(int tagId, string userId, params string[] roles)
    {
        return _ctx.Render<TagDetail>(parameters => parameters
            .Add(p => p.TagId, tagId)
            .AddCascadingValue(Task.FromResult(BunitTestSetup.CreateAuthState(userId, roles))));
    }

    [Fact]
    public async Task ClickingFollowButton_TogglesFollowStateAndUpdatesButtonLabel()
    {
        // Arrange
        const string userId = "follower-user";
        var tag = new TagEntity { Id = 15, Name = "FollowableTag", OwnerId = "owner-user", IsSystem = false };
        _ = _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(15, userId))
            .ReturnsAsync(CreatePageData(tag, isFollowing: false));
        _ = _tagLockServiceMock.Setup(l => l.IsTagOrSiblingLockedAsync(15))
            .ReturnsAsync(false);
        _ = _tagDetailDataMock.Setup(d => d.ToggleFollowAsync(15, userId))
            .ReturnsAsync(true);

        var cut = RenderTagDetail(15, userId);

        // Act
        IElement followBtn = cut.FindAll("button").First(b => b.TextContent.Trim() == "フォロー");
        await cut.InvokeAsync(() => followBtn.Click());

        // Assert
        _tagDetailDataMock.Verify(d => d.ToggleFollowAsync(15, userId), Times.Once);
        Assert.Contains("フォロー解除", cut.Markup);
    }

    [Fact]
    public async Task ClickingEditButton_AsOwner_OpensTagEditDialogAndReloadsData()
    {
        // Arrange
        const string ownerId = "owner-user";
        var tag = new TagEntity { Id = 20, Name = "EditableTag", OwnerId = ownerId, IsSystem = false };
        _ = _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(20, ownerId))
            .ReturnsAsync(CreatePageData(tag));
        _ = _tagLockServiceMock.Setup(l => l.IsTagOrSiblingLockedAsync(20))
            .ReturnsAsync(false);

        var dialogMock = new Mock<IDialogReference>();
        _ = dialogMock.Setup(d => d.Result).ReturnsAsync(DialogResult.Ok(true));
        _ = _dialogLauncherMock
            .Setup(l => l.ShowAsync(
                typeof(TagEditDialog),
                "タグを編集",
                It.IsAny<DialogParameters>(),
                It.IsAny<DialogOptions?>()))
            .ReturnsAsync(dialogMock.Object);

        var cut = RenderTagDetail(20, ownerId);

        // Act
        IElement editBtn = cut.FindAll("button").First(b => b.TextContent.Trim() == "編集");
        await cut.InvokeAsync(() => editBtn.Click());

        // Assert
        _dialogLauncherMock.Verify(
            l => l.ShowAsync(
                typeof(TagEditDialog),
                "タグを編集",
                It.IsAny<DialogParameters>(),
                It.IsAny<DialogOptions?>()),
            Times.Once);
        _tagDetailDataMock.Verify(d => d.GetTagDetailAsync(20, ownerId), Times.Exactly(2));
    }

    [Fact]
    public async Task ClickingRequestPermissionButton_AsNonOwner_OpensRequestTagPermissionDialog()
    {
        // Arrange
        const string nonOwnerId = "other-user";
        var tag = new TagEntity { Id = 25, Name = "TargetTag", OwnerId = "owner-user", IsSystem = false };
        _ = _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(25, nonOwnerId))
            .ReturnsAsync(CreatePageData(tag));
        _ = _tagLockServiceMock.Setup(l => l.IsTagOrSiblingLockedAsync(25))
            .ReturnsAsync(false);

        var dialogMock = new Mock<IDialogReference>();
        _ = dialogMock.Setup(d => d.Result).ReturnsAsync(DialogResult.Ok(true));
        _ = _dialogLauncherMock
            .Setup(l => l.ShowAsync(
                typeof(RequestTagPermissionDialog),
                "操作権限のリクエスト",
                It.IsAny<DialogParameters>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync(dialogMock.Object);

        var cut = RenderTagDetail(25, nonOwnerId);

        // Act
        IElement reqBtn = cut.FindAll("button").First(b => b.TextContent.Contains("操作権限をリクエスト"));
        await cut.InvokeAsync(() => reqBtn.Click());

        // Assert
        _dialogLauncherMock.Verify(
            l => l.ShowAsync(
                typeof(RequestTagPermissionDialog),
                "操作権限のリクエスト",
                It.IsAny<DialogParameters>(),
                It.IsAny<DialogOptions>()),
            Times.Once);
        _tagDetailDataMock.Verify(d => d.GetTagDetailAsync(25, nonOwnerId), Times.Exactly(2));
    }

    [Fact]
    public async Task ClickingReportButton_OpensReportContentDialog()
    {
        // Arrange
        const string userId = "reporter-user";
        var tag = new TagEntity { Id = 30, Name = "ReportedTag", Content = "Sample content", OwnerId = "owner-user", IsSystem = false };
        _ = _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(30, userId))
            .ReturnsAsync(CreatePageData(tag));
        _ = _tagLockServiceMock.Setup(l => l.IsTagOrSiblingLockedAsync(30))
            .ReturnsAsync(false);

        var dialogMock = new Mock<IDialogReference>();
        _ = dialogMock.Setup(d => d.Result).ReturnsAsync(DialogResult.Cancel());
        _ = _dialogLauncherMock
            .Setup(l => l.ShowAsync(
                typeof(ReportContentDialog),
                "不適切な投稿を通報",
                It.IsAny<DialogParameters>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync(dialogMock.Object);

        var cut = RenderTagDetail(30, userId);

        // Act
        IElement reportBtn = cut.FindAll("button").First(b => b.TextContent.Trim() == "通報");
        await cut.InvokeAsync(() => reportBtn.Click());

        // Assert
        _dialogLauncherMock.Verify(
            l => l.ShowAsync(
                typeof(ReportContentDialog),
                "不適切な投稿を通報",
                It.IsAny<DialogParameters>(),
                It.IsAny<DialogOptions>()),
            Times.Once);
    }

    [Fact]
    public void TagDetail_WhenPrivateGroupOnly_RendersGroupBadgeWithNavigation()
    {
        // Arrange
        const string userId = "viewer-user";
        var tag = new TagEntity
        {
            Id = 50,
            Name = "グループ限定タグ",
            OwnerId = "owner-user",
            IsPrivate = true,
            TargetUserGroupId = 3,
            TargetUserGroup = new SRNSMudApp.Data.UserGroup { Id = 3, Name = "デザイン部", OwnerId = "owner-user" }
        };
        _ = _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(50, userId))
            .ReturnsAsync(CreatePageData(tag));
        _ = _tagLockServiceMock.Setup(l => l.IsTagOrSiblingLockedAsync(50))
            .ReturnsAsync(false);

        // Act
        var cut = RenderTagDetail(50, userId);

        // Assert
        Assert.Contains("グループ: デザイン部", cut.Markup);
        Assert.Contains("href=\"/UserGroup/Members/3\"", cut.Markup);
    }
}
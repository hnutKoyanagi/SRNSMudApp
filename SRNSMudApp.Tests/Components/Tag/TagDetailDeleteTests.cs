using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.Tag;
using SRNSMudApp.Components.UI;
using SRNSMudApp.Data;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Dialogs;
using SRNSMudApp.Tests.TestSupport;

using TagEntity = SRNSMudApp.Data.Tag;

namespace SRNSMudApp.Tests.Components.Tag;

/// <summary>
/// <see cref="TagDetail"/> 画面におけるタグ削除ボタンの表示・無効化および削除処理の単体テスト。
/// </summary>
public sealed class TagDetailDeleteTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly Mock<ITagDetailDataProvider> _tagDetailDataMock = new();
    private readonly Mock<ITagLockService> _tagLockServiceMock = new();
    private readonly Mock<ITagContentProposalService> _contentProposalMock = new();
    private readonly Mock<ITagNameProposalService> _nameProposalMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();
    private readonly Mock<IDialogLauncher> _dialogLauncherMock = new();

    public TagDetailDeleteTests()
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

    private static TagDetailPageData CreatePageData(TagEntity tag) =>
        new(tag, false, [], [], [], [], []);

    private IRenderedComponent<TagDetail> RenderTagDetail(int tagId, string userId, params string[] roles)
    {
        return _ctx.Render<TagDetail>(parameters => parameters
            .Add(p => p.TagId, tagId)
            .AddCascadingValue(Task.FromResult(BunitTestSetup.CreateAuthState(userId, roles))));
    }

    [Fact]
    public void WhenUserIsOwner_DeleteButtonIsRendered()
    {
        // Arrange
        const string ownerId = "owner-user";
        var tag = new TagEntity { Id = 10, Name = "MyTag", OwnerId = ownerId, IsSystem = false };
        _ = _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(10, ownerId))
            .ReturnsAsync(CreatePageData(tag));
        _ = _tagLockServiceMock.Setup(l => l.IsTagOrSiblingLockedAsync(10))
            .ReturnsAsync(false);

        // Act
        var cut = RenderTagDetail(10, ownerId);

        // Assert
        var deleteButton = cut.Find("button[data-testid='delete-tag-button']");
        Assert.NotNull(deleteButton);
        Assert.False(deleteButton.HasAttribute("disabled"));
    }

    [Fact]
    public void WhenUserIsNotOwner_AndNotAdmin_DeleteButtonIsNotRendered()
    {
        // Arrange
        const string ownerId = "owner-user";
        const string otherUser = "other-user";
        var tag = new TagEntity { Id = 10, Name = "MyTag", OwnerId = ownerId, IsSystem = false };
        _ = _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(10, otherUser))
            .ReturnsAsync(CreatePageData(tag));
        _ = _tagLockServiceMock.Setup(l => l.IsTagOrSiblingLockedAsync(10))
            .ReturnsAsync(false);

        // Act
        var cut = RenderTagDetail(10, otherUser);

        // Assert
        Assert.Empty(cut.FindAll("button[data-testid='delete-tag-button']"));
    }

    [Fact]
    public void WhenUserIsAdmin_DeleteButtonIsRendered_EvenIfNotOwner()
    {
        // Arrange
        const string ownerId = "owner-user";
        const string adminId = "admin-user";
        var tag = new TagEntity { Id = 10, Name = "MyTag", OwnerId = ownerId, IsSystem = false };
        _ = _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(10, adminId))
            .ReturnsAsync(CreatePageData(tag));
        _ = _tagLockServiceMock.Setup(l => l.IsTagOrSiblingLockedAsync(10))
            .ReturnsAsync(false);

        // Act
        var cut = RenderTagDetail(10, adminId, "Admin");

        // Assert
        var deleteButton = cut.Find("button[data-testid='delete-tag-button']");
        Assert.NotNull(deleteButton);
        Assert.False(deleteButton.HasAttribute("disabled"));
    }

    [Fact]
    public void WhenTagIsSystem_DeleteButtonIsNotRendered()
    {
        // Arrange
        const string adminId = "admin-user";
        var tag = new TagEntity { Id = 10, Name = "SystemTag", OwnerId = adminId, IsSystem = true };
        _ = _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(10, adminId))
            .ReturnsAsync(CreatePageData(tag));
        _ = _tagLockServiceMock.Setup(l => l.IsTagOrSiblingLockedAsync(10))
            .ReturnsAsync(false);

        // Act
        var cut = RenderTagDetail(10, adminId, "Admin");

        // Assert
        Assert.Empty(cut.FindAll("button[data-testid='delete-tag-button']"));
    }

    [Fact]
    public void WhenTagIsLocked_AndUserIsNotAdmin_DeleteButtonIsDisabled()
    {
        // Arrange
        const string ownerId = "owner-user";
        var tag = new TagEntity { Id = 10, Name = "LockedTag", OwnerId = ownerId, IsSystem = false };
        _ = _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(10, ownerId))
            .ReturnsAsync(CreatePageData(tag));
        _ = _tagLockServiceMock.Setup(l => l.IsTagOrSiblingLockedAsync(10))
            .ReturnsAsync(true);

        // Act
        var cut = RenderTagDetail(10, ownerId);

        // Assert
        var deleteButton = cut.Find("button[data-testid='delete-tag-button']");
        Assert.NotNull(deleteButton);
        Assert.True(deleteButton.HasAttribute("disabled"));
    }

    [Fact]
    public void WhenTagIsLocked_AndUserIsAdmin_DeleteButtonIsEnabled()
    {
        // Arrange
        const string adminId = "admin-user";
        var tag = new TagEntity { Id = 10, Name = "LockedTag", OwnerId = "other-user", IsSystem = false };
        _ = _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(10, adminId))
            .ReturnsAsync(CreatePageData(tag));
        _ = _tagLockServiceMock.Setup(l => l.IsTagOrSiblingLockedAsync(10))
            .ReturnsAsync(true);

        // Act
        var cut = RenderTagDetail(10, adminId, "Admin");

        // Assert
        var deleteButton = cut.Find("button[data-testid='delete-tag-button']");
        Assert.NotNull(deleteButton);
        Assert.False(deleteButton.HasAttribute("disabled"));
    }

    [Fact]
    public async Task WhenDeleteConfirmed_CallsDeleteTagAsyncAndNavigates()
    {
        // Arrange
        const string ownerId = "owner-user";
        var tag = new TagEntity { Id = 10, Name = "TargetTag", OwnerId = ownerId, IsSystem = false };
        _ = _tagDetailDataMock.Setup(d => d.GetTagDetailAsync(10, ownerId))
            .ReturnsAsync(CreatePageData(tag));
        _ = _tagLockServiceMock.Setup(l => l.IsTagOrSiblingLockedAsync(10))
            .ReturnsAsync(false);
        _ = _tagDetailDataMock.Setup(d => d.DeleteTagAsync(10, false))
            .ReturnsAsync(true);

        var dialogMock = new Mock<IDialogReference>();
        _ = dialogMock.Setup(d => d.Result).ReturnsAsync(DialogResult.Ok(true));

        _ = _dialogLauncherMock.Setup(l => l.ShowAsync(
                typeof(ConfirmDeleteDialog),
                "タグの削除",
                It.IsAny<DialogParameters>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync(dialogMock.Object);

        var nav = _ctx.Services.GetRequiredService<NavigationManager>();

        // Act
        var cut = RenderTagDetail(10, ownerId);
        var deleteButton = cut.Find("button[data-testid='delete-tag-button']");
        await cut.InvokeAsync(() => deleteButton.Click());

        // Assert
        _dialogLauncherMock.Verify(l => l.ShowAsync(
            typeof(ConfirmDeleteDialog),
            "タグの削除",
            It.IsAny<DialogParameters>(),
            It.IsAny<DialogOptions>()), Times.Once);

        _tagDetailDataMock.Verify(d => d.DeleteTagAsync(10, false), Times.Once);
        Assert.Contains("Item/ItemList", nav.Uri);
    }
}


using System.Security.Claims;

using Bunit;
using Bunit.TestDoubles;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.UserGroup;
using SRNSMudApp.Data;
using SRNSMudApp.Models.Unions;
using SRNSMudApp.Services;
using SRNSMudApp.Services.Dialogs;

using UserGroupEntity = SRNSMudApp.Data.UserGroup;

namespace SRNSMudApp.Tests.Components.UserGroup;

/// <summary>
///     <see cref="UserGroupList"/> コンポーネントの UI インタラクションテスト。
///     「グループを作成」「新規グループ作成」ボタン押下時にダイアログが正しく起動されること、
///     および編集・削除・メンバー管理の操作が正しく動作することを検証する。
/// </summary>
public class UserGroupListTests : IAsyncDisposable
{
    private const string CurrentUserId = "test-user-123";

    private readonly BunitContext _ctx = new();
    private readonly Mock<IUserGroupDataProvider> _userGroupDataMock = new();
    private readonly Mock<IDialogLauncher> _dialogLauncherMock = new();
    private readonly Mock<IDialogReference> _dialogReferenceMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();
    private readonly BunitAuthorizationContext _authContext;

    public UserGroupListTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();

        _dialogReferenceMock.Setup(d => d.Result).ReturnsAsync(DialogResult.Cancel());
        _dialogLauncherMock
            .Setup(l => l.ShowAsync(It.IsAny<Type>(), It.IsAny<string>(), It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()))
            .ReturnsAsync(_dialogReferenceMock.Object);

        _ctx.Services.RemoveAll<IUserGroupDataProvider>();
        _ctx.Services.AddScoped(_ => _userGroupDataMock.Object);

        _ctx.Services.RemoveAll<IDialogLauncher>();
        _ctx.Services.AddScoped(_ => _dialogLauncherMock.Object);

        _ctx.Services.RemoveAll<ISnackbar>();
        _ctx.Services.AddScoped(_ => _snackbarMock.Object);

        _ctx.Services.AddTransient<UserGroupListViewModel>();
        _ctx.Services.AddTransient<UserGroupCreateEditViewModel>();
        _ctx.Services.AddTransient<UserGroupMembersViewModel>();

        _authContext = _ctx.AddAuthorization();
        _authContext.SetAuthorized("TestUser");
        _authContext.SetClaims(
            new Claim(ClaimTypes.NameIdentifier, CurrentUserId),
            new Claim(ClaimTypes.Name, "TestUser"));

        _userGroupDataMock
            .Setup(d => d.GetUserGroupsForUserAsync(CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact]
    public void WhenGroupsEmpty_ClickingCreateGroupButton_OpensCreateDialogViaLauncher()
    {
        // Arrange: グループが0件の状態で描画
        IRenderedComponent<UserGroupList> cut = _ctx.Render<UserGroupList>();

        // Act: 「グループを作成」ボタンを検索してクリック
        var buttons = cut.FindAll("button");
        var createButton = buttons.FirstOrDefault(b => b.TextContent.Contains("グループを作成"));
        Assert.NotNull(createButton);
        createButton.Click();

        // Assert: IDialogLauncher 経由で UserGroupCreateEditDialog が「新規グループ作成」タイトルで開かれたことを検証
        _dialogLauncherMock.Verify(
            l => l.ShowAsync(
                typeof(UserGroupCreateEditDialog),
                "新規グループ作成",
                It.Is<DialogParameters>(p => (string?)p[nameof(UserGroupCreateEditDialog.CurrentUserId)] == CurrentUserId),
                It.IsAny<DialogOptions>()),
            Times.Once);
    }

    [Fact]
    public void ClickingHeaderCreateButton_OpensCreateDialogViaLauncher()
    {
        // Arrange
        IRenderedComponent<UserGroupList> cut = _ctx.Render<UserGroupList>();

        // Act: ヘッダー部の「新規グループ作成」ボタンをクリック
        var buttons = cut.FindAll("button");
        var headerButton = buttons.FirstOrDefault(b => b.TextContent.Contains("新規グループ作成"));
        Assert.NotNull(headerButton);
        headerButton.Click();

        // Assert: IDialogLauncher 経由で開かれたことを検証
        _dialogLauncherMock.Verify(
            l => l.ShowAsync(
                typeof(UserGroupCreateEditDialog),
                "新規グループ作成",
                It.Is<DialogParameters>(p => (string?)p[nameof(UserGroupCreateEditDialog.CurrentUserId)] == CurrentUserId),
                It.IsAny<DialogOptions>()),
            Times.Once);
    }

    [Fact]
    public void WhenUserNotAuthenticated_ClickingCreateGroupButton_DoesNotOpenDialog()
    {
        // Arrange: 未ログイン状態に変更
        _authContext.SetNotAuthorized();
        _userGroupDataMock
            .Setup(d => d.GetUserGroupsForUserAsync(string.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        IRenderedComponent<UserGroupList> cut = _ctx.Render<UserGroupList>();

        // Act: 「グループを作成」ボタンをクリック
        var buttons = cut.FindAll("button");
        var createButton = buttons.FirstOrDefault(b => b.TextContent.Contains("グループを作成"));
        Assert.NotNull(createButton);
        createButton.Click();

        // Assert: ダイアログは開かれないこと
        _dialogLauncherMock.Verify(
            l => l.ShowAsync(It.IsAny<Type>(), It.IsAny<string>(), It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()),
            Times.Never);
    }

    [Fact]
    public void WhenGroupsExist_ClickingMembersButton_OpensMembersDialog()
    {
        // Arrange: グループが1件存在する状態
        var group = new UserGroupEntity
        {
            Id = 1,
            Name = "テストグループ",
            OwnerId = CurrentUserId,
            Description = "説明",
            UpdatedDate = DateTime.UtcNow,
            Members = []
        };
        _userGroupDataMock
            .Setup(d => d.GetUserGroupsForUserAsync(CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([group]);

        IRenderedComponent<UserGroupList> cut = _ctx.Render<UserGroupList>();

        // Act: 「メンバー」ボタンをクリック
        var membersButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("メンバー"));
        Assert.NotNull(membersButton);
        membersButton.Click();

        // Assert: UserGroupMembersDialog が「メンバー管理」で開かれること
        _dialogLauncherMock.Verify(
            l => l.ShowAsync(
                typeof(UserGroupMembersDialog),
                "メンバー管理",
                It.Is<DialogParameters>(p =>
                    (UserGroupEntity?)p[nameof(UserGroupMembersDialog.Group)] == group &&
                    (string?)p[nameof(UserGroupMembersDialog.CurrentUserId)] == CurrentUserId),
                It.IsAny<DialogOptions>()),
            Times.Once);
    }

    [Fact]
    public void WhenOwner_ClickingEditButton_OpensEditDialog()
    {
        // Arrange: 自分がオーナーのグループ
        var group = new UserGroupEntity
        {
            Id = 1,
            Name = "編集テストグループ",
            OwnerId = CurrentUserId,
            Description = "説明",
            UpdatedDate = DateTime.UtcNow,
            Members = []
        };
        _userGroupDataMock
            .Setup(d => d.GetUserGroupsForUserAsync(CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([group]);

        IRenderedComponent<UserGroupList> cut = _ctx.Render<UserGroupList>();

        // Act: 編集アイコンボタン (title="編集") をクリック
        var editButton = cut.FindAll("button").FirstOrDefault(b => b.GetAttribute("title") == "編集");
        Assert.NotNull(editButton);
        editButton.Click();

        // Assert: UserGroupCreateEditDialog が「グループを編集」で開かれること
        _dialogLauncherMock.Verify(
            l => l.ShowAsync(
                typeof(UserGroupCreateEditDialog),
                "グループを編集",
                It.Is<DialogParameters>(p =>
                    (UserGroupEntity?)p[nameof(UserGroupCreateEditDialog.Group)] == group &&
                    (string?)p[nameof(UserGroupCreateEditDialog.CurrentUserId)] == CurrentUserId),
                It.IsAny<DialogOptions>()),
            Times.Once);
    }

    [Fact]
    public void WhenOwner_ClickingDeleteButton_DeletesGroupAndShowsSnackbar()
    {
        // Arrange
        var group = new UserGroupEntity
        {
            Id = 1,
            Name = "削除テストグループ",
            OwnerId = CurrentUserId,
            Description = "説明",
            UpdatedDate = DateTime.UtcNow,
            Members = []
        };
        _userGroupDataMock
            .Setup(d => d.GetUserGroupsForUserAsync(CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([group]);
        _userGroupDataMock
            .Setup(d => d.DeleteUserGroupAsync(group.Id, CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        IRenderedComponent<UserGroupList> cut = _ctx.Render<UserGroupList>();

        // Act: 削除アイコンボタン (title="削除") をクリック
        var deleteButton = cut.FindAll("button").FirstOrDefault(b => b.GetAttribute("title") == "削除");
        Assert.NotNull(deleteButton);
        deleteButton.Click();

        // Assert: グループ削除が呼ばれ、Snackbar に成功通知が表示されること
        _userGroupDataMock.Verify(
            d => d.DeleteUserGroupAsync(group.Id, CurrentUserId, It.IsAny<CancellationToken>()),
            Times.Once);
        _snackbarMock.Verify(
            s => s.Add("グループ「削除テストグループ」を削除しました。", Severity.Success, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()),
            Times.Once);
    }

    public async ValueTask DisposeAsync()
    {
        await _ctx.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
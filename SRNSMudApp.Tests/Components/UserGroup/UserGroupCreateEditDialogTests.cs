using System.Security.Claims;

using Bunit;
using Bunit.TestDoubles;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moq;

using MudBlazor;
using MudBlazor.Services;

using SRNSMudApp.Components.UserGroup;
using SRNSMudApp.Data;
using SRNSMudApp.Services;

using UserGroupEntity = SRNSMudApp.Data.UserGroup;

namespace SRNSMudApp.Tests.Components.UserGroup;

/// <summary>
///     <see cref="UserGroupCreateEditDialog"/> の UI インタラクションテスト。
///     新規作成・編集モードの初期化、入力バリデーション（保存ボタンの活性/非活性）、
///     保存ボタンクリックによるグループ作成/更新、およびキャンセル動作を検証する。
/// </summary>
public class UserGroupCreateEditDialogTests : IAsyncDisposable
{
    private const string CurrentUserId = "test-user-dialog-123";

    private readonly BunitContext _ctx = new();
    private readonly Mock<IUserGroupDataProvider> _userGroupDataMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();

    public UserGroupCreateEditDialogTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();

        _ctx.Services.RemoveAll<IUserGroupDataProvider>();
        _ctx.Services.AddScoped(_ => _userGroupDataMock.Object);

        _ctx.Services.RemoveAll<ISnackbar>();
        _ctx.Services.AddScoped(_ => _snackbarMock.Object);

        _ctx.Services.AddTransient<UserGroupCreateEditViewModel>();

        var authContext = _ctx.AddAuthorization();
        authContext.SetAuthorized("TestUser");
        authContext.SetClaims(
            new Claim(ClaimTypes.NameIdentifier, CurrentUserId),
            new Claim(ClaimTypes.Name, "TestUser"));

        _ = _ctx.Render<MudPopoverProvider>();
    }

    [Fact]
    public async Task WhenCreateMode_EmptyName_SaveButtonIsDisabled()
    {
        // Arrange
        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters
        {
            [nameof(UserGroupCreateEditDialog.CurrentUserId)] = CurrentUserId
        };

        // Act
        _ = await dialogService.ShowAsync<UserGroupCreateEditDialog>("新規グループ作成", parameters);

        // Assert: 初期状態（名前未入力）では保存ボタンが無効化されていることを検証
        host.WaitForAssertion(() =>
        {
            var saveButton = host.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("保存"));
            Assert.NotNull(saveButton);
            Assert.True(saveButton.HasAttribute("disabled"));
        });
    }

    [Fact]
    public async Task WhenCreateMode_InputNameAndClickSave_CreatesGroupAndClosesDialog()
    {
        // Arrange
        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var createdGroup = new UserGroupEntity
        {
            Id = 55,
            Name = "新規チーム",
            Description = "チームの説明",
            OwnerId = CurrentUserId
        };

        _userGroupDataMock
            .Setup(d => d.CreateUserGroupAsync("新規チーム", "チームの説明", CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdGroup);

        var parameters = new DialogParameters
        {
            [nameof(UserGroupCreateEditDialog.CurrentUserId)] = CurrentUserId
        };

        var dialogRef = await dialogService.ShowAsync<UserGroupCreateEditDialog>("新規グループ作成", parameters);

        // Act: グループ名と説明を入力
        host.WaitForAssertion(() =>
        {
            var inputs = host.FindAll("input, textarea");
            Assert.NotEmpty(inputs);
        });

        var nameInput = host.Find("input");
        nameInput.Change("新規チーム");

        var descTextarea = host.Find("textarea");
        descTextarea.Change("チームの説明");

        // Act: 保存ボタンをクリック
        var saveButton = host.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("保存") && !b.HasAttribute("disabled"));
        Assert.NotNull(saveButton);
        saveButton.Click();

        // Assert: ダイアログが Ok で閉じ、データプロバイダーが呼ばれたことを検証
        var result = await dialogRef.Result;
        Assert.NotNull(result);
        Assert.False(result.Canceled);

        _userGroupDataMock.Verify(
            d => d.CreateUserGroupAsync("新規チーム", "チームの説明", CurrentUserId, It.IsAny<CancellationToken>()),
            Times.Once);

        _snackbarMock.Verify(
            s => s.Add("グループ「新規チーム」を作成しました。", Severity.Success, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task WhenEditMode_DisplaysExistingData_AndUpdatesGroup()
    {
        // Arrange
        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var existingGroup = new UserGroupEntity
        {
            Id = 10,
            Name = "既存グループ",
            Description = "既存の説明",
            OwnerId = CurrentUserId
        };

        _userGroupDataMock
            .Setup(d => d.UpdateUserGroupAsync(10, "更新後グループ", "更新後の説明", CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var parameters = new DialogParameters
        {
            [nameof(UserGroupCreateEditDialog.CurrentUserId)] = CurrentUserId,
            [nameof(UserGroupCreateEditDialog.Group)] = existingGroup
        };

        var dialogRef = await dialogService.ShowAsync<UserGroupCreateEditDialog>("グループ編集", parameters);

        // Act: 名前の変更
        var nameInput = host.Find("input");
        Assert.Equal("既存グループ", nameInput.GetAttribute("value"));
        nameInput.Change("更新後グループ");

        var descTextarea = host.Find("textarea");
        descTextarea.Change("更新後の説明");

        // Act: 保存ボタンをクリック
        var saveButton = host.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("保存") && !b.HasAttribute("disabled"));
        Assert.NotNull(saveButton);
        saveButton.Click();

        // Assert
        var result = await dialogRef.Result;
        Assert.NotNull(result);
        Assert.False(result.Canceled);

        _userGroupDataMock.Verify(
            d => d.UpdateUserGroupAsync(10, "更新後グループ", "更新後の説明", CurrentUserId, It.IsAny<CancellationToken>()),
            Times.Once);

        _snackbarMock.Verify(
            s => s.Add("グループ「更新後グループ」を更新しました。", Severity.Success, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ClickingCancel_CancelsDialog()
    {
        // Arrange
        IRenderedComponent<DialogHost> host = _ctx.Render<DialogHost>();
        IDialogService dialogService = _ctx.Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters
        {
            [nameof(UserGroupCreateEditDialog.CurrentUserId)] = CurrentUserId
        };

        var dialogRef = await dialogService.ShowAsync<UserGroupCreateEditDialog>("新規グループ作成", parameters);

        // Act: キャンセルボタンをクリック
        var cancelButton = host.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("キャンセル"));
        Assert.NotNull(cancelButton);
        cancelButton.Click();

        // Assert: ダイアログがキャンセルされたことを検証
        var result = await dialogRef.Result;
        Assert.NotNull(result);
        Assert.True(result.Canceled);
    }

    public async ValueTask DisposeAsync()
    {
        await _ctx.DisposeAsync();
        GC.SuppressFinalize(this);
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